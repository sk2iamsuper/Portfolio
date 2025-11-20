using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.Diagnostics;

/*
이 코드는 챔버 매거진 관리 시스템의 Windows Forms 애플리케이션으로, 다음과 같은 주요 기능을 제공합니다:

매거진 목록 관리: 데이터베이스에서 매거진 정보를 조회하고 상태에 따라 색상으로 표시
포트 상태 관리: 각 매거진의 슬롯별 사용 상태를 시각적으로 표시하고 수정
이력 추적: 매거진 변경 이력을 기록하고 조회
권한 관리: 특정 사용자만 슬롯 상태를 변경할 수 있도록 제한
다양한 통계: 포트 사용률, 상태별 매거진 목록 등을 제공
주요 특징:

MySQL 데이터베이스와 연동
더블 버퍼링을 통한 UI 깜빡임 방지
상태별 색상 구분 (Pink, LightGray, Yellow, Red, White)
사용자 권한에 따른 기능 제한
실시간 데이터 조회 및 업데이트
*/
namespace ITS
{
    public partial class frmMagazine : Form
    {
        private readonly MySqlConnection _connection; // MySQL 데이터베이스 연결 객체
        private string array_iarts = string.Empty; // 사용되지 않는 변수
        private string array_top = string.Empty; // 사용되지 않는 변수

        private int slotCount = 1; // 슬롯 카운트 변수

        public frmMagazine()
        {
            InitializeComponent();
            // MySQL 연결 초기화
            _connection = Helpers.MySqlHelper.GetConnection();
            // 더블 버퍼링 설정으로 깜빡임 방지
            DoubleBufferedHelper.SetDoubleBufferedParent(this);
        }

        private void frmM031_Load(object sender, EventArgs e)
        {
            // 매거진 목록 조회
            GetMagazineList();
            // 데이터 그리드뷰 바인딩 완료 이벤트 핸들러 등록
            dgvMagazines.DataBindingComplete += DgvMagazines_DataBindingComplete;
        }

        // 데이터 그리드뷰 바인딩 완료 시 호출되는 메서드
        private void DgvMagazines_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            // 각 행의 상태에 따라 배경색 설정
            for (int i = 0; i < dgvMagazines.RowCount - 1; i++)
            {
                // 'C' 상태인 행은 분홍색으로 표시
                if (dgvMagazines.Rows[i].Cells[5].Value.ToString() == "C")
                {
                    for (int col = 0; col < dgvMagazines.ColumnCount; col++)
                    {
                        dgvMagazines.Rows[i].Cells[col].Style.BackColor = Color.Pink;
                    }
                }

                // 'T' 상태인 행은 연한 회색으로 표시
                if (dgvMagazines.Rows[i].Cells[5].Value.ToString() == "T")
                {
                    for (int col = 0; col < dgvMagazines.ColumnCount; col++)
                    {
                        dgvMagazines.Rows[i].Cells[col].Style.BackColor = Color.LightGray;
                    }
                }
            }
        }

        // 탭 변경 시 호출되는 메서드
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(tabControl1.SelectedIndex == 0)
            {
                // 첫 번째 탭: 매거진 목록 조회
                GetMagazineList();
            }
            else if (tabControl1.SelectedIndex == 1)
            {
                // 두 번째 탭: 매거진 상세 정보 조회
                GetMagazines();
            }
        }

        // 매거진 목록을 조회하는 메서드
        private void GetMagazineList()
        {
            // 매거진 기본 정보 조회 SQL
            var sql = $"SELECT id, name, available, port_status, available_ports, flag, date_format(created_on, '%Y-%m-%d %H:%i:%s') as created_on FROM tb_chamber_magazines ORDER BY id";
            var dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            dgvMagazines.DataSource = dataTable;
            ITS.DataGridViewHelper.FitColumnSize(dgvMagazines);

            // 행 상태에 따른 색상 설정
            for (int i = 0; i < dgvMagazines.RowCount - 1; i++)
            {
                if (dgvMagazines.Rows[i].Cells[5].Value.ToString() == "C")
                {
                    for (int col = 0; col < dgvMagazines.ColumnCount; col++)
                    {
                        dgvMagazines.Rows[i].Cells[col].Style.BackColor = Color.Pink;
                    }
                }

                if (dgvMagazines.Rows[i].Cells[5].Value.ToString() == "T")
                {
                    for (int col = 0; col < dgvMagazines.ColumnCount; col++)
                    {
                        dgvMagazines.Rows[i].Cells[col].Style.BackColor = Color.LightGray;
                    }
                }
            }

            // 포트 사용 현황 조회 (dataGridView2)
            dataGridView2.Rows.Clear();
            sql = $@"SELECT available_ports, COUNT(*), ROUND(100 - (SUM(CHAR_LENGTH(SUBSTR(port_status, 1, available_ports)) - CHAR_LENGTH(REPLACE(SUBSTR(port_status, 1, available_ports), '1',''))) / SUM(available_ports) * 100), 2)
                    FROM tb_chamber_magazines WHERE flag = 'R'
                    GROUP BY available_ports";
            dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                dataGridView2.Rows.Add(row[0], row[1], row[2]);
                dataGridView2.ClearSelection();
            }

            // 'C' 상태인 매거진 목록 조회 (dataGridView3)
            dataGridView3.Rows.Clear();
            sql = $@"SELECT available_ports, name FROM tb_chamber_magazines WHERE flag = 'C' ORDER BY name";
            dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                dataGridView3.Rows.Add(row[0], row[1]);
                dataGridView3.ClearSelection();
            }
        }

        // 매거진 상세 정보를 조회하는 메서드
        private void GetMagazines()
        {
            dataGridView6.Rows.Clear();
            // 매거진 이름, 포트 상태, 사용 중인 포트 수 조회
            var sql = string.Format("SELECT NAME, PORT_STATUS, LENGTH(port_status)-LENGTH(REPLACE(port_status, '1' , '')) FROM tb_chamber_magazines ");
            var dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                dataGridView6.Rows.Add(dataGridView6.RowCount + 1, row[0], "'" + row[1], int.Parse(row[2].ToString()));
                dataGridView6.ClearSelection();

                // 2개 이상의 포트가 사용 중이면 노란색으로 강조 표시
                if (int.Parse(row[2].ToString()) >= 2)
                {
                    dataGridView6.Rows[dataGridView6.RowCount - 1].Cells[0].Style.BackColor = Color.Yellow;
                    dataGridView6.Rows[dataGridView6.RowCount - 1].Cells[1].Style.BackColor = Color.Yellow;
                    dataGridView6.Rows[dataGridView6.RowCount - 1].Cells[2].Style.BackColor = Color.Yellow;
                    dataGridView6.Rows[dataGridView6.RowCount - 1].Cells[3].Style.BackColor = Color.Yellow;
                }
            }
        }

        // 매거진 이름 입력 시 호출되는 메서드
        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            // Enter 키 입력 시 처리
            if (e.KeyData == Keys.Enter && textBox1.Text != string.Empty)
            {
                textBox1.Text = textBox1.Text.ToUpper(); // 대문자 변환

                txtMagazineId1.Text = txtMagazineName.Text = textBox1.Text; // 입력값을 관련 텍스트박스에 설정

                textBox1.Text = string.Empty; // 입력창 초기화

                // 선택된 매거진의 포트 상태 조회
                var portstatus = ITS.Helpers.MySqlHelper.GetOneData(_connection, $"SELECT port_status FROM tb_chamber_magazines WHERE  name = '{txtMagazineName.Text}' ");
                
                // 각 슬롯의 상태에 따라 배경색 설정 (0: 흰색, 1: 빨간색)
                txtT4Slot1.BackColor = (portstatus.Substring(0, 1) == "0") ? Color.White : Color.Red;
                txtT4Slot2.BackColor = (portstatus.Substring(1, 1) == "0") ? Color.White : Color.Red;
                txtT4Slot3.BackColor = (portstatus.Substring(2, 1) == "0") ? Color.White : Color.Red;
                txtT4Slot4.BackColor = (portstatus.Substring(3, 1) == "0") ? Color.White : Color.Red;
                txtT4Slot5.BackColor = (portstatus.Substring(4, 1) == "0") ? Color.White : Color.Red;
                txtT4Slot6.BackColor = (portstatus.Substring(5, 1) == "0") ? Color.White : Color.Red;
                txtT4Slot7.BackColor = (portstatus.Substring(6, 1) == "0") ? Color.White : Color.Red;
                txtT4Slot8.BackColor = (portstatus.Substring(7, 1) == "0") ? Color.White : Color.Red;
                txtT4Slot9.BackColor = (portstatus.Substring(8, 1) == "0") ? Color.White : Color.Red;
                txtT4SlotA.BackColor = (portstatus.Substring(9, 1) == "0") ? Color.White : Color.Red;
                txtT4SlotB.BackColor = (portstatus.Substring(10, 1) == "0") ? Color.White : Color.Red;
                txtT4SlotC.BackColor = (portstatus.Substring(11, 1) == "0") ? Color.White : Color.Red;

                // 매거진 이력 조회
                GetMagazinesHistory();
            }
        }

        // 매거진 이력을 조회하는 메서드
        private void GetMagazinesHistory()
        {
            dataGridView7.Rows.Clear();
            var sql =
                $"SELECT '{txtMagazineName.Text}', date_format(h.created_on, '%Y-%m-%d %H:%i:%s'), h.description, u.user_name FROM tb_chamber_magazine_histories h, tb_user u " +
                $"WHERE h.user_id = u.id AND h.magazine_id = (SELECT id FROM new_mes.tb_chamber_magazines WHERE name = '{txtMagazineName.Text}') AND has_error = 'False' ";
            var dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                dataGridView7.Rows.Add(row[0], row[1], row[2], row[3]);
                dataGridView7.ClearSelection();
            }
        }

        // 포트 상태 업데이트 버튼 클릭 시 호출되는 메서드
        private void button3_Click(object sender, EventArgs e)
        {
            if (txtMagazineName.Text == string.Empty)
                return;

            if (txtComment.Text == string.Empty)
            {
                MessageBox.Show("변경내용을 등록하세요. ");
                return;
            }

            if (_connection.State == ConnectionState.Closed)
                _connection.Open();

            // 각 슬롯의 상태를 문자열로 변환 (Red: 1, White: 0)
            string port_status = (txtT4Slot1.BackColor == Color.Red) ? "1" : "0";
            port_status = port_status + ((txtT4Slot2.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4Slot3.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4Slot4.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4Slot5.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4Slot6.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4Slot7.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4Slot8.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4Slot9.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4SlotA.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4SlotB.BackColor == Color.Red) ? "1" : "0");
            port_status = port_status + ((txtT4SlotC.BackColor == Color.Red) ? "1" : "0");

            // 포트 상태 업데이트
            var sql = $"UPDATE tb_chamber_magazines SET port_status = '{port_status}' WHERE name = '{txtMagazineName.Text}' ";
            MySql.Data.MySqlClient.MySqlHelper.ExecuteNonQuery(_connection, sql);

            // 변경 이력 저장
            sql = $"INSERT INTO tb_chamber_magazine_histories (magazine_id, user_id, has_error, description) " +
                $"SELECT id, {frmMain.userID}, 'False', '{txtComment.Text}' FROM new_mes.tb_chamber_magazines WHERE name = '{txtMagazineName.Text}' ";
            MySql.Data.MySqlClient.MySqlHelper.ExecuteNonQuery(_connection, sql);

            // UI 초기화
            txtMagazineName.Text = string.Empty;
            txtT4Slot1.BackColor = Color.White;
            txtT4Slot2.BackColor = Color.White;
            txtT4Slot3.BackColor = Color.White;
            txtT4Slot4.BackColor = Color.White;
            txtT4Slot5.BackColor = Color.White;
            txtT4Slot6.BackColor = Color.White;
            txtT4Slot7.BackColor = Color.White;
            txtT4Slot8.BackColor = Color.White;
            txtT4Slot9.BackColor = Color.White;
            txtT4SlotA.BackColor = Color.White;
            txtT4SlotB.BackColor = Color.White;
            txtT4SlotC.BackColor = Color.White;

            // 매거진 목록 새로고침
            GetMagazines();
        }

        // 슬롯 더블클릭 시 호출되는 메서드
        private void txtT4Slot1_DoubleClick(object sender, EventArgs e)
        {
            // 권한 체크 - 특정 사용자만 슬롯 상태 변경 가능
            if (frmMain.user_ID.ToUpper() != "admin" )
            {
                MessageBox.Show("You do not have permission to do this.");
                return;
            }

            // 슬롯 상태 토글 (Red <-> White)
            Label slot = sender as Label;
            slot.BackColor = (slot.BackColor == Color.Red) ? Color.White : Color.Red;
        }

        // 특정 슬롯의 테스트 결과 조회 버튼 클릭 시 호출되는 메서드
        private void button5_Click(object sender, EventArgs e)
        {
            var sql =
                $@"SELECT m.name, s.slot_index, s.port_index, t.reason, date_format(t.created_on, '%Y-%m-%d %H:%i:%s') as datetime, t.mv 
            FROM tb_chamber_magazine_slot s, tb_chamber_test_result t, tb_chamber_magazines m
            WHERE t.slot_id = s.id AND s.magazine_id = m.id AND m.name = '{txtMagazineId1.Text}' AND s.slot_index = {txtSlotId.Text} AND port_index = {txtPortid.Text}
            ORDER BY s.id DESC LIMIT 10";
            var dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            dataGridView5.DataSource = dataTable;
            ITS.DataGridViewHelper.FitColumnSize(dataGridView3);

            dataGridView5.ClearSelection();
        }
    }
}
