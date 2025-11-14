using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Windows.Forms;

/*
주요 기능 요약:

권한 관리: 제조기술 부서만 데이터 변경 가능
스크랩 코드 관리: H___ 패턴의 스크랩 코드 조회 및 선택
Lot 정보 조회: 바코드 스캔을 통한 Lot 정보 조회
공정 상태 관리:
    M119 단계: UPDATE 처리 및 M100으로 복귀 가능
    M100/M111 단계: M119로 이동 처리
메모 관리: 사용자 정보와 타임스탬프가 포함된 메모 자동 생성
재작업 이력: tb_rework_setinfo 테이블에 재작업 이력 저장
*/

namespace mes_
{
    public partial class frmReworkScrap : Form
    {
        // MySQL 연결 객체를 저장하는 필드 (생성자를 통해 주입받음)
        // readonly로 선언되어 생성자에서만 초기화 가능
        private readonly MySqlConnection _connection;
        
        // Lot의 기존 메모를 저장하는 필드
        // 새로운 메모 추가 시 기존 메모와 연결하기 위해 사용
        private string memo = string.Empty;

        /// <summary>
        /// 폼 생성자. 데이터베이스 연결 객체를 주입받아 초기화합니다.
        /// </summary>
        /// <param name="connection">이미 연결된 MySqlConnection 객체</param>
        public frmReworkScrap(MySqlConnection connection)
        {
            InitializeComponent(); // Windows Forms 디자이너에서 생성된 컴포넌트 초기화
            _connection = connection; // 의존성 주입을 통해 연결 객체 저장
        }

        /// <summary>
        /// 폼 로드 시 실행되는 이벤트 핸들러.
        /// 컴포넌트 초기화 및 초기 데이터 로드를 수행합니다.
        /// </summary>
        private void frmReworkScrap_Load(object sender, EventArgs e)
        {
            // '제조기술' 부서가 아니면 데이터 변경 버튼을 숨김 (권한 체크)
            // frmMain.department는 메인 폼에서 관리하는 전역 변수
            if (frmMain.department != "제조기술")
                btnCommit.Visible = false;

            // 스크랩 코드 섹션 1 콤보박스 초기화 및 데이터 로드
            cbSection1.Items.Clear();

            // 'H___' 패턴의 스크랩 코드 섹션 1 목록을 조회합니다.
            // H로 시작하는 4자리 스크랩 코드의 section1 값을 조회
            string sql = "SELECT section1 FROM tb_scrapcode WHERE scrap_code LIKE 'H___' ORDER BY id";

            try
            {
                // MySqlHelper.ExecuteDataset은 매개변수가 필요 없는 쿼리에 사용
                // DataSet의 첫 번째 테이블을 가져옴
                var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];

                // 조회된 각 행의 section1 값을 콤보박스에 추가
                foreach (DataRow row in dataTable.Rows)
                {
                    cbSection1.Items.Add(row[0].ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("스크랩 코드 로드 중 오류 발생: " + ex.Message, "오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 스크랩 코드 섹션 1 콤보박스 선택 항목이 변경되었을 때 실행됩니다.
        /// 선택된 section1에 해당하는 상세 스크랩 정보를 조회하여 표시합니다.
        /// </summary>
        private void cbSection1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 콤보박스에 선택된 항목이 있을 때만 실행
            if (cbSection1.Text != string.Empty)
            {
                string sql = string.Empty;
                string descriptionColumn;
                
                // DB 사이트 설정에 따라 한국어 또는 영어 설명을 선택합니다.
                // frmMain.dbsite는 데이터베이스 사이트 정보를 저장하는 전역 변수
                if (frmMain.dbsite.Contains("KR"))
                    descriptionColumn = "description_kr"; // 한국어 설명 컬럼
                else
                    descriptionColumn = "description_en"; // 영어 설명 컬럼

                // 선택된 section1에 해당하는 스크랩 코드와 설명을 조회
                sql = $"SELECT scrap_code, {descriptionColumn} FROM tb_scrapcode WHERE section1 = @Section1";

                try
                {
                    using (var command = new MySqlCommand(sql, _connection))
                    {
                        // SQL 인젝션 방지를 위해 매개변수 사용
                        command.Parameters.AddWithValue("@Section1", cbSection1.Text);

                        // 쿼리 실행 및 결과 테이블 가져오기
                        var dataTable = MySqlHelper.ExecuteDataset(_connection, command).Tables[0];
                        
                        // 조회된 첫 번째 행의 스크랩 코드와 설명을 텍스트박스에 표시
                        if (dataTable.Rows.Count > 0)
                        {
                            DataRow row = dataTable.Rows[0];
                            txtScrapCode.Text = row[0].ToString(); // 스크랩 코드 표시
                            txtDescription_Kr.Text = row[1].ToString(); // 설명을 표시하는 텍스트박스는 하나(Kr)로 사용
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("스크랩 코드 상세 정보 로드 중 오류 발생: " + ex.Message, 
                        "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 스캔 데이터 입력 후 Enter 키를 눌렀을 때 Lot 정보를 조회합니다.
        /// 바코드 스캐너 등에서 입력된 데이터를 처리합니다.
        /// </summary>
        private void txtScanData_KeyUp(object sender, KeyEventArgs e)
        {
            // Enter 키가 눌렸고 입력값이 비어있지 않은 경우에만 처리
            if (e.KeyData == Keys.Enter && txtScanData.Text != string.Empty)
            {
                // 입력 데이터를 대문자로 변환 (일관성 유지)
                txtScanData.Text = txtScanData.Text.ToUpper();

                // 화면 초기화: 이전 조회 결과를 모두 지움
                txtSeries.Text = string.Empty;
                txtProdCode.Text = string.Empty;
                txtLotID.Text = string.Empty;
                txtLotQty.Text = string.Empty;
                txtNandOption.Text = string.Empty;
                btnCommit.Enabled = false; // 처리 버튼 비활성화
                btnReturn.Visible = false; // Return 버튼 숨김
                dataGridView1.Rows.Clear(); // 데이터그리드뷰 초기화
                memo = string.Empty; // 기존 메모 초기화

                string scanData = txtScanData.Text;

                // Lot ID (길이 10) 스캔 데이터 처리
                // 일반적으로 Lot ID는 10자리로 고정되어 있다고 가정
                if (scanData.Length == 10)
                {
                    // Lot 정보를 조회하는 쿼리 (여러 테이블 Join)
                    string sql = 
                        "SELECT s.pcbserial, s.p_sn, s.rework_sn, l.lotid, l.status, l.start_lot_qty, " +
                        "e.series, e.prod_code, p.step, l.comp_k9_opt, l.m_opt_code, l.lot_memo " +
                        "FROM tb_mes_lotid l " +
                        "JOIN tb_mes_std_espec e ON l.espec_id = e.id " + // 제품 사양 정보
                        "JOIN tb_mes_dat_setinfo s ON l.id = s.lot_id " + // 설정 정보
                        "JOIN tb_mes_process p ON l.next_step_id = p.id " + // 공정 정보
                        "WHERE l.lotid = @LotID";

                    try
                    {
                        using (var command = new MySqlCommand(sql, _connection))
                        {
                            command.Parameters.AddWithValue("@LotID", scanData);

                            var dataTable = MySqlHelper.ExecuteDataset(_connection, command).Tables[0];

                            if (dataTable.Rows.Count > 0)
                            {
                                DataRow row = dataTable.Rows[0];
                                string currentStep = row[8].ToString(); // 현재 공정 단계

                                // 현재 공정 단계(step)에 따른 버튼 상태 결정
                                if (currentStep == "M119")
                                {
                                    // 이미 M119 단계인 경우 UPDATE 처리
                                    btnCommit.Text = "UPDATE";
                                    btnReturn.Visible = true; // Return 버튼 표시 (M100으로 복귀 가능)
                                }
                                else if (currentStep == "M100" || currentStep == "M111")
                                {
                                    // M100 또는 M111 단계인 경우 M119로 이동
                                    btnCommit.Text = "MOVE TO M119";
                                    btnReturn.Visible = false; // Return 버튼 숨김
                                }
                                else
                                {
                                    // 지원하지 않는 공정 단계인 경우 오류 처리
                                    txtLotQty.Text = "STEP ERROR";
                                    txtScanData.Text = string.Empty;
                                    return; // 조기 종료
                                }

                                // 조회된 Lot 정보를 데이터그리드뷰에 표시
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], row[1], row[2]);
                                dataGridView1.ClearSelection(); // 선택 해제

                                // 각 텍스트박스에 해당 정보 표시
                                txtLotID.Text = row[3].ToString(); // Lot ID
                                // var status = row[4].ToString(); // 현재 사용되지 않음
                                txtLotQty.Text = row[5].ToString(); // Lot 수량
                                txtSeries.Text = row[6].ToString(); // 시리즈
                                txtProdCode.Text = row[7].ToString(); // 제품 코드
                                // var nextstep = row[8].ToString(); // currentStep 변수에 저장됨
                                txtNandOption.Text = row[9].ToString(); // NAND 옵션
                                memo = row[11].ToString(); // 기존 메모 저장 (나중에 업데이트 시 사용)

                                btnCommit.Enabled = true; // 처리 버튼 활성화
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lot 정보 조회 중 오류 발생: " + ex.Message, 
                            "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                txtScanData.Text = string.Empty; // 스캔 데이터 입력창 초기화 (다음 입력 대기)
            }
        }

        /// <summary>
        /// 재작업/스크랩 처리 또는 M119 단계로 이동을 실행합니다.
        /// 데이터베이스에 Lot 상태를 업데이트하고 재작업 이력을 생성합니다.
        /// </summary>
        private void btnCommit_Click(object sender, EventArgs e)
        {
            // 스크랩 코드가 선택되지 않은 경우 오류 처리
            if (txtScrapCode.Text == string.Empty)
            {
                txtLotQty.Text = "SCRAP CODE ERROR";
                return;
            }

            // DB 연결 상태 확인 및 열기
            if (_connection.State == ConnectionState.Closed)
                _connection.Open();

            // 메모 업데이트 로직
            string newMemo = txtMemo.Text;
            if (memo == string.Empty)
            {
                // 기존 메모가 없으면 새 메모를 사용자 정보와 함께 기록
                // 형식: "사용자명(사용자ID) 날짜시간 : 메모내용"
                newMemo = $"{frmMain.userName}({frmMain.userID}) {DateTime.Now:yyyy-MM-dd HH:mm:ss} : {newMemo}";
            }
            else
            {
                // 기존 메모가 있으면 새 메모를 추가
                if (newMemo == string.Empty)
                    newMemo = memo; // 새 메모가 없으면 기존 메모 유지
                else
                    // 새 메모를 기존 메모 아래에 추가 (환경에 따른 줄바꿈 문자 사용)
                    newMemo = $"{memo}{Environment.NewLine}{frmMain.userName}({frmMain.userID}) {DateTime.Now:yyyy-MM-dd HH:mm:ss} : {newMemo}";
            }

            // 1. tb_mes_lotid 업데이트 (스크랩 처리 및 M119 이동)
            string sqlUpdateLot =
                "UPDATE tb_mes_lotid " +
                "SET step_id = 6, next_step_id = (SELECT id FROM tb_mes_process WHERE step = 'M119'), " +
                "status = 'Terminated', lot_memo = @LotMemo, lot_flag = 'C', hold_code = @ScrapCode " +
                "WHERE lotid = @LotID";

            try
            {
                using (var command = new MySqlCommand(sqlUpdateLot, _connection))
                {
                    command.Parameters.AddWithValue("@LotID", txtLotID.Text);
                    command.Parameters.AddWithValue("@LotMemo", newMemo);
                    command.Parameters.AddWithValue("@ScrapCode", txtScrapCode.Text);
                    MySqlHelper.ExecuteNonQuery(_connection, command);
                }

                // 2. tb_rework_setinfo 에 재작업 이력 삽입
                string sqlInsertRework =
                    "INSERT INTO tb_rework_setinfo (setinfo_id, lot_id, step_id, espec_id) " +
                    "SELECT s.id, s.lot_id, l.next_step_id, l.espec_id " +
                    "FROM tb_mes_lotid l " +
                    "JOIN tb_mes_dat_setinfo s ON l.id = s.lot_id " +
                    "WHERE l.lotid = @LotIDForInsert";

                using (var command = new MySqlCommand(sqlInsertRework, _connection))
                {
                    command.Parameters.AddWithValue("@LotIDForInsert", txtLotID.Text);
                    MySqlHelper.ExecuteNonQuery(_connection, command);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Commit 처리 중 오류 발생: " + ex.Message, 
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return; // 오류 발생 시 초기화하지 않고 리턴
            }

            // 처리 완료 후 화면 초기화
            txtSeries.Text = string.Empty;
            txtProdCode.Text = string.Empty;
            txtLotID.Text = string.Empty;
            txtLotQty.Text = string.Empty;
            txtNandOption.Text = string.Empty;
            btnCommit.Enabled = false;
            btnReturn.Visible = false;
            dataGridView1.Rows.Clear();
            txtMemo.Text = string.Empty;
        }

        /// <summary>
        /// Lot을 'M100' 단계로 되돌려 재작업을 취소하거나 초기 단계로 복귀시킵니다.
        /// M119에서 M100으로의 역진행을 처리합니다.
        /// </summary>
        private void btnReturn_Click(object sender, EventArgs e)
        {
            // DB 연결 상태 확인 및 열기
            if (_connection.State == ConnectionState.Closed)
                _connection.Open();

            // tb_mes_lotid 업데이트 (M100 단계로 복귀)
            string sqlReturn =
                "UPDATE tb_mes_lotid " +
                "SET step_id = 6, next_step_id = (SELECT id FROM tb_mes_process WHERE step = 'M100'), " +
                "status = 'Terminated', lot_flag = 'R', hold_code = NULL " + // hold_code 초기화
                "WHERE lotid = @LotID";

            try
            {
                using (var command = new MySqlCommand(sqlReturn, _connection))
                {
                    // SQL 인젝션 방지를 위해 매개변수 사용
                    command.Parameters.AddWithValue("@LotID", txtLotID.Text);
                    MySqlHelper.ExecuteNonQuery(_connection, command);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Return 처리 중 오류 발생: " + ex.Message, 
                    "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return; // 오류 발생 시 초기화하지 않고 리턴
            }

            // 처리 완료 후 화면 초기화
            txtSeries.Text = string.Empty;
            txtProdCode.Text = string.Empty;
            txtLotID.Text = string.Empty;
            txtLotQty.Text = string.Empty;
            txtNandOption.Text = string.Empty;
            btnCommit.Enabled = false;
            btnReturn.Visible = false;
            dataGridView1.Rows.Clear();
            txtMemo.Text = string.Empty;
        }
    }
}
