using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.Diagnostics;

namespace ITS
{
    public partial class frmMagazine : Form
    {
        private readonly MySqlConnection _connection;
        private string array_iarts = string.Empty;
        private string array_top = string.Empty;

        private int slotCount = 1;

        public frmMagazine()
        {
            InitializeComponent();
            _connection = Helpers.MySqlHelper.GetConnection();
            DoubleBufferedHelper.SetDoubleBufferedParent(this);
        }

        private void frmM031_Load(object sender, EventArgs e)
        {
            GetMagazineList();
            dgvMagazines.DataBindingComplete += DgvMagazines_DataBindingComplete;
        }

        private void DgvMagazines_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
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
        }
        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(tabControl1.SelectedIndex == 0)
            {
                GetMagazineList();
            }
            else if (tabControl1.SelectedIndex == 1)
            {
                GetMagazines();
            }
        }

        private void GetMagazineList()
        {
            var sql = $"SELECT id, name, available, port_status, available_ports, flag, date_format(created_on, '%Y-%m-%d %H:%i:%s') as created_on FROM tb_chamber_magazines ORDER BY id";
            var dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            dgvMagazines.DataSource = dataTable;
            ITS.DataGridViewHelper.FitColumnSize(dgvMagazines);


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

            dataGridView3.Rows.Clear();
            sql = $@"SELECT available_ports, name FROM tb_chamber_magazines WHERE flag = 'C' ORDER BY name";
            dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                dataGridView3.Rows.Add(row[0], row[1]);
                dataGridView3.ClearSelection();
            }
        }

        private void GetMagazines()
        {
            dataGridView6.Rows.Clear();
            var sql = string.Format("SELECT NAME, PORT_STATUS, LENGTH(port_status)-LENGTH(REPLACE(port_status, '1' , '')) FROM tb_chamber_magazines ");
            var dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                dataGridView6.Rows.Add(dataGridView6.RowCount + 1, row[0], "'" + row[1], int.Parse(row[2].ToString()));
                dataGridView6.ClearSelection();

                if (int.Parse(row[2].ToString()) >= 2)
                {
                    dataGridView6.Rows[dataGridView6.RowCount - 1].Cells[0].Style.BackColor = Color.Yellow;
                    dataGridView6.Rows[dataGridView6.RowCount - 1].Cells[1].Style.BackColor = Color.Yellow;
                    dataGridView6.Rows[dataGridView6.RowCount - 1].Cells[2].Style.BackColor = Color.Yellow;
                    dataGridView6.Rows[dataGridView6.RowCount - 1].Cells[3].Style.BackColor = Color.Yellow;
                }
            }
        }

        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && textBox1.Text != string.Empty)
            {
                textBox1.Text = textBox1.Text.ToUpper();

                txtMagazineId1.Text = txtMagazineName.Text = textBox1.Text;

                textBox1.Text = string.Empty;

                var portstatus = ITS.Helpers.MySqlHelper.GetOneData(_connection, $"SELECT port_status FROM tb_chamber_magazines WHERE  name = '{txtMagazineName.Text}' ");
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


                GetMagazinesHistory();


            }
        }
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



            var sql = $"UPDATE tb_chamber_magazines SET port_status = '{port_status}' WHERE name = '{txtMagazineName.Text}' ";
            MySql.Data.MySqlClient.MySqlHelper.ExecuteNonQuery(_connection, sql);

            sql = $"INSERT INTO tb_chamber_magazine_histories (magazine_id, user_id, has_error, description) " +
                $"SELECT id, {frmMain.userID}, 'False', '{txtComment.Text}' FROM new_mes.tb_chamber_magazines WHERE name = '{txtMagazineName.Text}' ";
            MySql.Data.MySqlClient.MySqlHelper.ExecuteNonQuery(_connection, sql);


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


            GetMagazines();
        }

        private void txtT4Slot1_DoubleClick(object sender, EventArgs e)
        {
            if (frmMain.user_ID.ToUpper() != "PHS" && frmMain.user_ID.ToUpper() != "LUAN_IT" && frmMain.user_ID.ToUpper() != "HDLEE"
                && frmMain.user_ID.ToUpper() != "VO_KT" && frmMain.user_ID.ToUpper() != "DIEP KT" && frmMain.user_ID.ToUpper() != "HUNG" && frmMain.user_ID.ToUpper() != "DUC KT")
            {
                MessageBox.Show("You do not have permission to do this.");
                return;
            }

            Label slot = sender as Label;
            slot.BackColor = (slot.BackColor == Color.Red) ? Color.White : Color.Red;
        }

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
