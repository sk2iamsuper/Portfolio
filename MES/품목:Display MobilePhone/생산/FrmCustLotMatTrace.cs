#region namespace
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevCommon;
#endregion namespace

namespace Module.CustPop
{
    public partial class FrmCustLotMatTrace : XtraForm
    {
        public string SetCustID = string.Empty;

        #region Constructor
        public FrmCustLotMatTrace()
        {
            InitializeComponent();
        }
        #endregion

        #region Form Load / Initialize
        private void FrmCustLotMatTrace_Load(object sender, EventArgs e)
        {
            InitializeForm();
        }

        private void InitializeForm()
        {
            InitializeControl();
            InitializeGridMatLotList();
            InitializeLang();

            SetCustID = string.IsNullOrEmpty(GlobalUtil.Instance.CustID)
                        ? "C29"
                        : GlobalUtil.Instance.CustID;
        }

        private void InitializeControl()
        {
            // 향후 컨트롤 초기화 필요 시 추가
        }
        #endregion

        #region Grid Initialize
        private void InitializeGridMatLotList()
        {
            try
            {
                var textEdit = (RepositoryItemTextEdit)DevGridUtil.Instance
                    .SetRepositoryItem(DevGridUtil.ItemType.RepositoryItemTextEdit);

                var numericEdit = (RepositoryItemTextEdit)DevGridUtil.Instance
                    .SetRepositoryItem(DevGridUtil.ItemType.RepositoryItemTextEdit);

                numericEdit.Mask.EditMask = "###,###,###,##0;(###,###,###,##0)";
                numericEdit.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.Numeric;
                numericEdit.Mask.UseMaskAsDisplayFormat = true;

                // 컬럼 정의 리스트
                var cols = new (string Caption, string Field, RepositoryItem Editor, int Width, bool Visible, DevExpress.Utils.HorzAlignment Align)[]
                {
                    ("생산번호","ppWorkNo",textEdit,100,true,DevExpress.Utils.HorzAlignment.Center),
                    ("생산LotNo.","LotNo",textEdit,100,true,DevExpress.Utils.HorzAlignment.Center),
                    ("작업일자","ppWorkDt",textEdit,100,true,DevExpress.Utils.HorzAlignment.Center),
                    ("작업장","WorkCenterName",textEdit,60,true,DevExpress.Utils.HorzAlignment.Center),
                    ("라인","LineName",textEdit,60,true,DevExpress.Utils.HorzAlignment.Center),
                    ("주/야","WorkPart",textEdit,60,true,DevExpress.Utils.HorzAlignment.Center),
                    ("고객사","CustName",textEdit,140,true,DevExpress.Utils.HorzAlignment.Center),
                    ("고객품번","Cust_ItemID",textEdit,100,true,DevExpress.Utils.HorzAlignment.Center),
                    ("고객품명","Cust_ItemName",textEdit,150,true,DevExpress.Utils.HorzAlignment.Near),
                    ("Lot단위","LotQty",numericEdit,70,true,DevExpress.Utils.HorzAlignment.Far),
                    ("생산수량","PressQty",numericEdit,70,true,DevExpress.Utils.HorzAlignment.Far),
                    ("생산일자","PressDate",textEdit,80,true,DevExpress.Utils.HorzAlignment.Center),
                    ("작업자","PressBy",textEdit,80,true,DevExpress.Utils.HorzAlignment.Center),
                    ("자재SerialNo","Material_Sn",textEdit,90,true,DevExpress.Utils.HorzAlignment.Center),
                    ("자재LotNo.","MatLot",textEdit,90,true,DevExpress.Utils.HorzAlignment.Near),
                    ("자재코드","Material_OrigID",textEdit,90,true,DevExpress.Utils.HorzAlignment.Center),
                    ("자재명","Material_Name",textEdit,120,true,DevExpress.Utils.HorzAlignment.Near),
                    ("자재 폭(mm)","Material_Width",numericEdit,70,true,DevExpress.Utils.HorzAlignment.Center),
                    ("자재 길이","Material_Length",numericEdit,70,true,DevExpress.Utils.HorzAlignment.Far),
                    ("사용량","Material_UseLength",numericEdit,70,true,DevExpress.Utils.HorzAlignment.Far),
                    ("자재입고일","MatSerial_Date",textEdit,120,true,DevExpress.Utils.HorzAlignment.Center),
                    ("자재제조일","Manufacture_Date",textEdit,120,true,DevExpress.Utils.HorzAlignment.Center),
                    ("자재유효기간","Expair_Date",textEdit,120,true,DevExpress.Utils.HorzAlignment.Center),
                    ("검사양품","QcQty",numericEdit,70,true,DevExpress.Utils.HorzAlignment.Far),
                    ("검사불량","QcBadQty",numericEdit,70,false,DevExpress.Utils.HorzAlignment.Far),
                    ("검사일자","QcOutDate",textEdit,80,true,DevExpress.Utils.HorzAlignment.Center),
                    ("검사자","OutWorker",textEdit,80,true,DevExpress.Utils.HorzAlignment.Center),
                    ("출하검사양품","OqcQty",numericEdit,70,true,DevExpress.Utils.HorzAlignment.Far),
                    ("출하검사불량","OqcBadQty",numericEdit,70,false,DevExpress.Utils.HorzAlignment.Far),
                    ("출하검사일자","OQcOutDate",textEdit,80,true,DevExpress.Utils.HorzAlignment.Center),
                    ("출하검사자","OqcApplyUser",textEdit,80,true,DevExpress.Utils.HorzAlignment.Center)
                };

                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    DevGridUtil.Instance.InitializeGrid(gridView4, i, c.Caption, c.Field, c.Field, c.Editor,
                        c.Align, DevExpress.Utils.VertAlignment.Center, c.Visible, c.Width, false);
                }

                // 스타일 및 병합 설정
                ApplyGridStyles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Grid 초기화 오류:\r\n{ex.Message}");
            }
        }

        private void ApplyGridStyles()
        {
            DevGridUtil.Instance.SetHeaderRowHeight(gridView4, 30);
            DevGridUtil.Instance.ScrollVisibility(gridView4,
                DevExpress.XtraGrid.Views.Base.ScrollVisibility.Auto,
                DevExpress.XtraGrid.Views.Base.ScrollVisibility.Auto);

            gridView4.RowHeight = 26;
            gridView4.OptionsBehavior.Editable = true;
            gridView4.OptionsBehavior.EditorShowMode = DevExpress.Utils.EditorShowMode.MouseDownFocused;
            gridView4.OptionsSelection.MultiSelect = true;
            gridView4.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CellSelect;
            gridView4.OptionsBehavior.CopyToClipboardWithColumnHeaders = false;
            gridView4.OptionsNavigation.EnterMoveNextColumn = true;
            gridView4.OptionsView.EnableAppearanceEvenRow = true;
            gridView4.OptionsView.EnableAppearanceOddRow = true;

            // 주요 색상 포인트
            ColorizeGridCells();
            SetMergeRules();
        }

        private void ColorizeGridCells()
        {
            gridView4.Columns["LotNo"].AppearanceCell.ForeColor = Color.Blue;
            gridView4.Columns["Material_Sn"].AppearanceCell.ForeColor = Color.Blue;
            gridView4.Columns["MatLot"].AppearanceCell.ForeColor = Color.Red;
            gridView4.Columns["Material_Length"].AppearanceCell.ForeColor = Color.Red;
            gridView4.Columns["Material_UseLength"].AppearanceCell.ForeColor = Color.Blue;
            gridView4.Columns["LotQty"].AppearanceCell.ForeColor = Color.Teal;
            gridView4.Columns["PressQty"].AppearanceCell.ForeColor = Color.Maroon;
            gridView4.Columns["PressDate"].AppearanceCell.ForeColor = Color.Maroon;
            gridView4.Columns["PressBy"].AppearanceCell.ForeColor = Color.Maroon;
            gridView4.Columns["QcQty"].AppearanceCell.ForeColor = Color.DarkSlateBlue;
            gridView4.Columns["QcBadQty"].AppearanceCell.ForeColor = Color.DarkSlateBlue;
            gridView4.Columns["QcOutDate"].AppearanceCell.ForeColor = Color.DarkSlateBlue;
            gridView4.Columns["OutWorker"].AppearanceCell.ForeColor = Color.DarkSlateBlue;
            gridView4.Columns["OqcQty"].AppearanceCell.ForeColor = Color.SaddleBrown;
            gridView4.Columns["OqcBadQty"].AppearanceCell.ForeColor = Color.SaddleBrown;
            gridView4.Columns["OQcOutDate"].AppearanceCell.ForeColor = Color.SaddleBrown;
            gridView4.Columns["OqcApplyUser"].AppearanceCell.ForeColor = Color.SaddleBrown;
        }

        private void SetMergeRules()
        {
            gridView4.OptionsView.AllowCellMerge = true;

            string[] noMergeCols = {
                "Material_Sn","MatLot","Material_OrigID","Material_Name",
                "Material_Width","Material_Length","Material_UseLength",
                "MatSerial_Date","Manufacture_Date","Expair_Date"
            };

            foreach (string col in noMergeCols)
            {
                gridView4.Columns[col].OptionsColumn.AllowMerge = DevExpress.Utils.DefaultBoolean.False;
            }
        }
        #endregion

        #region Multi-language
        private void InitializeLang()
        {
            if (GlobalUtil.Instance.LangType.Equals("KO")) return;

            try
            {
                string formName = this.Name;
                var ht = LanguageUtil.BindTableCellText(formName, GlobalUtil.Instance.LangType.ToString());

                labelControl15.Text = ht["labelControl15"].ToString();
                btnSearch3.Text = ht["btnSearch3"].ToString();
                btnExcel3.Text = ht["btnExcel3"].ToString();
                groupControl4.Text = ht["groupControl4"].ToString();
                ht.Clear();

                ht = LanguageUtil.BindTableCellText(formName + ".GRID", GlobalUtil.Instance.LangType.ToString());
                LanguageUtil.setGridViewText(gridView4, ht);
            }
            catch (Exception ex)
            {
                MessageBox.Show("언어 초기화 오류:\r\n" + ex.Message);
            }
        }
        #endregion

        #region Button Events
        private void btnSearch3_Click(object sender, EventArgs e)
        {
            fnSearch3();
        }

        private void btnExcel3_Click(object sender, EventArgs e)
        {
            DevGridUtil.Instance.Export2(gridView4, "Material_Lot_Trace");
        }

        private void txtMatLot_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
                fnSearch3();
        }
        #endregion

        #region Grid Events
        private void gridView4_CellMerge(object sender, DevExpress.XtraGrid.Views.Grid.CellMergeEventArgs e)
        {
            string val1 = gridView4.GetRowCellValue(e.RowHandle1, "LotNo")?.ToString();
            string val2 = gridView4.GetRowCellValue(e.RowHandle2, "LotNo")?.ToString();

            e.Merge = val1 == val2;
            e.Handled = true;
        }
        #endregion

        #region Search Function
        private void fnSearch3()
        {
            WaitFormUtil.Instnace.ShowWaitForm(this);
            try
            {
                string query = "EXEC SP_CUST_PP_MAT_TRACE_SELECT4 @MatLot, @CustID";
                SqlParameter[] parameters = {
                    new SqlParameter("@MatLot", txtMatLot.Text),
                    new SqlParameter("@CustID", SetCustID)
                };

                DataTable dt = DatabaseUtil.Instance.ExecuteQuery(query, parameters);

                gridControl4.DataSource = dt;
                gridView4.BestFitColumns();
            }
            catch (Exception ex)
            {
                MessageBox.Show("조회 중 오류 발생:\r\n" + ex.Message);
            }
            finally
            {
                WaitFormUtil.Instnace.HideWaitForm();
            }
        }
        #endregion
    }
}
