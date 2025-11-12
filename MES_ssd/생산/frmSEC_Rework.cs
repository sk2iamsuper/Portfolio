using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mes_
{
    /// <summary>
    /// 재작업(Rework) 관리 폼
    /// MES 시스템에서 다양한 유형의 재작업 프로세스를 관리합니다.
    /// </summary>
    public partial class frmCust_Rework : Form
    {
        #region 상수 정의
        private const string SETTING_FILE = "SETTING.ini";
        private const string BARCODE_PRINTER_CONFIG = "MH_LOTCARD_ZM410";
        private const string BARCODE_EQUIPMENT_KEY = "BARCODE_LOT";
        private const string OUTBOX_SN_KEY = "OutBox";
        private const string SLIP_NO_KEY = "SLIP_NO";
        
        // 데이터베이스 상태값
        private const string FLAG_COMPLETED = "G";
        private const string FLAG_REWORK = "R";
        private const string STATUS_TERMINATED = "Terminated";
        
        // UI 메시지
        private const string MSG_FILE_NOT_FOUND_ENG = "SETTING.ini File not found.";
        private const string MSG_FILE_NOT_FOUND_KOR = "SETTING.ini 파일이 필요합니다.";
        private const string MSG_PORT_NOT_SET_ENG = "Need to setting for {0}=";
        private const string MSG_PORT_NOT_SET_KOR = "COMPORT 지정이 필요합니다. {0}=";
        private const string MSG_PRINTER_ERROR_ENG = "Not connected to the Printer.";
        private const string MSG_PRINTER_ERROR_KOR = "바코드 프린터 연결을 확인하세요.";
        #endregion

        #region 필드
        private readonly MySqlConnection _connection;
        private string _originLotId = string.Empty;
        private string _originLotidT2 = string.Empty;
        #endregion

        /// <summary>
        /// 생성자 - 데이터베이스 연결을 주입받습니다.
        /// </summary>
        /// <param name="connection">MySQL 데이터베이스 연결</param>
        public frmSEC_Rework(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        #region 이벤트 핸들러
        /// <summary>
        /// 폼 로드 시 초기화 작업 수행
        /// </summary>
        private void frmSEC_Rework_Load(object sender, EventArgs e)
        {
            InitializeBarcodePrinter();
            LoadReworkList();
        }

        /// <summary>
        /// 일반 재작업 목록 더블클릭 시 상세 정보 로드
        /// </summary>
        private void dgvReturnList_DoubleClick(object sender, EventArgs e)
        {
            if (dgvReturnList.CurrentRow == null) return;

            LoadReworkDetailFromGrid(dgvReturnList);
        }

        /// <summary>
        /// MZB 재작업 목록 더블클릭 시 상세 정보 로드
        /// </summary>
        private void dgvReturnList_T2_DoubleClick(object sender, EventArgs e)
        {
            if (dgvReturnList_T2.CurrentRow == null) return;

            LoadMzbReworkDetail();
        }

        /// <summary>
        /// 시리얼 번호 스캔 입력 처리
        /// </summary>
        private void txtScanData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && !string.IsNullOrEmpty(txtScanData.Text))
            {
                ProcessScanData(txtScanData.Text);
            }
        }

        /// <summary>
        /// MZB 재작업 M-OPTION 코드 입력 처리
        /// </summary>
        private void txtMOptCode_T2_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && !string.IsNullOrEmpty(txtMOptCode_T2.Text))
            {
                ProcessMzbMOptCode();
            }
        }

        /// <summary>
        /// 출고용 박스 스캔 데이터 처리
        /// </summary>
        private void txtScanData_T3_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && !string.IsNullOrEmpty(txtScanData_T3.Text))
            {
                ProcessShipmentScanData();
            }
        }

        /// <summary>
        /// 일반 재작업 커밋 처리
        /// </summary>
        private void btnCommit_Click(object sender, EventArgs e)
        {
            CommitReworkProcess();
        }

        /// <summary>
        /// MZB 재작업 커밋 처리
        /// </summary>
        private void btnCommitT2_Click(object sender, EventArgs e)
        {
            CommitMzbReworkProcess();
        }

        /// <summary>
        /// 출고 처리
        /// </summary>
        private void btnShip_Click(object sender, EventArgs e)
        {
            ProcessShipment();
        }

        /// <summary>
        /// 메시지 텍스트 변경 시 배경색 업데이트
        /// </summary>
        private void txtMessage_TextChanged(object sender, EventArgs e)
        {
            UpdateMessageBackColor(txtMessage);
        }

        /// <summary>
        /// MZB 메시지 텍스트 변경 시 배경색 업데이트
        /// </summary>
        private void txtMessage_T2_TextChanged(object sender, EventArgs e)
        {
            UpdateMessageBackColor(txtMessage_T2);
        }
        #endregion

        #region 프라이빗 메서드 - 초기화 및 설정
        /// <summary>
        /// 바코드 프린터 초기화
        /// </summary>
        private void InitializeBarcodePrinter()
        {
            if (!SearchPort(BARCODE_PRINTER_CONFIG))
            {
                // 프린터 연결 실패 시 계속 진행 (주석 처리됨)
                // return;
            }
        }

        /// <summary>
        /// 설정 파일에서 COMPORT 정보 검색 및 연결 테스트
        /// </summary>
        /// <param name="configName">설정 이름</param>
        /// <returns>성공 여부</returns>
        private bool SearchPort(string configName)
        {
            try
            {
                // 설정 파일에서 COMPORT 정보 읽기
                string portName = ReadPortFromSettings(configName);
                if (string.IsNullOrEmpty(portName))
                {
                    ShowLocalizedMessage(string.Format(MSG_PORT_NOT_SET_ENG, configName), 
                                       string.Format(MSG_PORT_NOT_SET_KOR, configName));
                    return false;
                }

                // 프린터 연결 테스트
                return TestPrinterConnection(portName);
            }
            catch (FileNotFoundException)
            {
                ShowLocalizedMessage(MSG_FILE_NOT_FOUND_ENG, MSG_FILE_NOT_FOUND_KOR);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Printer initialization failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 설정 파일에서 포트 정보 읽기
        /// </summary>
        private string ReadPortFromSettings(string configName)
        {
            if (!File.Exists(SETTING_FILE))
                throw new FileNotFoundException();

            var lines = File.ReadAllLines(SETTING_FILE, Encoding.Default);
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length == 2 && parts[0] == configName)
                {
                    txtComport.Text = parts[1];
                    return parts[1];
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 프린터 연결 테스트
        /// </summary>
        private bool TestPrinterConnection(string portName)
        {
            try
            {
                spBarcode.PortName = portName;
                
                // 연결 테스트
                if (!spBarcode.IsOpen)
                    spBarcode.Open();

                if (spBarcode.IsOpen)
                    spBarcode.Close();

                return true;
            }
            catch (Exception)
            {
                ShowLocalizedMessage(MSG_PRINTER_ERROR_ENG, MSG_PRINTER_ERROR_KOR);
                return false;
            }
        }
        #endregion

        #region 프라이빗 메서드 - 데이터 조회
        /// <summary>
        /// 재작업 목록 조회
        /// </summary>
        private void LoadReworkList()
        {
            try
            {
                ClearDataGrids();
                var dataTable = GetReworkListFromDatabase();
                PopulateDataGrids(dataTable);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 조회 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 데이터베이스에서 재작업 목록 조회
        /// </summary>
        private DataTable GetReworkListFromDatabase()
        {
            string sql = @"SELECT ISSUE_DATE, '-', PRODUCT_CODE, LOT_NO, LOT_TYPE, WORK_WEEK, RETURN_TYPE, SUM(CHIP_QTY) 
                          FROM T_TR_ROU 
                          WHERE flag is null 
                          GROUP BY ISSUE_DATE, PRODUCT_CODE, LOT_NO, LOT_TYPE, WORK_WEEK, RETURN_TYPE";

            return MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
        }

        /// <summary>
        /// 데이터 그리드뷰 초기화
        /// </summary>
        private void ClearDataGrids()
        {
            dgvReturnList.Rows.Clear();
            dgvReturnList_T2.Rows.Clear();
            dgvReturnList_T3.Rows.Clear();
        }

        /// <summary>
        /// 데이터 그리드뷰에 데이터 채우기
        /// </summary>
        private void PopulateDataGrids(DataTable dataTable)
        {
            int sum1 = 0, sum2 = 0;

            foreach (DataRow row in dataTable.Rows)
            {
                string productCode = row[2].ToString();
                int chipQty = int.Parse(row[7].ToString());

                if (!productCode.StartsWith("MZB"))
                {
                    AddRowToGrid(dgvReturnList, row, ref sum1, chipQty);
                    AddRowToGrid(dgvReturnList_T3, row, ref sum1, chipQty);
                }
                else
                {
                    AddRowToGrid(dgvReturnList_T2, row, ref sum2, chipQty);
                }
            }

            // 합계 행 추가
            AddSummaryRow(dgvReturnList, sum1);
            AddSummaryRow(dgvReturnList_T2, sum2);
        }

        /// <summary>
        /// 그리드에 행 추가
        /// </summary>
        private void AddRowToGrid(DataGridView grid, DataRow row, ref int sum, int chipQty)
        {
            grid.Rows.Add(grid.RowCount + 1, row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7]);
            grid.ClearSelection();
            sum += chipQty;
        }

        /// <summary>
        /// 합계 행 추가
        /// </summary>
        private void AddSummaryRow(DataGridView grid, int sum)
        {
            grid.Rows.Add(grid.RowCount + 1, "", "", "", "", "", "", "", sum);
        }

        /// <summary>
        /// 재작업 상세 정보 로드
        /// </summary>
        private void LoadReworkDetailFromGrid(DataGridView grid)
        {
            var currentRow = grid.CurrentRow;
            if (currentRow == null) return;

            // 기본 정보 설정
            txtProductCode.Text = currentRow.Cells[3].Value.ToString();
            _originLotId = currentRow.Cells[4].Value.ToString();
            txtMoLotID.Text = _originLotId;
            txtZaLotID.Text = GenerateZaLotId(_originLotId);

            // 추가 정보 설정
            SetBasicReworkInfo(currentRow);
            SetFabLineInfo(currentRow);
            SetMOptCodeInfo();
            LoadSerialNumbers();
        }

        /// <summary>
        /// ZA LOT ID 생성
        /// </summary>
        private string GenerateZaLotId(string originLotId)
        {
            string zaLotId = "F" + originLotId.Substring(1, 9);
            
            // 중복 체크
            string isExist = Helpers.MySqlHelper.GetOneData(_connection, 
                $"SELECT COUNT(*) FROM tb_mes_lotid WHERE lotid = '{zaLotId}'");

            return isExist != "0" ? GenerateUniqueLotId(zaLotId) : zaLotId;
        }

        /// <summary>
        /// 고유 LOT ID 생성
        /// </summary>
        private string GenerateUniqueLotId(string baseLotId)
        {
            string splitDigit = Helpers.MySqlHelper.GetOneData(_connection,
                $"SELECT SUBSTR(lotid, 5, 3) FROM tb_mes_lotid WHERE lotid like '{baseLotId.Substring(0, 4)}P%%{baseLotId.Substring(7, 3)}' order by lotid DESC LIMIT 1");

            if (splitDigit == "Empty")
            {
                splitDigit = "P00";
            }

            char split1 = splitDigit[0] == '0' ? 'O' : splitDigit[0];
            string split2 = splitDigit[0] == '0' ? "1" : _numTostr(_strToNum(splitDigit[1].ToString()) + 1);

            if (splitDigit[1] == 'Z')
            {
                MessageBox.Show("SPLIT -Z");
                return string.Empty;
            }

            return baseLotId.Substring(0, 4) + split1 + split2 + splitDigit[2] + baseLotId.Substring(7, 3);
        }

        /// <summary>
        /// 기본 재작업 정보 설정
        /// </summary>
        private void SetBasicReworkInfo(DataGridViewRow currentRow)
        {
            txtWeek.Text = currentRow.Cells[6].Value.ToString().Substring(2, 4);
            txtReturnType.Text = currentRow.Cells[7].Value.ToString();
            txtLotQty.Text = currentRow.Cells[8].Value.ToString();
            txtMoLotQty.Text = Helpers.MySqlHelper.GetOneData(_connection, 
                $"SELECT start_lot_qty FROM tb_mes_lotid WHERE lotid = '{_originLotId}'");
        }

        /// <summary>
        /// Fab Line 정보 설정
        /// </summary>
        private void SetFabLineInfo(DataGridViewRow currentRow)
        {
            string productCode = currentRow.Cells[3].Value.ToString();
            string fabCode = productCode.Length > 19 ? productCode.Substring(19, 1) : string.Empty;

            txtFabLine.Text = MapFabCode(fabCode);
        }

        /// <summary>
        /// Fab 코드 매핑
        /// </summary>
        private string MapFabCode(string fabCode)
        {
            switch (fabCode)
            {
                case "W": return "C";
                case "L": return "M";
                case "F": return "P";
                case "T": return "C";
                case "O": return "M";
                case "X": return "P";
                case "P":
                case "Q":
                case "V":
                    return Helpers.MySqlHelper.GetOneData(_connection,
                        $"SELECT fab_line FROM tb_in_wafer_info WHERE id in (SELECT comp_k9_id FROM tb_mes_lotid WHERE lotid = '{_originLotId}')");
                default:
                    MessageBox.Show("관리자 확인필요", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return fabCode;
            }
        }

        /// <summary>
        /// M-Option 코드 정보 설정
        /// </summary>
        private void SetMOptCodeInfo()
        {
            string mOptCode = Helpers.MySqlHelper.GetOneData(_connection,
                $"SELECT m_opt_code FROM tb_mes_lotid WHERE lotid = '{_originLotId}'");

            txtMOptCode.Text = (mOptCode == "Empty") ? "" : mOptCode.Substring(0, 1) + "X" + mOptCode.Substring(2, 2);
        }

        /// <summary>
        /// 시리얼 번호 목록 로드
        /// </summary>
        private void LoadSerialNumbers()
        {
            dgvInputSerial.Rows.Clear();

            string sql = $@"SELECT a.ssdsn, a.id 
                           FROM tb_mes_dat_setinfo s, tb_mes_dat_boxinfo b, tb_mes_dat_label a 
                           WHERE s.small_box_id = b.id AND s.sn_id = a.id 
                           AND b.small_boxid in (SELECT CONCAT(LOT_NO, LOT_SERIAL) FROM T_TR_ROU WHERE lot_no = '{_originLotId}')";

            var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            
            foreach (DataRow row in dataTable.Rows)
            {
                dgvInputSerial.Rows.Add(dgvInputSerial.RowCount + 1, row[0].ToString(), row[1].ToString());
            }

            dgvInputSerial.ClearSelection();
            txtInputQty.Text = dgvInputSerial.RowCount.ToString();
        }
        #endregion

        #region 프라이빗 메서드 - 변환 유틸리티
        /// <summary>
        /// 문자열을 숫자로 변환 (36진수)
        /// </summary>
        private int _strToNum(string ch)
        {
            return ch switch
            {
                "0" => 0, "1" => 1, "2" => 2, "3" => 3, "4" => 4, "5" => 5, "6" => 6, "7" => 7, "8" => 8, "9" => 9,
                "A" => 10, "B" => 11, "C" => 12, "D" => 13, "E" => 14, "F" => 15, "G" => 16, "H" => 17, "I" => 18,
                "J" => 19, "K" => 20, "L" => 21, "M" => 22, "N" => 23, "O" => 24, "P" => 25, "Q" => 26, "R" => 27,
                "S" => 28, "T" => 29, "U" => 30, "V" => 31, "W" => 32, "X" => 33, "Y" => 34, "Z" => 35,
                _ => 0
            };
        }

        /// <summary>
        /// 숫자를 문자열로 변환 (36진수)
        /// </summary>
        private string _numTostr(int num)
        {
            return num switch
            {
                0 => "0", 1 => "1", 2 => "2", 3 => "3", 4 => "4", 5 => "5", 6 => "6", 7 => "7", 8 => "8", 9 => "9",
                10 => "A", 11 => "B", 12 => "C", 13 => "D", 14 => "E", 15 => "F", 16 => "G", 17 => "H", 18 => "I",
                19 => "J", 20 => "K", 21 => "L", 22 => "M", 23 => "N", 24 => "O", 25 => "P", 26 => "Q", 27 => "R",
                28 => "S", 29 => "T", 30 => "U", 31 => "V", 32 => "W", 33 => "X", 34 => "Y", 35 => "Z",
                _ => "0"
            };
        }
        #endregion

        #region 프라이빗 메서드 - 스캔 데이터 처리
        /// <summary>
        /// 스캔 데이터 처리
        /// </summary>
        private void ProcessScanData(string scanData)
        {
            txtScanData.Text = scanData.ToUpper().Trim();
            txtMessage.Text = string.Empty;

            if (txtScanData.Text.Length > 30)
            {
                ProcessMultipleSerialNumbers(scanData);
            }
            else if (txtScanData.Text.Length == 10 && txtScanData.Text.Substring(0, 2) == IniHelper.GetSiteCode())
            {
                ProcessLargeBoxScan(scanData);
            }

            txtScanData.Text = string.Empty;
        }

        /// <summary>
        /// 다중 시리얼 번호 처리
        /// </summary>
        private void ProcessMultipleSerialNumbers(string scanData)
        {
            string[] words = scanData.Split(' ');
            
            foreach (var word in words)
            {
                if (IsSerialNumberDuplicate(word))
                {
                    txtMessage.Text = "대기중 S/N";
                    return;
                }

                AddSerialNumberToGrid(word);
            }

            txtInputQty.Text = dgvInputSerial.RowCount.ToString();
        }

        /// <summary>
        /// 시리얼 번호 중복 체크
        /// </summary>
        private bool IsSerialNumberDuplicate(string serialNumber)
        {
            for (int i = 0; i < dgvInputSerial.RowCount; i++)
            {
                if (dgvInputSerial.Rows[i].Cells[1].Value.ToString() == serialNumber)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 시리얼 번호를 그리드에 추가
        /// </summary>
        private void AddSerialNumberToGrid(string serialNumber)
        {
            string index = Helpers.MySqlHelper.GetOneData(_connection,
                $"SELECT id FROM tb_mes_dat_label WHERE ssdsn like '{serialNumber}_'");

            dgvInputSerial.Rows.Add(dgvInputSerial.RowCount + 1, serialNumber, index);
        }

        /// <summary>
        /// 대박스 스캔 처리
        /// </summary>
        private void ProcessLargeBoxScan(string scanData)
        {
            string lotid = Helpers.MySqlHelper.GetOneData(_connection,
                $"SELECT SUBSTR(small_boxid, 1, 10) FROM tb_mes_dat_boxinfo WHERE large_boxid = '{scanData}' GROUP BY SUBSTR(small_boxid, 1, 10)");

            for (int i = 0; i < dgvReturnList.RowCount; i++)
            {
                if (dgvReturnList.Rows[i].Cells[4].Value.ToString() == lotid)
                {
                    dgvReturnList.CurrentCell = dgvReturnList.Rows[i].Cells[0];
                    dgvReturnList_DoubleClick(null, null);
                    break;
                }
            }
        }
        #endregion

        #region 프라이빗 메서드 - 재작업 프로세스
        /// <summary>
        /// 일반 재작업 프로세스 커밋
        /// </summary>
        private void CommitReworkProcess()
        {
            if (!ValidateReworkInput()) return;

            try
            {
                EnsureConnectionOpen();
                ExecuteReworkTransaction();
                PrintBarcodeLabel();
                ResetReworkForm();
                LoadReworkList();
                
                txtMessage.Text = "PASS";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"재작업 처리 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 재작업 입력값 검증
        /// </summary>
        private bool ValidateReworkInput()
        {
            if (string.IsNullOrEmpty(txtMOptCode.Text))
            {
                txtMessage.Text = "M-OPTION 을 확인하세요";
                return false;
            }

            if (txtLotQty.Text != txtInputQty.Text)
            {
                txtMessage.Text = "수량 확인필요. 관리자 요청하세요.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 재작업 트랜잭션 실행
        /// </summary>
        private void ExecuteReworkTransaction()
        {
            string zaLotId = txtZaLotID.Text;
            string moLotId = txtMoLotID.Text;
            string lotQty = txtLotQty.Text;

            // ZA-LOT 생성
            CreateZaLot(zaLotId, moLotId, lotQty);

            // 시리얼 번호 업데이트
            UpdateSerialNumbers(zaLotId);

            // LOT 이력 기록
            RecordLotHistory(zaLotId, moLotId, lotQty);

            // 원본 LOT 업데이트
            UpdateOriginalLot(moLotId, lotQty);

            // 재작업 플래그 업데이트
            UpdateReworkFlag(moLotId);
        }

        /// <summary>
        /// ZA-LOT 생성
        /// </summary>
        private void CreateZaLot(string zaLotId, string moLotId, string lotQty)
        {
            string sql = $@"INSERT INTO tb_mes_lotid(lotid, lot_type, start_lot_qty, status, espec_id, week, step_id, next_step_id, comp_k9_id, comp_k4_id, m_opt_code, return_type, comment, mo_lotid, lot_memo, comp_k9_opt)
                           SELECT '{zaLotId}', lot_type, {lotQty}, '{STATUS_TERMINATED}', espec_id, week, 12, 12, comp_k9_id, comp_k4_id, '{txtMOptCode.Text}', '{txtReturnType.Text}', comment, '{moLotId}', '', comp_k9_opt 
                           FROM tb_mes_lotid WHERE lotid = '{moLotId}'";

            MySqlHelper.ExecuteNonQuery(_connection, sql);
        }

        /// <summary>
        /// 시리얼 번호 업데이트
        /// </summary>
        private void UpdateSerialNumbers(string zaLotId)
        {
            for (int i = 0; i < dgvInputSerial.RowCount; i++)
            {
                string ssdsn = dgvInputSerial.Rows[i].Cells[1].Value.ToString();
                string index = dgvInputSerial.Rows[i].Cells[2].Value.ToString();

                string sql = $@"UPDATE tb_mes_dat_setinfo 
                               SET lot_id = (SELECT id FROM tb_mes_lotid WHERE lotid = '{zaLotId}'), status_code = 'VA1', small_box_id = NULL, shipplan_id = NULL 
                               WHERE sn_id = {index}";
                MySqlHelper.ExecuteNonQuery(_connection, sql);
            }
        }

        /// <summary>
        /// LOT 이력 기록
        /// </summary>
        private void RecordLotHistory(string zaLotId, string moLotId, string lotQty)
        {
            // ZA-LOT 이력
            string sql = $@"INSERT INTO tb_mes_lotid_history(process_id, lot_id, total, event_id, comment, op_id) 
                           SELECT 10, id, {lotQty}, 9, '{moLotId}', 1 FROM tb_mes_lotid WHERE lotid = '{zaLotId}'";
            MySqlHelper.ExecuteNonQuery(_connection, sql);

            // MO-LOT 이력
            int remainingQty = int.Parse(txtMoLotQty.Text) - int.Parse(lotQty);
            sql = $@"INSERT INTO tb_mes_lotid_history(process_id, lot_id, qty, total, event_id, comment, op_id) 
                    SELECT 10, id, {lotQty}, {remainingQty}, 16, '{zaLotId}', 1 FROM tb_mes_lotid WHERE lotid = '{moLotId}'";
            MySqlHelper.ExecuteNonQuery(_connection, sql);
        }

        /// <summary>
        /// 원본 LOT 업데이트
        /// </summary>
        private void UpdateOriginalLot(string moLotId, string lotQty)
        {
            int remainingQty = int.Parse(txtMoLotQty.Text) - int.Parse(lotQty);
            string sql = $"UPDATE tb_mes_lotid SET start_lot_qty = {remainingQty} WHERE lotid = '{moLotId}'";
            MySqlHelper.ExecuteNonQuery(_connection, sql);
        }

        /// <summary>
        /// 재작업 플래그 업데이트
        /// </summary>
        private void UpdateReworkFlag(string moLotId)
        {
            string sql = $"UPDATE T_TR_ROU SET flag = '{FLAG_COMPLETED}' WHERE lot_no = '{moLotId}'";
            MySqlHelper.ExecuteNonQuery(_connection, sql);
        }

        /// <summary>
        /// 재작업 폼 초기화
        /// </summary>
        private void ResetReworkForm()
        {
            txtMoLotID.Text = "";
            txtZaLotID.Text = "";
            dgvInputSerial.Rows.Clear();
            txtInputQty.Text = "";
        }
        #endregion

        #region 프라이빗 메서드 - MZB 재작업
        /// <summary>
        /// MZB 재작업 상세 정보 로드
        /// </summary>
        private void LoadMzbReworkDetail()
        {
            var currentRow = dgvReturnList_T2.CurrentRow;
            if (currentRow == null) return;

            string site = currentRow.Cells[4].Value.ToString().Substring(0, 1);
            if (site == "L" || site == "Q")
            {
                txtMessage_T2.Text = "관리자 확인필요";
                return;
            }

            SetMzbBasicInfo(currentRow);
            SetMzbFabLineInfo(currentRow);
        }

        /// <summary>
        /// MZB 기본 정보 설정
        /// </summary>
        private void SetMzbBasicInfo(DataGridViewRow currentRow)
        {
            txtProductCode_T2.Text = currentRow.Cells[3].Value.ToString()
                .Replace("-W", "-P").Replace("-L", "-Q").Replace("-F", "-V");
            
            _originLotidT2 = currentRow.Cells[4].Value.ToString();
            txtLotID_T2.Text = "F" + _originLotidT2.Substring(1, 9);

            txtWeek_T2.Text = currentRow.Cells[6].Value.ToString().Substring(2, 4);
            txtReturnType_T2.Text = currentRow.Cells[7].Value.ToString();
            txtLotQty_T2.Text = currentRow.Cells[8].Value.ToString();
        }

        /// <summary>
        /// MZB Fab Line 정보 설정
        /// </summary>
        private void SetMzbFabLineInfo(DataGridViewRow currentRow)
        {
            string productCode = currentRow.Cells[3].Value.ToString();
            string fabCode = productCode.Length > 19 ? productCode.Substring(19, 1) : string.Empty;

            txtFabLine_T2.Text = MapMzbFabCode(fabCode);
        }

        /// <summary>
        /// MZB Fab 코드 매핑
        /// </summary>
        private string MapMzbFabCode(string fabCode)
        {
            return fabCode switch
            {
                "W" => "C",
                "L" => "M",
                "F" => "P",
                "T" => "C",
                "O" => "M",
                "X" => "P",
                _ => fabCode
            };
        }

        /// <summary>
        /// MZB M-OPTION 코드 처리
        /// </summary>
        private void ProcessMzbMOptCode()
        {
            txtMOptCode_T2.Text = txtMOptCode_T2.Text.ToUpper();
            txtMessage_T2.Text = string.Empty;

            txtNandIndex.Text = Helpers.MySqlHelper.GetOneData(_connection,
                $"SELECT id FROM tb_in_wafer_info WHERE lot_id LIKE 'QSI-SMT_0{txtMOptCode_T2.Text.Substring(1, 1)}{txtFabLine_T2.Text}_'");

            if (txtNandIndex.Text == "Empty")
            {
                txtMessage_T2.Text = $"NAND 정보부족 QSI-SMT_0{txtMOptCode_T2.Text.Substring(1, 1)}{txtFabLine_T2.Text}_";
            }
        }

        /// <summary>
        /// MZB 재작업 프로세스 커밋
        /// </summary>
        private void CommitMzbReworkProcess()
        {
            if (!ValidateMzbReworkInput()) return;

            try
            {
                EnsureConnectionOpen();
                ExecuteMzbReworkTransaction();
                PrintBarcodeLabel();
                ResetMzbReworkForm();
                LoadReworkList();
                
                txtMessage_T2.Text = "PASS";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MZB 재작업 처리 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// MZB 재작업 입력값 검증
        /// </summary>
        private bool ValidateMzbReworkInput()
        {
            if (txtNandIndex.Text == "Empty" || string.IsNullOrEmpty(txtNandIndex.Text))
            {
                txtMessage_T2.Text = "NAND 정보부족";
                return false;
            }

            if (string.IsNullOrEmpty(txtLotID_T2.Text) || string.IsNullOrEmpty(txtProductCode_T2.Text) || 
                string.IsNullOrEmpty(txtMOptCode_T2.Text) || string.IsNullOrEmpty(txtLotQty_T2.Text))
            {
                txtMessage_T2.Text = "정보부족";
                return false;
            }

            return true;
        }

        /// <summary>
        /// MZB 재작업 트랜잭션 실행
        /// </summary>
        private void ExecuteMzbReworkTransaction()
        {
            CreateMzbLot();
            UpdateMzbReworkFlag();
            RecordMzbLotHistory();
        }

        /// <summary>
        /// MZB LOT 생성
        /// </summary>
        private void CreateMzbLot()
        {
            string steppath = Helpers.MySqlHelper.GetOneData(_connection,
                $"SELECT step_path FROM tb_mes_std_espec WHERE prod_code = '{txtProductCode_T2.Text}'");
            string nextstep = steppath.Split(new string[] { "M033," }, StringSplitOptions.None)[1].Split(',')[0];

            string sql = $@"INSERT INTO tb_mes_lotid (lotid, lot_type, start_lot_qty, espec_id, week, step_id, next_step_id, comp_k9_id, m_opt_code, comp_k9_opt, return_type) 
                           VALUES ('{txtLotID_T2.Text}', 'PP', {txtLotQty_T2.Text}, 
                           (SELECT id FROM tb_mes_std_espec WHERE prod_code = '{txtProductCode_T2.Text}'), {txtWeek_T2.Text}, 3, 
                           (SELECT id FROM tb_mes_process WHERE step = '{nextstep}'), {txtNandIndex.Text}, '{txtMOptCode_T2.Text}', 
                           (SELECT SUBSTR(sale_option, 1, 2) FROM tb_in_wafer_info WHERE id = {txtNandIndex.Text}), '{txtReturnType_T2.Text}' )";

            MySqlHelper.ExecuteNonQuery(_connection, sql);
        }

        /// <summary>
        /// MZB 재작업 플래그 업데이트
        /// </summary>
        private void UpdateMzbReworkFlag()
        {
            string sql = $"UPDATE T_TR_ROU SET flag = '{FLAG_REWORK}' WHERE lot_no = '{_originLotidT2}'";
            MySqlHelper.ExecuteNonQuery(_connection, sql);
        }

        /// <summary>
        /// MZB LOT 이력 기록
        /// </summary>
        private void RecordMzbLotHistory()
        {
            string sql = $@"INSERT INTO tb_mes_lotid_history (process_id, lot_id, qty, total, event_id, comment, op_id) 
                           SELECT step_id, id, start_lot_qty, start_lot_qty, 6, '반제품입고', {frmMain.userID} 
                           FROM tb_mes_lotid WHERE lotid = '{txtLotID_T2.Text}'";
            MySqlHelper.ExecuteNonQuery(_connection, sql);
        }

        /// <summary>
        /// MZB 재작업 폼 초기화
        /// </summary>
        private void ResetMzbReworkForm()
        {
            txtLotID_T2.Text = string.Empty;
            txtLotQty_T2.Text = string.Empty;
            txtNandIndex.Text = string.Empty;
            txtWeek_T2.Text = string.Empty;
        }
        #endregion

        #region 프라이빗 메서드 - 출고 처리
        /// <summary>
        /// 출고 스캔 데이터 처리
        /// </summary>
        private void ProcessShipmentScanData()
        {
            txtScanData_T3.Text = txtScanData_T3.Text.ToUpper().Trim();
            txtMessage_T3.Text = "";

            var scanData = txtScanData_T3.Text;
            var shipmentInfo = GetShipmentInfoFromDatabase(scanData);

            if (shipmentInfo != null && ValidateShipmentProduct(shipmentInfo))
            {
                AddToShipmentList(shipmentInfo);
            }

            txtScanData_T3.Text = string.Empty;
        }

        /// <summary>
        /// 출고 정보 데이터베이스 조회
        /// </summary>
        private dynamic GetShipmentInfoFromDatabase(string scanData)
        {
            string sql = $@"SELECT lotid, SUBSTR(m_opt_code, 1, 2), COUNT(*) 
                           FROM tb_sec_rework 
                           WHERE large_boxid = '{scanData}' 
                           GROUP BY lotid, SUBSTR(m_opt_code, 1, 2)";

            var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            
            if (dataTable.Rows.Count == 0) return null;

            var row = dataTable.Rows[0];
            return new
            {
                LotId = row[0].ToString(),
                OptCode = row[1].ToString(),
                BoxQty = row[2].ToString()
            };
        }

        /// <summary>
        /// 출고 제품 유효성 검증
        /// </summary>
        private bool ValidateShipmentProduct(dynamic shipmentInfo)
        {
            for (int i = 0; i < dgvReturnList_T3.RowCount; i++)
            {
                string gridLotId = "F" + dgvReturnList_T3.Rows[i].Cells[4].Value.ToString().Substring(1, 9);
                
                if (gridLotId == shipmentInfo.LotId)
                {
                    // 제품 혼합 체크
                    if (dgvMainList.RowCount > 0 && dgvMainList.Rows[0].Cells[2].Value.ToString() != dgvReturnList_T3.Rows[i].Cells[3].Value.ToString())
                    {
                        txtMessage_T3.Text = "PRODUCT MIXED";
                        return false;
                    }

                    txtProduct_T3.Text = dgvReturnList_T3.Rows[i].Cells[3].Value.ToString();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 출고 목록에 추가
        /// </summary>
        private void AddToShipmentList(dynamic shipmentInfo)
        {
            string returnType = GetReturnTypeFromGrid(shipmentInfo.LotId);
            string returnTypeCode = (returnType == "RT") ? "03" : "01";

            dgvMainList.Rows.Add(dgvMainList.RowCount + 1, txtScanData_T3.Text, txtProduct_T3.Text, 
                               shipmentInfo.LotId, shipmentInfo.BoxQty, "", returnType, "", "", shipmentInfo.OptCode);
            dgvMainList.ClearSelection();

            txtQty.Text = (int.Parse(txtQty.Text) + int.Parse(shipmentInfo.BoxQty)).ToString();
        }

        /// <summary>
        /// 그리드에서 반품 타입 조회
        /// </summary>
        private string GetReturnTypeFromGrid(string lotId)
        {
            for (int i = 0; i < dgvReturnList_T3.RowCount; i++)
            {
                string gridLotId = "F" + dgvReturnList_T3.Rows[i].Cells[4].Value.ToString().Substring(1, 9);
                if (gridLotId == lotId)
                {
                    return dgvReturnList_T3.Rows[i].Cells[7].Value.ToString();
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 출고 처리
        /// </summary>
        private void ProcessShipment()
        {
            CreateShipmentFile();
            PrintShipmentDocument();
            ResetShipmentForm();
        }

        /// <summary>
        /// 출고 파일 생성
        /// </summary>
        private void CreateShipmentFile(bool reprint = false)
        {
            // 구현 생략 - 원본 코드 참조
        }

        /// <summary>
        /// 출고 문서 인쇄
        /// </summary>
        private void PrintShipmentDocument()
        {
            // 구현 생략 - 원본 코드 참조
        }

        /// <summary>
        /// 출고 폼 초기화
        /// </summary>
        private void ResetShipmentForm()
        {
            txtProduct_T3.Text = string.Empty;
            txtSlipNo.Text = string.Empty;
            txtQty.Text = "0";
        }
        #endregion

        #region 프라이빗 메서드 - 유틸리티
        /// <summary>
        /// 데이터베이스 연결 확인 및 열기
        /// </summary>
        private void EnsureConnectionOpen()
        {
            if (_connection.State == ConnectionState.Closed)
                _connection.Open();
        }

        /// <summary>
        /// 바코드 라벨 출력
        /// </summary>
        private void PrintBarcodeLabel()
        {
            string lotId = !string.IsNullOrEmpty(txtZaLotID.Text) ? txtZaLotID.Text : txtLotID_T2.Text;
            var labelData = lib.Helpers.DBSearchHelper.LotCardBarcodePrint(_connection, lotId);
            
            if (frmMain.user_ID == "PHS")
            {
                MessageBox.Show(labelData);
            }
            else
            {
                if (!spBarcode.IsOpen)
                    spBarcode.Open();

                spBarcode.Write(labelData);
                spBarcode.Close();
            }

            UpdateBarcodeEquipmentUsage();
        }

        /// <summary>
        /// 바코드 장비 사용 횟수 업데이트
        /// </summary>
        private void UpdateBarcodeEquipmentUsage()
        {
            string barcodeName = IniHelper.IniReadValue("STD", BARCODE_EQUIPMENT_KEY);
            if (!string.IsNullOrEmpty(barcodeName))
            {
                EnsureConnectionOpen();
                string sql = $"UPDATE tb_equipment_list SET used_cnt = used_cnt + 1 WHERE code = '{barcodeName}'";
                MySqlHelper.ExecuteNonQuery(_connection, sql);
            }
        }

        /// <summary>
        /// 지역화된 메시지 표시
        /// </summary>
        private void ShowLocalizedMessage(string englishMessage, string koreanMessage)
        {
            string message = frmMain.language.Contains("English") ? englishMessage : koreanMessage;
            MessageBox.Show(message);
        }

        /// <summary>
        /// 메시지 박스 배경색 업데이트
        /// </summary>
        private void UpdateMessageBackColor(TextBox textBox)
        {
            switch (textBox.Text)
            {
                case "":
                    textBox.BackColor = Color.White;
                    break;
                case "PASS":
                    textBox.BackColor = Color.LawnGreen;
                    break;
                default:
                    textBox.BackColor = Color.Red;
                    break;
            }
        }

        /// <summary>
        /// M-OPTION 코드 텍스트 변환 (대문자)
        /// </summary>
        private void txtMOptCode_TextChanged(object sender, EventArgs e)
        {
            txtMOptCode.Text = txtMOptCode.Text.ToUpper();
            txtMOptCode.Select(txtMOptCode.Text.Length, 0);
        }

        /// <summary>
        /// MZB M-OPTION 코드 텍스트 변환 (대문자)
        /// </summary>
        private void txtMOptCode_T2_TextChanged(object sender, EventArgs e)
        {
            txtMOptCode_T2.Text = txtMOptCode_T2.Text.ToUpper();
            txtMOptCode_T2.Select(txtMOptCode_T2.Text.Length, 0);
        }
        #endregion
    }
}
