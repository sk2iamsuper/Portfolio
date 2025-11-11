using MySql.Data.MySqlClient;
using System;
using System.Data;
using System.Windows.Forms;

namespace mes_
{
    public partial class frmReworkScrap : Form
    {
        // MySQL 연결 객체를 저장하는 필드 (생성자를 통해 주입받음)
        private readonly MySqlConnection _connection;
        // Lot의 기존 메모를 저장하는 필드
        private string memo = string.Empty;

        /// <summary>
        /// 폼 생성자. 데이터베이스 연결 객체를 주입받아 초기화합니다.
        /// </summary>
        /// <param name="connection">이미 연결된 MySqlConnection 객체</param>
        public frmReworkScrap(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;
        }

        /// <summary>
        /// 폼 로드 시 실행되는 이벤트 핸들러.
        /// </summary>
        private void frmReworkScrap_Load(object sender, EventArgs e)
        {
            // '제조기술' 부서가 아니면 데이터 변경 버튼을 숨김 (권한 체크)
            if (frmMain.department != "제조기술")
                btnCommit.Visible = false;

            // 스크랩 코드 섹션 1 콤보박스 초기화 및 데이터 로드
            cbSection1.Items.Clear();

            // 'H___' 패턴의 스크랩 코드 섹션 1 목록을 조회합니다.
            string sql = "SELECT section1 FROM tb_scrapcode WHERE scrap_code LIKE 'H___' ORDER BY id";

            try
            {
                // MySqlHelper.ExecuteDataset은 매개변수가 필요 없는 쿼리에 사용
                var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];

                foreach (DataRow row in dataTable.Rows)
                {
                    cbSection1.Items.Add(row[0].ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("스크랩 코드 로드 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 스크랩 코드 섹션 1 콤보박스 선택 항목이 변경되었을 때 실행됩니다.
        /// </summary>
        private void cbSection1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbSection1.Text != string.Empty)
            {
                string sql = string.Empty;
                string descriptionColumn;
                
                // DB 사이트 설정에 따라 한국어 또는 영어 설명을 선택합니다.
                if (frmMain.dbsite.Contains("KR"))
                    descriptionColumn = "description_kr";
                else
                    descriptionColumn = "description_en";

                sql = $"SELECT scrap_code, {descriptionColumn} FROM tb_scrapcode WHERE section1 = @Section1";

                try
                {
                    using (var command = new MySqlCommand(sql, _connection))
                    {
                        // SQL 인젝션 방지를 위해 매개변수 사용
                        command.Parameters.AddWithValue("@Section1", cbSection1.Text);

                        var dataTable = MySqlHelper.ExecuteDataset(_connection, command).Tables[0];
                        
                        // 조회된 첫 번째 행의 스크랩 코드와 설명을 텍스트박스에 표시
                        if (dataTable.Rows.Count > 0)
                        {
                            DataRow row = dataTable.Rows[0];
                            txtScrapCode.Text = row[0].ToString();
                            txtDescription_Kr.Text = row[1].ToString(); // 설명을 표시하는 텍스트박스는 하나(Kr)로 사용
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("스크랩 코드 상세 정보 로드 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 스캔 데이터 입력 후 Enter 키를 눌렀을 때 Lot 정보를 조회합니다.
        /// </summary>
        private void txtScanData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && txtScanData.Text != string.Empty)
            {
                txtScanData.Text = txtScanData.Text.ToUpper();

                // 화면 초기화
                txtSeries.Text = string.Empty;
                txtProdCode.Text = string.Empty;
                txtLotID.Text = string.Empty;
                txtLotQty.Text = string.Empty;
                txtNandOption.Text = string.Empty;
                btnCommit.Enabled = false;
                btnReturn.Visible = false;
                dataGridView1.Rows.Clear();
                memo = string.Empty; // 기존 메모 초기화

                string scanData = txtScanData.Text;

                // Lot ID (길이 10) 스캔 데이터 처리
                if (scanData.Length == 10)
                {
                    // Lot 정보를 조회하는 쿼리 (Join)
                    string sql = 
                        "SELECT s.pcbserial, s.p_sn, s.rework_sn, l.lotid, l.status, l.start_lot_qty, e.series, e.prod_code, p.step, l.comp_k9_opt, l.m_opt_code, l.lot_memo " +
                        "FROM tb_mes_lotid l " +
                        "JOIN tb_mes_std_espec e ON l.espec_id = e.id " +
                        "JOIN tb_mes_dat_setinfo s ON l.id = s.lot_id " +
                        "JOIN tb_mes_process p ON l.next_step_id = p.id " +
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
                                string currentStep = row[8].ToString();

                                // 현재 공정 단계(step)에 따른 버튼 상태 결정
                                if (currentStep == "M119")
                                {
                                    btnCommit.Text = "UPDATE";
                                    btnReturn.Visible = true;
                                }
                                else if (currentStep == "M100" || currentStep == "M111")
                                {
                                    btnCommit.Text = "MOVE TO M119";
                                    btnReturn.Visible = false;
                                }
                                else
                                {
                                    txtLotQty.Text = "STEP ERROR";
                                    txtScanData.Text = string.Empty;
                                    return;
                                }

                                // 조회된 Lot 정보를 화면에 표시
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], row[1], row[2]);
                                dataGridView1.ClearSelection();

                                txtLotID.Text = row[3].ToString();
                                // var status = row[4].ToString(); // 현재 사용되지 않음
                                txtLotQty.Text = row[5].ToString();
                                txtSeries.Text = row[6].ToString();
                                txtProdCode.Text = row[7].ToString();
                                // var nextstep = row[8].ToString(); // currentStep 변수에 저장됨
                                txtNandOption.Text = row[9].ToString();
                                memo = row[11].ToString(); // 기존 메모 저장

                                btnCommit.Enabled = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Lot 정보 조회 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                txtScanData.Text = string.Empty; // 스캔 데이터 입력창 초기화
            }
        }

        /// <summary>
        /// 재작업/스크랩 처리 또는 M119 단계로 이동을 실행합니다.
        /// </summary>
        private void btnCommit_Click(object sender, EventArgs e)
        {
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
                newMemo = $"{frmMain.userName}({frmMain.userID}) {DateTime.Now:yyyy-MM-dd HH:mm:ss} : {newMemo}";
            }
            else
            {
                // 기존 메모가 있으면 새 메모를 추가
                if (newMemo == string.Empty)
                    newMemo = memo; // 새 메모가 없으면 기존 메모 유지
                else
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

                // 2. tb_rework_setinfo 에 이력 삽입
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
                MessageBox.Show("Commit 처리 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show("Return 처리 중 오류 발생: " + ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
