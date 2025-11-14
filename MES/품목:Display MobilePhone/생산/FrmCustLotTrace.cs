#region namespace
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid;
using DevCommon;
#endregion namespace

namespace Module.CustPop
{
    public partial class FrmCustLotTrace : XtraForm
    {
        #region Property
        public string strLotNo = string.Empty;
        public string FormName = "FrmCustLotTrace";
        public string SetCustID = "";

        public string mLotNo
        {
            get => strLotNo;
            set => strLotNo = value;
        }
        #endregion

        BaseEdit inplaceEditor;

        #region Constructor
        public FrmCustLotTrace()
        {
            InitializeComponent();
        }
        #endregion

        #region Initialize
        private void FrmCustLotTrace_Load(object sender, EventArgs e)
        {
            InitializeForm();
        }

        private void InitializeForm()
        {
            try
            {
                InitializeGrid(gridView1, 1);
                InitializeGrid(gridView2, 2);
                InitializeLang();

                SetCustID = string.IsNullOrWhiteSpace(GlobalUtil.Instance.CustID)
                    ? "C29"
                    : GlobalUtil.Instance.CustID;

                if (!string.IsNullOrWhiteSpace(strLotNo))
                {
                    txtBarcode.Text = strLotNo.Trim();
                    FnListResult();
                }

                gridView1.BestFitColumns();
                gridView2.BestFitColumns();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"폼 초기화 오류\n{ex.Message}");
            }
        }

        /// <summary>
        /// Grid 초기화 (1=자재, 2=LOT)
        /// </summary>
        private void InitializeGrid(GridView gridView, int type)
        {
            try
            {
                var checkBox = (RepositoryItemCheckEdit)DevGridUtil.Instance.SetRepositoryItem(DevGridUtil.ItemType.RepositoryItemCheckEdit);
                var textEdit = (RepositoryItemTextEdit)DevGridUtil.Instance.SetRepositoryItem(DevGridUtil.ItemType.RepositoryItemTextEdit);
                var numEdit = (RepositoryItemTextEdit)DevGridUtil.Instance.SetRepositoryItem(DevGridUtil.ItemType.RepositoryItemTextEdit);
                numEdit.Mask.EditMask = "###,###,###,##0;(###,###,###,##0)";
                numEdit.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.Numeric;
                numEdit.Mask.UseMaskAsDisplayFormat = true;

                if (type == 1)
                {
                    // 🔹 자재 추적용 그리드
                    string[,] cols = {
                        {"생산번호","ppWorkNo"}, {"생산유형","ppType"}, {"생산일자","ppWorkDt"},
                        {"생산라인","LineName"}, {"생산LOTNO","ppLotNo"}, {"자재SerialNo","Material_Sn"},
                        {"자재LotNo.","MatLot"}, {"자재코드","Material_OrigID"}, {"자재규격","Material_Name"},
                        {"자재폭(mm)","Material_Width"}, {"사용량","Material_UseLength"},
                        {"작업일자","CreatedDate"}, {"작업자","CreateBy"}, {"MatType","MatType"}
                    };

                    for (int i = 0; i < cols.GetLength(0); i++)
                    {
                        var edit = (i == 9 || i == 10) ? numEdit : textEdit;
                        DevGridUtil.Instance.InitializeGrid(gridView, i, cols[i, 0], cols[i, 1], cols[i, 1],
                            edit, DevExpress.Utils.HorzAlignment.Center, DevExpress.Utils.VertAlignment.Center, true, 100, false);
                    }

                    gridView.Columns["Material_UseLength"].AppearanceCell.ForeColor = Color.Red;
                }
                else
                {
                    // 🔹 Lot 정보 그리드
                    string[,] cols = {
                        {"생산LotNo","LotNo"},{"고객사","CustName"},{"고객품번","Cust_ItemID"},
                        {"고객품명","Cust_ItemName"},{"세경품번","ItemId"},{"ProjectName","ItemName"},
                        {"Lot단위","LotQty"},{"생산수량","PressQty"},{"생산일자","PressDate"},
                        {"작업자","PressBy"},{"검사양품수","QcQty"},{"검사불량수","QcBadQty"},
                        {"검사일자","QcOutDate"},{"작업자","OutWorker"},
                        {"출하검사수","OqcQty"},{"출하검사불량수","OqcBadQty"},
                        {"출하검사일자","OQcOutDate"},{"작업자","OqcApplyUser"}
                    };

                    for (int i = 0; i < cols.GetLength(0); i++)
                    {
                        var edit = (cols[i, 1].Contains("Qty")) ? numEdit : textEdit;
                        DevGridUtil.Instance.InitializeGrid(gridView, i, cols[i, 0], cols[i, 1], cols[i, 1],
                            edit, DevExpress.Utils.HorzAlignment.Center, DevExpress.Utils.VertAlignment.Center, true, 90, false);
                    }

                    gridView.Columns["LotNo"].AppearanceCell.ForeColor = Color.Blue;
                    gridView.Columns["LotNo"].AppearanceCell.Font = new Font("Tahoma", 9, FontStyle.Bold);
                }

                DevGridUtil.Instance.SetHeaderRowHeight(gridView, 28);
                gridView.RowHeight = 26;
                gridView.OptionsView.ShowGroupPanel = false;
                gridView.OptionsBehavior.Editable = true;
                gridView.OptionsView.EnableAppearanceEvenRow = true;
                gridView.OptionsView.EnableAppearanceOddRow = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Grid 초기화 오류\n{ex.Message}");
            }
        }

        private void InitializeLang()
        {
            if (GlobalUtil.Instance.LangType.Equals("KO")) return;

            try
            {
                string formName = this.Name;

                var ht = LanguageUtil.BindTableCellText(formName, GlobalUtil.Instance.LangType.ToString());
                btnSearch.Text = ht["btnSearch"].ToString();
                btnExcel.Text = ht["btnExcel"].ToString();
                groupControl1.Text = ht["groupControl1"].ToString();
                cmsTrace.Text = ht["cmsTrace"].ToString();
                label1.Text = ht["label1"].ToString();

                ht = LanguageUtil.BindTableCellText(formName + ".GRID", GlobalUtil.Instance.LangType.ToString());
                LanguageUtil.setGridViewText(gridView1, ht);

                ht = LanguageUtil.BindTableCellText(formName + ".GRID2", GlobalUtil.Instance.LangType.ToString());
                LanguageUtil.setGridViewText(gridView2, ht);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"다국어 설정 오류\n{ex.Message}");
            }
        }
        #endregion

        #region Events
        private void txtBarcode_Enter(object sender, EventArgs e) => txtBarcode.SelectAll();

        private void txtBarcode_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) FnListResult();
        }

        private void btnExcel_Click(object sender, EventArgs e)
        {
            DevGridUtil.Instance.Export2(gridView1, "Lot_Material_Trace");
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            FnListResult();
        }

        private void cmsTrace_Click(object sender, EventArgs e)
        {
            if (gridView1.FocusedRowHandle < 0) return;

            string matType = gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "MatType").ToString().Trim();

            if (matType == "M")
            {
                string msg = "반제품인 경우만 재추적가능합니다. 자재는 재추적이 불가능합니다.";
                MessageBox.Show(CodeUtil.Instance.GetMsgName2(FormName, msg, GlobalUtil.Instance.LangType));
                return;
            }

            string hLotNo = gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "Material_Sn").ToString();

            var frm = new FrmCustLotTrace
            {
                Text = this.Name,
                strLotNo = hLotNo,
                WindowState = FormWindowState.Normal,
                StartPosition = FormStartPosition.CenterScreen
            };
            frm.Show();
        }
        #endregion

        #region User Functions
        private void FnListResult()
        {
            WaitFormUtil.Instnace.ShowWaitForm(this);
            try
            {
                string barcode = txtBarcode.Text.Trim();

                // 1️⃣ Lot 정보 확인
                string query1 = $"EXEC SP_CUST_PP_MAT_TRACE_SELECT1_CHECK N'{barcode}'";
                DataTable dtCheck = DatabaseUtil.Instance.ExecuteQuery(query1);

                if (dtCheck.Rows.Count == 0)
                {
                    MessageBox.Show("해당 Serial No 정보를 찾을 수 없습니다.");
                    gridControl1.DataSource = null;
                    return;
                }

                string lotNo = dtCheck.Rows[0]["LotNo"].ToString().Trim();
                string barcodeType = dtCheck.Rows[0]["BarcodeType"].ToString().Trim();
                lblLabeltype.Text = barcodeType;

                // 2️⃣ Lot 정보
                LoadGridData(gridControl2, gridView2, "SP_CUST_PP_MAT_TRACE_SELECT3", lotNo, barcodeType);

                // 3️⃣ 자재 정보
                LoadGridData(gridControl1, gridView1, "SP_CUST_PP_MAT_TRACE_SELECT2", lotNo, barcodeType);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"조회 오류\n{ex.Message}");
            }
            finally
            {
                WaitFormUtil.Instnace.HideWaitForm();
            }
        }

        private void LoadGridData(GridControl grid, GridView view, string spName, string lotNo, string barcodeType)
        {
            string query = $"EXEC {spName} N'{lotNo}', N'{barcodeType}', N'{SetCustID}'";
            DataTable dt = DatabaseUtil.Instance.ExecuteQuery(query);
            grid.DataSource = dt;
            view.BestFitColumns();
        }
        #endregion
    }
}
