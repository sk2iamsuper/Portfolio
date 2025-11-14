#region namespace
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Data.SqlClient;  // 상단으로 이동
using System.Globalization;
using System.Text;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
//Add
using DevCommon;
using DevExpress.XtraEditors.Repository;
using DevExpress.XtraGrid.Views.BandedGrid;
using DevExpress.XtraGrid.Views.Grid;
using System.Collections;
#endregion namespace

namespace SG_Module.CustPop
{
    /// <summary>
    /// 고객별 재고 현황 조회 폼
    /// 주요 기능: 고객사별 재고 조회, 다국어 지원, Excel 내보내기
    /// </summary>
    public partial class FrmCustPPStock : DevExpress.XtraEditors.XtraForm
    {
        #region Private Fields
        
        // 상수 정의
        private const string SYSTEM_TYPE_CUST = "CUST";
        private const string LANGUAGE_KO = "KO";
        private const string STORED_PROCEDURE_NAME = "SP_CUST_PP_PROCESSINVENT_PRESS_SELECT1_REV1";
        private const string EXCEL_EXPORT_NAME = "Press_StockList";
        
        // 색상 상수
        private readonly Color READONLY_BACKGROUND_COLOR = Color.LightGray;
        private readonly Color INBOUND_COLOR = Color.Blue;
        private readonly Color OUTBOUND_COLOR = Color.Black;
        private readonly Color CURRENT_STOCK_COLOR = Color.Red;
        
        #endregion

        #region Constructor

        /// <summary>
        /// 기본 생성자 - 폼 컴포넌트 초기화
        /// </summary>
        public FrmCustPPStock()
        {
            try
            {
                InitializeComponent();
                Logger.Info("FrmCustPPStock 폼이 성공적으로 초기화되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "폼 초기화 중 오류가 발생했습니다.");
                throw;
            }
        }

        #endregion Constructor

        #region Initialize Methods

        /// <summary>
        /// 폼 전체 초기화 - 각 구성 요소 초기화 메서드 호출
        /// </summary>
        private void InitializeForm()
        {
            try
            {
                lblTitle.Text = this.Text ?? "재고 관리";
                InitializeControl();
                InitializeGridAssy();
                InitializeLang();
                
                Logger.Info("폼 초기화가 완료되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "폼 초기화 중 오류가 발생했습니다.");
                ShowErrorMessage("폼을 초기화하는 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 컨트롤 초기화 - 고객사 검색 컨트롤 설정 및 시스템 유형에 따른 편집 모드 설정
        /// </summary>
        private void InitializeControl()
        {
            try
            {
                // 고객사 검색 LookupEdit 바인딩
                DevLookupEditUtil.Instance.bindSearchLookupEdit_Customer(slueCustName);

                // 시스템 유형에 따른 편집 모드 설정
                if (IsCustomerSystem())
                {
                    SetCustomerMode();
                }
                else
                {
                    SetNormalMode();
                }
                
                Logger.Info("컨트롤 초기화가 완료되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "컨트롤 초기화 중 오류가 발생했습니다.");
                throw; // 상위 메서드에서 처리하도록 전파
            }
        }

        /// <summary>
        /// 고객사 모드 설정 - 특정 고객사로 고정된 읽기 전용 모드
        /// </summary>
        private void SetCustomerMode()
        {
            slueCustName.EditValue = GlobalUtil.Instance.CustID.ToString();
            slueCustName.Properties.ReadOnly = true;
            slueCustName.Properties.Appearance.ReadOnly.BackColor = READONLY_BACKGROUND_COLOR;
        }

        /// <summary>
        /// 일반 모드 설정 - 모든 고객사 선택 가능한 편집 모드
        /// </summary>
        private void SetNormalMode()
        {
            slueCustName.EditValue = string.Empty;
            slueCustName.Properties.ReadOnly = false;
        }

        /// <summary>
        /// 시스템 유형이 고객사 모드인지 확인
        /// </summary>
        private bool IsCustomerSystem()
        {
            return SYSTEM_TYPE_CUST.Equals(GlobalUtil.Instance.SysType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 다국어 지원 초기화 - 한국어가 아닌 경우 다국어 리소스 적용
        /// </summary>
        private void InitializeLang()
        {
            try
            {
                if (!IsKoreanLanguage())
                {
                    ApplyLocalization();
                }
                
                Logger.Info("다국어 초기화가 완료되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "다국어 초기화 중 오류가 발생했습니다.");
                ShowErrorMessage("언어 설정을 적용하는 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 현재 언어가 한국어인지 확인
        /// </summary>
        private bool IsKoreanLanguage()
        {
            return LANGUAGE_KO.Equals(GlobalUtil.Instance.LangType?.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 다국어 리소스 적용 - 폼 텍스트와 그리드 헤더 텍스트 변경
        /// </summary>
        private void ApplyLocalization()
        {
            string formName = this.Name;
            Hashtable localizedTexts = null;

            try
            {
                // 폼 컨트롤 텍스트 지역화
                localizedTexts = LanguageUtil.BindTableCellText(formName, GlobalUtil.Instance.LangType.ToString());
                ApplyFormLocalization(localizedTexts);
                localizedTexts.Clear();

                // 그리드 헤더 텍스트 지역화
                localizedTexts = LanguageUtil.BindTableCellText(formName + ".GRID", GlobalUtil.Instance.LangType.ToString());
                LanguageUtil.setGridViewText(gridView1, localizedTexts);
            }
            finally
            {
                // 리소스 정리
                localizedTexts?.Clear();
            }
        }

        /// <summary>
        /// 폼 컨트롤에 지역화된 텍스트 적용
        /// </summary>
        private void ApplyFormLocalization(Hashtable localizedTexts)
        {
            if (localizedTexts == null) return;

            // 안전하게 컨트롤 텍스트 설정
            SetControlTextSafely(xtraTabPage1, localizedTexts, "xtraTabPage1");
            SetControlTextSafely(btnExcel, localizedTexts, "btnExcel");
            SetControlTextSafely(btnSearch, localizedTexts, "btnSearch");
            SetControlTextSafely(labelControl3, localizedTexts, "labelControl3");
            SetControlTextSafely(labelControl2, localizedTexts, "labelControl2");
        }

        /// <summary>
        /// 안전하게 컨트롤 텍스트 설정 - 키가 존재하는 경우에만 설정
        /// </summary>
        private void SetControlTextSafely(Control control, Hashtable localizedTexts, string key)
        {
            if (localizedTexts.ContainsKey(key) && control != null)
            {
                control.Text = localizedTexts[key].ToString();
            }
        }

        #endregion Initialize Methods

        #region Grid Initialization

        /// <summary>
        /// 그리드 초기화 - 컬럼 정의, 스타일 설정, 동작 구성
        /// </summary>
        private void InitializeGridAssy()
        {
            try
            {
                InitializeGridColumns();
                ApplyGridStyling();
                ConfigureGridBehavior();
                
                Logger.Info("그리드 초기화가 완료되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "그리드 초기화 중 오류가 발생했습니다.");
                throw;
            }
        }

        /// <summary>
        /// 그리드 컬럼 초기화 - 각 컬럼의 속성과 표시 형식 정의
        /// </summary>
        private void InitializeGridColumns()
        {
            // RepositoryItem 생성
            var checkEdit = CreateRepositoryCheckEdit();
            var textEdit = CreateRepositoryTextEdit();
            var numericEdit = CreateRepositoryNumericEdit();

            // 기본 정보 컬럼 (읽기 전용)
            AddReadOnlyColumns(textEdit);
            
            // 재고 수량 컬럼 (숫자 형식)
            AddStockQuantityColumns(numericEdit);
            
            // 날짜 컬럼
            DevGridUtil.Instance.InitializeGrid(this.gridView1, 15, "기준일자", "Stock_BaseDate", 
                "Stock_BaseDate", textEdit, DevExpress.Utils.HorzAlignment.Far, 
                DevExpress.Utils.VertAlignment.Center, true, 80, false);
        }

        /// <summary>
        /// 읽기 전용 기본 정보 컬럼 추가
        /// </summary>
        private void AddReadOnlyColumns(RepositoryItemTextEdit textEdit)
        {
            var readOnlyColumns = new[]
            {
                new { Index = 0, Caption = "ProcessID", FieldName = "WareHouseCde", Visible = false },
                new { Index = 1, Caption = "공정정보", FieldName = "WarehouseName", Visible = true },
                new { Index = 2, Caption = "CustID", FieldName = "CustID", Visible = false },
                new { Index = 3, Caption = "고객사", FieldName = "CustName", Visible = true },
                new { Index = 4, Caption = "고객품번", FieldName = "Cust_ItemID", Visible = true },
                new { Index = 5, Caption = "고객품명", FieldName = "Cust_ItemName", Visible = true },
                new { Index = 6, Caption = "세경코드", FieldName = "Orig_Cde", Visible = true },
                new { Index = 7, Caption = "POPID", FieldName = "ItemID", Visible = true }
            };

            foreach (var col in readOnlyColumns)
            {
                DevGridUtil.Instance.InitializeGrid(this.gridView1, col.Index, col.Caption, 
                    col.FieldName, col.FieldName, textEdit, 
                    col.Index == 3 || col.Index == 5 ? DevExpress.Utils.HorzAlignment.Near : DevExpress.Utils.HorzAlignment.Center,
                    DevExpress.Utils.VertAlignment.Center, col.Visible, 
                    col.Visible ? (col.Index == 3 ? 140 : (col.Index == 5 ? 150 : 100)) : 100, false);
            }
        }

        /// <summary>
        /// 재고 수량 관련 컬럼 추가 (숫자 형식)
        /// </summary>
        private void AddStockQuantityColumns(RepositoryItemTextEdit numericEdit)
        {
            var stockColumns = new[]
            {
                new { Index = 8, Caption = "이월재고", FieldName = "Stock_BaseQty" },
                new { Index = 9, Caption = "생산입고", FieldName = "Stock_InQty" },
                new { Index = 10, Caption = "기타입고", FieldName = "Stock_KitaInQty" },
                new { Index = 11, Caption = "전수출고", FieldName = "Stock_OutQty" },
                new { Index = 12, Caption = "재고조정", FieldName = "Stock_LossQty" },
                new { Index = 13, Caption = "기타출고", FieldName = "Stock_KitaOutQty" },
                new { Index = 14, Caption = "현재고", FieldName = "Stock_Qty" }
            };

            foreach (var col in stockColumns)
            {
                DevGridUtil.Instance.InitializeGrid(this.gridView1, col.Index, col.Caption, 
                    col.FieldName, col.FieldName, numericEdit, 
                    DevExpress.Utils.HorzAlignment.Far, DevExpress.Utils.VertAlignment.Center, true, 80, false);
            }
        }

        /// <summary>
        /// RepositoryItemCheckEdit 생성
        /// </summary>
        private RepositoryItemCheckEdit CreateRepositoryCheckEdit()
        {
            return (RepositoryItemCheckEdit)DevGridUtil.Instance.SetRepositoryItem(
                DevGridUtil.ItemType.RepositoryItemCheckEdit);
        }

        /// <summary>
        /// RepositoryItemTextEdit 생성 (일반 텍스트)
        /// </summary>
        private RepositoryItemTextEdit CreateRepositoryTextEdit()
        {
            return (RepositoryItemTextEdit)DevGridUtil.Instance.SetRepositoryItem(
                DevGridUtil.ItemType.RepositoryItemTextEdit);
        }

        /// <summary>
        /// RepositoryItemTextEdit 생성 (숫자 형식)
        /// </summary>
        private RepositoryItemTextEdit CreateRepositoryNumericEdit()
        {
            var numericEdit = (RepositoryItemTextEdit)DevGridUtil.Instance.SetRepositoryItem(
                DevGridUtil.ItemType.RepositoryItemTextEdit);
            
            numericEdit.Mask.EditMask = "###,###,###,##0;-###,###,###,##0";
            numericEdit.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.Numeric;
            numericEdit.Mask.UseMaskAsDisplayFormat = true;
            
            return numericEdit;
        }

        /// <summary>
        /// 그리드 스타일 적용 - 색상, 폰트, 행 높이 등
        /// </summary>
        private void ApplyGridStyling()
        {
            // 행 높이 설정
            DevGridUtil.Instance.SetHeaderRowHeight(gridView1, 35);
            gridView1.RowHeight = 29;

            // 스크롤 설정
            DevGridUtil.Instance.ScrollVisibility(gridView1, 
                DevExpress.XtraGrid.Views.Base.ScrollVisibility.Auto, 
                DevExpress.XtraGrid.Views.Base.ScrollVisibility.Auto);

            // 읽기 전용 컬럼 배경색
            ApplyReadOnlyColumnStyles();
            
            // 수량 컬럼 색상
            ApplyQuantityColumnStyles();
        }

        /// <summary>
        /// 읽기 전용 컬럼에 배경색 적용
        /// </summary>
        private void ApplyReadOnlyColumnStyles()
        {
            string[] readOnlyFields = { "WareHouseCde", "WarehouseName", "CustID", "CustName", 
                                      "Cust_ItemID", "Cust_ItemName", "Orig_Cde", "ItemID" };
            
            foreach (string field in readOnlyFields)
            {
                if (gridView1.Columns[field] != null)
                {
                    gridView1.Columns[field].AppearanceCell.BackColor = READONLY_BACKGROUND_COLOR;
                }
            }
        }

        /// <summary>
        /// 수량 컬럼에 색상과 스타일 적용
        /// </summary>
        private void ApplyQuantityColumnStyles()
        {
            // 입고 관련 (파란색)
            ApplyColorToColumns(new[] { "Stock_BaseQty", "Stock_InQty", "Stock_KitaInQty" }, INBOUND_COLOR);
            
            // 출고 관련 (검정색)
            ApplyColorToColumns(new[] { "Stock_OutQty", "Stock_LossQty", "Stock_KitaOutQty" }, OUTBOUND_COLOR);
            
            // 현재고 (빨간색, 굵게)
            if (gridView1.Columns["Stock_Qty"] != null)
            {
                gridView1.Columns["Stock_Qty"].AppearanceCell.ForeColor = CURRENT_STOCK_COLOR;
                gridView1.Columns["Stock_Qty"].AppearanceCell.Font = new Font("Tahoma", 9, FontStyle.Bold);
            }
        }

        /// <summary>
        /// 여러 컬럼에 동일한 색상 적용
        /// </summary>
        private void ApplyColorToColumns(string[] fieldNames, Color color)
        {
            foreach (string field in fieldNames)
            {
                if (gridView1.Columns[field] != null)
                {
                    gridView1.Columns[field].AppearanceCell.ForeColor = color;
                }
            }
        }

        /// <summary>
        /// 그리드 동작 구성 - 편집, 선택, 네비게이션 설정
        /// </summary>
        private void ConfigureGridBehavior()
        {
            gridView1.OptionsBehavior.Editable = true;
            gridView1.OptionsBehavior.EditorShowMode = DevExpress.Utils.EditorShowMode.MouseDownFocused;
            gridView1.OptionsSelection.MultiSelect = true;
            gridView1.OptionsSelection.MultiSelectMode = DevExpress.XtraGrid.Views.Grid.GridMultiSelectMode.CellSelect;
            gridView1.OptionsBehavior.CopyToClipboardWithColumnHeaders = false;
            gridView1.OptionsBehavior.ImmediateUpdateRowPosition = true;
            gridView1.OptionsBehavior.AutoSelectAllInEditor = true;
            gridView1.OptionsNavigation.EnterMoveNextColumn = true;
            gridView1.OptionsView.EnableAppearanceEvenRow = true;
            gridView1.OptionsView.EnableAppearanceOddRow = true;
            gridView1.OptionsView.ShowGroupPanel = false;
        }

        #endregion Grid Initialization

        #region Event Handlers

        /// <summary>
        /// 폼 로드 이벤트 - 폼 초기화 실행
        /// </summary>
        private void FrmCustPPStock_Load(object sender, EventArgs e)
        {
            try
            {
                InitializeForm();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "폼 로드 중 오류가 발생했습니다.");
                ShowErrorMessage("폼을 로드하는 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// 검색 버튼 클릭 이벤트 - 재고 데이터 조회
        /// </summary>
        private void btnSearch_Click(object sender, EventArgs e)
        {
            try
            {
                SearchInventoryData();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "재고 데이터 조회 중 오류가 발생했습니다.");
                ShowErrorMessage("데이터를 조회하는 중 오류가 발생했습니다.");
            }
        }

        /// <summary>
        /// Excel 내보내기 버튼 클릭 이벤트
        /// </summary>
        private void btnExcel_Click(object sender, EventArgs e)
        {
            try
            {
                DevGridUtil.Instance.Export2(gridView1, EXCEL_EXPORT_NAME);
                Logger.Info("Excel 내보내기가 완료되었습니다.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Excel 내보내기 중 오류가 발생했습니다.");
                ShowErrorMessage("Excel 파일로 내보내는 중 오류가 발생했습니다.");
            }
        }

        #endregion Event Handlers

        #region Business Logic Methods

        /// <summary>
        /// 재고 데이터 검색 - 저장 프로시저 호출 및 결과 바인딩
        /// </summary>
        private void SearchInventoryData()
        {
            WaitFormUtil.Instnace.ShowWaitForm(this);

            try
            {
                DataTable data = ExecuteInventoryQuery();
                BindDataToGrid(data);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "재고 데이터 검색 중 오류가 발생했습니다.");
                ShowErrorMessage("재고 데이터를 검색하는 중 오류가 발생했습니다.");
                gridControl1.DataSource = null;
            }
            finally
            {
                WaitFormUtil.Instnace.HideWaitForm();
            }
        }

        /// <summary>
        /// 재고 조회 쿼리 실행 - 매개변수화된 쿼리 사용
        /// </summary>
        private DataTable ExecuteInventoryQuery()
        {
            // 매개변수화된 쿼리 사용 (SQL Injection 방지)
            var parameters = new SqlParameter[]
            {
                new SqlParameter("@CustID", GetCustomerId()),
                new SqlParameter("@ItemName", GetItemNameFilter())
            };

            return DatabaseUtil.Instance.ExecuteStoredProcedure(STORED_PROCEDURE_NAME, parameters);
        }

        /// <summary>
        /// 고객사 ID 가져오기 - null 안전하게 처리
        /// </summary>
        private string GetCustomerId()
        {
            return slueCustName.EditValue?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// 품명 필터 값 가져오기 - null 및 공백 처리
        /// </summary>
        private string GetItemNameFilter()
        {
            return string.IsNullOrWhiteSpace(txtItemName.Text) ? string.Empty : txtItemName.Text.Trim();
        }

        /// <summary>
        /// 데이터를 그리드에 바인딩 - 결과에 따른 분기 처리
        /// </summary>
        private void BindDataToGrid(DataTable data)
        {
            if (data == null || data.Rows.Count == 0)
            {
                gridControl1.DataSource = null;
                ShowInformationMessage("조회된 데이터가 없습니다.");
                Logger.Info("조회된 재고 데이터가 없습니다.");
                return;
            }

            gridControl1.DataSource = data;
            gridView1.BestFitColumns();
            Logger.Info($"총 {data.Rows.Count}개의 재고 데이터를 조회했습니다.");
        }

        #endregion Business Logic Methods

        #region Utility Methods

        /// <summary>
        /// 오류 메시지 표시 - 사용자 친화적인 메시지
        /// </summary>
        private void ShowErrorMessage(string message)
        {
            MessageBox.Show($"{message}\r\n자세한 내용은 로그를 참조하세요.", 
                          "오류", 
                          MessageBoxButtons.OK, 
                          MessageBoxIcon.Error);
        }

        /// <summary>
        /// 정보 메시지 표시
        /// </summary>
        private void ShowInformationMessage(string message)
        {
            MessageBox.Show(message, "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion Utility Methods
    }
}
