#region namespace
using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevCommon;
#endregion

namespace SG_Module.Edi
{
    public partial class FrmCtqMst : XtraForm
    {
        #region Constructor

        public FrmCtqMst() => InitializeComponent();

        #endregion

        #region Events

        private async void FrmCtqMst_Load(object sender, EventArgs e)
        {
            try
            {
                InitializeForm();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ShowError("폼 초기화 실패", ex);
            }
        }

        private async void btnSearch_Click(object sender, EventArgs e) => await LoadDataAsync();

        private void btnExcel_Click(object sender, EventArgs e)
            => DevGridUtil.Instance.Export2(gridView1, "EDI-CTQ LIST");

        private async void cmsCtqReg_Click(object sender, EventArgs e)
        {
            using var frm = new FrmCtqMstReg
            {
                Text = cmsCtqReg.Text,
                strDiv = "N",
                StartPosition = FormStartPosition.CenterScreen
            };

            if (frm.ShowDialog() == DialogResult.OK)
                await LoadDataAsync();
        }

        private async void cmsCtqEdit_Click(object sender, EventArgs e)
        {
            if (!TryGetFocusedRow(out var ctq)) return;

            using var frm = new FrmCtqMstReg
            {
                Text = cmsCtqEdit.Text,
                strDiv = "E",
                strCtqNo = ctq.CtqNo,
                strCtqName = ctq.CtqName,
                strCtqUnit = ctq.CtqUnit,
                StartPosition = FormStartPosition.CenterScreen
            };

            if (frm.ShowDialog() == DialogResult.OK)
                await LoadDataAsync();
        }

        private async void cmsCtqDelete_Click(object sender, EventArgs e)
        {
            if (!TryGetFocusedRow(out var ctq)) return;

            var message = $"선택하신 CTQ 정보를 삭제하시겠습니까?\n\n" +
                          $"CTQ 번호: {ctq.CtqNo}\n" +
                          $"CTQ 항목: {ctq.CtqName}";

            if (MessageBox.Show(message, "CTQ 삭제", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                return;

            try
            {
                var result = await DatabaseUtil.Instance.ExecuteSPAsync("SP_CU_CTQMST_DELETE",
                    new SqlParameter("CtqNo", ctq.CtqNo));

                if (result.Rows.Count > 0 && result.Rows[0]["RTN"].ToString() == "-1")
                    throw new ApplicationException(result.Rows[0]["MSG"].ToString());

                gridView1.DeleteSelectedRows();
            }
            catch (Exception ex)
            {
                ShowError("CTQ 삭제 실패", ex);
            }
        }

        #endregion

        #region Initialize

        private void InitializeForm()
        {
            lblTitle.Text = Text;
            InitializeGrid();
            InitializeLanguage();
        }

        private void InitializeGrid()
        {
            try
            {
                var textEdit = DevGridUtil.Instance.CreateTextEdit();
                var numericEdit = DevGridUtil.Instance.CreateNumericEdit("###,###,###,##0;-###,###,###,##0");

                int col = 0;
                col = DevGridUtil.Instance.InitializeGridRet(gridView1, col, "CTQ 번호", "CtqNo", "CtqNo", textEdit);
                col = DevGridUtil.Instance.InitializeGridRet(gridView1, col, "CTQ 항목", "CtqName", "CtqName", textEdit);
                col = DevGridUtil.Instance.InitializeGridRet(gridView1, col, "CTQ 단위", "CtqUnit", "CtqUnit", textEdit);
                col = DevGridUtil.Instance.InitializeGridRet(gridView1, col, "등록자", "CreatedBy", "CreatedBy", textEdit);
                col = DevGridUtil.Instance.InitializeGridRet(gridView1, col, "등록일자", "CreatedDate", "CreatedDate", textEdit);
                col = DevGridUtil.Instance.InitializeGridRet(gridView1, col, "수정자", "ModifiedBy", "ModifiedBy", textEdit);
                col = DevGridUtil.Instance.InitializeGridRet(gridView1, col, "수정일자", "ModifiedDate", "ModifiedDate", textEdit);

                DevGridUtil.Instance.ApplyDefaultGridStyle(gridView1);
            }
            catch (Exception ex)
            {
                ShowError("그리드 초기화 실패", ex);
            }
        }

        private void InitializeLanguage()
        {
            if (GlobalUtil.Instance.LangType.ToString().Equals("KO"))
                return;

            try
            {
                string formName = Name;
                Hashtable ht = LanguageUtil.BindTableCellText(formName, GlobalUtil.Instance.LangType.ToString());

                cmsCtqReg.Text = ht.GetValueOrDefault("cmsCtqReg", cmsCtqReg.Text).ToString();
                cmsCtqEdit.Text = ht.GetValueOrDefault("cmsCtqEdit", cmsCtqEdit.Text).ToString();
                cmsCtqDelete.Text = ht.GetValueOrDefault("cmsCtqDelete", cmsCtqDelete.Text).ToString();
                btnSearch.Text = ht.GetValueOrDefault("btnSearch", btnSearch.Text).ToString();
                btnExcel.Text = ht.GetValueOrDefault("btnExcel", btnExcel.Text).ToString();

                LanguageUtil.scaleFont(btnSearch);
                LanguageUtil.scaleFont(btnExcel);

                var gridLang = LanguageUtil.BindTableCellText(formName + ".GRID", GlobalUtil.Instance.LangType.ToString());
                LanguageUtil.setGridViewText(gridView1, gridLang);
            }
            catch (Exception ex)
            {
                ShowError("언어 초기화 실패", ex);
            }
        }

        #endregion

        #region Data

        private async Task LoadDataAsync()
        {
            WaitFormUtil.Instnace.ShowWaitForm(this);
            try
            {
                var parameters = new[] { new SqlParameter("CtqName", txtName.Text.Trim()) };
                var dt = await DatabaseUtil.Instance.ExecuteSPAsync("SP_CU_CTQMST_SELECT", parameters);

                gridControl1.DataSource = dt;
                gridView1.BestFitColumns();
            }
            catch (Exception ex)
            {
                ShowError("데이터 조회 실패", ex);
            }
            finally
            {
                WaitFormUtil.Instnace.HideWaitForm();
            }
        }

        #endregion

        #region Helpers

        private bool TryGetFocusedRow(out (string CtqNo, string CtqName, string CtqUnit) ctq)
        {
            ctq = default;
            if (gridView1.FocusedRowHandle < 0) return false;

            ctq = (
                gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "CtqNo")?.ToString(),
                gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "CtqName")?.ToString(),
                gridView1.GetRowCellValue(gridView1.FocusedRowHandle, "CtqUnit")?.ToString()
            );
            return true;
        }

        private void ShowError(string context, Exception ex)
            => MessageBox.Show($"{context}\n\n{ex.Message}", "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);

        #endregion
    }
}
