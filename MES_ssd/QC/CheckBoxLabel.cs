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
        public frmBoxLabelCheck(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;

        }

        private void frmBoxLabelCheck_Load(object sender, EventArgs e)
        {
            var sql = $"SELECT * FROM tb_ssd_std_program WHERE name like 'BOX_LABEL_%'";
            var dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];

            foreach (DataRow row in dataTable.Rows)
            {
                var byteArray = (byte[])row["program"];

                using (var ms = new MemoryStream(byteArray))
                {
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

        private void txtScanData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && txtScanData.Text != string.Empty)
            {
                var sql = string.Empty;
                DataTable dataTable;
                if (txtScanData.Text.Length == 21 && dataGridView2.RowCount == 0)
                {
                    // L03LOG030900120230901

                    var smallboxid = txtScanData.Text.Substring(0, 10) + txtScanData.Text.Substring(19, 2);
                    var scnt = txtScanData.Text.Substring(19, 2);

                    dataGridView1.Rows.Clear();
                    dataGridView2.Rows.Clear();

                    var isPssd = false;
                    // 소박스

                    var modelname = Helpers.MySqlHelper.GetOneData(_connection,
                            $@"SELECT model_name FROM tb_mes_std_espec WHERE id = (SELECT espec_id FROM tb_mes_lotid WHERE lotid = '{smallboxid.Substring(0, 10)}') ");

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
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var oemConfig = row[5].ToString();

                        if (isPssd)
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 6;

                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], "");
                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            txtLotid.Text = row[6].ToString();
                            string hexValue =
                                row[4].ToString().Substring(0, row[4].ToString().Length - 2)
                                + smallboxid.Substring(smallboxid.Length - 2, 2);
                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], "");
                                //dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[4], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, hexValue, "");
                            }
                        }
                        else if (oemConfig == "") // BRAND
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 0;

                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], "");
                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            txtLotid.Text = row[6].ToString();
                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[4], "");
                            }
                        }
                        else if (row[3].ToString().Contains("-00W")) // DC
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 0;
                            txtLotid.Text = row[6].ToString();

                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            if (sn.Split(' ').Length == 60)
                            {
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");
                                sn = "";
                            }

                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[4], "");
                            }
                        }
                        else
                        {
                            txtLotid.Text = row[6].ToString();
                            if (row[7].ToString() == "HP")
                            {
                                tabControl1.SelectedIndex = 3;

                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[8], ""); // _FULLSALECODE
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[9], ""); // _HPPN
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[4], ""); // _MLOT
                            }
                            else
                            {
                                tabControl1.SelectedIndex = 2;

                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[4], "");
                            }

                            break;
                        }
                    }

                    if (isBrand)
                    {
                        // SSD / PSSD 관계없이 공백 추가
                        //if (isPssd == true)
                        //{
                        if (sn != "")
                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, " " + sn, "");
                        //}
                    }

                }
                else if (txtScanData.Text.Length == 12 && dataGridView1.RowCount == 0)
                {
                    // 대박스
                    var largeboxid = txtScanData.Text.Trim();

                    dataGridView1.Rows.Clear();
                    dataGridView2.Rows.Clear();
                    var isPssd = false;

                        var modelname = Helpers.MySqlHelper.GetOneData(_connection,
                            $@"SELECT DISTINCT model_name FROM tb_mes_std_espec WHERE id in (
                            SELECT espec_id FROM tb_mes_lotid WHERE id in (
                            SELECT lot_id FROM tb_mes_dat_setinfo WHERE small_box_id in (
                            SELECT id FROM tb_mes_dat_boxinfo WHERE large_boxid = '{largeboxid}'))) ");

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
                    foreach (DataRow row in dataTable.Rows)
                    {
                        var oemConfig = row[4].ToString();

                        if (isPssd)
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 5;

                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], "");
                            txtLotid.Text = row[5].ToString();

                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            if (dataGridView1.RowCount == 15)
                            {
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");
                                sn = "";
                            }

                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], "");
                            }
                        }
                        else if (oemConfig == "") // BRAND
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 1;

                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, row[0], "");
                            txtLotid.Text = row[5].ToString();

                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            if (dataGridView1.RowCount == 30)
                            {
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");

                                sn = "";
                            }

                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], "");
                            }
                        }
                        else if (row[6].ToString().Contains("-00W")) // DC
                        {
                            isBrand = true;
                            tabControl1.SelectedIndex = 1;
                            txtLotid.Text = row[5].ToString();

                            sn = (sn == "") ? row[0].ToString() : sn + " " + row[0].ToString();

                            if (sn.Split(' ').Length == 60)
                            {
                                dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");
                                sn = "";
                            }

                            if (dataGridView2.RowCount == 0)
                            {
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[1], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[2], "");
                                dataGridView2.Rows.Add(dataGridView2.RowCount + 1, row[3], "");
                            }
                        }
                        else
                        {
                            tabControl1.SelectedIndex = 4;

                            txtLotid.Text = row[5].ToString();
                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, largeboxid + "  ", "");

                            break;
                        }
                    }

                    if (isBrand)
                    {
                        if (sn != "")
                            dataGridView1.Rows.Add(dataGridView1.RowCount + 1, sn, "");
                    }
                }
                else
                {
                    var isExist = false;
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


                dataGridView1.ClearSelection();
                dataGridView2.ClearSelection();
                txtScanData.Text = string.Empty;
                txtScanData.Focus();
            }
        }

        private bool ResultAllScan()
        {
            for (int i = 0; i < dataGridView1.RowCount; i++)
            {
                if (dataGridView1.Rows[i].Cells[2].Value.ToString() == "")
                    return false;
            }

            for (int i = 0; i < dataGridView2.RowCount; i++)
            {
                if (dataGridView2.Rows[i].Cells[2].Value.ToString() == "")
                    return false;
            }

            return true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (ResultAllScan())
            {
                if (txtLotid.Text == "")
                    return;

                if (_connection.State == ConnectionState.Closed)
                    _connection.Open();

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

                var sql = "";
                    sql =
                    $@"INSERT INTO tb_ccs_lot_log (lot_id, step_id, log, result, user_id) VALUES ((SELECT id FROM tb_mes_lotid WHERE lotid = '{txtLotid.Text}'), 37, '{log}', 'PASS', {frmMain.userID})";

                MySqlHelper.ExecuteNonQuery(_connection, sql);

                dataGridView1.Rows.Clear();
                dataGridView2.Rows.Clear();
                txtLotid.Text = "";
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            dgvReport.Rows.Clear();
            var sql = "";

            sql =
            $@"SELECT g.id, l.lotid, g.step_id, g.log, g.result, u.user_name, date_format(g.created_on, '%Y-%m-%d %H:%i:%s') 
            FROM tb_ccs_lot_log g, tb_mes_lotid l, tb_user u
            WHERE g.step_id = 37 AND g.lot_id = l.id AND g.user_id = u.id AND SUBSTR(g.created_on, 1, 10) >= '{dtpStart.Value.ToString("yyyy-MM-dd")}' 
            AND SUBSTR(g.created_on, 1, 10) <= '{dtpEnd.Value.ToString("yyyy-MM-dd")}' AND role is null ";

            var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                dgvReport.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], row[6]);
                dgvReport.ClearSelection();
            }
        }

        private void dgvReport_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dgvReport.Rows[e.RowIndex].Cells[e.ColumnIndex].Value != null)
            {
                var index = dgvReport.Rows[e.RowIndex].Cells[0].Value.ToString();
                var sql = $@"SELECT log FROM tb_ccs_lot_log WHERE id = {index} ";
                var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
                foreach (DataRow row in dataTable.Rows)
                {
                    txtMain.Text = row[0].ToString().Replace("\n", Environment.NewLine);
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            dataGridView1.Rows.Clear();
            dataGridView2.Rows.Clear();
            txtLotid.Text = "";
        }
    }
}
