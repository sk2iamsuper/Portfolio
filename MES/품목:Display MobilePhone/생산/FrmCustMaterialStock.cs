#region namespace
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
// Add
using DevCommon;
using System.Data.SqlClient;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.XtraGrid;
#endregion

namespace VTMES3_RE
{
    public partial class FrmCustMaterialStock : DevExpress.XtraEditors.XtraForm
    {
        // ✅ 비동기 취소 제어용 CancellationTokenSource
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public FrmCustMaterialStock()
        {
            InitializeComponent();
        }

        #region Form Load & Initialize
        private void FrmCustMaterialStock_Load(object sender, EventArgs e)
        {
            InitializeForm();
        }

        private void InitializeForm()
        {
            InitializeControl();
            InitializeGridStock();
            InitializeLang();
        }

        /// <summary>
        /// LookupEdit 등 컨트롤 초기화
        /// </summary>
        private void InitializeControl()
        {
            // 창고 코드 조회
            DevLookupEditUtil.BindWarehouseLookup(le_Warehouse);
            DevLookupEditUtil.BindCustomerLookup(le_Customer);

            // 기본 선택값 지정
            le_Warehouse.EditValue = "";
            le_Customer.EditValue = "";
        }

        /// <summary>
        /// 재고 그리드 초기화
        /// </summary>
        private void InitializeGridStock()
        {
            var grid = gridView1;
            grid.OptionsBehavior.Editable = false;
            grid.OptionsView.ShowGroupPanel = false;
            grid.OptionsView.ShowAutoFilterRow = true;

            DevGridUtil.AddColumn(grid, "WarehouseCode", "창고코드", 80);
            DevGridUtil.AddColumn(grid, "WarehouseName", "창고명", 100);
            DevGridUtil.AddColumn(grid, "Material_ID", "품목코드", 100);
            DevGridUtil.AddColumn(grid, "Material_Name", "품목명", 150);
            DevGridUtil.AddColumn(grid, "Stock_Qty", "재고수량", 80, DevGridUtil.GridCellType.Qty, HorzAlignment.Far, FontStyle.Bold, Color.Red);
            DevGridUtil.AddColumn(grid, "Location", "위치", 100);
        }

        private void InitializeLang()
        {
            LanguageUtil.ApplyLanguage(this);
        }
        #endregion

        #region 버튼 이벤트

        /// <summary>
        /// [조회] 버튼 클릭 시 비동기 재고 조회 실행
        /// </summary>
        private async void btnSearch_Click(object sender, EventArgs e)
        {
            try
            {
                btnSearch.Enabled = false; // 중복 클릭 방지
                await LoadStockAsync();     // 비동기 조회 실행
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("조회가 취소되었습니다.", "취소", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류가 발생했습니다:\n{ex.Message}", "에러", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogUtil.LogError(ex); // 공통 로그 기록
            }
            finally
            {
                btnSearch.Enabled = true;
            }
        }

        /// <summary>
        /// [취소] 버튼 클릭 시 현재 조회 취소
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        /// <summary>
        /// 폼 닫힐 때 실행 중인 비동기 작업 중단
        /// </summary>
        private void FrmCustMaterialStock_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        #endregion

        #region 재고 조회 (비동기)
        /// <summary>
        /// 비동기 재고 조회 메서드
        /// </summary>
        private async Task LoadStockAsync()
        {
            // 취소 토큰 갱신
            if (_cts != null)
                _cts.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            string warehouse = Convert.ToString(le_Warehouse.EditValue ?? "");
            string customer = Convert.ToString(le_Customer.EditValue ?? "");
            string material = txtMaterialName.Text.Trim();

            WaitFormUtil.Instnace.ShowWaitForm(this, "재고를 불러오는 중입니다...");

            try
            {
                // 실제 비동기 DB 호출
                DataTable dt = await GetStockFromDatabaseAsync(warehouse, customer, material, ct).ConfigureAwait(false);

                // ✅ UI 업데이트는 반드시 UI 스레드에서 수행해야 함
                if (!ct.IsCancellationRequested)
                {
                    Invoke(new Action(() =>
                    {
                        gridControl1.DataSource = dt;
                        gridView1.BestFitColumns();
                    }));
                }
            }
            catch (OperationCanceledException)
            {
                // 취소 시 예외 발생 → 호출부에서 처리됨
                throw;
            }
            catch (Exception ex)
            {
                LogUtil.LogError(ex);
                throw;
            }
            finally
            {
                WaitFormUtil.Instnace.HideWaitForm();
            }
        }

        /// <summary>
        /// DB에서 재고정보를 비동기 호출로 가져옴
        /// </summary>
        private async Task<DataTable> GetStockFromDatabaseAsync(string warehouse, string customer, string material, CancellationToken ct)
        {
            DataTable dt = new DataTable();
            string spName = "SP_CUST_MM_INVENTSTOCK_SELECT1_REV1";

            using (SqlConnection conn = new SqlConnection(DBInfo.Instance.ConnectionString))
            using (SqlCommand cmd = new SqlCommand(spName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = 60; // 초 단위

                cmd.Parameters.AddWithValue("@WAREHOUSECDE", warehouse);
                cmd.Parameters.AddWithValue("@CUSTOMERID", customer);
                cmd.Parameters.AddWithValue("@MATERIALNAME", material);

                await conn.OpenAsync(ct).ConfigureAwait(false);

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    dt.Load(reader);
                }
            }
            return dt;
        }
        #endregion
    }
}
