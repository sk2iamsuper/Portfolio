#region namespace
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
//Add
using DevCommon;
using System.Data.SqlClient;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Grid;
using System.Collections;
#endregion namespace

namespace SG_Module.CustPop
{
    /// <summary>
    /// 고객 프레스 자재 재고 관리 폼
    /// 프레스 공정에서 사용하는 자재의 재고 현황을 조회하고 관리하는 기능 제공
    /// </summary>
    public partial class FrmCustPPMatStock : DevExpress.XtraEditors.XtraForm
    {
        #region 상수 정의
        // SQL 쿼리 및 설정값 상수화
        private const string WAREHOUSE_QUERY = @"SELECT WarehouseCode AS CDE, WarehouseName AS NAME 
                                               FROM co_Warehouse WITH (NOLOCK) 
                                               WHERE WarehouseType='W02' 
                                               AND WarehouseCode IN ('WP0','WPH')";
        
        private const string STORED_PROCEDURE_NAME = "SP_CUST_PP_MATSTOCK_SELECT2_REV1";
        private const string DEFAULT_WAREHOUSE = "WP0";
        private const string SYSTEM_TYPE_CUSTOMER = "CUST";
        private const string LANGUAGE_KOREAN = "KO";
        #endregion

        #region Constructor

        /// <summary>
        /// 생성자 - 폼 컴포넌트 초기화
        /// </summary>
        public FrmCustPPMatStock()
        {
            InitializeComponent();
        }

        #endregion Constructor

        #region Initialize

        /// <summary>
        /// 폼 초기화 메인 메서드
        /// </summary>
        private void InitializeForm()
        {
            try
            {
                InitializeControl();
                InitializeGridStockRoll();
                InitializeLang();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("폼 초기화 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 컨트롤 초기화 - 데이터 바인딩 및 기본값 설정
        /// </summary>
        private void InitializeControl()
        {
            try
            {
                // 1. 창고 정보 LookupEdit 바인딩
                DevLookupEditUtil.Instance.bindSLookupEdit_Sql(lueWarehouse, WAREHOUSE_QUERY);
                lueWarehouse.EditValue = DEFAULT_WAREHOUSE;

                // 2. 고객명 SearchLookupEdit 바인딩
                DevLookupEditUtil.Instance.bindSearchLookupEdit_Customer(slueCustName);

                // 3. 시스템 타입에 따른 컨트롤 상태 설정
                SetControlsBySystemType();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("컨트롤 초기화 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 시스템 타입에 따라 컨트롤의 읽기 전용 상태 설정
        /// </summary>
        private void SetControlsBySystemType()
        {
            bool isCustomerSystem = GlobalUtil.Instance.SysType.Equals(SYSTEM_TYPE_CUSTOMER);
            
            if (isCustomerSystem)
            {
                // 고객 시스템: 고정값 설정 및 읽기 전용
                slueCustName.EditValue = GlobalUtil.Instance.CustID.ToString();
                slueCustName.Properties.ReadOnly = true;
                lueWarehouse.Properties.ReadOnly = true;
            }
            else
            {
                // 관리자 시스템: 빈값 설정 및 편집 가능
                slueCustName.EditValue = "";
                slueCustName.Properties.ReadOnly = false;
                lueWarehouse.Properties.ReadOnly = false;
            }
        }

        /// <summary>
        /// 다국어 지원 초기화
        /// </summary>
        private void InitializeLang()
        {
            // 한국어인 경우 다국어 처리 생략
            if (GlobalUtil.Instance.LangType.ToString().Equals(LANGUAGE_KOREAN))
                return;

            try
            {
                string formName = this.Name;
                Hashtable languageTexts = new Hashtable();

                // 폼 컨트롤 텍스트 다국어 처리
                languageTexts = LanguageUtil.BindTableCellText(formName, GlobalUtil.Instance.LangType.ToString());
                ApplyControlTexts(languageTexts);

                // 그리드 컬럼 텍스트 다국어 처리
                languageTexts = LanguageUtil.BindTableCellText(formName + ".GRID2", GlobalUtil.Instance.LangType.ToString());
                LanguageUtil.setGridViewText(gridView2, languageTexts);
                
                languageTexts.Clear();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("다국어 초기화 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 컨트롤에 다국어 텍스트 적용
        /// </summary>
        private void ApplyControlTexts(Hashtable languageTexts)
        {
            labelControl1.Text = languageTexts["labelControl1"]?.ToString();
            labelControl3.Text = languageTexts["labelControl3"]?.ToString();
            labelControl2.Text = languageTexts["labelControl2"]?.ToString();
            labelControl4.Text = languageTexts["labelControl4"]?.ToString();

            btnExcel.Text = languageTexts["btnExcel"]?.ToString();
            btnSearch.Text = languageTexts["btnSearch"]?.ToString();
            xtraTabPage2.Text = languageTexts["xtraTabPage2"]?.ToString();
        }

        #endregion Initialize

        #region InitializeGridSet

        /// <summary>
        /// 재고 현황 그리드 초기화 - 컬럼 설정 및 스타일 적용
        /// </summary>
        private void InitializeGridStockRoll()
        {
            try
            {
                // RepositoryItem 생성
                var textEdit = CreateTextEditRepository();
                var numericTextEdit = CreateNumericTextEditRepository();

                // 그리드 컬럼 초기화
                InitializeGridColumns(textEdit, numericTextEdit);

                // 그리드 스타일 및 옵션 설정
                ConfigureGridAppearance();

                // 컬럼 색상 강조 설정
                ApplyColumnHighlighting();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("그리드 초기화 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 일반 텍스트 편집 Repository 생성
        /// </summary>
        private RepositoryItemTextEdit CreateTextEditRepository()
        {
            return (RepositoryItemTextEdit)DevGridUtil.Instance.SetRepositoryItem(
                DevGridUtil.ItemType.RepositoryItemTextEdit);
        }

        /// <summary>
        /// 숫자 형식 텍스트 편집 Repository 생성
        /// </summary>
        private RepositoryItemTextEdit CreateNumericTextEditRepository()
        {
            var numericEdit = (RepositoryItemTextEdit)DevGridUtil.Instance.SetRepositoryItem(
                DevGridUtil.ItemType.RepositoryItemTextEdit);
            
            numericEdit.Mask.EditMask = "###,###,###,##0;-###,###,###,##0";
            numericEdit.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.Numeric;
            numericEdit.Mask.UseMaskAsDisplayFormat = true;
            
            return numericEdit;
        }

        /// <summary>
        /// 그리드 컬럼 초기화
        /// </summary>
        private void InitializeGridColumns(RepositoryItemTextEdit textEdit, RepositoryItemTextEdit numericTextEdit)
        {
            int columnIndex = 0;

            // 기본 정보 컬럼
            InitializeGridColumn(columnIndex++, "WarehouseCde", "WarehouseCde", "WarehouseCde", 
                               textEdit, DevExpress.Utils.HorzAlignment.Center, 70, false);
            InitializeGridColumn(columnIndex++, "창고정보", "WarehouseName", "WarehouseName", 
                               textEdit, DevExpress.Utils.HorzAlignment.Center, 100, true);
            InitializeGridColumn(columnIndex++, "자재코드", "Material_ID", "Material_ID", 
                               textEdit, DevExpress.Utils.HorzAlignment.Center, 90, true);
            InitializeGridColumn(columnIndex++, "자재명", "Material_Name", "Material_Name", 
                               textEdit, DevExpress.Utils.HorzAlignment.Near, 120, true);
            InitializeGridColumn(columnIndex++, "BatchID", "BatchID", "BatchID", 
                               textEdit, DevExpress.Utils.HorzAlignment.Center, 60, false);

            // 자재 규격 컬럼
            InitializeGridColumn(columnIndex++, "폭(mm)", "Material_Width", "Material_Width", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);
            InitializeGridColumn(columnIndex++, "길이(M)", "Material_Length", "Material_Length", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);

            // 현재 재고 컬럼
            InitializeGridColumn(columnIndex++, "재고(R/L)", "StockRL", "StockRL", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);
            InitializeGridColumn(columnIndex++, "재고(M)", "StockQtyM", "StockQtyM", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);

            // 기초 재고 컬럼
            InitializeGridColumn(columnIndex++, "기초재고(RL)", "BaseStock", "BaseStock", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);
            InitializeGridColumn(columnIndex++, "기초길이(M)", "BaseStockM", "BaseStockM", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);

            // 입고 재고 컬럼
            InitializeGridColumn(columnIndex++, "입고재고(RL)", "Stock_InQty", "Stock_InQty", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);
            InitializeGridColumn(columnIndex++, "입고길이(M)", "Stock_InQtyM", "Stock_InQtyM", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);

            // 회수 재고 컬럼
            InitializeGridColumn(columnIndex++, "회수재고(RL)", "Stock_ReturnInQty", "Stock_ReturnInQty", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);
            InitializeGridColumn(columnIndex++, "회수길이(M)", "Stock_ReturnInQtyM", "Stock_ReturnInQtyM", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);

            // 생산 투입 컬럼
            InitializeGridColumn(columnIndex++, "생산투입(RL)", "Stock_OutQty", "Stock_OutQty", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);
            InitializeGridColumn(columnIndex++, "투입길이(M)", "Stock_OutQtyM", "Stock_OutQtyM", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);

            // 창고 반납 컬럼
            InitializeGridColumn(columnIndex++, "창고반납(RL)", "Stock_ReturnQty", "Stock_ReturnQty", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);
            InitializeGridColumn(columnIndex++, "반납길이(M)", "Stock_ReturnQtyM", "Stock_ReturnQtyM", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);

            // 재고 조정 컬럼
            InitializeGridColumn(columnIndex++, "재고조정(RL)", "Stock_LossQty", "Stock_LossQty", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);
            InitializeGridColumn(columnIndex++, "조정길이(RL)", "Stock_LossQtyM", "Stock_LossQtyM", 
                               numericTextEdit, DevExpress.Utils.HorzAlignment.Far, 60, true);
        }

        /// <summary>
        /// 개별 그리드 컬럼 초기화 헬퍼 메서드
        /// </summary>
        private void InitializeGridColumn(int index, string caption, string fieldName, string name,
                                        RepositoryItemTextEdit repository, 
                                        DevExpress.Utils.HorzAlignment alignment, 
                                        int width, bool visible)
        {
            DevGridUtil.Instance.InitializeGrid(this.gridView2, index, caption, fieldName, name, 
                                              repository, alignment, DevExpress.Utils.VertAlignment.Center, 
                                              visible, width, false);
        }

        /// <summary>
        /// 그리드 외관 설정
        /// </summary>
        private void ConfigureGridAppearance()
        {
            // 그리드 크기 설정
            DevGridUtil.Instance.SetHeaderRowHeight(gridView2, 35);
            gridView2.RowHeight = 29;

            // 스크롤 설정
            DevGridUtil.Instance.ScrollVisibility(gridView2, 
                DevExpress.XtraGrid.Views.Base.ScrollVisibility.Auto, 
                DevExpress.XtraGrid.Views.Base.ScrollVisibility.Auto);

            // 그리드 옵션 설정
            gridView2.OptionsBehavior.Editable = true;
            gridView2.OptionsBehavior.EditorShowMode = DevExpress.Utils.EditorShowMode.MouseDownFocused;
            gridView2.OptionsSelection.MultiSelect = true;
            gridView2.OptionsSelection.MultiSelectMode = DevExpress.XtraGrid.Views.Grid.GridMultiSelectMode.CellSelect;
            gridView2.OptionsBehavior.CopyToClipboardWithColumnHeaders = false;
            gridView2.OptionsBehavior.ImmediateUpdateRowPosition = true;
            gridView2.OptionsBehavior.AutoSelectAllInEditor = true;
            gridView2.OptionsNavigation.EnterMoveNextColumn = false;
            
            // 행 색상 교차 설정
            gridView2.OptionsView.EnableAppearanceEvenRow = true;
            gridView2.OptionsView.EnableAppearanceOddRow = true;
        }

        /// <summary>
        /// 중요 컬럼 색상 강조 적용
        /// </summary>
        private void ApplyColumnHighlighting()
        {
            // 파란색 강조: 자재 규격 정보
            gridView2.Columns["Material_Width"].AppearanceCell.ForeColor = Color.Blue;
            gridView2.Columns["Material_Length"].AppearanceCell.ForeColor = Color.Blue;

            // 빨간색 강조: 현재 재고 (가장 중요한 정보)
            var redBoldFont = new Font("Tahoma", 9, FontStyle.Bold);
            gridView2.Columns["StockRL"].AppearanceCell.ForeColor = Color.Red;
            gridView2.Columns["StockRL"].AppearanceCell.Font = redBoldFont;
            gridView2.Columns["StockQtyM"].AppearanceCell.ForeColor = Color.Red;
            gridView2.Columns["StockQtyM"].AppearanceCell.Font = redBoldFont;

            // 검은색: 기타 재고 정보
            ApplyBlackTextColor("BaseStockM", "Stock_InQtyM", "Stock_ReturnInQtyM", 
                              "Stock_OutQtyM", "Stock_ReturnQtyM", "Stock_LossQtyM");

            // 파란색: RL 단위 재고 이동 정보
            ApplyBlueTextColor("BaseStock", "Stock_InQty", "Stock_ReturnInQty", 
                             "Stock_OutQty", "Stock_ReturnQty", "Stock_LossQty");
        }

        /// <summary>
        /// 검은색 텍스트 컬럼 적용
        /// </summary>
        private void ApplyBlackTextColor(params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                if (gridView2.Columns[columnName] != null)
                    gridView2.Columns[columnName].AppearanceCell.ForeColor = Color.Black;
            }
        }

        /// <summary>
        /// 파란색 텍스트 컬럼 적용
        /// </summary>
        private void ApplyBlueTextColor(params string[] columnNames)
        {
            foreach (var columnName in columnNames)
            {
                if (gridView2.Columns[columnName] != null)
                    gridView2.Columns[columnName].AppearanceCell.ForeColor = Color.Blue;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// 폼 로드 이벤트 - 초기화 실행
        /// </summary>
        private void FrmCustPPMatStock_Load(object sender, EventArgs e)
        {
            InitializeForm();
        }

        #endregion Events

        #region Events::Button

        /// <summary>
        /// 조회 버튼 클릭 이벤트 - 재고 데이터 조회
        /// </summary>
        private async void btnSearch_Click(object sender, EventArgs e)
        {
            await ExecuteSearchAsync();
        }

        /// <summary>
        /// 엑셀 내보내기 버튼 클릭 이벤트
        /// </summary>
        private void btnExcel_Click(object sender, EventArgs e)
        {
            try
            {
                DevGridUtil.Instance.Export2(gridView2, "Press_Material_Stock");
            }
            catch (Exception ex)
            {
                ShowErrorMessage("엑셀 내보내기 중 오류가 발생했습니다.", ex);
            }
        }

        #endregion

        #region UserDefined

        /// <summary>
        /// 비동기 조회 실행 메서드
        /// </summary>
        private async System.Threading.Tasks.Task ExecuteSearchAsync()
        {
            try
            {
                WaitFormUtil.Instnace.ShowWaitForm(this);
                
                // 비동기 조회 실행 (실제 비동기 처리를 위해 Task.Run 사용)
                await System.Threading.Tasks.Task.Run(() => fnSearchPPMatStockRoll());
            }
            catch (Exception ex)
            {
                ShowErrorMessage("데이터 조회 중 오류가 발생했습니다.", ex);
            }
            finally
            {
                WaitFormUtil.Instnace.HideWaitForm();
            }
        }

        /// <summary>
        /// 프레스 자재 재고 현황 조회 메인 메서드
        /// </summary>
        private void fnSearchPPMatStockRoll()
        {
            try
            {
                // 안전한 파라미터 처리로 SQL 인젝션 방지
                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@WarehouseCde", lueWarehouse.EditValue?.ToString() ?? ""),
                    new SqlParameter("@CustID", slueCustName.EditValue?.ToString() ?? ""),
                    new SqlParameter("@Material_Name", txtMaterialSpec.Text.Trim()),
                    new SqlParameter("@Material_Width", Material_Width.Text ?? "")
                };

                // 저장 프로시저 실행
                DataTable dt = DatabaseUtil.Instance.ExecuteStoredProcedure(STORED_PROCEDURE_NAME, parameters);

                // UI 스레드에서 그리드 업데이트
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action<DataTable>(UpdateGridData), dt);
                }
                else
                {
                    UpdateGridData(dt);
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("재고 현황 조회 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 그리드 데이터 업데이트
        /// </summary>
        private void UpdateGridData(DataTable data)
        {
            gridControl2.DataSource = data;

            if (data.Rows.Count > 0)
            {
                gridView2.BestFitColumns();
            }
            else
            {
                MessageBox.Show("조회된 데이터가 없습니다.", "알림", 
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 통합 에러 메시지 표시 메서드
        /// </summary>
        private void ShowErrorMessage(string message, Exception ex)
        {
            string fullMessage = $"{message}\r\n상세 오류: {ex.Message}";
            
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string>(msg => 
                    MessageBox.Show(msg, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error)), fullMessage);
            }
            else
            {
                MessageBox.Show(fullMessage, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // 실제 애플리케이션에서는 로깅 시스템에 기록
            System.Diagnostics.Debug.WriteLine($"Error: {fullMessage}\nStackTrace: {ex.StackTrace}");
        }

        #endregion UserDefined
    }
}
