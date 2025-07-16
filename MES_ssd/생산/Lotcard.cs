using ITS.lib.Database;
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
    public partial class frmLOTCARD_PRINT : Form
    {
        private MySqlConnection _connection;
        public frmLOTCARD_PRINT(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection;
        }

        private void frmLOTCARD_PRINT_Load(object sender, EventArgs e)
        {
            if (!SearchPort("MH_LOTCARD_ZM410"))
            {

            }
        }

        private bool SearchPort(string name)
        {
            try
            {
                using (var fs = File.Open("SETTING.ini", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    fs.Close();

                    var readData = File.ReadAllText("SETTING.ini", Encoding.Default);

                    string[] stringSeparators = new string[] { "\r\n" };
                    string[] lines = readData.Split(stringSeparators, StringSplitOptions.None);
                    foreach (string s in lines)
                    {
                        if (s.Split('=')[0] == name)
                        {
                            txtComport.Text = s.Split('=')[1];
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("SETTING.ini File not found.");
                return false;
            }

            if (txtComport.Text == string.Empty)
            {
                MessageBox.Show($"NO COMPORT {name}");
                return false;
            }
            else
            {
                try
                {
                    spBarcode.PortName = txtComport.Text;
                    spBarcode.Open();

                    if (spBarcode.IsOpen)
                        spBarcode.Close();
                }
                catch (Exception)
                {
                    MessageBox.Show("바코드 프린터 연결을 확인하세요. ", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }

            return true;
        }

        private void txtScandata_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && txtScandata.Text != string.Empty)
            {
                txtScandata.Text = txtScandata.Text.ToUpper();

                txtMessage.Text = string.Empty;
                txtMessage.BackColor = SystemColors.Control;

                if (txtScandata.Text.Length == 10)
                {
                    var labelData = LotCardBarcodePrint(_connection, txtScandata.Text);

                    if (labelData == "FAB LINE NG")
                    {
                        txtMessage.Text = "FAB LINE NG";
                        txtMessage.BackColor = Color.Red;
                        return;
                    }

                    try
                    {
                        if (!spBarcode.IsOpen)
                            spBarcode.Open();

                        spBarcode.Write(labelData);
                        spBarcode.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                        MessageBox.Show(labelData);
                    }
                }

                txtScandata.Text = string.Empty;
            }
        }

        private string LotCardBarcodePrint(MySqlConnection _connection, string lotid)
        {
            var prod_code = string.Empty;
            var sale_code = string.Empty;
            var lot_type = string.Empty;
            var reflow_type = string.Empty;
            var k9prod_code = string.Empty;
            var k9fab_line = string.Empty;
            var k9option_code = string.Empty;
            var comp_k9_opt = string.Empty;
            var k4prod_code = string.Empty;
            var issueWeek = string.Empty;
            var k9CompLotid = string.Empty;
            var lot_qty = string.Empty;
            var date_format = string.Empty;
            var label_pdf = string.Empty;
            var series = string.Empty;
            var k9Week = string.Empty;

            string sql =
                $@"SELECT PROD_ID, LOT_ID, LOT_TYPE, PROC_ID, CHIP_QTY, COMPLOT, TIER, OPTCODE, WEEKCODE, OPTION_CODE, FABSITE, OTHER, ASSYINTIME, 
                (SELECT SALESCODE FROM MODULE.MC_CONSM WHERE CONSM_ID = COMPLOT) AS COMP_SALESCODE, 
                (SELECT SUBSTR(SALESCODE, 20, 1) FROM MODULE.MC_CONSM WHERE CONSM_ID = COMPLOT) AS FABLINE,
                (SELECT DESIGN_SPEC_ID||'-'||REV_ID AS ID FROM LEGACY_MDM.BE_MES_MOD_SSD_PRODCT WHERE PROD_ID LIKE L.PROD_ID) AS LABEL_SPEC,
                (SELECT MODEL_NAME FROM SSD_ESPEC WHERE PROD_ID = SUBSTR((L.PROD_ID), 1, 18) )
                FROM MODULE.MC_LOT L
                WHERE LOT_ID = '{lotid}' ";
            var dr = OracleHelper.GetDataList(sql);
            while (dr.Read())
            {
                prod_code = dr[0].ToString();
                sale_code = dr[0].ToString().Substring(0, 18);
                lotid = dr[1].ToString();
                lot_type = dr[2].ToString();
                reflow_type = string.Empty;
                k9prod_code = dr[13].ToString();
                k9fab_line = dr[14].ToString();
                k9option_code = string.Empty;
                comp_k9_opt = dr[6].ToString() + dr[7].ToString();
                k4prod_code = string.Empty;
                issueWeek = dr[8].ToString();
                k9CompLotid = dr[5].ToString();
                lot_qty = dr[4].ToString();
                date_format = dr[12].ToString();
                label_pdf = dr[15].ToString();
                k9Week = string.Empty;
                series = dr[16].ToString();
            }

            sql =
                $@"SELECT C.CONSM_PROD_ID, C.CONSM_ID , C.FABLINE, C.OPTIONCODE, SUBSTR(C.OPTIONCODE, 1, 2), C.WEEKCODE, (SELECT DISTINCT OTHER FROM MODULE.MC_LOT_HIST WHERE EVENT_NAME = 'ConsumeMaterial' AND LOT_ID = H.LOT_ID AND OTHER  IS NOT NULL ) 
                FROM MODULE.MC_LOT_HIST H, MODULE.MC_CONSM C
                WHERE H.CONSMED_CONSM_ID = C.CONSM_ID 
                AND H.LOT_ID = '{lotid}'
                AND H.EVENT_NAME  = 'CompConsume' 
                ORDER BY C.CONSM_PROD_ID ";
            dr = OracleHelper.GetDataList(sql);
            var pcbavl = "";
            while (dr.Read())
            {
                if (dr[0].ToString().Substring(0, 2) == "K9" || dr[0].ToString().Substring(0, 2) == "KL")
                {
                    k9prod_code = dr[0].ToString();
                    k9CompLotid = dr[1].ToString();
                    k9fab_line = dr[2].ToString();
                    k9option_code = dr[3].ToString();
                    comp_k9_opt = dr[4].ToString();
                    k9Week = dr[5].ToString();
                    pcbavl = dr[6].ToString();
                }
                else if (dr[0].ToString().Substring(0, 2) == "K4")
                {
                    k4prod_code = dr[0].ToString();
                }
            }




            // MZ7L3500HBLU-1BW00-MQ2
            // "* FAB Info.  M (M/L), S (P/B)"
            if (prod_code.Substring(19, 1) == "M")
            {
                if (k9fab_line != "M" && k9fab_line != "L")
                {
                    return "FAB LINE NG";
                }
            }
            else if (prod_code.Substring(19, 1) == "S")
            {
                if (k9fab_line != "P" && k9fab_line != "B")
                {
                    return "FAB LINE NG";
                }
            }
            else
            {
                return "FAB LINE NG";
            }


            txtProductCode.Text = prod_code;
            txtLotid.Text = lotid;
            txtLotQty.Text = lot_qty;
            txtOption.Text = comp_k9_opt;
            txtWeek.Text = issueWeek;

            MessageBox.Show("tb_mos_rules", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Stop);

            var datname = "Lot_Card_R0";
            var labelString = MySqlHelper.ExecuteDataset(_connection, $"SELECT dat FROM tb_mes_std_dat WHERE dat_name = '{datname}' ").Tables[0].Rows[0][0].ToString();

            var labelData = labelString.Replace("_PRODCODE", prod_code);
            labelData = labelData.Replace("_SALECODE", sale_code);
            labelData = labelData.Replace("_LOTIDBARCODE", lotid);
            labelData = labelData.Replace("_LOTID", lotid);
            //labelData = labelData.Replace("_YYYY/MM/DD_HH:MM:SS", $@"{date_format.Substring(0, 4)}/{date_format.Substring(4, 2)}/{date_format.Substring(6, 2)} {date_format.Substring(8, 2)}:{date_format.Substring(10, 2)}:{date_format.Substring(12, 2)}");
            labelData = labelData.Replace("_PCBAVL", pcbavl);
            labelData = labelData.Replace("_LOTTYPE", lot_type);
            labelData = labelData.Replace("_SERIES", series);

            if (reflow_type.Split(',').Length == 2)
            {
                labelData = labelData.Replace("_T/REFLOWTYPE", reflow_type.Split(',')[0]);
                labelData = labelData.Replace("_B/REFLOWTYPE", reflow_type.Split(',')[1]);
            }
            else
            {
                labelData = labelData.Replace("_T/REFLOWTYPE", reflow_type);
            }

            labelData = labelData.Replace("_LOTQTY", lot_qty);
            labelData = labelData.Replace("_LOTNO", "");
            labelData = labelData.Replace("_K9WEEK", k9Week);
            labelData = labelData.Replace("_WEEK", issueWeek); // ISSUE WEEK

            labelData = labelData.Replace("_OPTIONCODE", comp_k9_opt);

            labelData = labelData.Replace("_FAB", k9fab_line);
            labelData = labelData.Replace("_K9OPTIONCODE", k9option_code);
            labelData = labelData.Replace("_K4PRODCODE", k4prod_code);
            labelData = labelData.Replace("_K9PRODCODE", k9prod_code);
            labelData = labelData.Replace("_K9LOTID", k9CompLotid.Replace("QSI-SMT_", ""));
            labelData = labelData.Replace("_LABELPDF", label_pdf);

            return labelData;
        }
    }
}
