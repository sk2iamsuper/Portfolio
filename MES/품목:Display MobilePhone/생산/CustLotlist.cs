#region namespace
using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Repository;
using DevCommon;
#endregion

namespace Module.CustPop
{
    /// <summary>
    /// 고객 LOT 리스트를 조회하고 Excel로 내보내는 화면
    /// </summary>
    public partial class FrmCustLotList : XtraForm
    {
        // 공정코드 (현재는 0001로 고정)
        private const string ProcessCode = "0001";

        public FrmCustLotList()
        {
            InitializeComponent();
        }

        #region Form Initialize
        private void FrmCustLotList_Load(object sender, EventArgs e)
        {
            // 폼 로드 시 기본 초기화 수행
            InitializeForm();
        }

        /// <summary>
        /// 폼 타이틀, 컨트롤, 다국어 설정 초기화
        /// </summary>
        private void InitializeForm()
        {
            lblTitle.Text = Text;
            InitializeControl();    // UI 기본값 설정
            InitializeLanguage();   // 언어 적용
        }

        /// <summary>
        /// 날짜, 시간, 고객사 검색 조건 및 그리드 초기화
        /// </summary>
        private void InitializeControl()
        {
            try
            {
                // 날짜 기본값: 시작일 = 어제, 종료일 = 오늘
                dptEdt.Text = ComboUtil.Instance.SET_DT(0).ToString();
                dptSdt.Text = ComboUtil.Instance.SET_DT(-1).ToString();

                // 시간 기본값 설정 (08시~다음날 07:59)
                dttStt.EditValue = "08:00:00";
                dttEtt.EditValue = "07:59:59";

                // 고객사 검색 콤보박스 바인딩
                DevLookupEditUtil.Instance.bindSearchLookupEdit_Customer(slueCustName);

                // 고객사 로그인 여부에 따라 ReadOnly 설정
                bool isCustomer = GlobalUtil.Instance.SysType.Equals("CUST");
                slueCustName.EditValue = isCustomer ? GlobalUtil.Instance.CustID : "";
                slueCustName.Properties.ReadOnly = isCustomer;

                // LOT 그리드 초기화
                GridHelper.InitializeLotGrid(gridView1);
            }
            catch (Exception ex)
            {
                ErrorHelper.Show("InitializeControl", ex);
            }
        }

        /// <summary>
        /// 다국어(한국어 외) 적용
        /// </summary>
        private void InitializeLanguage()
        {
            if (GlobalUtil.Instance.LangType.Equals("KO")) return;

            try
            {
                // 언어 리소스 가져오기
                var labels = LanguageUtil.BindTableCellText(Name, GlobalUtil.Instance.LangType);
                ApplyLanguage(labels);
            }
            catch (Exception ex)
            {
                ErrorHelper.Show("InitializeLanguage", ex);
            }
        }

        /// <summary>
        /// 가져온 언어 리소스를 UI 컨트롤에 적용
        /// </summary>
        private void ApplyLanguage(Hashtable ht)
        {
            xtraTabPage2.Text = ht["xtraTabPage2"].ToString();
            labelControl9.Text = ht["labelControl9"].ToString();
            labelControl5.Text = ht["labelControl5"].ToString();
            labelControl6.Text = ht["labelControl6"].ToString();
            labelControl7.Text = ht["labelControl7"].ToString();
            tab2_btnExcel.Text = ht["tab2_btnExcel"].ToString();
            tab2_btnSearch.Text = ht["tab2_btnSearch"].ToString();
        }
        #endregion

        #region Event
        // 품목명 입력 후 엔터 → 검색 실행
        private void txtItemName_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) DoSearch();
        }

        // LotNo 입력 후 엔터 → 검색 실행
        private void txtLotNo_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13) DoSearch();
        }

        // [검색] 버튼 클릭
        private void tab2_btnSearch_Click(object sender, EventArgs e)
        {
            DoSearch();
        }

        // [엑셀] 버튼 클릭 → 그리드 내보내기
        private void tab2_btnExcel_Click(object sender, EventArgs e)
        {
            DevGridUtil.Instance.Export2(gridView1, "Product_LotList");
        }
        #endregion

        #region Search
        /// <summary>
        /// 검색 조건을 기반으로 LOT 리스트 조회
        /// </summary>
        private void DoSearch()
        {
            try
            {
                // 로딩(대기) 폼 표시
                WaitFormUtil.Instnace.ShowWaitForm(this);

                // 검색 수행
                var dt = SearchHelper.SearchLotList(
                    dptSdt.Text, dptEdt.Text,
                    dttStt.Text, dttEtt.Text,
                    slueCustName.EditValue?.ToString() ?? "",
                    txtItemName.Text, txtLotNo.Text
                );

                // 그리드에 바인딩
                gridControl1.DataSource = dt;

                // 데이터가 있으면 컬럼 너비 자동 조정
                if (dt.Rows.Count > 0)
                    gridView1.BestFitColumns();
            }
            catch (Exception ex)
            {
                ErrorHelper.Show("Search", ex);
            }
            finally
            {
                // 로딩 폼 닫기
                WaitFormUtil.Instnace.HideWaitForm();
            }
        }
        #endregion
    }

    // ---------------- 유틸리티 클래스 분리 ----------------

    /// <summary>
    /// 그리드 초기화 관련 유틸리티
    /// </summary>
    internal static class GridHelper
    {
        public static void InitializeLotGrid(DevExpress.XtraGrid.Views.Grid.GridView gridView)
        {
            var numeric = CreateNumericEditor();

            // 컬럼 구성 정의
            AddColumn(gridView, "작업지시서-NO", "LotNo", 100);
            AddColumn(gridView, "순번", "LotSeq", 30);
            AddColumn(gridView, "타발", "PressCnt", 30);
            AddColumn(gridView, "구분", "ResultDiv", 30);
            AddColumn(gridView, "작업번호", "ppWorkNo", 80);
            AddColumn(gridView, "작업지시일자", "ppWorkDt", 70);
            AddColumn(gridView, "주/야", "WorkPart", 60);
            AddColumn(gridView, "고객사", "CustName", 100);
            AddColumn(gridView, "고객품번", "Cust_ItemID", 80);
            AddColumn(gridView, "고객품명", "Cust_ItemName", 100);
            AddColumn(gridView, "품번", "ItemId", 70);
            AddColumn(gridView, "ProjectName", "ItemName", 100);
            AddColumn(gridView, "Lot단위", "LotQty", 70, numeric);
            AddColumn(gridView, "실적수량", "PressQty", 70, numeric, Color.Blue);

            // 합계 표시
            gridView.OptionsView.ShowFooter = true;
            gridView.Columns["PressQty"].Summary.Add(DevExpress.Data.SummaryItemType.Sum, "PressQty", "{0:###,###}");
        }

        // 컬럼 추가 공통 함수
        private static void AddColumn(
            DevExpress.XtraGrid.Views.Grid.GridView grid,
            string caption, string field, int width,
            RepositoryItem editor = null, Color? color = null)
        {
            DevGridUtil.Instance.InitializeGrid(grid, -1, caption, field, field,
                editor ?? new RepositoryItemTextEdit(),
                DevExpress.Utils.HorzAlignment.Center,
                DevExpress.Utils.VertAlignment.Center,
                true, width, false);

            // 컬럼 색상 지정 (필요 시)
            if (color.HasValue)
                grid.Columns[field].AppearanceCell.ForeColor = color.Value;
        }

        // 숫자 표시용 에디터 생성
        private static RepositoryItemTextEdit CreateNumericEditor()
        {
            var numeric = new RepositoryItemTextEdit();
            numeric.Mask.EditMask = "###,###,###,##0;(###,###,###,##0)";
            numeric.Mask.MaskType = DevExpress.XtraEditors.Mask.MaskType.Numeric;
            numeric.Mask.UseMaskAsDisplayFormat = true;
            return numeric;
        }
    }

    /// <summary>
    /// DB 검색 관련 헬퍼 클래스
    /// </summary>
    internal static class SearchHelper
    {
        public static DataTable SearchLotList(
            string sdt, string edt, string stt, string ett,
            string custId, string itemId, string lotNo)
        {
            const string spName = "SP_CUST_PP_WORKLIST_SELECT01_REV1";

            // 파라미터 구성
            var parameters = new[]
            {
                new SqlParameter("SDT", $"{sdt.Trim()} {stt}"),
                new SqlParameter("EDT", $"{edt.Trim()} {ett}"),
                new SqlParameter("CustID", custId),
                new SqlParameter("ITEMID", itemId.Trim()),
                new SqlParameter("LOTNO", lotNo.Trim())
            };

            // 저장 프로시저 실행
            return DatabaseUtil.Instance.ExecuteSP(spName, parameters);
        }
    }

    /// <summary>
    /// 예외 메시지 처리 헬퍼
    /// </summary>
    internal static class ErrorHelper
    {
        public static void Show(string location, Exception ex)
        {
            MessageBox.Show(
                $"{location} Error\r\n{ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
