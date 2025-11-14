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
/*
이 프로그램은 MES(Manufacturing Execution System)에서 불량품 관리를 담당하는 모듈로, 제조 공정에서 발생하는 불량품을 체계적으로 분류, 등록, 추적하는 시스템입니다.
1. 스크랩 코드 등록 및 관리
계층형 불량 분류 체계
2. Lot 기반 불량 이력 관리
Lot ID별 불량 현황 추적,바코드 스캔 지원, 자동 데이터 연동
3. 다양한 조회 기능
  Tab 1: Lot별 조회
  Tab 2: 전체 조회
  기간별 종합 분석
  모든 Lot의 불량 현황 통합 조회
  삭제 기능: 잘못 등록된 데이터 더블클릭으로 삭제
4. 공정 및 설비 정보 관리
공정(Step) 정보: 생산 공정별 분류
설비(EQP) 정보: 불량 발생 장비 추적
라인(Line) 정보: 생산 라인별 집계
*/
namespace mes_
{
    public partial class frmScrapCode : Form
    {
        // 데이터베이스 연결 객체 (읽기 전용으로 선언하여 안전성 확보)
        // readonly 키워드를 사용하여 생성자 외부에서 수정 불가능하게 함
        private readonly MySqlConnection _connection;
        
        // 폼 내에서 사용할 주요 변수들
        private string _lotid;      // 현재 작업 중인 Lot ID 저장
        private string _scripcode;  // 선택된 스크랩 코드 저장
        
        // 상수 정의 - 하드코딩 방지 및 유지보수성 향상
        // 데이터베이스 테이블명과 날짜 형식을 상수로 관리하여 일관성 유지
        private const string DateFormat = "yyyy-MM-dd";                    // MySQL DATE 형식
        private const string ScrapCodeTable = "tb_scrapcode";             // 스크랩 코드 마스터 테이블
        private const string ScrapHistoryTable = "tb_scrapcode_history";  // 스크랩 이력 테이블
        private const string MesProcessTable = "tb_mes_process";          // 공정 정보 테이블
        private const string MesLotidTable = "tb_mes_lotid";              // Lot 정보 테이블

        /// <summary>
        /// 생성자 - Lot ID와 사용자 ID를 받는 경우
        /// 주로 특정 Lot에 대한 스크랩 코드 등록 시 사용
        /// </summary>
        /// <param name="connection">데이터베이스 연결 객체</param>
        /// <param name="lotid">작업 대상 Lot ID</param>
        /// <param name="userid">현재 작업 수행 사용자 ID</param>
        public frmScrapCode(MySqlConnection connection, string lotid, string userid)
        {
            InitializeComponent();  // Windows Forms 디자이너에서 생성된 컴포넌트 초기화
            _connection = connection;  // 의존성 주입 방식으로 DB 연결 객체 전달
            txtLotID.Text = _lotid = lotid;  // UI와 내부 변수에 Lot ID 설정
            txtUserName.Text = userid;       // 사용자명 표시 (읽기 전용으로 설정됨)
        }

        /// <summary>
        /// 생성자 - 데이터베이스 연결만 받는 경우
        /// 주로 스크랩 코드 조회만 필요한 경우 사용
        /// </summary>
        /// <param name="connection">데이터베이스 연결 객체</param>
        public frmScrapCode(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;
            // 메인 폼에서 전역으로 관리하는 사용자명을 가져옴
            txtUserName.Text = frmMain.userName;
        }

        /// <summary>
        /// 폼 로드 이벤트 - 초기 데이터 조회 및 화면 설정
        /// 폼이 사용자에게 보여지기 전에 필요한 데이터를 미리 로드
        /// </summary>
        private void frmScrapCode_Load(object sender, EventArgs e)
        {
            try
            {
                // 콤보박스에 공정 정보와 Section1 데이터 로드
                GetStep();      // 공정(Step) 목록 조회
                GetSection1();  // 스크랩 코드 대분류 조회

                // Lot ID가 이미 지정된 경우 해당 Lot의 수율 정보 조회
                if (!string.IsNullOrEmpty(txtLotID.Text))
                    GetYield(txtLotID.Text);  // Lot별 스크랩 이력 표시
            }
            catch (Exception ex)
            {
                // 초기화 중 발생한 오류를 사용자에게 친숙한 메시지로 표시
                ShowError("폼 로드 중 오류가 발생했습니다.", ex);
            }
        }

        #region 데이터 조회 메서드
        // 관련된 데이터 조회 메서드들을 region으로 그룹화하여 가독성 향상

        /// <summary>
        /// 공정(Step) 정보를 콤보박스에 로드
        /// tb_mes_process 테이블에서 공정 목록을 가져와 정렬하여 표시
        /// </summary>
        private void GetStep()
        {
            try
            {
                cbStep.Items.Clear();  // 기존 항목 제거
                var sql = $"SELECT step FROM {MesProcessTable} ORDER BY id";  // ID 기준 정렬
                
                // using 문을 사용하여 리소스 자동 해제 (메모리 누수 방지)
                // MySqlHelper.ExecuteDataset은 DataSet을 반환하며, 사용 후 자동으로 Dispose
                using (var dataset = MySqlHelper.ExecuteDataset(_connection, sql))
                using (var dataTable = dataset.Tables[0])  // 첫 번째 테이블 사용
                {
                    // DataTable의 각 행을 순회하며 콤보박스에 항목 추가
                    foreach (DataRow row in dataTable.Rows)
                    {
                        cbStep.Items.Add(row[0].ToString());  // step 컬럼 값 추가
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
        /// 스크랩 코드의 대분류(예: 외관불량, 기능불량 등)를 조회
        /// </summary>
        private void GetSection1()
        {
            try
            {
                comboBox1.Items.Clear();
                // DISTINCT로 중복 제거 후 정렬
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
        /// Section1 선택 시 연관된 Section2(중분류)를 동적으로 로드
        /// </summary>
        private void GetSection2()
        {
            try
            {
                comboBox2.Items.Clear();
                // 매개변수화된 쿼리 사용으로 SQL Injection 방지
                var sql = $"SELECT DISTINCT section2 FROM {ScrapCodeTable} WHERE section1 = @section1 ORDER BY section2";
                
                var parameters = new MySqlParameter[] {
                    new MySqlParameter("@section1", comboBox1.Text)  // 사용자 선택값을 파라미터로 전달
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
        /// 실제 불량 현상에 대한 상세 설명을 한국어로 표시
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
                        comboBox3.Items.Add(row[0].ToString());  // 한국어 설명 표시
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
        /// 사용자가 지정한 기간 동안의 모든 스크랩 이력을 종합적으로 표시
        /// </summary>
        private void GetYield()
        {
            try
            {
                dgvList.Rows.Clear();  // 데이터그리드뷰 초기화
                
                // 복잡한 조인 쿼리로 여러 테이블에서 관련 정보를 한번에 조회
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
                    // 조회된 각 행을 데이터그리드뷰에 추가
                    foreach (DataRow row in dataTable.Rows)
                    {
                        dgvList.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], row[6], 
                                       row[7], row[8], row[9], row[10], row[11], row[12]);
                    }
                    dgvList.ClearSelection(); // 자동 선택 해제로 사용자 혼동 방지
                }
            }
            catch (Exception ex)
            {
                ShowError("수율 정보 조회 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 특정 Lot ID의 스크랩 코드 이력 조회 (Lot별 조회 탭용)
        /// 하나의 Lot에 대한 상세한 스크랩 이력을 집중적으로 표시
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
                        dataGridView1.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], 
                                            row[6], row[7], row[8], row[9], row[10], row[11]);
                    }
                    dataGridView1.ClearSelection();
                }
            }
            catch (Exception ex)
            {
                ShowError("Lot별 수율 정보 조회 중 오류가 발생했습니다.", ex);
            }
        }
        #endregion

        #region 이벤트 핸들러
        // 사용자 인터페이스 이벤트 처리 메서드들을 region으로 그룹화

        /// <summary>
        /// 스캔 데이터 입력 처리 (Enter 키 이벤트)
        /// 바코드 스캐너 등에서 Lot ID 입력 시 자동 처리
        /// </summary>
        private void txtScandata_KeyUp(object sender, KeyEventArgs e)
        {
            // Enter 키가 눌리고 스캔 데이터가 비어있지 않은 경우 처리
            if (e.KeyData == Keys.Enter && !string.IsNullOrWhiteSpace(txtScandata.Text))
            {
                try
                {
                    // 입력 데이터 표준화 (대문자 변환)
                    txtScandata.Text = txtScandata.Text.ToUpper();

                    // 입력된 Lot ID로 데이터베이스 조회
                    var sql = $"SELECT lotid, start_lot_qty FROM {MesLotidTable} WHERE lotid = @lotid";
                    var parameters = new MySqlParameter[] {
                        new MySqlParameter("@lotid", txtScandata.Text)
                    };

                    using (var dataset = MySqlHelper.ExecuteDataset(_connection, sql, parameters))
                    using (var dataTable = dataset.Tables[0])
                    {
                        if (dataTable.Rows.Count > 0)
                        {
                            // Lot 정보가 존재하는 경우 UI에 표시
                            DataRow row = dataTable.Rows[0];
                            txtLotID.Text = row[0].ToString();      // Lot ID 표시
                            txtLotQty.Text = row[1].ToString();     // 시작 수량 표시
                        }
                        else
                        {
                            MessageBox.Show("해당 Lot ID를 찾을 수 없습니다.", "알림", 
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }

                    txtScandata.Text = string.Empty; // 입력 필드 초기화 (다음 입력 준비)
                }
                catch (Exception ex)
                {
                    ShowError("Lot 정보 조회 중 오류가 발생했습니다.", ex);
                }
            }
        }

        /// <summary>
        /// Section1 선택 변경 이벤트 - Section2 데이터 로드
        /// 계층형 코드 선택을 위한 연쇄적 데이터 로딩
        /// </summary>
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(comboBox1.Text))
                GetSection2();  // Section1 선택 시 해당하는 Section2 로드
        }

        /// <summary>
        /// Section2 선택 변경 이벤트 - Section3 데이터 로드
        /// </summary>
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(comboBox2.Text))
                GetSection3();  // Section2 선택 시 해당하는 Section3 로드
        }

        /// <summary>
        /// Section3 선택 변경 이벤트 - 스크랩 코드 값 설정
        /// 사용자가 선택한 상세 설명에 해당하는 실제 스크랩 코드값을 조회
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

                // 선택된 설명에 해당하는 스크랩 코드 조회하여 내부 변수에 저장
                _scripcode = Helpers.MySqlHelper.GetOneData(_connection, sql, parameters);
            }
            catch (Exception ex)
            {
                ShowError("스크랩 코드 조회 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 탭 변경 이벤트 - 선택된 탭에 따른 데이터 조회
        /// 사용자가 다른 탭을 선택할 때 해당 탭에 맞는 데이터를 동적으로 로드
        /// </summary>
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                switch (tabControl1.SelectedIndex)
                {
                    case 0: // Lot별 조회 탭 - 현재 Lot의 이력 표시
                        GetYield(txtLotID.Text);
                        break;
                    case 1: // 전체 조회 탭 - 기간별 전체 이력 표시
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
        /// 사용자가 선택한 기간에 따른 스크랩 이력 조회
        /// </summary>
        private void btnSearch_Click(object sender, EventArgs e)
        {
            GetYield();  // 전체 조회 수행
        }

        /// <summary>
        /// 저장 버튼 클릭 이벤트 - 스크랩 코드 이력 저장
        /// 사용자가 입력한 스크랩 정보를 데이터베이스에 저장
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            // 입력값 유효성 검사 실패 시 저장 중단
            if (!ValidateInput())
                return;

            try
            {
                // 데이터베이스 연결 상태 확인 및 연결 확보
                if (_connection.State == ConnectionState.Closed)
                    _connection.Open();

                // 서브쿼리를 사용한 INSERT 문 - 여러 테이블의 ID를 조회하여 저장
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

                // 스크랩 코드 이력 저장 실행
                MySqlHelper.ExecuteNonQuery(_connection, sql, parameters);

                // 저장 후 데이터 갱신 및 입력 필드 초기화
                GetYield(txtLotID.Text);  // 변경된 데이터 반영
                ClearInputFields();       // 입력 폼 초기화

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
        /// 전체 조회 탭에서 잘못 입력된 데이터를 삭제하기 위한 기능
        /// </summary>
        private void dgvList_DoubleClick(object sender, EventArgs e)
        {
            // 현재 선택된 행이 없는 경우 처리 중단
            if (dgvList.CurrentRow == null) return;

            try
            {
                // 선택된 행의 스크랩 코드 ID 추출 (null 안전하게 처리)
                var scrapcodeid = dgvList.CurrentRow.Cells[0].Value?.ToString();
                if (string.IsNullOrEmpty(scrapcodeid)) return;

                // 삭제 확인 대화상자 표시 (실수 방지)
                var result = MessageBox.Show("삭제 하시겠습니까?", "확인", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);

                if (result == DialogResult.Yes)
                {
                    // 데이터베이스 연결 상태 확인
                    if (_connection.State == ConnectionState.Closed)
                        _connection.Open();

                    var sql = $"DELETE FROM {ScrapHistoryTable} WHERE id = @id";
                    var parameters = new MySqlParameter[] {
                        new MySqlParameter("@id", scrapcodeid)
                    };

                    // 스크랩 코드 이력 삭제 실행
                    MySqlHelper.ExecuteNonQuery(_connection, sql, parameters);
                    
                    // 데이터 갱신으로 UI 동기화
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
        // 재사용 가능한 공통 기능 메서드들을 region으로 그룹화

        /// <summary>
        /// 입력값 유효성 검사
        /// 저장 전 모든 필수 입력값이 올바르게 입력되었는지 확인
        /// </summary>
        /// <returns>유효성 통과 여부 (true: 통과, false: 실패)</returns>
        private bool ValidateInput()
        {
            // 필수 입력 필드 정의 (컨트롤과 사용자에게 표시할 이름)
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

            // 각 필수 필드에 대해 빈값 검증
            foreach (var field in requiredFields)
            {
                if (string.IsNullOrWhiteSpace(field.Control.Text))
                {
                    MessageBox.Show($"{field.Name}을(를) 선택해주세요.", "입력 오류", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    field.Control.Focus(); // 오류 발생 필드로 포커스 이동
                    return false;
                }
            }

            // Lot ID 필수 검증
            if (string.IsNullOrWhiteSpace(txtLotID.Text))
            {
                MessageBox.Show("Lot ID를 입력해주세요.", "입력 오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLotID.Focus();
                return false;
            }

            // 불량 수량 숫자 형식 및 유효범위 검증
            if (!int.TryParse(txtFailQty.Text, out int failQty) || failQty <= 0)
            {
                MessageBox.Show("유효한 불량 수량을 입력해주세요.", "입력 오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtFailQty.Focus();
                txtFailQty.SelectAll(); // 전체 선택하여 수정 용이하게 함
                return false;
            }

            // 스크랩 코드 검증 (내부 변수에 값이 설정되었는지 확인)
            if (string.IsNullOrEmpty(_scripcode))
            {
                MessageBox.Show("스크랩 코드를 선택해주세요.", "입력 오류", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                comboBox3.Focus();
                return false;
            }

            return true; // 모든 검증 조건 통과
        }

        /// <summary>
        /// 입력 필드 초기화
        /// 저장 성공 후 또는 특정 조건에서 입력 폼을 깨끗이 정리
        /// </summary>
        private void ClearInputFields()
        {
            cbStep.SelectedIndex = -1;        // 공정 선택 해제
            cbEQPid.SelectedIndex = -1;       // 설비 선택 해제
            cbline.SelectedIndex = -1;        // 라인 선택 해제
            txtFailQty.Text = string.Empty;   // 불량 수량 초기화
            comboBox1.SelectedIndex = -1;     // Section1 선택 해제
            comboBox2.SelectedIndex = -1;     // Section2 선택 해제
            comboBox3.SelectedIndex = -1;     // Section3 선택 해제
            _scripcode = string.Empty;        // 스크랩 코드 초기화
        }

        /// <summary>
        /// 오류 메시지 표시
        /// 일관된 형식으로 오류 메시지를 사용자에게 표시
        /// </summary>
        /// <param name="message">사용자에게 표시할 메시지</param>
        /// <param name="ex">발생한 예외 객체</param>
        private void ShowError(string message, Exception ex)
        {
            // 사용자 친화적 메시지와 기술적 상세 정보를 함께 표시
            MessageBox.Show($"{message}\n\n상세 정보: {ex.Message}", "오류", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        #endregion
    }
}
