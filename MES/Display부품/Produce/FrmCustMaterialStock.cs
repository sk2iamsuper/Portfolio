#region namespace
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.Grid;
using DevExpress.Utils;
using DevCommon;
#endregion namespace

namespace Module.CustPop
{
    // 간단 로그 유틸 
    public static class LogUtil
    {
        private static readonly object _lock = new object();
        private static readonly string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        public static void WriteError(string message, Exception ex = null)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(logDir);
                    string path = Path.Combine(logDir, $"error_{DateTime.Now:yyyyMMdd}.log");
                    File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}\r\n{ex?.ToString()}\r\n\r\n");
                }
            }
            catch { }
        }

        public static void WriteInfo(string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(logDir);
                    string path = Path.Combine(logDir, $"info_{DateTime.Now:yyyyMMdd}.log");
                    File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}\r\n");
                }
            }
            catch { }
        }
    }

    // DB 접근 분리: StoredProc 호출을 안전하게 수행하는 간단 Repository
    public class StockRepository
    {
        private readonly string _connectionString;

        public StockRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        
        public async Task<DataTable> GetInventoryAsync(string warehouseCode, string custId, string matSpec, CancellationToken ct = default)
        {
            var dt = new DataTable();

            // Stored Proc 명 (원본 코드 기반)
            string spName = "SP_CUST_MM_INVENTSTOCK_SELECT1_REV1";

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(spName, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                  
                    cmd.Parameters.Add(new SqlParameter("@WAREHOUSECDE", SqlDbType.NVarChar, 50) { Value = (object)warehouseCode ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@CUSTID", SqlDbType.NVarChar, 50) { Value = (object)custId ?? DBNull.Value });
                    cmd.Parameters.Add(new SqlParameter("@MATSPEC", SqlDbType.NVarChar, 200) { Value = (object)matSpec ?? DBNull.Value });

                    await conn.OpenAsync(ct).ConfigureAwait(false);

                    using (var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false))
                    {
                        dt.Load(reader);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogUtil.WriteError("GetInventoryAsync failed", ex);
                throw;
            }

            return dt;
        }
    }

    public partial class FrmCustMaterialStock : DevExpress.XtraEditors.XtraForm
    {
        #region Fields

        private readonly StockRepository _stockRepo;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        #endregion Fields

        #region Constructor

        public int CellRID = 0;

        public FrmCustMaterialStock()
        {
            InitializeComponent();

            // 연결 문자열을 app.config의 connectionStrings에서 읽어옵니다. (키: MainDb)
            string conn = GetConnectionStringOrFallback();
            _stockRepo = new StockRepository(conn);

            // 폼 로드 이벤트는 디자이너에서 연결되어 있다고 가정
            this.FormClosing += FrmCustMaterialStock_FormClosing;
        }

        private string GetConnectionStringOrFallback()
        {
            try
            {
                var cs = ConfigurationManager.ConnectionStrings["MainDb"];
                if (cs != null && !string.IsNullOrWhiteSpace(cs.ConnectionString))
                    return cs.ConnectionString;
            }
            catch (Exception ex)
            {
                LogUtil.WriteError("ConnectionString load failed", ex);
            }

            // fallback: 기존 프로젝트의 DatabaseUtil이 내부적으로 연결 문자열을 관리한다면 사용
            try
            {
                // DatabaseUtil.Instance.ConnectionString 가 존재한다고 가정한다면:
                // return DatabaseUtil.Instance.ConnectionString;
            }
            catch { }

            throw new InvalidOperationException("No connection string found. Please add 'MainDb' to connectionStrings.");
        }

        private void FrmCustMaterialStock_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _cts.Cancel();
            }
            catch { }
        }

        #endregion Constructor

        #region Initialize

        private void InitializeForm()
        {
            InitializeControl();
            InitializeGridStock1();
            InitializeLang();
        }

        private void InitializeControl()
        {
            string CommSql = "";

            lblTitle.Text = this.Text;

            try
            {
                CommSql = " EXEC SP_PO_COMMON 'MATWAREHOUSE','','','','' ";
                DevLookupEditUtil.Instance.bindSLookupEdit_Sql(lueWarehouse, CommSql);

                if (GlobalUtil.Instance.SysType.Equals("CUST"))
                {
                    lueWarehouse.EditValue = "M01";
                    lueWarehouse.Properties.ReadOnly = true;
                }
                else
                {
                    lueWarehouse.EditValue = "";
                    lueWarehouse.Properties.ReadOnly = false;
                }

                DevLookupEditUtil.Instance.bindSearchLookupEdit_Customer(slueCustName);

                if (GlobalUtil.Instance.SysType.Equals("CUST"))
                {
                    slueCustName.EditValue = GlobalUtil.Instance.CustID.ToString();
                    slueCustName.Properties.ReadOnly = true;
                }
                else
                {
                    slueCustName.EditValue = "";
                    slueCustName.Properties.ReadOnly = false;
                }
            }
            catch (Exception ex)
            {
                LogUtil.WriteError("InitializeControl failed", ex);
                MessageBox.Show("초기화 중 오류가 발생했습니다. 로그를 확인하세요.");
            }
        }

        private void InitializeGridStock1()
        {
            // 컬럼 정의 및 GridView 옵션 설정을 별도 메서드로 깔끔하게 유지
            RepositoryItemTextEdit textEdit = (RepositoryItemTextEdit)DevGridUtil.Instance.SetRepositoryItem(DevGridUtil.ItemType.RepositoryItemTextEdit);

            RepositoryItemTextEdit textEdit1 = (RepositoryItemTextEdit)DevGridUtil.Instance.SetRepositoryItem(DevGridUtil.ItemType.RepositoryItemTextEdit);
            textEdit1.Mask.EditMask = "###,###,###,##0;-###,###,###,##0";
            textEdit1.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.Numeric;
            textEdit1.Mask.UseMaskAsDisplayFormat = true;

            // 컬럼 초기화 (원본을 유지하되 메서드로 분리)
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 0, "WarehouseCde", "WarehouseCde", "WarehouseCde", textEdit, HorzAlignment.Center, DevExpress.Utils.VertAlignment.Center, false, 150, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 1, "창고정보", "WarehouseName", "WarehouseName", textEdit, HorzAlignment.Center, DevExpress.Utils.VertAlignment.Center, true, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 2, "창고구분", "WarehouseType", "WarehouseType", textEdit, HorzAlignment.Center, DevExpress.Utils.VertAlignment.Center, true, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 3, "자재코드", "Material_ID", "Material_ID", textEdit, HorzAlignment.Center, DevExpress.Utils.VertAlignment.Center, true, 90, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 4, "자재명", "Material_Name", "Material_Name", textEdit, HorzAlignment.Near, DevExpress.Utils.VertAlignment.Center, true, 120, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 5, "구분", "BatchGroup", "BatchGroup", textEdit, HorzAlignment.Center, DevExpress.Utils.VertAlignment.Center, false, 60, false);

            DevGridUtil.Instance.InitializeGrid(this.gridView1, 6, "폭(mm)", "Material_Width", "Material_Width", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 7, "길이(M)", "Material_Length", "Material_Length", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);

            DevGridUtil.Instance.InitializeGrid(this.gridView1, 8, "재고(R/L)", "Stock_Qty", "Stock_Qty", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 9, "재고(M)", "Stock_QtyM", "Stock_QtyM", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);

            DevGridUtil.Instance.InitializeGrid(this.gridView1, 10, "예약(R/L)", "RegvStockQty", "RegvStockQty", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, false, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 11, "예약(M)", "RegvStockLen", "RegvStockLen", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, false, 100, false);

            DevGridUtil.Instance.InitializeGrid(this.gridView1, 12, "기초재고(R/L)", "Stock_BaseQty", "Stock_BaseQty", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 13, "입고(R/L)", "Stock_InQty", "Stock_InQty", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 14, "기타입고(R/L)", "Stock_KitaInQty", "Stock_KitaInQty", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 15, "출고(R/L)", "Stock_OutQty", "Stock_OutQty", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 16, "기타출고(R/L)", "Stock_KitaOutQty", "Stock_KitaOutQty", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 17, "폐기(R/L)", "Stock_DisUseQty", "Stock_DisUseQty", textEdit1, HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 100, false);

            DevGridUtil.Instance.SetHeaderRowHeight(gridView1, 35);
            gridView1.RowHeight = 29;

            DevGridUtil.Instance.ScrollVisibility(gridView1, DevExpress.XtraGrid.Views.Base.ScrollVisibility.Auto, DevExpress.XtraGrid.Views.Base.ScrollVisibility.Auto);

            gridView1.OptionsBehavior.Editable = true;
            gridView1.OptionsBehavior.EditorShowMode = DevExpress.Utils.EditorShowMode.MouseDownFocused;
            gridView1.OptionsSelection.MultiSelect = true;
            gridView1.OptionsSelection.MultiSelectMode = GridMultiSelectMode.CellSelect;
            gridView1.OptionsBehavior.CopyToClipboardWithColumnHeaders = false;
            gridView1.OptionsBehavior.ImmediateUpdateRowPosition = true;
            gridView1.OptionsBehavior.AutoSelectAllInEditor = true;
            gridView1.OptionsNavigation.EnterMoveNextColumn = true;
            gridView1.OptionsView.EnableAppearanceEvenRow = true;
            gridView1.OptionsView.EnableAppearanceOddRow = true;

            // 컬럼 색상/폰트
            ApplyColumnStyles();
        }

        private void ApplyColumnStyles()
        {
            try
            {
                if (gridView1.Columns.ColumnByFieldName("Material_Width") != null)
                    gridView1.Columns["Material_Width"].AppearanceCell.ForeColor = Color.Blue;
                if (gridView1.Columns.ColumnByFieldName("Material_Length") != null)
                    gridView1.Columns["Material_Length"].AppearanceCell.ForeColor = Color.Blue;

                if (gridView1.Columns.ColumnByFieldName("RegvStockQty") != null)
                    gridView1.Columns["RegvStockQty"].AppearanceCell.ForeColor = Color.Sienna;
                if (gridView1.Columns.ColumnByFieldName("RegvStockLen") != null)
                    gridView1.Columns["RegvStockLen"].AppearanceCell.ForeColor = Color.Sienna;

                if (gridView1.Columns.ColumnByFieldName("Stock_Qty") != null)
                {
                    gridView1.Columns["Stock_Qty"].AppearanceCell.ForeColor = Color.Red;
                    gridView1.Columns["Stock_Qty"].AppearanceCell.Font = new Font("Tahoma", 9, FontStyle.Bold);
                }

                if (gridView1.Columns.ColumnByFieldName("Stock_QtyM") != null)
                {
                    gridView1.Columns["Stock_QtyM"].AppearanceCell.ForeColor = Color.Red;
                    gridView1.Columns["Stock_QtyM"].AppearanceCell.Font = new Font("Tahoma", 9, FontStyle.Bold);
                }

                // 기타 컬럼 색상 기본값
            }
            catch (Exception ex)
            {
                LogUtil.WriteError("ApplyColumnStyles failed", ex);
            }
        }

        private void InitializeLang()
        {
            if (!GlobalUtil.Instance.LangType.ToString().Equals("KO"))
            {
                try
                {
                    string FormName = this.Name;
                    Hashtable ht1 = LanguageUtil.BindTableCellText(FormName, GlobalUtil.Instance.LangType.ToString());

                    if (ht1.Count > 0)
                    {
                        labelControl3.Text = ht1["labelControl3"].ToString();
                        labelControl2.Text = ht1["labelControl2"].ToString();
                        labelControl1.Text = ht1["labelControl1"].ToString();

                        btnExcel.Text = ht1["btnExcel"].ToString();
                        btnSearch.Text = ht1["btnSearch"].ToString();
                        xtraTabPage1.Text = ht1["xtraTabPage1"].ToString();

                        cmsViewGroup.Text = ht1["cmsViewGroup"].ToString();
                        cmsViewUnGroup.Text = ht1["cmsViewUnGroup"].ToString();
                    }
                    ht1.Clear();

                    ht1 = LanguageUtil.BindTableCellText(FormName + ".GRID", GlobalUtil.Instance.LangType.ToString());
                    if (ht1.Count > 0)
                    {
                        LanguageUtil.setGridViewText(gridView1, ht1);
                    }
                }
                catch (Exception ex)
                {
                    LogUtil.WriteError("InitializeLang failed", ex);
                    MessageBox.Show("언어 초기화 중 오류가 발생했습니다.");
                }
            }
        }

        #endregion Initialize

        #region Events

        private void FrmCustMaterialStock_Load(object sender, EventArgs e)
        {
            InitializeForm();
        }

        #endregion Events

        #region Events::Button

        private async void btnSearch_Click(object sender, EventArgs e)
        {
            btnSearch.Enabled = false;
            try
            {
                await LoadStockAsync();
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("작업이 취소되었습니다.");
            }
            catch (Exception ex)
            {
                LogUtil.WriteError("btnSearch_Click error", ex);
                MessageBox.Show("조회 중 오류가 발생했습니다. 상세 로그를 확인하세요.");
            }
            finally
            {
                btnSearch.Enabled = true;
            }
        }

        private void btnExcel_Click(object sender, EventArgs e)
        {
            try
            {
                DevGridUtil.Instance.Export2(gridView1, "Material_Warehouse_Stock");
            }
            catch (Exception ex)
            {
                LogUtil.WriteError("Excel export failed", ex);
                MessageBox.Show("엑셀 내보내기 중 오류가 발생했습니다.");
            }
        }

        private async void txtMaterialSpec_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                btnSearch.Enabled = false;
                try
                {
                    await LoadStockAsync();
                }
                catch (Exception ex)
                {
                    LogUtil.WriteError("txtMaterialSpec_KeyPress load failed", ex);
                    MessageBox.Show("조회 중 오류가 발생했습니다.");
                }
                finally
                {
                    btnSearch.Enabled = true;
                }
            }
        }

        #endregion Events::Button

        #region Events::SubButton

        private void cmsViewGroup_Click(object sender, EventArgs e)
        {
            try
            {
                gridView1.OptionsView.ShowGroupPanel = true;
                gridView1.Columns["WarehouseName"].GroupIndex = 0;
                gridView1.Columns["Material_Name"].GroupIndex = 1;
                gridView1.BestFitColumns();
                gridView1.ExpandAllGroups();
            }
            catch (Exception ex)
            {
                LogUtil.WriteError("cmsViewGroup_Click failed", ex);
            }
        }

        private void cmsViewUnGroup_Click(object sender, EventArgs e)
        {
            try
            {
                gridView1.OptionsView.ShowGroupPanel = false;
                gridView1.Columns["WarehouseName"].UnGroup();
                gridView1.Columns["Material_Name"].UnGroup();

                // 그룹 해제 시 컬럼 스타일/언어는 그대로 유지 -> 재초기화 비용 제거
                gridView1.BestFitColumns();
            }
            catch (Exception ex)
            {
                LogUtil.WriteError("cmsViewUnGroup_Click failed", ex);
            }
        }

        #endregion

        #region UserDefined

        // 핵심: 비동기 조회. 기존 FnStock1을 대체.
        private async Task LoadStockAsync()
        {
            // 취소 토큰 사용 (폼 닫기 시 취소 가능)
            var ct = _cts.Token;

            string warehouse = lueWarehouse.EditValue?.ToString() ?? string.Empty;
            string custId = slueCustName.EditValue?.ToString() ?? string.Empty;
            string matSpec = txtMaterialSpec.Text ?? string.Empty;

            try
            {
                WaitFormUtil.Instnace.ShowWaitForm(this, "데이터 조회 중...");

                // Repository에서 안전하게 파라미터 바인딩 후 DataTable 반환
                DataTable dt = await _stockRepo.GetInventoryAsync(warehouse, custId, matSpec, ct).ConfigureAwait(false);

                // UI 스레드에서 그리드 바인딩
                if (this.IsHandleCreated)
                {
                    this.Invoke(new Action(() =>
                    {
                        gridControl1.DataSource = dt;
                        gridView1.BestFitColumns();

                        if (gridView1.GroupCount > 0)
                        {
                            gridView1.ExpandAllGroups();
                        }

                        ApplyColumnStyles(); // 로드 후 스타일 재적용
                    }));
                }
            }
            catch (OperationCanceledException)
            {
                LogUtil.WriteInfo("LoadStockAsync cancelled by user.");
                throw;
            }
            catch (Exception ex)
            {
                LogUtil.WriteError("LoadStockAsync failed", ex);
                // 사용자에게는 친절한 메시지
                if (this.IsHandleCreated)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show("재고 조회 중 오류가 발생했습니다. 관리자에게 문의하거나 로그를 확인하세요.");
                    }));
                }
            }
            finally
            {
                try { WaitFormUtil.Instnace.HideWaitForm(); } catch { }
            }
        }

        #endregion UserDefined
    }
}
