using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace mes_
{
    public partial class frmProcessInOut : Form
    {
        private readonly MySqlConnection _connection;
        private string step = string.Empty;
        private const string LotPrefix = "L0";
        private const string ScanInTime = "INTIME";
        private const string ScanOutTime = "OUTTIME";
        public frmProcessInOut(string _step)
        {
            InitializeComponent();
            _connection = Helpers.MySqlHelper.GetConnection();
            step = _step;

        }

        private void frmProcessInOut_Load(object sender, EventArgs e)
        {
            //txtCCSSpec.Text = Helpers.MySqlHelper.GetOneData(_connection, string.Format("SELECT ccs_item FROM new_mes.tb_mes_process WHERE step = '{0}' ", step));
        }

        private string eSpecStep = string.Empty;
        private string eSpecSmtCode = string.Empty;
        private string eSpecLotSize = string.Empty;
        private void txtScanData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData != Keys.Enter)
                return;

            var scan = txtScanData.Text?.Trim();
            if (string.IsNullOrEmpty(scan))
                return;

            scan = scan.ToUpperInvariant();
            txtScanData.Text = scan;
            txtMessage.Text = string.Empty;

            if (IsLotId(scan) && !TryLoadLot(scan))
            {
                ClearScan();
                return;
            }

            if (txtLotID.Text == string.Empty)
            {
                ClearScan();
                return;
            }

            if (TryEnableInOutButtonsForCurrentStep())
            {
                ClearScan();
                return;
            }

            EnsureConnectionOpen();

            if (scan == ScanInTime)
            {
                btnInTime_Click(null, null);
            }
            else if (scan == ScanOutTime)
            {
                btnOutTime_Click(null, null);
            }
            else
            {
                SetMessage("정의되지 않은 스캔정보입니다. ");
            }

            ClearScan();
        }

        private static bool IsLotId(string scan)
            => scan.Length == 10 && scan.StartsWith(LotPrefix, StringComparison.Ordinal);

        private bool TryLoadLot(string lotId)
        {
            ClearLotFields();
            LoadSchedule(lotId);

            if (txtSchCode.Text == string.Empty)
            {
                SetMessage("계획이 존재하지 않습니다. ");
                return false;
            }

            PopulateSteps(lotId);

            if (dataGridView1.RowCount == 0)
                return false;

            GetStepTime(lotId);
            txtNextStep.Text = GetNextStep();

            if (txtTitleName.Text != txtNextStep.Text)
            {
                SetMessage("공정에 맞지 않습니다. STEP 을 확인하세요. ");
                return false;
            }

            txtStartQty.Text = GetLastOutQty(lotId);
            txtLotID.Text = lotId;
            return true;
        }

        private void ClearLotFields()
        {
            txtProductCode.Text = string.Empty;
            txtSaleCode.Text = string.Empty;
            txtLotID.Text = string.Empty;
            txtOptionCode.Text = string.Empty;
            txtCompLot.Text = string.Empty;
            txtFab.Text = string.Empty;
            txtWeek.Text = string.Empty;
            txtType.Text = string.Empty;
            txtStartQty.Text = string.Empty;
            txtIssue.Text = string.Empty;
            txtNextStep.Text = string.Empty;
            dataGridView1.Rows.Clear();
            txtSchCode.Text = string.Empty;
            txtProdCode.Text = string.Empty;
        }

        private void LoadSchedule(string lotId)
        {
            for (int i = 0; i < dgvSchList.RowCount; i++)
            {
                if (dgvSchList.Rows[i].Cells[4].Value.ToString() == lotId)
                {
                    txtSchCode.Text = dgvSchList.Rows[i].Cells[0].Value.ToString();
                    txtProdCode.Text = dgvSchList.Rows[i].Cells[1].Value.ToString();
                }
            }
        }

        private void PopulateSteps(string lotId)
        {
            var especStep = Helpers.MySqlHelper.GetOneData(
                _connection,
                string.Format(
                    "SELECT step_path FROM tb_mes_std_espec WHERE id in (SELECT espec_id FROM tb_mes_lotid WHERE lotid = '{0}') ",
                    lotId));

            string[] steps = especStep.Split(',');
            foreach (var step in steps)
            {
                if (step == "Empty")
                    break;

                dataGridView1.Rows.Add(step, "", "", "", "", "", "");
            }
        }

        private string GetNextStep()
        {
            for (int i = 0; i < dataGridView1.RowCount; i++)
            {
                if (dataGridView1.Rows[i].Cells[4].Value.ToString() == string.Empty)
                    return dataGridView1.Rows[i].Cells[0].Value.ToString();
            }

            return string.Empty;
        }

        private string GetLastOutQty(string lotId)
        {
            return Helpers.MySqlHelper.GetOneData(
                _connection,
                string.Format(
                    "SELECT out_qty FROM tb_mes_lotid_history " +
                    "WHERE lot_id in (SELECT id FROM tb_mes_lotid WHERE lotid = '{0}') AND result is not null " +
                    "ORDER BY process_id DESC LIMIT 1 ",
                    lotId));
        }

        private bool TryEnableInOutButtonsForCurrentStep()
        {
            for (int i = 0; i < dataGridView1.RowCount; i++)
            {
                if (dataGridView1.Rows[i].Cells[0].Value.ToString() != txtTitleName.Text)
                    continue;

                if (dataGridView1.Rows[i].Cells[1].Value.ToString() == string.Empty)
                {
                    btnInTime.Enabled = true;
                    btnOutTime.Enabled = false;
                    return true;
                }

                if (dataGridView1.Rows[i].Cells[4].Value.ToString() == string.Empty)
                {
                    btnInTime.Enabled = false;
                    btnOutTime.Enabled = true;
                    return true;
                }
            }

            return false;
        }

        private void EnsureConnectionOpen()
        {
            if (_connection.State == ConnectionState.Closed)
                _connection.Open();
        }

        private void SetMessage(string message)
        {
            txtMessage.Text = message;
        }

        private void ClearScan()
        {
            txtScanData.Text = string.Empty;
        }

        private void GetStepTime(string lotid)
        {
            var sql = string.Format("SELECT p.step, h.created_on, h.in_qty, h.updated_at, h.out_qty, h.in_qty - h.out_qty, ROUND(100 - (h.in_qty-h.out_qty) / h.in_qty * 100, 2), result " +
                    "FROM tb_mes_lotid_history h, tb_mes_process p, tb_mes_lotid l " +
                    "WHERE h.process_id = p.id AND h.lot_id = l.id AND l.lotid = '{0}' ORDER BY p.step ", lotid);

            var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                for (int i = 0; i < dataGridView1.RowCount; i++)
                {
                    if (dataGridView1.Rows[i].Cells[0].Value.ToString() == row[0].ToString())
                    {
                        dataGridView1.Rows[i].Cells[1].Value = row[1].ToString();
                        dataGridView1.Rows[i].Cells[2].Value = row[2].ToString();

                        dataGridView1.Rows[i].Cells[3].Value = (row[7].ToString() != string.Empty) ? row[3].ToString() : "";
                        dataGridView1.Rows[i].Cells[4].Value = (row[7].ToString() != string.Empty) ? row[4].ToString() : "";
                        dataGridView1.Rows[i].Cells[5].Value = (row[7].ToString() != string.Empty) ? row[5].ToString() : "";
                        dataGridView1.Rows[i].Cells[6].Value = (row[7].ToString() != string.Empty) ? row[6].ToString() : "";
                    }
                }
            }

            dataGridView1.ClearSelection();
        }

        private void btnInTime_Click(object sender, EventArgs e)
        {
            ///////////////////////////////////////////////////////////////
            for (int i = 0; i < dataGridView1.RowCount; i++)
            {
                if (dataGridView1.Rows[i].Cells[0].Value.ToString() == txtTitleName.Text)
                {
                    if (dataGridView1.Rows[i].Cells[1].Value.ToString() != string.Empty)
                    {
                        SetMessage("이미 INTIME 정보가 존재합니다. ");
                        ClearScan();
                        return;
                    }
                }
            }

            EnsureConnectionOpen();

            var sql = string.Format("INSERT INTO tb_mes_lotid_history (process_id, lot_id, in_qty) " +
                "values ((SELECT id FROM tb_mes_process WHERE step = '{0}'), (SELECT id FROM tb_mes_lotid WHERE lotid = '{1}'), {2}) ",
                txtTitleName.Text, txtLotID.Text, txtStartQty.Text);
            MySqlHelper.ExecuteNonQuery(_connection, sql);


            // 화면 초기화
            GetStepTime(txtLotID.Text);

            txtSchCode.Text = string.Empty;
            txtProdCode.Text = string.Empty;
            txtLotID.Text = string.Empty;
        }

        private void btnOutTime_Click(object sender, EventArgs e)
        {
            ///////////////////////////////////////////////////////////////
            for (int i = 0; i < dataGridView1.RowCount; i++)
            {
                if (dataGridView1.Rows[i].Cells[0].Value.ToString() == txtTitleName.Text)
                {
                    if (dataGridView1.Rows[i].Cells[1].Value.ToString() == string.Empty)
                    {
                        SetMessage("INTIME 정보를 찾을수 없습니다. ");
                        ClearScan();
                        return;
                    }

                    if (dataGridView1.Rows[i].Cells[4].Value.ToString() != string.Empty)
                    {
                        SetMessage("이미 OUTTIME 정보가 존재합니다. ");
                        ClearScan();
                        return;
                    }
                }
            }

            ///////////////////////////////////////////////////////////////
            // 저수율관리
            var pass = 0;
            var fail = 0;  // FAIL 이지만 다음 LOT에 IN 되어야 할 수량 
            var sql = string.Empty;
            if (txtTitleName.Text == "M010")
            {
                sql = string.Format("SELECT aoi_1, COUNT(*) FROM tb_mes_dat_setinfo WHERE lot_id in (SELECT id FROM tb_mes_lotid WHERE lotid = '{1}') GROUP BY aoi_1 ", txtTitleName.Text, txtLotID.Text);
            }
            else
            {
                sql = string.Format("SELECT aoi_2, COUNT(*) FROM tb_mes_dat_setinfo WHERE lot_id in (SELECT id FROM tb_mes_lotid WHERE lotid = '{1}') GROUP BY aoi_2 ", txtTitleName.Text, txtLotID.Text);
            }

            var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                if (row[0].ToString() == "G" || row[0].ToString() == "P")
                    pass = pass + int.Parse(row[1].ToString());
                else
                    fail = int.Parse(row[1].ToString());
            }

            if (int.Parse(txtStartQty.Text) != (pass + fail))
            {
                SetMessage("SMT 이력이 모두 들어오지 않았습니다. ");
                ClearScan();
                return;
            }











            EnsureConnectionOpen();

            var yield = string.Format("{0:F1}", 100 - ((double)(float.Parse(fail.ToString()) / (float.Parse(pass.ToString()) + float.Parse(fail.ToString()))) * 100));
            var spec = Helpers.MySqlHelper.GetOneData(_connection, string.Format("SELECT yield FROM tb_mes_process WHERE step = '{0}' ", txtTitleName.Text));
            if (double.Parse(spec) > double.Parse(yield))
            {
                sql = string.Format("UPDATE tb_mes_lotid SET status = 'Hold' WHERE lotid = '{0}' ", txtLotID.Text);
                MySqlHelper.ExecuteNonQuery(_connection, sql);

                SetMessage("저수율로 인해 LOT 가 HOLD 되었습니다. ");
                ClearScan();
            }
            else
            {
                sql = string.Format("UPDATE tb_mes_lotid SET status = 'Wait' WHERE lotid = '{0}' ", txtLotID.Text);
                MySqlHelper.ExecuteNonQuery(_connection, sql);
            }


            ///////////////////////////////////////////////////////////////
            // LOT 정리
            var OutQty = txtStartQty.Text;  // 라우터 이전까지는 LOTIN, LOTOUT 수량 동일
            sql = string.Format("UPDATE tb_mes_lotid_history SET out_qty = {0}, updated_at = {1}, result = 'P' " +
                "WHERE process_id = (SELECT id FROM tb_mes_process WHERE step = '{2}') AND lot_id in (SELECT id FROM tb_mes_lotid WHERE lotid = '{3}')",
                OutQty, DateTime.Now.ToString("yyyyMMddHHmmss"), txtTitleName.Text, txtLotID.Text);
            MySqlHelper.ExecuteNonQuery(_connection, sql);

            GetStepTime(txtLotID.Text);

            txtLotID.Text = string.Empty;
        }
    }
}
