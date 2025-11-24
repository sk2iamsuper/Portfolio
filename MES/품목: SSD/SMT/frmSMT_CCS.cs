using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mes_
{
    public partial class frmSMT_CCS : Form
    {
        #region Constants
        // 상수 정의: 데이터베이스 스텝 ID, 최대 행 수, 콤보박스 항목 등
        private const int STEP_ID_CCS = 33; // CCS 단계 ID
        private const int MAX_DGV1_ROWS = 33; // DataGridView1 최대 행 수
        private const int MAX_DGV2_ROWS = 44; // DataGridView2 최대 행 수
        private const string COMBO_ITEMS = "OK,NG,"; // 콤보박스 기본 항목
        private const string SHIFT_ITEMS = "A,B,C,"; // Shift 콤보박스 항목
        #endregion

        #region Fields
        // 필드 정의: 데이터베이스 연결 및 관리 클래스
        private readonly MySqlConnection _connection; // MySQL 데이터베이스 연결
        private readonly CCSDataManager _dataManager; // 데이터 관리 클래스
        private readonly CCSLanguageManager _languageManager; // 언어 관리 클래스
        #endregion

        // 생성자: 데이터베이스 연결을 받아 초기화
        public frmSMT_CCS(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection ?? throw new ArgumentNullException(nameof(connection)); // null 체크
            _dataManager = new CCSDataManager(_connection); // 데이터 관리자 초기화
            _languageManager = new CCSLanguageManager(); // 언어 관리자 초기화
        }

        // 폼 로드 이벤트 핸들러
        private void frmSMT_CCS_Load(object sender, EventArgs e)
        {
            try
            {
                InitializeDataGridViews(); // DataGridView 초기화
                GetList(); // 데이터 목록 가져오기
            }
            catch (Exception ex)
            {
                MessageBox.Show($"폼 로드 중 오류: {ex.Message}", "오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Initialization Methods
        // DataGridView 컬럼 초기화 메서드
        private void InitializeDataGridViews()
        {
            // DataGridView1 컬럼 설정 (항목, 값)
            if (dataGridView1.Columns.Count == 0)
            {
                dataGridView1.Columns.Add("Item", "항목");
                dataGridView1.Columns.Add("Value", "값");
                dataGridView1.Columns[0].Width = 200; // 항목 컬럼 너비
                dataGridView1.Columns[1].Width = 150; // 값 컬럼 너비
            }

            // DataGridView3 컬럼 설정 (Zone, Setting, Actual, Spec)
            if (dataGridView3.Columns.Count == 0)
            {
                dataGridView3.Columns.Add("Zone", "ZONE");
                dataGridView3.Columns.Add("Setting", "SETTING");
                dataGridView3.Columns.Add("Actual", "ACTUAL");
                dataGridView3.Columns.Add("Spec", "SPEC");
            }
        }
        #endregion

        #region Data Grid Management
        // 데이터 그리드 목록 가져오기 메인 메서드
        private void GetList()
        {
            try
            {
                ClearDataGrids(); // 기존 데이터 클리어
                InitializeDataGridsByLanguage(); // 언어별 데이터 그리드 초기화
                ApplyStyling(); // 스타일 적용
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류: {ex.Message}", "오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 모든 데이터 그리드 클리어
        private void ClearDataGrids()
        {
            dataGridView1.Rows.Clear();
            dataGridView2.Rows.Clear();
            dataGridView3.Rows.Clear();
        }

        // 언어별 데이터 그리드 초기화
        private void InitializeDataGridsByLanguage()
        {
            // 설정된 언어에 따른 데이터 가져오기
            var languageData = _languageManager.GetLanguageData(Properties.Settings.Default.language);
            InitializeDataGridView1(languageData); // 첫 번째 그리드 초기화
            InitializeDataGridView2(languageData); // 두 번째 그리드 초기화
            InitializeDataGridView3(languageData); // 세 번째 그리드 초기화
        }

        // DataGridView1 초기화 (기본 정보 및 검사 항목)
        private void InitializeDataGridView1(Dictionary<string, string> languageData)
        {
            // 기본 정보 섹션
            AddTitleRow(dataGridView1, languageData["BASIC_INFORMATION"]);
            AddRowWithComboBox(dataGridView1, languageData["CH_CLASSIFY"], "ALL");
            AddRow(dataGridView1, languageData["CH_IMPLEMENT"], "");
            AddRow(dataGridView1, languageData["CH_CHECK"], "");
            AddRow(dataGridView1, languageData["MODEL_NAME"], "");
            AddRow(dataGridView1, languageData["LOT_NO"], "");
            AddRow(dataGridView1, languageData["LINE"], "");
            AddRowWithComboBox(dataGridView1, languageData["SHIFT"], SHIFT_ITEMS);

            // STENCIL 검사 섹션
            AddTitleRow(dataGridView1, languageData["STENCIL_HOLE_CHECK"]);
            AddRow(dataGridView1, languageData["TECHNICAL"], "");

            // 온도 프로파일 섹션
            AddTitleRow(dataGridView1, languageData["TEMPERATURE_PROFILE"]);
            AddRow(dataGridView1, languageData["TECHNICAL"], "");

            // COMP 역삽 검사 섹션
            AddTitleRow(dataGridView1, languageData["COMP_REVERSE_CHECK"]);
            AddRowWithComboBox(dataGridView1, languageData["FIRST_SIDE"], COMBO_ITEMS);
            AddRowWithComboBox(dataGridView1, languageData["SECOND_SIDE"], COMBO_ITEMS);

            // 능동소자 역삽 검사 섹션
            AddTitleRow(dataGridView1, languageData["ACTIVE_REVERSE_CHECK"]);
            AddRowWithComboBox(dataGridView1, languageData["FIRST_SIDE"], COMBO_ITEMS);
            AddRowWithComboBox(dataGridView1, languageData["SECOND_SIDE"], COMBO_ITEMS);

            // OCR 검사 섹션
            AddTitleRow(dataGridView1, languageData["OCR_CHECK"]);
            AddRow(dataGridView1, languageData["EMPLOYEE"], "");
            AddRow(dataGridView1, languageData["TECHNICIAN"], "");
            AddRow(dataGridView1, languageData["SHIFT_LD"], "");

            // STENCIL 정보 섹션
            AddTitleRow(dataGridView1, languageData["STENCIL_SD"]);
            AddRow(dataGridView1, languageData["STENCIL_CODE"], "");
            AddRow(dataGridView1, languageData["ARRAY"], "");

            // MOUNT 검사 섹션
            AddTitleRow(dataGridView1, languageData["MOUNT_CHECK"]);
            AddRowWithComboBox(dataGridView1, languageData["FIRST_SIDE"], COMBO_ITEMS);
            AddRowWithComboBox(dataGridView1, languageData["SECOND_SIDE"], COMBO_ITEMS);
            AddRow(dataGridView1, languageData["ABNORMALITY"], "");

            // X-RAY 검사 섹션
            AddTitleRow(dataGridView1, languageData["XRAY_CHECK"]);
            AddRowWithComboBox(dataGridView1, languageData["FIRST_SIDE"], COMBO_ITEMS);
            AddRowWithComboBox(dataGridView1, languageData["SECOND_SIDE"], COMBO_ITEMS);
            AddRow(dataGridView1, languageData["ABNORMALITY"], "");
        }

        // DataGridView2 초기화 (P-AOI 및 REFLOW 검사)
        private void InitializeDataGridView2(Dictionary<string, string> languageData)
        {
            // P-AOI 검사 섹션
            AddTitleRow(dataGridView2, languageData["PAOI_CHECK"]);
            for (int i = 1; i <= 5; i++)
            {
                AddRow(dataGridView2, $"{languageData["SOLDER_VOLUME"]} #{i}", "0");
            }

            // REFLOW 검사 섹션
            AddTitleRow(dataGridView2, languageData["REFLOW_CHECK"]);
            AddRow(dataGridView2, languageData["RECIPE"], "");
            AddRowWithComboBox(dataGridView2, languageData["N2_ABNORMALITY"], COMBO_ITEMS);
            AddRow(dataGridView2, languageData["O2_PPM"], "0");
            AddRow(dataGridView2, languageData["BELT_SPEED_FIRST"], "0");
            AddRow(dataGridView2, languageData["BELT_SPEED_SECOND"], "");

            // Marking 검사 섹션
            AddTitleRow(dataGridView2, languageData["MARKING_CHECK"]);
            AddRow(dataGridView2, languageData["NAND"], "");
            AddRow(dataGridView2, languageData["DRAM"], "");
            AddRow(dataGridView2, languageData["CONTROLLER"], "");
        }

        // DataGridView3 초기화 (REFLOW ZONE 온도)
        private void InitializeDataGridView3(Dictionary<string, string> languageData)
        {
            // REFLOW ZONE 온도 섹션
            AddTitleRow(dataGridView3, languageData["REFLOW_ZONE_TEMP"]);
            for (int i = 1; i <= 14; i++)
            {
                AddRow(dataGridView3, $"{languageData["ZONE"]} {i:D2}", "", "", "");
            }

            // 영어 또는 베트남어일 경우 컬럼 헤더 텍스트 변경
            if (Properties.Settings.Default.language == "English" || 
                Properties.Settings.Default.language == "Tiếng việt")
            {
                dataGridView3.Columns[1].HeaderText = languageData["TEMP_SETTING"];
                dataGridView3.Columns[2].HeaderText = languageData["TEMP_ACTUAL"];
                dataGridView3.Columns[3].HeaderText = languageData["SPEC_TABLE"];
            }
        }

        // 제목 행 추가 메서드 (회색 배경)
        private void AddTitleRow(DataGridView dgv, string title)
        {
            int rowIndex = dgv.Rows.Add(title, "");
            // 2개 이상의 컬럼이 있을 경우 빈 값 추가
            if (dgv.ColumnCount > 2)
            {
                dgv.Rows[rowIndex].Cells[2].Value = "";
                dgv.Rows[rowIndex].Cells[3].Value = "";
            }
            
            // 제목 행 스타일 적용 (회색 배경, 굵은 글씨)
            for (int i = 0; i < dgv.ColumnCount; i++)
            {
                dgv.Rows[rowIndex].Cells[i].Style.BackColor = Color.LightGray;
                dgv.Rows[rowIndex].Cells[i].Style.Font = new Font(dgv.Font, FontStyle.Bold);
            }
        }

        // 일반 행 추가 메서드 (2컬럼)
        private void AddRow(DataGridView dgv, string itemName, string value)
        {
            dgv.Rows.Add(itemName, value);
        }

        // 일반 행 추가 메서드 (4컬럼)
        private void AddRow(DataGridView dgv, string itemName, string value1, string value2, string value3)
        {
            dgv.Rows.Add(itemName, value1, value2, value3);
        }

        // 콤보박스가 있는 행 추가 메서드
        private void AddRowWithComboBox(DataGridView dgv, string itemName, string comboItems)
        {
            int rowIndex = dgv.Rows.Add(itemName, "");
            SetComboBox(dgv, rowIndex, comboItems); // 콤보박스 설정
        }

        // DataGridView에 콤보박스 셀 설정
        private void SetComboBox(DataGridView dgv, int rowIndex, string items)
        {
            var comboCell = new DataGridViewComboBoxCell();
            comboCell.DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox;

            // 콤보박스 항목 분리 및 추가
            string[] itemArray = items.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in itemArray)
            {
                comboCell.Items.Add(item);
            }

            // 지정된 행과 셀에 콤보박스 적용
            dgv.Rows[rowIndex].Cells[1] = comboCell;
        }

        // 모든 DataGridView에 스타일 적용
        private void ApplyStyling()
        {
            ApplyDataGridViewStyling(dataGridView1);
            ApplyDataGridViewStyling(dataGridView2);
            ApplyDataGridViewStyling(dataGridView3);
        }

        // 개별 DataGridView 스타일 적용
        private void ApplyDataGridViewStyling(DataGridView dgv)
        {
            dgv.ClearSelection(); // 선택 해제

            // 모든 행과 셀에 대해 스타일 적용
            for (int i = 0; i < dgv.RowCount; i++)
            {
                for (int j = 0; j < dgv.ColumnCount; j++)
                {
                    // 제목 행이 아닌 경우 연한 파란색 배경 적용
                    if (dgv.Rows[i].Cells[j].Style.BackColor != Color.LightGray)
                    {
                        dgv.Rows[i].Cells[j].Style.BackColor = Color.LightBlue;
                    }
                }
            }
        }
        #endregion

        #region Event Handlers
        // 스캔 데이터 입력 이벤트 핸들러 (Enter 키)
        private void txtScanData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && !string.IsNullOrWhiteSpace(txtScanData.Text))
            {
                try
                {
                    ProcessScanData(); // 스캔 데이터 처리
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"스캔 데이터 처리 중 오류: {ex.Message}", "오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // 저장/업데이트 버튼 클릭 이벤트 핸들러
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                if (!ValidateInputs()) return; // 입력 유효성 검사

                SaveOrUpdateData(); // 데이터 저장 또는 업데이트
                ResetForm(); // 폼 초기화
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 저장 중 오류: {ex.Message}", "오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 이력 조회 버튼 클릭 이벤트 핸들러
        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                GetHistoryList(); // 이력 목록 가져오기
            }
            catch (Exception ex)
            {
                MessageBox.Show($"이력 조회 중 오류: {ex.Message}", "오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 탭 변경 이벤트 핸들러
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 이력 탭 선택 시 이력 목록 자동 조회
            if (tabControl1.SelectedIndex == 1)
            {
                button2_Click(null, null);
            }
        }

        // 이력 목록 셀 클릭 이벤트 핸들러
        private void dataGridView4_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // 유효한 행 클릭 시 상세 정보 표시
            if (e.RowIndex >= 0 && e.RowIndex < dataGridView4.RowCount && 
                dataGridView4.Rows[e.RowIndex].Cells[0].Value != null)
            {
                try
                {
                    DisplayHistoryDetail(dataGridView4.Rows[e.RowIndex].Cells[0].Value.ToString());
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"이력 상세 조회 중 오류: {ex.Message}", "오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // LOT ID 검색 이벤트 핸들러 (Enter 키)
        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && !string.IsNullOrWhiteSpace(textBox1.Text))
            {
                try
                {
                    // LOT ID로 이력 검색
                    GetHistoryList(textBox1.Text.Trim().ToUpper());
                    textBox1.Text = string.Empty; // 검색 후 입력창 클리어
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"LOT ID 검색 중 오류: {ex.Message}", "오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        #endregion

        #region Business Logic
        // 스캔 데이터 처리 메서드
        private void ProcessScanData()
        {
            txtScanData.Text = txtScanData.Text.ToUpper(); // 대문자 변환
            button1.Text = "SAVE"; // 버튼 텍스트를 SAVE로 변경
            txtTob.Text = string.Empty; // TOB 필드 클리어
            txtLogid.Text = string.Empty; // 로그 ID 필드 클리어
            GetList(); // 데이터 목록 새로고침

            // 스캔된 데이터로 스케줄 정보 조회
            var scheduleData = _dataManager.GetScheduleData(txtScanData.Text);
            if (scheduleData != null)
            {
                UpdateFormWithScheduleData(scheduleData); // 폼 데이터 업데이트
            }

            txtSchno.Text = txtScanData.Text; // 스케줄 번호 설정
            txtScanData.Text = string.Empty; // 스캔 입력창 클리어
        }

        // 스케줄 데이터로 폼 업데이트
        private void UpdateFormWithScheduleData(Dictionary<string, string> scheduleData)
        {
            // 조회된 데이터를 각 필드에 설정
            dataGridView1.Rows[4].Cells[1].Value = scheduleData["prod_code"]; // 제품 코드
            dataGridView1.Rows[5].Cells[1].Value = scheduleData["lotid"]; // LOT ID
            dataGridView1.Rows[6].Cells[1].Value = scheduleData["line_name"]; // 라인 이름
            dataGridView1.Rows[24].Cells[1].Value = scheduleData["array_size"]; // 배열 크기
            txtTob.Text = scheduleData["tob"]; // TOB 값
        }

        // 입력 데이터 유효성 검사
        private bool ValidateInputs()
        {
            // 데이터 그리드에 데이터가 있는지 확인
            if (dataGridView1.RowCount == 0)
            {
                MessageBox.Show("데이터가 없습니다.", "경고", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // 스케줄 번호가 입력되었는지 확인
            if (string.IsNullOrEmpty(txtSchno.Text))
            {
                MessageBox.Show("스케줄 번호를 먼저 스캔하세요.", "경고", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        // 데이터 저장 또는 업데이트
        private void SaveOrUpdateData()
        {
            string logData = BuildLogData(); // 로그 데이터 생성
            string lineName = GetValueFromDataGridView(dataGridView1, "LINE"); // 라인 이름 추출
            string lotId = GetValueFromDataGridView(dataGridView1, "LOT NO"); // LOT ID 추출

            // 트랜잭션 시작 (데이터 무결성 보장)
            using (var transaction = _connection.BeginTransaction())
            {
                try
                {
                    // 버튼 텍스트에 따라 저장 또는 업데이트
                    if (button1.Text == "SAVE")
                    {
                        _dataManager.InsertCCSLog(txtSchno.Text, logData, txtTob.Text, frmMain.userID, STEP_ID_CCS);
                    }
                    else
                    {
                        _dataManager.UpdateCCSLog(txtLogid.Text, logData);
                    }

                    SaveO2StabilityData(lineName, lotId); // O2 안정성 데이터 저장
                    transaction.Commit(); // 트랜잭션 커밋

                    MessageBox.Show("데이터가 성공적으로 저장되었습니다.", "성공", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception)
                {
                    transaction.Rollback(); // 오류 시 트랜잭션 롤백
                    throw;
                }
            }
        }

        // 로그 데이터 생성
        private string BuildLogData()
        {
            var logBuilder = new StringBuilder();
            logBuilder.AppendLine($"CCS NUMBER : {txtSchno.Text}"); // CCS 번호 추가

            // 각 데이터 그리드의 데이터를 로그에 추가
            AppendDataGridViewData(logBuilder, dataGridView1);
            AppendDataGridViewData(logBuilder, dataGridView2);
            AppendDataGridViewData(logBuilder, dataGridView3);

            return logBuilder.ToString();
        }

        // DataGridView 데이터를 로그에 추가
        private void AppendDataGridViewData(StringBuilder logBuilder, DataGridView dgv)
        {
            for (int i = 0; i < dgv.RowCount; i++)
            {
                var row = dgv.Rows[i];
                if (row.Cells[0].Value == null) continue;

                // 행 데이터를 파이프로 구분하여 구성
                string rowData = row.Cells[0].Value.ToString();
                for (int j = 1; j < dgv.ColumnCount; j++)
                {
                    rowData += "|" + (row.Cells[j].Value?.ToString() ?? "");
                }
                logBuilder.AppendLine(rowData);
            }
        }

        // O2 안정성 데이터 저장
        private void SaveO2StabilityData(string lineName, string lotId)
        {
            string o2Value = GetValueFromDataGridView(dataGridView2, "O2 ppm");
            // O2 값이 유효한 숫자인지 확인
            if (!string.IsNullOrEmpty(o2Value) && double.TryParse(o2Value, out double o2Ppm))
            {
                string dateTime = DateTime.Now.ToString("yyyyMMddHH");
                _dataManager.InsertStabilityData(dateTime, lineName, o2Ppm, frmMain.userID, lotId);
            }
        }

        // DataGridView에서 특정 항목의 값 추출
        private string GetValueFromDataGridView(DataGridView dgv, string searchText)
        {
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.Cells[0].Value?.ToString().Contains(searchText) == true)
                {
                    return row.Cells[1].Value?.ToString() ?? "";
                }
            }
            return "";
        }

        // 폼 초기화
        private void ResetForm()
        {
            button1.Text = "SAVE";
            txtSchno.Text = string.Empty;
            txtTob.Text = string.Empty;
            txtLogid.Text = string.Empty;
            GetList(); // 데이터 목록 새로고침
        }

        // 이력 목록 가져오기
        private void GetHistoryList(string lotId = null)
        {
            dataGridView4.Rows.Clear(); // 기존 이력 데이터 클리어

            // LOT ID 유무에 따라 다른 조회 방법 사용
            var historyData = string.IsNullOrEmpty(lotId) 
                ? _dataManager.GetHistoryData(dtpStart.Value, dtpEnd.Value, STEP_ID_CCS) // 기간 조회
                : _dataManager.GetHistoryDataByLotId(lotId, STEP_ID_CCS); // LOT ID 조회

            // 조회된 데이터를 이력 그리드에 추가
            foreach (var item in historyData)
            {
                dataGridView4.Rows.Add(item.Id, item.Series, item.ProductCode, item.LotId, 
                                     item.CCSNumber, item.CreatedDate);
            }
            dataGridView4.ClearSelection(); // 선택 해제
        }

        // 이력 상세 정보 표시
        private void DisplayHistoryDetail(string logId)
        {
            string logContent = _dataManager.GetLogDetail(logId);
            txtMain.Text = logContent?.Replace("\n", Environment.NewLine) ?? ""; // 개행 문자 변환
        }
        #endregion
    }

    #region Support Classes
    // 데이터 관리 클래스: 데이터베이스 작업 전담
    public class CCSDataManager
    {
        private readonly MySqlConnection _connection;

        public CCSDataManager(MySqlConnection connection)
        {
            _connection = connection;
        }

        // 스케줄 데이터 조회
        public Dictionary<string, string> GetScheduleData(string scheduleNo)
        {
            var sql = @"
                SELECT e.prod_code, l.lotid, n.line_name, e.array_size, s.tob 
                FROM tb_mes_sch_smt s, tb_mrp_std_line n, tb_mes_std_espec e, tb_mes_sch_daily d, tb_mes_lotid l
                WHERE s.line_id = n.id AND s.prod_id = e.id AND s.dailyorder_id = d.id AND d.lot_id = l.id
                AND s.sch_no = @scheduleNo";

            using (var command = new MySqlCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@scheduleNo", scheduleNo);
                
                if (_connection.State != ConnectionState.Open)
                    _connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new Dictionary<string, string>
                        {
                            ["prod_code"] = reader["prod_code"].ToString(),
                            ["lotid"] = reader["lotid"].ToString(),
                            ["line_name"] = reader["line_name"].ToString(),
                            ["array_size"] = reader["array_size"].ToString(),
                            ["tob"] = reader["tob"].ToString()
                        };
                    }
                }
            }
            return null;
        }

        // CCS 로그 삽입
        public void InsertCCSLog(string scheduleNo, string log, string tob, int userId, int stepId)
        {
            var sql = @"
                INSERT INTO tb_ccs_lot_log (lot_id, step_id, log, result, user_id) 
                SELECT d.lot_id, @stepId, @log, @tob, @userId 
                FROM tb_mes_sch_smt s, tb_mes_sch_daily d 
                WHERE s.dailyorder_id = d.id AND sch_no = @scheduleNo";

            ExecuteNonQuery(sql, 
                new MySqlParameter("@stepId", stepId),
                new MySqlParameter("@log", log),
                new MySqlParameter("@tob", tob),
                new MySqlParameter("@userId", userId),
                new MySqlParameter("@scheduleNo", scheduleNo));
        }

        // CCS 로그 업데이트
        public void UpdateCCSLog(string logId, string log)
        {
            var sql = "UPDATE tb_ccs_lot_log SET log = @log WHERE id = @logId";
            ExecuteNonQuery(sql,
                new MySqlParameter("@log", log),
                new MySqlParameter("@logId", logId));
        }

        // 안정성 데이터 삽입 (O2 분석기 데이터)
        public void InsertStabilityData(string dateTime, string lineName, double o2Value, int userId, string lotId)
        {
            var sql = @"
                INSERT INTO tb_stability_log (measurement_date, stability_espec_id, line_id, sample_no, point_no, measured_value, op_id, x_bar, r, dept, lot_id) 
                SELECT @dateTime, id, (SELECT id FROM tb_mrp_std_line WHERE line_name = @lineName), 1, 1, @o2Value, @userId, @o2Value, 0, 'P', (SELECT id FROM tb_mes_lotid WHERE lotid = @lotId) 
                FROM tb_stability_espec WHERE role = 'O2-Analyzer'";

            ExecuteNonQuery(sql,
                new MySqlParameter("@dateTime", dateTime),
                new MySqlParameter("@lineName", lineName),
                new MySqlParameter("@o2Value", o2Value),
                new MySqlParameter("@userId", userId),
                new MySqlParameter("@lotId", lotId));
        }

        // 기간별 이력 데이터 조회
        public List<HistoryItem> GetHistoryData(DateTime startDate, DateTime endDate, int stepId)
        {
            var sql = @"
                SELECT g.id, e.series, e.prod_code, l.lotid, SUBSTRING_INDEX(g.log, '\n', 1), date_format(g.created_on, '%Y-%m-%d %H:%i:%s')
                FROM tb_ccs_lot_log g, tb_mes_lotid l, tb_mes_sch_daily d, tb_mes_std_espec e
                WHERE g.lot_id = l.id AND l.id = d.lot_id AND d.prod_id = e.id 
                AND g.created_on > @startDate AND g.created_on <= @endDate
                AND g.step_id = @stepId";

            return ExecuteHistoryQuery(sql,
                new MySqlParameter("@startDate", startDate.ToString("yyyy-MM-dd 00:00:00")),
                new MySqlParameter("@endDate", endDate.ToString("yyyy-MM-dd 23:59:59")),
                new MySqlParameter("@stepId", stepId));
        }

        // LOT ID별 이력 데이터 조회
        public List<HistoryItem> GetHistoryDataByLotId(string lotId, int stepId)
        {
            var sql = @"
                SELECT g.id, e.series, e.prod_code, l.lotid, SUBSTRING_INDEX(g.log, '\n', 1), date_format(g.created_on, '%Y-%m-%d %H:%i:%s')
                FROM tb_ccs_lot_log g, tb_mes_lotid l, tb_mes_sch_daily d, tb_mes_std_espec e
                WHERE g.lot_id = l.id AND l.id = d.lot_id AND d.prod_id = e.id 
                AND l.lotid = @lotId AND g.step_id = @stepId";

            return ExecuteHistoryQuery(sql,
                new MySqlParameter("@lotId", lotId),
                new MySqlParameter("@stepId", stepId));
        }

        // 로그 상세 정보 조회
        public string GetLogDetail(string logId)
        {
            var sql = "SELECT log FROM tb_ccs_lot_log WHERE id = @logId";
            
            using (var command = new MySqlCommand(sql, _connection))
            {
                command.Parameters.AddWithValue("@logId", logId);
                
                if (_connection.State != ConnectionState.Open)
                    _connection.Open();

                var result = command.ExecuteScalar();
                return result?.ToString();
            }
        }

        // Non-Query SQL 실행 (INSERT, UPDATE, DELETE)
        private void ExecuteNonQuery(string sql, params MySqlParameter[] parameters)
        {
            using (var command = new MySqlCommand(sql, _connection))
            {
                command.Parameters.AddRange(parameters);
                
                if (_connection.State != ConnectionState.Open)
                    _connection.Open();

                command.ExecuteNonQuery();
            }
        }

        // 이력 데이터 조회 실행
        private List<HistoryItem> ExecuteHistoryQuery(string sql, params MySqlParameter[] parameters)
        {
            var results = new List<HistoryItem>();

            using (var command = new MySqlCommand(sql, _connection))
            {
                command.Parameters.AddRange(parameters);
                
                if (_connection.State != ConnectionState.Open)
                    _connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new HistoryItem
                        {
                            Id = reader[0].ToString(),
                            Series = reader[1].ToString(),
                            ProductCode = reader[2].ToString(),
                            LotId = reader[3].ToString(),
                            CCSNumber = reader[4].ToString().Replace("CCS NUMBER : ", ""),
                            CreatedDate = reader[5].ToString()
                        });
                    }
                }
            }
            return results;
        }
    }

    // 언어 관리 클래스: 다국어 지원 전담
    public class CCSLanguageManager
    {
        // 언어별 데이터 반환
        public Dictionary<string, string> GetLanguageData(string language)
        {
            return language?.ToLower() switch
            {
                "english" => GetEnglishData(), // 영어 데이터
                "tiếng việt" => GetVietnameseData(), // 베트남어 데이터
                _ => GetKoreanData() // 기본 한국어 데이터
            };
        }

        // 한국어 데이터
        private Dictionary<string, string> GetKoreanData()
        {
            return new Dictionary<string, string>
            {
                ["BASIC_INFORMATION"] = "BASIC INFORMATION",
                ["CH_CLASSIFY"] = "  C/H 구분",
                ["CH_IMPLEMENT"] = "  C/H 실시",
                ["CH_CHECK"] = "  C/H 확인",
                ["MODEL_NAME"] = "  제품명",
                ["LOT_NO"] = "  LOT NO",
                ["LINE"] = "  LINE",
                ["SHIFT"] = "  SHIFT",
                ["STENCIL_HOLE_CHECK"] = "STENCIL 쪽접합 개구부 확인",
                ["TEMPERATURE_PROFILE"] = "온도 PROFILE 측정 확인",
                ["TECHNICAL"] = "  기 술",
                ["COMP_REVERSE_CHECK"] = "COMP 역삽 CHECK",
                ["ACTIVE_REVERSE_CHECK"] = "능동소자 역삽 CHECK",
                ["FIRST_SIDE"] = "  1차면",
                ["SECOND_SIDE"] = "  2차면",
                ["OCR_CHECK"] = "OCR 확인 (초도시 SHIFT장 확인)",
                ["EMPLOYEE"] = "  작업자",
                ["TECHNICIAN"] = "  기 술",
                ["SHIFT_LD"] = "  SHIFT장",
                ["STENCIL_SD"] = "STENCIL, S/D",
                ["STENCIL_CODE"] = "  STENCIL CODE",
                ["ARRAY"] = "  연배열",
                ["MOUNT_CHECK"] = "MOUNT 자재 CHECK (Spec,외관확인)",
                ["XRAY_CHECK"] = "X-RAY 검사",
                ["ABNORMALITY"] = "  이상 유무",
                ["PAOI_CHECK"] = "P-AOI CHECK",
                ["SOLDER_VOLUME"] = "  Solder Volume",
                ["REFLOW_CHECK"] = "REFLOW CHECK",
                ["RECIPE"] = "  RECIPE",
                ["N2_ABNORMALITY"] = "  N2 이상유무",
                ["O2_PPM"] = "  O2 ppm",
                ["BELT_SPEED_FIRST"] = "  BELT SPEED (1차면)",
                ["BELT_SPEED_SECOND"] = "  BELT SPEED (2차면)",
                ["MARKING_CHECK"] = "Marking Checking",
                ["NAND"] = "  NAND",
                ["DRAM"] = "  DRAM",
                ["CONTROLLER"] = "  CONTROLLER",
                ["REFLOW_ZONE_TEMP"] = "REFLOW ZONE 별 온도",
                ["ZONE"] = "  Zone",
                ["TEMP_SETTING"] = "TEMPERATURE SETTING",
                ["TEMP_ACTUAL"] = "TEMPERATUE ACTUAL",
                ["SPEC_TABLE"] = "SPEC TABLE"
            };
        }

        // 영어 데이터 (한국어 데이터 기반으로 키는 동일, 값만 영어로 변경)
        private Dictionary<string, string> GetEnglishData()
        {
            var data = GetKoreanData();
            data["BASIC_INFORMATION"] = "BASIC INFORMATION";
            data["CH_CLASSIFY"] = "  C/H Classify";
            data["CH_IMPLEMENT"] = "  C/H implement";
            data["CH_CHECK"] = "  C/H check";
            data["MODEL_NAME"] = "  Model name";
            data["STENCIL_HOLE_CHECK"] = "STENCIL hole
