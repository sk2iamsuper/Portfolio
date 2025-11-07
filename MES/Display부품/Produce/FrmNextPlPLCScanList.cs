#region namespace
using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using DevExpress.XtraGrid.Views.Grid;
using DevCommon;
#endregion

namespace Module.ProdNextPL
{
    /// <summary>
    /// UTG / PL 스캔 결과 조회 화면
    /// (PLC에서 스캔된 이력은 DB 저장 후, 이 폼에서 조회)
    /// </summary>
    public partial class FrmNextPlPLCScanList : XtraForm
    {
        // ──────────────────────────────────────────────
        // 전역 변수 및 상수
        // ──────────────────────────────────────────────
        private readonly string connectionString = UtilHelper.ConnectionString;   // DB 연결 문자열
        private readonly string spScanList = "SP_UTGLAMI_PLC_SACN_LIST";          // 스캔 조회용 Stored Procedure 이름

        // ──────────────────────────────────────────────
        // 생성자
        // ──────────────────────────────────────────────
        public FrmNextPlPLCScanList()
        {
            InitializeComponent(); // DevExpress 디자이너 초기화
        }

        // ──────────────────────────────────────────────
        // Form Load 이벤트: 최초 진입 시 UI 초기화
        // ──────────────────────────────────────────────
        private void FrmNextPlPLCScanList_Load(object sender, EventArgs e)
        {
            InitUI();
        }

        // ──────────────────────────────────────────────
        // [검색 버튼 클릭] → SP 실행 후 Grid에 바인딩
        // ──────────────────────────────────────────────
        private async void btnSearch_Click(object sender, EventArgs e)
        {
            await ExecuteSafe(async () =>
            {
                string qr = txtUtgQR.Text.Trim();
                if (string.IsNullOrEmpty(qr))
                    throw new Exception("UTG QR을 입력하세요.");

                // SP 호출 및 결과 DataTable 수신
                DataTable result = await GetPlcScanListAsync(qr);

                // 조회 결과 바인딩
                gridControl.DataSource = result;

                ShowLog($"{result.Rows.Count}건의 데이터가 조회되었습니다.");
            });
        }

        // ──────────────────────────────────────────────
        // [초기화 버튼 클릭] → 입력값 및 Grid 초기화
        // ──────────────────────────────────────────────
        private void btnInit_Click(object sender, EventArgs e)
        {
            ResetUI();
            ShowLog("화면이 초기화되었습니다.");
        }

        // ──────────────────────────────────────────────
        // [닫기 버튼 클릭]
        // ──────────────────────────────────────────────
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // ──────────────────────────────────────────────
        // DB 호출부 : PLC 스캔 내역 조회용 Stored Procedure 실행
        // ──────────────────────────────────────────────
        private async Task<DataTable> GetPlcScanListAsync(string utgQr)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(spScanList, conn))
            using (SqlDataAdapter da = new SqlDataAdapter(cmd))
            {
                cmd.CommandType = CommandType.StoredProcedure;

                // SP 입력 파라미터 전달
                cmd.Parameters.AddWithValue("@UTG_QR", utgQr);

                DataTable dt = new DataTable();
                await conn.OpenAsync();   // 비동기 DB 연결
                da.Fill(dt);              // DataTable 채우기
                return dt;
            }
        }

        // ──────────────────────────────────────────────
        // UI 초기화 : 화면 진입 시 기본 상태로 세팅
        // ──────────────────────────────────────────────
        private void InitUI()
        {
            txtUtgQR.Text = string.Empty;     // QR 입력란 초기화
            gridControl.DataSource = null;    // Grid 초기화
            lblStatus.Text = "준비 완료";      // 상태 표시
        }

        // ──────────────────────────────────────────────
        // UI 리셋 : 버튼 클릭 시 모든 입력/결과 초기화
        // ──────────────────────────────────────────────
        private void ResetUI()
        {
            txtUtgQR.Clear();
            gridControl.DataSource = null;
        }

        // ──────────────────────────────────────────────
        // 예외 처리 래퍼 : 비동기 작업 실행 시 안전하게 예외 잡기
        // ──────────────────────────────────────────────
        private async Task ExecuteSafe(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (SqlException ex)
            {
                // DB 관련 오류 처리
                XtraMessageBox.Show($"데이터베이스 오류: {ex.Message}", "SQL Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                // 일반 예외 처리
                XtraMessageBox.Show(ex.Message, "오류",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ──────────────────────────────────────────────
        // 로그 표시 : 화면 하단 상태바에 메시지 표시
        // ──────────────────────────────────────────────
        private void ShowLog(string message)
        {
            lblStatus.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        }

        // ──────────────────────────────────────────────
        // Grid Row 스타일 제어 : PASS/NG 상태별 색상 구분
        // ──────────────────────────────────────────────
        private void gridView_RowCellStyle(object sender, RowCellStyleEventArgs e)
        {
            GridView view = sender as GridView;
            if (e.RowHandle < 0) return;

            string status = view.GetRowCellDisplayText(e.RowHandle, "RESULT");

            // 결과 값에 따라 색상 표시
            if (status == "NG")
                e.Appearance.BackColor = System.Drawing.Color.LightCoral;  // 불량
            else if (status == "OK" || status == "PASS")
                e.Appearance.BackColor = System.Drawing.Color.LightGreen;  // 양품
        }
    }
}
