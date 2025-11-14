using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ITS
{
    public partial class frmBoxLabelCheck : Form
    {
        private readonly MySqlConnection _connection;
        
        // 생성자: MySQL 연결을 받아 폼을 초기화
        public frmBoxLabelCheck(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;
        }

        // 폼 로드 시 실행되는 이벤트 핸들러
        private void frmBoxLabelCheck_Load(object sender, EventArgs e)
        {
            // BOX_LABEL_로 시작하는 모든 프로그램 데이터 조회
            var sql = $"SELECT * FROM tb_ssd_std_program WHERE name like 'BOX_LABEL_%'";
            var dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];

            // 각 행을 순회하며 이미지 데이터를 적절한 PictureBox에 할당
            foreach (DataRow row in dataTable.Rows)
            {
                var byteArray = (byte[])row["program"];

                using (var ms = new MemoryStream(byteArray))
                {
                    // 이름에 따라 다른 PictureBox에 이미지 설정
                    if (row["name"].ToString().Contains("BRAND_S"))
                        pictureBox1.Image = System.Drawing.Image.FromStream(ms);
                    else if (row["name"].ToString().Contains("BRAND_L"))
                        pictureBox2.Image = System.Drawing.Image.FromStream(ms);
                    else if (row["name"].ToString().Contains("OEM_S"))
                        pictureBox7.Image = System.Drawing.Image.FromStream(ms);
                    else if (row["name"].ToString().Contains("HP_S"))
                        pictureBox4.Image = System.Drawing.Image.FromStream(ms);
                    else if (row["name"].ToString().Contains("OEM_L"))
                        pictureBox3.Image = System.Drawing.Image.FromStream(ms);
                    else if (row["name"].ToString().Contains("PSSD_L"))
                        pictureBox5.Image = System.Drawing.Image.FromStream(ms);
                    else if (row["name"].ToString().Contains("PSSD_S"))
                        pictureBox6.Image = System.Drawing.Image.FromStream(ms);
                }
            }
        }

        // 스캔 데이터 입력 시 실행되는 키업 이벤트 핸들러
        private void txtScanData_KeyUp(object sender, KeyEventArgs e)
        {
            // 엔터키가 눌리고 텍스트가 비어있지 않을 때 처리
            if (e.KeyData == Keys.Enter && txtScanData.Text != string.Empty)
            {
                var sql = string.Empty;
                DataTable dataTable;
                
                // 21자리 스캔 데이터 처리 (소박스)
                if (txtScanData.Text.Length == 21 && dataGridView2.RowCount == 0)
                {
                    // L03LOG030900120230901 형식의 데이터 파싱
                    var smallboxid = txtScanData.Text.Substring(0, 10) + txtScanData.Text.Substring(19, 2);
                    var scnt = txtScanData.Text.Substring(19, 2);

                    // 데이터그리드 초기화
                    dataGridView1.Rows.Clear();
                    dataGridView2.Rows.Clear();

                    var isPssd = false;
                    
                    // 모델명 조회하여 PSSD 여부 확인
                    var modelname = Helpers.MySqlHelper.GetOneData(_connection,
                            $@"SELECT model_name FROM tb_mes_std_espec WHERE id = (SELECT espec_id FROM tb_mes_lotid WHERE lotid = '{smallboxid.Substring(0, 10)}') ");

                    // PSSD 모델인 경우 다른 SQL 쿼리 사용
                    if (modelname.Contains("PSSD"))
                    {
                        sql =
                        $@"SELECT SUBSTR(a.ssdsn, 1, 14), IF (e.upc IS NULL, '000000000000', e.upc), IF (e.ean IS NULL, '000000000000', e.ean), CONCAT(SUBSTR(e.sale_code, 1, 16), '    '), 
                        CONCAT(l.lotid, (SELECT LPAD(COUNT(*), 5, 0) FROM tb_mes_dat_setinfo WHERE small_box_id = (SELECT id FROM tb_mes_dat_boxinfo WHERE small_boxid = '{smallboxid}')), l.week, b.scnt)
                        , e.oem_config, l.lotid, e.customer, e.sale_code, CONCAT(e.hppn, '          ')  
                        FROM tb_mes_std_espec e, tb_mes_lotid l, tb_mes_dat_setinfo s, tb_mes_dat_boxinfo b, tb_mes_dat_label a
                        WHERE e.id = l.espec_id AND l.id = s.lot_id AND s.small_box_id = b.id AND s.sn_id = a.id AND b.small_boxid = '{smallboxid}'
                        ORDER BY a.ssdsn ";

                        isPssd = true;
                    }
                    else
                    {
                        sql =
                        $@"SELECT SUBSTR(a.ssdsn, 1, 14), IF (e.upc IS NULL, '000000000000', e.upc), IF (e.ean IS NULL, '000000000000', e.ean), CONCAT(SUBSTR(e.sale_code, 1, 16), '    '), 
                        CONCAT(l.lotid, (SELECT LPAD(COUNT(*), 5, 0) FROM tb_mes_dat_setinfo WHERE small_box_id = (SELECT id FROM tb_mes_dat_boxinfo WHERE small_boxid = '{smallboxid}')), l.week, b.scnt)
                        , e.oem_config, l.lotid, e.customer, e.sale_code, CONCAT(e.hppn, '          ')  
                        FROM tb_mes_std_espec e, tb_mes_lotid l, tb_mes_dat_setinfo s, tb_mes_dat_boxinfo b, tb_mes_dat_label a
                        WHERE e.id = l.espec_id AND l.id = s.lot_id AND s.small_box_id = b.id AND s.sn_id = a.id AND b.small_boxid = '{smallboxid}'
                        ORDER BY a.ssdsn ";
                    }

                    dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
                    bool isBrand = false;
                    var sn = "";
                    
                    // 조회된 데이터 행 처리
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var oemConfig = row[5].ToString();

                        // PSSD 제품 처리
                        if (isPssd)
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 6; // PSSD 소박스 탭

                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], "");
                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            txtLotid.Text = row[6].ToString();
                            string hexValue =
                                row[4].ToString().Substring(0, row[4].ToString().Length - 2)
                                + smallboxid.Substring(smallboxid.Length - 2, 2);
                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], ""); // UPC
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], ""); // EAN
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], ""); // Sale Code
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, hexValue, ""); // HEX 값
                            }
                        }
                        else if (oemConfig == "") // BRAND 제품
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 0; // BRAND 소박스 탭

                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], "");
                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            txtLotid.Text = row[6].ToString();
                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], ""); // UPC
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], ""); // EAN
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], ""); // Sale Code
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[4], ""); // MLOT
                            }
                        }
                        else if (row[3].ToString().Contains("-00W")) // DC 제품
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 0; // BRAND 소박스 탭
                            txtLotid.Text = row[6].ToString();

                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            // 60개 SN을 모아서 한 행에 표시
                            if (sn.Split(' ').Length == 60)
                            {
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");
                                sn = "";
                            }

                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], ""); // UPC
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], ""); // EAN
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], ""); // Sale Code
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[4], ""); // MLOT
                            }
                        }
                        else // OEM 제품
                        {
                            txtLotid.Text = row[6].ToString();
                            if (row[7].ToString() == "HP") // HP 제품
                            {
                                tabControl1.SelectedIndex = 3; // HP 소박스 탭

                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[8], ""); // _FULLSALECODE
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[9], ""); // _HPPN
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[4], ""); // _MLOT
                            }
                            else // 일반 OEM 제품
                            {
                                tabControl1.SelectedIndex = 2; // OEM 소박스 탭

                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], ""); // Sale Code
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[4], ""); // MLOT
                            }

                            break;
                        }
                    }

                    // BRAND/PSSD 제품의 남은 SN 처리
                    if (isBrand)
                    {
                        if (sn != "")
                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, " " + sn, "");
                    }
                }
                // 12자리 스캔 데이터 처리 (대박스)
                else if (txtScanData.Text.Length == 12 && dataGridView1.RowCount == 0)
                {
                    // 대박스 ID 처리
                    var largeboxid = txtScanData.Text.Trim();

                    dataGridView1.Rows.Clear();
                    dataGridView2.Rows.Clear();
                    var isPssd = false;

                    // 모델명 조회하여 PSSD 여부 확인
                    var modelname = Helpers.MySqlHelper.GetOneData(_connection,
                            $@"SELECT DISTINCT model_name FROM tb_mes_std_espec WHERE id in (
                            SELECT espec_id FROM tb_mes_lotid WHERE id in (
                            SELECT lot_id FROM tb_mes_dat_setinfo WHERE small_box_id in (
                            SELECT id FROM tb_mes_dat_boxinfo WHERE large_boxid = '{largeboxid}'))) ");

                    // PSSD 모델인 경우 다른 SQL 쿼리 사용
                    if (modelname.Contains("PSSD"))
                    {
                        sql =
                        $@"SELECT SUBSTR(a.ssdsn, 1, 14), concat(b.large_boxid, '  '), IF (e.upc IS NULL, '000000000000', e.upc), IF (e.ean IS NULL, '000000000000', e.ean)
                        , e.large_box_label_dat, l.lotid
                        FROM tb_mes_std_espec e, tb_mes_lotid l, tb_mes_dat_setinfo s, tb_mes_dat_boxinfo b, tb_mes_dat_label a
                        WHERE e.id = l.espec_id AND l.id = s.lot_id AND s.small_box_id = b.id AND s.sn_id = a.id AND b.large_boxid = '{largeboxid}'
                        ORDER BY b.small_boxid, a.ssdsn ";

                        isPssd = true;
                    }
                    else
                    {
                        sql =
                        $@"SELECT SUBSTR(a.ssdsn, 1, 14), concat(b.large_boxid, '  '), IF (e.upc IS NULL, '000000000000', e.upc), IF (e.ean IS NULL, '000000000000', e.ean)
                        , e.oem_config, l.lotid, e.prod_code
                        FROM tb_mes_std_espec e, tb_mes_lotid l, tb_mes_dat_setinfo s, tb_mes_dat_boxinfo b, tb_mes_dat_label a
                        WHERE e.id = l.espec_id AND l.id = s.lot_id AND s.small_box_id = b.id AND s.sn_id = a.id AND b.large_boxid = '{largeboxid}'
                        ORDER BY b.small_boxid, a.ssdsn ";
                    }

                    dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
                    var sn = "";
                    bool isBrand = false;
                    
                    // 조회된 데이터 행 처리
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var oemConfig = row[4].ToString();

                        // PSSD 제품 처리
                        if (isPssd)
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 5; // PSSD 대박스 탭

                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], "");
                            txtLotid.Text = row[5].ToString();

                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            // 15개 행마다 SN을 모아서 표시
                            if (dataGridView1.RowCount == 15)
                            {
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");
                                sn = "";
                            }

                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], ""); // Large Box ID
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], ""); // UPC
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], ""); // EAN
                            }
                        }
                        else if (oemConfig == "") // BRAND 제품
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 1; // BRAND 대박스 탭

                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], "");
                            txtLotid.Text = row[5].ToString();

                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            // 30개 행마다 SN을 모아서 표시
                            if (dataGridView1.RowCount == 30)
                            {
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");
                                sn = "";
                            }

                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], ""); // Large Box ID
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], ""); // UPC
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], ""); // EAN
                            }
                        }
                        else if (row[6].ToString().Contains("-00W")) // DC 제품
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 1; // BRAND 대박스 탭
                            txtLotid.Text = row[5].ToString();

                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            // 60개 SN을 모아서 한 행에 표시
                            if (sn.Split(' ').Length == 60)
                            {
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");
                                sn = "";
                            }

                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], ""); // Large Box ID
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], ""); // UPC
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], ""); // EAN
                            }
                        }
                        else // OEM 제품
                        {
                            tabControl1.SelectedIndex = 4; // OEM 대박스 탭

                            txtLotid.Text = row[5].ToString();
                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, largeboxid + "  ", ""); // Large Box ID

                            break;
                        }
                    }

                    // BRAND/PSSD 제품의 남은 SN 처리
                    if (isBrand)
                    {
                        if (sn != "")
                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");
                    }
                }
                // 개별 스캔 데이터 처리 (SN 또는 라벨 데이터)
                else
                {
                    var isExist = false;
                    
                    // dataGridView1에서 스캔 데이터 검색 및 매칭
                    for (int i = 0; i < dataGridView1.RowCount; i++)
                    {
                        if (dataGridView1.Rows[i].Cells[1].Value.ToString() == txtScanData.Text)
                        {
                            dataGridView1.Rows[i].Cells[2].Value = txtScanData.Text;
                            dataGridView1.Rows[i].Cells[0].Style.BackColor = Color.LawnGreen;
                            dataGridView1.Rows[i].Cells[1].Style.BackColor = Color.LawnGreen;
                            dataGridView1.Rows[i].Cells[2].Style.BackColor = Color.LawnGreen;
                            dataGridView1.CurrentCell = dataGridView1.Rows[i].Cells[2];
                            isExist = true;
                            break;
                        }
                    }

                    // dataGridView2에서 스캔 데이터 검색 및 매칭
                    if (!isExist)
                    {
                        for (int i = 0; i < dataGridView2.RowCount; i++)
                        {
                            if (dataGridView2.Rows[i].Cells[1].Value.ToString() == txtScanData.Text)
                            {
                                dataGridView2.Rows[i].Cells[2].Value = txtScanData.Text;
                                dataGridView2.Rows[i].Cells[0].Style.BackColor = Color.LawnGreen;
                                dataGridView2.Rows[i].Cells[1].Style.BackColor = Color.LawnGreen;
                                dataGridView2.Rows[i].Cells[2].Style.BackColor = Color.LawnGreen;
                                dataGridView2.CurrentCell = dataGridView2.Rows[i].Cells[2];

                                isExist = true;
                                break;
                            }
                        }
                    }
                }

                // UI 정리 및 포커스 설정
                dataGridView1.ClearSelection();
                dataGridView2.ClearSelection();
                txtScanData.Text = string.Empty;
                txtScanData.Focus();
            }
        }

        // 모든 스캔이 완료되었는지 확인하는 메서드
        private bool ResultAllScan()
        {
            // dataGridView1의 모든 행이 스캔되었는지 확인
            for (int i = 0; i < dataGridView1.RowCount; i++)
            {
                if (dataGridView1.Rows[i].Cells[2].Value.ToString() == "")
                    return false;
            }

            // dataGridView2의 모든 행이 스캔되었는지 확인
            for (int i = 0; i < dataGridView2.RowCount; i++)
            {
                if (dataGridView2.Rows[i].Cells[2].Value.ToString() == "")
                    return false;
            }

            return true;
        }

        // 확인 버튼 클릭 이벤트 핸들러
        private void button1_Click(object sender, EventArgs e)
        {
            // 모든 스캔이 완료된 경우에만 처리
            if (ResultAllScan())
            {
                if (txtLotid.Text == "")
                    return;

                // 데이터베이스 연결 확인
                if (_connection.State == ConnectionState.Closed)
                    _connection.Open();

                // 로그 데이터 생성
                var log = "";
                for (int i = 0; i < dataGridView1.RowCount; i++)
                {
                    if (log == "")
                        log = dataGridView1.Rows[i].Cells[1].Value.ToString();
                    else
                        log = log + "\n" + dataGridView1.Rows[i].Cells[1].Value.ToString();
                }

                for (int i = 0; i < dataGridView2.RowCount; i++)
                {
                    if (log == "")
                        log = dataGridView2.Rows[i].Cells[1].Value.ToString();
                    else
                        log = log + "\n" + dataGridView2.Rows[i].Cells[1].Value.ToString();
                }

                // 로그 데이터베이스에 저장
                var sql = "";
                sql =
                $@"INSERT INTO tb_ccs_lot_log (lot_id, step_id, log, result, user_id) VALUES ((SELECT id FROM tb_mes_lotid WHERE lotid = '{txtLotid.Text}'), 37, '{log}', 'PASS', {frmMain.userID})";

                MySqlHelper.ExecuteNonQuery(_connection, sql);

                // UI 초기화
                dataGridView1.Rows.Clear();
                dataGridView2.Rows.Clear();
                txtLotid.Text = "";
            }
        }

        // 리포트 조회 버튼 클릭 이벤트 핸들러
        private void button3_Click(object sender, EventArgs e)
        {
            dgvReport.Rows.Clear();
            var sql = "";

            // 지정된 기간 동안의 로그 데이터 조회
            sql =
            $@"SELECT g.id, l.lotid, g.step_id, g.log, g.result, u.user_name, date_format(g.created_on, '%Y-%m-%d %H:%i:%s') 
            FROM tb_ccs_lot_log g, tb_mes_lotid l, tb_user u
            WHERE g.step_id = 37 AND g.lot_id = l.id AND g.user_id = u.id AND SUBSTR(g.created_on, 1, 10) >= '{dtpStart.Value.ToString("yyyy-MM-dd")}' 
            AND SUBSTR(g.created_on, 1, 10) <= '{dtpEnd.Value.ToString("yyyy-MM-dd")}' AND role is null ";

            var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            
            // 조회된 데이터를 리포트 그리드에 표시
            foreach (DataRow row in dataTable.Rows)
            {
                dgvReport.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], row[6]);
                dgvReport.ClearSelection();
            }
        }

        // 리포트 그리드 셀 클릭 이벤트 핸들러
        private void dgvReport_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvReport.Rows[e.RowIndex].Cells[e.ColumnIndex].Value != null)
            {
                // 선택된 로그의 상세 내용 조회 및 표시
                var index = dgvReport.Rows[e.RowIndex].Cells[0].Value.ToString();
                var sql = $@"SELECT log FROM tb_ccs_lot_log WHERE id = {index} ";
                var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
                foreach (DataRow row in dataTable.Rows)
                {
                    txtMain.Text = row[0].ToString().Replace("\n", Environment.NewLine);
                }
            }
        }

        // 초기화 버튼 클릭 이벤트 핸들러
        private void button2_Click(object sender, EventArgs e)
        {
            dataGridView1.Rows.Clear();
            dataGridView2.Rows.Clear();
            txtLotid.Text = "";
        }
    }
}
