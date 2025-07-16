using ITS.lib.Database;
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

namespace ITS
{
    public partial class frmCOMP_INVENTORY : Form
    {
        private MySqlConnection _connection;
        public frmCOMP_INVENTORY(MySqlConnection connection)
        {
            InitializeComponent();
            DoubleBufferedHelper.SetDoubleBufferedParent(this);
            _connection = connection;
        }

        private void frmCOMP_INVENTORY_Load(object sender, EventArgs e)
        {
            string sql =
                $@"SELECT DISTINCT CONSM_PROD_ID FROM MODULE.MC_CONSM
            WHERE CONSM_TYPE = 'COMP' AND CONSM_STATUS_SEG != 'TERMINATED' AND SALESCODE IS NOT NULL
            ORDER BY CONSM_PROD_ID ";
            var dr = OracleHelper.GetDataList(sql);
            while (dr.Read())
            {
                cbSeries.Items.Add(dr[0].ToString());
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            GetMatScanData(cbProdCode.Text);
        }

        private void GetMatScanData(string product)
        {
            dgvTab1MainList.Rows.Clear();
            dgvTab3MainList.Rows.Clear();
            string sql =
                $@"SELECT CONSM_PROD_ID, CONSM_ID, CONSM_LOT_TYPE, INV_QTY, FABLINE , OPTIONCODE, SALESCODE, CONSM_STATUS_SEG, WEEKCODE, 
                (SELECT COMP_LOC FROM COMP_LOC WHERE COMP_LOTID = CONSM_ID) AS LOCATION
                FROM MODULE.MC_CONSM
                WHERE CONSM_TYPE = 'COMP' AND CONSM_STATUS_SEG != 'TERMINATED' AND SALESCODE IS NOT NULL AND INV_QTY > 0
                AND SALESCODE LIKE '{product}%' ";
            var dr = OracleHelper.ExecuteDataset(sql);
            foreach (DataRow row in dr.Rows)
            {
                var compLot = row[1].ToString();
                var holdLot = row[7].ToString();

                var prodcode = row[0].ToString();

                if (prodcode.Substring(0, 2) != "K4")
                {
                    dgvTab1MainList.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7], row[8], row[9]);
                    dgvTab1MainList.ClearSelection();
                    if (holdLot == "HOLD")
                    {
                        for (int i = 0; i < dgvTab1MainList.ColumnCount; i++)
                            dgvTab1MainList.Rows[dgvTab1MainList.RowCount - 1].Cells[i].Style.ForeColor = Color.Red;
                    }
                }
                else
                {
                    dgvTab3MainList.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7], row[8], row[9]);
                    dgvTab3MainList.ClearSelection();
                    if (holdLot == "HOLD")
                    {
                        for (int i = 0; i < dgvTab3MainList.ColumnCount; i++)
                            dgvTab3MainList.Rows[dgvTab3MainList.RowCount - 1].Cells[i].Style.ForeColor = Color.Red;
                    }
                }
            }
        }

        private void cbSeries_SelectedIndexChanged(object sender, EventArgs e)
        {
            cbProdCode.Text = "";
            GetMatScanData(cbSeries.Text);
        }

        private void cbProdCode_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && cbProdCode.Text != string.Empty)
            {
                cbProdCode.Text = cbProdCode.Text.ToUpper();

                GetMatScanData(cbProdCode.Text);
            }
        }

        private void txtScanData_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && txtScanData.Text != string.Empty)
            {
                txtScanData.Text = txtScanData.Text.ToUpper();

                dgvTab1MainList.Rows.Clear();

                var sql =
                    $@"SELECT COMP_CODE FROM OMS_ETL.BS_CM_MOD_BOM  
                    WHERE PROD_CODE = '{txtScanData.Text}' AND (SUBSTR(COMP_CODE, 1, 2) = 'K9' OR SUBSTR(COMP_CODE, 1, 2) = 'KL' OR SUBSTR(COMP_CODE, 1, 2) = 'K4')
                    ORDER BY COMP_CODE ";

                var dr = OracleHelper.ExecuteDataset(sql);
                foreach (DataRow row in dr.Rows)
                {
                    var sakecode = row[0].ToString();

                    GetCompList(sakecode, txtScanData.Text.Substring(19, 1));
                }

                dgvOpt2MainList.Rows.Clear();

                sql =
                    $@"SELECT GAM_PROD, PROD_CODE, CONCAT(OPTION2, FEB), GAM_COND, COMMENTS FROM OMS_ETL.BE_LOTPEG_SSD_OPT2_N 
                    WHERE DEL_FLAG = 'N' AND (FEB = 'M' OR FEB = 'L' OR FEB = 'P' OR FEB = 'B') AND GAM_COND = '!=' 
                    AND '{txtScanData.Text.Substring(0, 18)}' LIKE GAM_PROD  ";

                dr = OracleHelper.ExecuteDataset(sql);
                foreach (DataRow row in dr.Rows)
                {
                    dgvOpt2MainList.Rows.Add(row[0], row[1], row[2], row[3], row[4]);

                    var optionfab = row[2].ToString();

                    for(int i = 0; i< dgvTab1MainList.RowCount; i++)
                    {
                        var opt2 = dgvTab1MainList.Rows[i].Cells[7].Value.ToString().Substring(1, 2);

                        if (optionfab == opt2)
                        {
                            for(int n = 0; n< dgvTab1MainList.ColumnCount; n++)
                            {
                                dgvTab1MainList.Rows[i].Cells[n].Style.BackColor = Color.Red;
                            }
                        }
                    }
                }

                try
                {
                    var value = int.Parse(textBox1.Text);
                }
                catch (Exception ex)
                {
                    textBox1.Text = "1";
                }

                GetModBom();
            }
        }

        private void GetCompList(string salecode, string fab)
        {
            string sql = string.Empty;
            switch(fab)
            {
                case "M":
                    if (salecode.Substring(0, 2) == "K4")
                    {
                        sql =
                            $@"SELECT CONSM_PROD_ID, CONSM_ID, CONSM_LOT_TYPE, INV_QTY, FABLINE , OPTIONCODE, SALESCODE, CONSM_STATUS_SEG, WEEKCODE, 
                        (SELECT COMP_LOC FROM COMP_LOC WHERE COMP_LOTID = CONSM_ID) AS LOCATION
                        FROM MODULE.MC_CONSM
                        WHERE CONSM_TYPE = 'COMP' AND CONSM_STATUS_SEG != 'TERMINATED' AND SALESCODE IS NOT NULL  
                        AND CONSM_PROD_ID = '{salecode}' AND INV_QTY != '0' ";
                    }
                    else
                    {
                        sql =
                            $@"SELECT CONSM_PROD_ID, CONSM_ID, CONSM_LOT_TYPE, INV_QTY, FABLINE , OPTIONCODE, SALESCODE, CONSM_STATUS_SEG, WEEKCODE, 
                        (SELECT COMP_LOC FROM _COMP_LOC WHERE COMP_LOTID = CONSM_ID) AS LOCATION
                        FROM MODULE.MC_CONSM
                        WHERE CONSM_TYPE = 'COMP' AND CONSM_STATUS_SEG != 'TERMINATED' AND SALESCODE IS NOT NULL  
                        AND (FABLINE = 'M' OR FABLINE = 'L') AND CONSM_PROD_ID = '{salecode}' AND INV_QTY != '0' ";
                    }
                    break;

                case "S":
                    if (salecode.Substring(0, 2) == "K4")
                    {
                        sql =
                        $@"SELECT CONSM_PROD_ID, CONSM_ID, CONSM_LOT_TYPE, INV_QTY, FABLINE , OPTIONCODE, SALESCODE, CONSM_STATUS_SEG, WEEKCODE, 
                        (SELECT COMP_LOC FROM COMP_LOC WHERE COMP_LOTID = CONSM_ID) AS LOCATION
                        FROM MODULE.MC_CONSM
                        WHERE CONSM_TYPE = 'COMP' AND CONSM_STATUS_SEG != 'TERMINATED' AND SALESCODE IS NOT NULL  
                        AND CONSM_PROD_ID = '{salecode}' AND INV_QTY != '0' ";
                    }
                    else
                    {
                        sql =
                        $@"SELECT CONSM_PROD_ID, CONSM_ID, CONSM_LOT_TYPE, INV_QTY, FABLINE , OPTIONCODE, SALESCODE, CONSM_STATUS_SEG, WEEKCODE, 
                        (SELECT COMP_LOC FROM COMP_LOC WHERE COMP_LOTID = CONSM_ID) AS LOCATION
                        FROM MODULE.MC_CONSM
                        WHERE CONSM_TYPE = 'COMP' AND CONSM_STATUS_SEG != 'TERMINATED' AND SALESCODE IS NOT NULL  
                        AND (FABLINE = 'P' OR FABLINE = 'B') AND CONSM_PROD_ID = '{salecode}' AND INV_QTY != '0' ";
                    }
                    break;
            }

            var dr = OracleHelper.ExecuteDataset(sql);
            foreach (DataRow row in dr.Rows)
            {
                var compLot = row[1].ToString();
                var holdLot = row[7].ToString();

                if (salecode.Substring(0, 2) == "K4")
                {
                    dgvTab3MainList.ClearSelection();
                    dgvTab3MainList.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7], row[8], row[9]);

                    if (holdLot == "HOLD")
                    {
                        for (int i = 0; i < dgvTab3MainList.ColumnCount; i++)
                            dgvTab3MainList.Rows[dgvTab3MainList.RowCount - 1].Cells[i].Style.ForeColor = Color.Red;
                    }
                }
                else
                {
                    dgvTab1MainList.ClearSelection();
                    dgvTab1MainList.Rows.Add(row[0], row[1], row[2], row[3], row[4], row[5], row[6], row[7], row[8], row[9]);

                    if (holdLot == "HOLD")
                    {
                        for (int i = 0; i < dgvTab1MainList.ColumnCount; i++)
                            dgvTab1MainList.Rows[dgvTab1MainList.RowCount - 1].Cells[i].Style.ForeColor = Color.Red;
                    }
                }
            }
        }

        private void dgvTab1MainList_SelectionChanged(object sender, EventArgs e)
        {
            var sum = 0;

            for (var i = 0; i < dgvTab1MainList.Rows.Count; i++)
            {
                if (dgvTab1MainList.Rows[i].Cells[3].Selected)
                {
                    if (dgvTab1MainList.Rows[i].Cells[3].Value != null)
                    {
                        var value = dgvTab1MainList.Rows[i].Cells[3].Value.ToString();

                        value = (value == "") ? "0" : value;

                        sum = sum + int.Parse(value);
                    }
                }
            }

            txtSelectQty.Text = sum.ToString();
        }

        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && textBox1.Text != string.Empty)
            {
                textBox1.Text = textBox1.Text.ToUpper();

                try
                {
                    var value = int.Parse(textBox1.Text);
                }
                catch(Exception ex)
                {
                    textBox1.Text = "1";
                }

                GetModBom();
            }
        }

        private string nand = "";
        private string dram = "";
        private void GetModBom()
        {
            dgvTab2MainList.Rows.Clear();

            nand = "";
            dram = "";
            var sql =
            $@"SELECT COMP_CODE, COMP_QTY FROM OMS_ETL.BS_CM_MOD_BOM  
                WHERE PROD_CODE = '{txtScanData.Text}' 
                AND (SUBSTR(COMP_CODE, 1, 2) = 'K9' OR SUBSTR(COMP_CODE, 1, 2) = 'K4' OR SUBSTR(COMP_CODE, 1, 2) = 'KL')
                ORDER BY COMP_CODE";
            var dr = OracleHelper.ExecuteDataset(sql);
            foreach (DataRow row in dr.Rows)
            {
                var qty = int.Parse(row[1].ToString());
                dgvTab2MainList.Rows.Add(row[0], row[1].ToString(), qty * int.Parse(textBox1.Text));

                if (row[0].ToString().Substring(0, 2) == "K9" || row[0].ToString().Substring(0, 2) == "KL")
                    nand = row[0].ToString();

                if (row[0].ToString().Substring(0, 2) == "K4")
                    dram = row[0].ToString();
            }

            sql =
            $@"SELECT PIECE_PART_NO, FROM_PIECE_QTY FROM OMS_ETL.BS_CM_MATS_PART_BOM
                WHERE PROD_CODE = '{txtScanData.Text}' AND PIECE_PART_NO LIKE 'LA41%' AND DEL_FLAG = 'N' ";
            dr = OracleHelper.ExecuteDataset(sql);
            foreach (DataRow row in dr.Rows)
            {
                var qty = int.Parse(row[1].ToString());
                dgvTab2MainList.Rows.Add(row[0], row[1].ToString(), qty * int.Parse(textBox1.Text));
            }
        }

        private void txtLotID_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && txtLotID.Text != string.Empty)
            {
                txtLotID.Text = txtLotID.Text.ToUpper();

                for(int i = 0; i< dgvTab1MainList.RowCount; i++)
                {
                    if (dgvTab1MainList.Rows[i].Cells[1].Value.ToString() == txtLotID.Text)
                    {
                        dgvTab1MainList.CurrentCell = dgvTab1MainList.Rows[i].Cells[1];
                        break;
                    }
                }

                for (int i = 0; i < dgvTab3MainList.RowCount; i++)
                {
                    if (dgvTab3MainList.Rows[i].Cells[1].Value.ToString() == txtLotID.Text)
                    {
                        dgvTab3MainList.CurrentCell = dgvTab1MainList.Rows[i].Cells[1];
                        break;
                    }
                }
            }
        }

        private void dgvTab1MainList_DoubleClick(object sender, EventArgs e)
        {
            if (dgvTab1MainList.CurrentRow != null)
            {
                dataGridView2.Rows.Clear();

                var CONSM_ID = dgvTab1MainList.Rows[dgvTab1MainList.CurrentCell.RowIndex].Cells[1].Value.ToString();

                var sql =
                $@"
                SELECT TO_CHAR(EVENT_TMSTP, 'YYYY-MM-DD HH24:MI:SS') AS EVENT_TMSTP, EVENT_NAME, CONSM_QTY, ADD_QTY, REMOVE_QTY, 
                (SELECT CONSM_ID FROM MODULE.MC_CONSM_HIST WHERE OBJECT_ID = H.CHILD_EVENT_OBJECT_ID) AS CHILD, CONSMR_LOT_ID, CONSMR_PROD_ID,
                (SELECT CONSM_ID FROM MODULE.MC_CONSM_HIST WHERE OBJECT_ID = H.PARENT_EVENT_OBJECT_ID) AS PARENT
                FROM MODULE.MC_CONSM_HIST H
                WHERE CONSM_ID = '{CONSM_ID}'
                AND (EVENT_NAME = 'CompMerge' OR EVENT_NAME = 'CompSplit' OR EVENT_NAME LIKE 'CompReceive%' OR EVENT_NAME LIKE 'CompConsume' )
                ORDER BY EVENT_TMSTP ";
                var dr = OracleHelper.GetDataList(sql);
                while (dr.Read())
                {
                    var memo = dr[5].ToString();
                    var consmr_prod_id = dr[7].ToString();
                    var parent = dr[8].ToString();

                    if (dr[1].ToString().Contains("CompConsume"))
                    {
                        if (consmr_prod_id == "")
                            continue;

                        memo = dr[6].ToString();
                    }

                    if (parent != "")
                        memo = parent;

                    dataGridView2.Rows.Add(dr[0], dr[1], dr[2], dr[3], dr[4], memo);
                    dataGridView2.ClearSelection();

                    if (dr[1].ToString().Contains("Split"))
                    {
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[0].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[1].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[2].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[3].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[4].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[5].Style.ForeColor = Color.Red;
                    }
                }
            }
        }

        private void dgvTab3MainList_DoubleClick(object sender, EventArgs e)
        {
            if (dgvTab3MainList.CurrentRow != null)
            {
                dataGridView2.Rows.Clear();

                var CONSM_ID = dgvTab3MainList.Rows[dgvTab3MainList.CurrentCell.RowIndex].Cells[1].Value.ToString();

                var sql =
                $@"
                SELECT TO_CHAR(EVENT_TMSTP, 'YYYY-MM-DD HH24:MI:SS') AS EVENT_TMSTP, EVENT_NAME, CONSM_QTY, ADD_QTY, REMOVE_QTY, 
                (SELECT CONSM_ID FROM MODULE.MC_CONSM_HIST WHERE OBJECT_ID = H.CHILD_EVENT_OBJECT_ID) AS CHILD, CONSMR_LOT_ID, CONSMR_PROD_ID,
                (SELECT CONSM_ID FROM MODULE.MC_CONSM_HIST WHERE OBJECT_ID = H.PARENT_EVENT_OBJECT_ID) AS PARENT
                FROM MODULE.MC_CONSM_HIST H
                WHERE CONSM_ID = '{CONSM_ID}'
                AND (EVENT_NAME = 'CompMerge' OR EVENT_NAME = 'CompSplit' OR EVENT_NAME LIKE 'CompReceive%' OR EVENT_NAME LIKE 'CompConsume' )
                ORDER BY EVENT_TMSTP ";
                var dr = OracleHelper.GetDataList(sql);
                while (dr.Read())
                {
                    var memo = dr[5].ToString();
                    var consmr_prod_id = dr[7].ToString();
                    var parent = dr[8].ToString();

                    if (dr[1].ToString().Contains("CompConsume"))
                    {
                        if (consmr_prod_id == "")
                            continue;

                        memo = dr[6].ToString();
                    }

                    if (parent != "")
                        memo = parent;

                    dataGridView2.Rows.Add(dr[0], dr[1], dr[2], dr[3], dr[4], memo);
                    dataGridView2.ClearSelection();

                    if (dr[1].ToString().Contains("Split"))
                    {
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[0].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[1].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[2].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[3].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[4].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[5].Style.ForeColor = Color.Red;
                    }
                }
            }
        }

        private void dataGridView2_DoubleClick(object sender, EventArgs e)
        {
            if (dataGridView2.CurrentRow != null)
            {
                var CONSM_ID = dataGridView2.Rows[dataGridView2.CurrentCell.RowIndex].Cells[dataGridView2.CurrentCell.ColumnIndex].Value.ToString();
                dataGridView2.Rows.Clear();

                var sql =
                $@"
                SELECT TO_CHAR(EVENT_TMSTP, 'YYYY-MM-DD HH24:MI:SS') AS EVENT_TMSTP, EVENT_NAME, CONSM_QTY, ADD_QTY, REMOVE_QTY, 
                (SELECT CONSM_ID FROM MODULE.MC_CONSM_HIST WHERE OBJECT_ID = H.CHILD_EVENT_OBJECT_ID) AS CHILD, CONSMR_LOT_ID, CONSMR_PROD_ID,
                (SELECT CONSM_ID FROM MODULE.MC_CONSM_HIST WHERE OBJECT_ID = H.PARENT_EVENT_OBJECT_ID) AS PARENT
                FROM MODULE.MC_CONSM_HIST H
                WHERE CONSM_ID = '{CONSM_ID}'
                AND (EVENT_NAME = 'CompMerge' OR EVENT_NAME = 'CompSplit' OR EVENT_NAME LIKE 'CompReceive%' OR EVENT_NAME LIKE 'CompConsume' )
                ORDER BY EVENT_TMSTP ";
                var dr = OracleHelper.GetDataList(sql);
                while (dr.Read())
                {
                    var memo = dr[5].ToString();
                    var consmr_prod_id = dr[7].ToString();
                    var parent = dr[8].ToString();

                    if (dr[1].ToString().Contains("CompConsume"))
                    {
                        if (consmr_prod_id == "")
                            continue;

                        memo = dr[6].ToString();
                    }

                    if (parent != "")
                        memo = parent;

                    dataGridView2.Rows.Add(dr[0], dr[1], dr[2], dr[3], dr[4], memo);
                    dataGridView2.ClearSelection();

                    if (dr[1].ToString().Contains("Split"))
                    {
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[0].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[1].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[2].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[3].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[4].Style.ForeColor = Color.Red;
                        dataGridView2.Rows[dataGridView2.RowCount - 1].Cells[5].Style.ForeColor = Color.Red;
                    }
                }
            }
        }
    }
}
