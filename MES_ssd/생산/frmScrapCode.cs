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
    public partial class frmScrapCode : Form
    {
        // 데이터베이스 연결 객체 (읽기 전용으로 선언하여 안전성 확보)
        private readonly MySqlConnection _connection;
        private string _lotid;
        private string _scripcode;
        
        // 상수 정의 - 하드코딩 방지 및 유지보수성 향상
        private const string DateFormat = "yyyy-MM-dd";
        private const string ScrapCodeTable = "tb_scrapcode";
        private const string ScrapHistoryTable = "tb_scrapcode_history";
        private const string MesProcessTable = "tb_mes_process";
        private const string MesLotidTable = "tb_mes_lotid";

        /// <summary>
        /// 생성자 - Lot ID와 사용자 ID를 받는 경우
        /// </summary>
        /// <param name="connection">데이터베이스 연결 객체</param>
        /// <param name="lotid">Lot ID</param>
        /// <param name="userid">사용자 ID</param>
        public frmScrapCode(MySqlConnection connection, string lotid, string userid)
        {
            InitializeComponent();
            _connection = connection;
            txtLotID.Text = _lotid = lotid;
            txtUserName.Text = userid;
        }

        /// <summary>
        /// 생성자 - 데이터베이스 연결만 받는 경우
        /// </summary>
        /// <param name="connection">데이터베이스 연결 객체</param>
        public frmScrapCode(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;
            txtUserName.Text = frmMain.userName;
        }

        /// <summary>
        /// 폼 로드 이벤트 - 초기 데이터 조회
        /// </summary>
        private void frmScrapCode_Load(object sender, EventArgs e)
        {
            try
            {
                // 공정 정보와 Section1 데이터 조회
                GetStep();
                GetSection1();

                // Lot ID가 있는 경우 해당 Lot의 수율 정보 조회
                if (!string.IsNullOrEmpty(txtLotID.Text))
                    GetYield(txtLotID.Text);
            }
            catch (Exception ex)
            {
                ShowError("폼 로드 중 오류가 발생했습니다.", ex);
            }
        }

        #region 데이터 조회 메서드

        /// <summary>
        /// 공정(Step) 정보를 콤보박스에 로드
        /// </summary>
        private void GetStep()
        {
            try
            {
                cbStep.Items.Clear();
                var sql = $"SELECT step FROM {MesProcessTable} ORDER BY id";
                
                // using 문을 사용하여 리소스 자동 해제 (메모리 누수 방지)
                using (var dataset = MySqlHelper.ExecuteDataset(_connection, sql))
                using (var dataTable = dataset.Tables[0])
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        cbStep.Items.Add(row[0].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("공정 정보 조회 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// Section1 정보를 콤보박스에 로드
        /// </summary>
        private void GetSection1()
        {
            try
            {
                comboBox1.Items.Clear();
                var sql = $"SELECT DISTINCT section1 FROM {ScrapCodeTable} ORDER BY section1";
                
                using (var dataset = MySqlHelper.ExecuteDataset(_connection, sql))
                using (var dataTable = dataset.Tables[0])
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        comboBox1.Items.Add(row[0].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Section1 정보 조회 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 선택된 Section1에 해당하는 Section2 정보를 콤보박스에 로드
        /// </summary>
        private void GetSection2()
        {
            try
            {
                comboBox2.Items.Clear();
                var sql = $"SELECT DISTINCT section2 FROM {ScrapCodeTable} WHERE section1 = @section1 ORDER BY section2";
                
                // 매개변수화된 쿼리 사용으로 SQL Injection 방지
                var parameters = new MySqlParameter[] {
                    new MySqlParameter("@section1", comboBox1.Text)
                };
                
                using (var dataset = MySqlHelper.ExecuteDataset(_connection, sql, parameters))
                using (var dataTable = dataset.Tables[0])
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        comboBox2.Items.Add(row[0].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Section2 정보 조회 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 선택된 Section1, Section2에 해당하는 Section3(설명) 정보를 콤보박스에 로드
        /// </summary>
        private void GetSection3()
        {
            try
            {
                comboBox3.Items.Clear();
                var sql = $"SELECT description_kr FROM {ScrapCodeTable} WHERE section1 = @section1 AND section2 = @section2 ORDER BY section2";
                
                var parameters = new MySqlParameter[] {
                    new MySqlParameter("@section1", comboBox1.Text),
                    new MySqlParameter("@section2", comboBox2.Text)
                };
                
                using (var dataset = MySqlHelper.ExecuteDataset(_connection, sql, parameters))
                using (var dataTable = dataset.Tables[0])
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        comboBox3.Items.Add(row[0].ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Section3 정보 조회 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 기간별 스크랩 코드 이력 조회 (전체 조회 탭용)
        /// </summary>
        private void GetYield()
        {
            try
            {
                dgvList.Rows.Clear();
                var sql = $@"SELECT h.id, SUBSTR(h.created_on, 1, 10), e.prod_code, l.lotid, p.step, h.eqp_name, h.line_name, 
                            h.in_qty, h.fail_qty, ROUND((1 - (h.fail_qty / h.in_qty)) * 100, 2), s.section1, s.section2, s.description_kr  
                            FROM {ScrapHistoryTable} h, {MesLotidTable} l, {MesProcessTable} p, {ScrapCodeTable} s, tb_mes_std_espec e 
                            WHERE h.lot_id = l.id AND h.step_id = p.id AND h.scrap_id = s.id AND l.espec_id = e.id 
                            AND SUBSTR(h.created_on, 1, 10) >= @startDate AND SUBSTR(h.created_on, 1, 10) <= @endDate 
                            ORDER BY SUBSTR(h.created_on, 1, 10)";
                
                var parameters = new MySqlParameter[] {
                    new MySqlParameter("@startDate", dtpStart.Value.ToString(DateFormat)),
                    new MySqlParameter("@endDate", dtpEnd.Value.ToString(DateFormat))
                };

                using (var dataset = MySqlHelper.ExecuteDataset(_connection, sql, parameters))
                using (var dataTable = dataset.Tables[0])
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        // 데이터 그리드뷰에 행 추가
                        dgvList.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7], row[8], row[9], row[10], row[11], row[12]);
                    }
                    dgvList.ClearSelection(); // 선택 해제
                }
            }
            catch (Exception ex)
            {
                ShowError("수율 정보 조회 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 특정 Lot ID의 스크랩 코드 이력 조회 (Lot별 조회 탭용)
        /// </summary>
        /// <param name="lotid">조회할 Lot ID</param>
        private void GetYield(string lotid)
        {
            try
            {
                dataGridView1.Rows.Clear();
                var sql = $@"SELECT SUBSTR(h.created_on, 1, 10), e.prod_code, l.lotid, p.step, h.eqp_name, h.line_name, 
                            h.in_qty, h.fail_qty, ROUND((1 - (h.fail_qty / h.in_qty)) * 100, 2), s.section1, s.section2, s.description_kr  
                            FROM {ScrapHistoryTable} h, {MesLotidTable} l, {MesProcessTable} p, {ScrapCodeTable} s, tb_mes_std_espec e 
                            WHERE h.lot_id = l.id AND h.step_id = p.id AND h.scrap_id = s.id AND l.espec_id = e.id 
                            AND l.lotid = @lotid ORDER BY SUBSTR(h.created_on, 1, 10)";
                
                var parameters = new MySqlParameter[] {
                    new MySqlParameter("@lotid", lotid)
                };

                using (var dataset = MySqlHelper.ExecuteDataset(_connection, sql, parameters))
                using (var dataTable = dataset.Tables[0])
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        dataGridView1.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7], row[8], row[9], row[10], row[11]);
                    }
                    dataGridView1.ClearSelection(); // 선택 해제
                }
            }
            catch (Exception ex)
            {
                ShowError("Lot별 수율 정보 조회 중 오류가 발생했습니다.", ex);
            }
        }
        #endregion

        #region 이벤트 핸들러

        /// <summary>
        /// 스캔 데이터 입력 처리 (Enter 키 이벤트)
        /// </summary>
        private void txtScandata_KeyUp(object sender, KeyEventArgs e)
        {
            // Enter 키가 눌리고 스캔 데이터가 있는 경우 처리
            if (e.KeyData == Keys.Enter && !string.IsNullOrWhiteSpace(txtScandata.Text))
            {
                try
                {
                    txtScandata.Text = txtScandata.Text.ToUpper(); // 대문자 변환

                    var sql = $"SELECT lotid, start_lot_qty FROM {MesLotidTable} WHERE lotid = @lotid";
                    var parameters = new MySqlParameter[] {
                        new MySqlParameter("@lotid", txtScandata.Text)
                    };

                    using (var dataset = MySqlHelper.ExecuteDataset(_connection, sql, parameters))
                    using (var dataTable = dataset.Tables[0])
                    {
                        if (dataTable.Rows.Count > 0)
                        {
                            // Lot 정보가 있는 경우 텍스트박스에 표시
                            DataRow row = dataTable.Rows[0];
                            txtLotID.Text = row[0].ToString();
                            txtLotQty.Text = row[1].ToString();
                        }
                        else
                        {
                            MessageBox.Show("해당 Lot ID를 찾을 수 없습니다.", "알림", 
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }

                    txtScandata.Text = string.Empty; // 입력 필드 초기화
                }
                catch (Exception ex)
                {
                    ShowError("Lot 정보 조회 중 오류가 발생했습니다.", ex);
                }
            }
        }

        /// <summary>
        /// Section1 선택 변경 이벤트 - Section2 데이터 로드
        /// </summary>
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(comboBox1.Text))
                GetSection2();
        }

        /// <summary>
        /// Section2 선택 변경 이벤트 - Section3 데이터 로드
        /// </summary>
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(comboBox2.Text))
                GetSection3();
        }

        /// <summary>
        /// Section3 선택 변경 이벤트 - 스크랩 코드 값 설정
        /// </summary>
        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(comboBox3.Text))
                return;

            try
            {
                var sql = $"SELECT scrap_code FROM {ScrapCodeTable} WHERE section1 = @section1 AND section2 = @section2 AND description_kr = @description";
                var parameters = new MySqlParameter[] {
                    new MySqlParameter("@section1", comboBox1.Text),
                    new MySqlParameter("@section2", comboBox2.Text),
                    new MySqlParameter("@description", comboBox3.Text)
                };

                // 선택된 설명에 해당하는 스크랩 코드 조회
                _scripcode = Helpers.MySqlHelper.GetOneData(_connection, sql, parameters);
            }
            catch (Exception ex)
            {
                ShowError("스크랩 코드 조회 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 탭 변경 이벤트 - 선택된 탭에 따른 데이터 조회
        /// </summary>
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                switch (tabControl1.SelectedIndex)
                {
                    case 0: // Lot별 조회 탭
                        GetYield(txtLotID.Text);
                        break;
                    case 1: // 전체 조회 탭
                        GetYield();
                        break;
                }
            }
            catch (Exception ex)
            {
                ShowError("탭 전환 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 검색 버튼 클릭 이벤트 - 전체 조회 실행
        /// </summary>
        private void btnSearch_Click(object sender, EventArgs e)
        {
            GetYield();
        }

        /// <summary>
        /// 저장 버튼 클릭 이벤트 - 스크랩 코드 이력 저장
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            // 입력값 유효성 검사
            if (!ValidateInput())
                return;

            try
            {
                // 데이터베이스 연결 확인
                if (_connection.State == ConnectionState.Closed)
                    _connection.Open();

                var sql = $@"INSERT INTO {ScrapHistoryTable} (lot_id, step_id, eqp_name, line_name, in_qty, fail_qty, scrap_id) 
                            SELECT id, (SELECT id FROM {MesProcessTable} WHERE step = @step), @eqpName, @lineName, 
                            start_lot_qty, @failQty, (SELECT id FROM {ScrapCodeTable} WHERE scrap_code = @scrapCode) 
                            FROM {MesLotidTable} WHERE lotid = @lotid";

                var parameters = new MySqlParameter[] {
                    new MySqlParameter("@step", cbStep.Text),
                    new MySqlParameter("@eqpName", cbEQPid.Text),
                    new MySqlParameter("@lineName", cbline.Text),
                    new MySqlParameter("@failQty", txtFailQty.Text),
                    new MySqlParameter("@scrapCode", _scripcode),
                    new MySqlParameter("@lotid", txtLotID.Text)
                };

                // 스크랩 코드 이력 저장
                MySqlHelper.ExecuteNonQuery(_connection, sql, parameters);

                // 저장 후 데이터 갱신 및 입력 필드 초기화
                GetYield(txtLotID.Text);
                ClearInputFields();

                MessageBox.Show("저장되었습니다.", "성공", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError("저장 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 데이터 그리드뷰 더블클릭 이벤트 - 스크랩 코드 이력 삭제
        /// </summary>
        private void dgvList_DoubleClick(object sender, EventArgs e)
        {
            // 현재 행이 없는 경우 처리 중단
            if (dgvList.CurrentRow == null) return;

            try
            {
                // 선택된 행의 스크랩 코드 ID 추출 (null 안전하게 처리)
                var scrapcodeid = dgvList.CurrentRow.Cells[0].Value?.ToString();
                if (string.IsNullOrEmpty(scrapcodeid)) return;

                // 삭제 확인 대화상자 표시
                var result = MessageBox.Show("삭제 하시겠습니까?", "확인", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

                if (result == DialogResult.Yes)
                {
                    // 데이터베이스 연결 확인
                    if (_connection.State == ConnectionState.Closed)
                        _connection.Open();

                    var sql = $"DELETE FROM {ScrapHistoryTable} WHERE id = @id";
                    var parameters = new MySqlParameter[] {
                        new MySqlParameter("@id", scrapcodeid)
                    };

                    // 스크랩 코드 이력 삭제
                    MySqlHelper.ExecuteNonQuery(_connection, sql, parameters);
                    
                    // 데이터 갱신
                    GetYield();

                    MessageBox.Show("삭제되었습니다.", "성공", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ShowError("삭제 중 오류가 발생했습니다.", ex);
            }
        }
        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 입력값 유효성 검사
        /// </summary>
        /// <returns>유효성 통과 여부</returns>
        private bool ValidateInput()
        {
            // 필수 입력 필드 정의
            var requiredFields = new[]
            {
                new { Control = cbStep, Name = "공정" },
                new { Control = cbEQPid, Name = "설비" },
                new { Control = cbline, Name = "라인" },
                new { Control = txtFailQty, Name = "불량 수량" },
                new { Control = comboBox1, Name = "Section1" },
                new { Control = comboBox2, Name = "Section2" },
                new { Control = comboBox3, Name = "Section3" }
            };

            // 필수 필드 검증
            foreach (var field in requiredFields)
            {
                if (string.IsNullOrWhiteSpace(field.Control.Text))
                {
                    MessageBox.Show($"{field.Name}을(를) 선택해주세요.", "입력 오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    field.Control.Focus(); // 오류 필드로 포커스 이동
                    return false;
                }
            }

            // Lot ID 검증
            if (string.IsNullOrWhiteSpace(txtLotID.Text))
            {
                MessageBox.Show("Lot ID를 입력해주세요.", "입력 오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLotID.Focus();
                return false;
            }

            // 불량 수량 숫자 형식 검증
            if (!int.TryParse(txtFailQty.Text, out int failQty) || failQty <= 0)
            {
                MessageBox.Show("유효한 불량 수량을 입력해주세요.", "입력 오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtFailQty.Focus();
                txtFailQty.SelectAll(); // 전체 선택하여 수정 용이하게 함
                return false;
            }

            // 스크랩 코드 검증
            if (string.IsNullOrEmpty(_scripcode))
            {
                MessageBox.Show("스크랩 코드를 선택해주세요.", "입력 오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                comboBox3.Focus();
                return false;
            }

            return true; // 모든 검증 통과
        }

        /// <summary>
        /// 입력 필드 초기화
        /// </summary>
        private void ClearInputFields()
        {
            cbStep.SelectedIndex = -1;
            cbEQPid.SelectedIndex = -1;
            cbline.SelectedIndex = -1;
            txtFailQty.Text = string.Empty;
            comboBox1.SelectedIndex = -1;
            comboBox2.SelectedIndex = -1;
            comboBox3.SelectedIndex = -1;
            _scripcode = string.Empty;
        }

        /// <summary>
        /// 오류 메시지 표시
        /// </summary>
        /// <param name="message">사용자에게 표시할 메시지</param>
        /// <param name="ex">발생한 예외 객체</param>
        private void ShowError(string message, Exception ex)
        {
            MessageBox.Show($"{message}\n\n상세 정보: {ex.Message}", "오류", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        #endregion
    }
}
