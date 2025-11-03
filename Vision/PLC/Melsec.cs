using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using kyTech_VisionSystem.pDefine;
using System.Threading;
using System.Threading.Tasks;

namespace kyTech_VisionSystem
{
    public partial class UserControl_Melsec : UserControl, IDisposable
    {
        // UserControl 종료 시 상위 Form 에게 Close 명령을 내리기 위한 Delegate 와 이벤트 선언
        public delegate void SendMsgDelegate(string str_CMD);
        public event SendMsgDelegate UserControl_Melsel_SendCMD;

        private PLCProcess _tempPLCProcess;

        // Background update task & cancellation
        private CancellationTokenSource _ctsInterface;
        private Task _interfaceTask;

        // Dispose guard
        private bool _disposed = false;

        public UserControl_Melsec()
        {
            InitializeComponent();

            dataGridView_Melsel_Interface.Dock = DockStyle.Fill;
            dataGridView_Melsel_Interface.AllowUserToAddRows = false;
            dataGridView_Melsel_Interface.AllowUserToDeleteRows = false;

            DataGridView_Init();

            _tempPLCProcess = new PLCProcess();
        }

        #region DataGridView Init / Config / Setting

        private void DataGridView_Init()
        {
            // PLC 관련 변수 초기화
            Melsec_Define.struct_Plc_Input.init();
            Melsec_Define.struct_Plc_Output.init();
            Melsec_Define.struct_Plc_Cmd.init();

            dataGridView_Melsel_Interface.ColumnCount = (int)Melsec_Define.enumDataGridView_Melsel_Size.COLUMN_COUNT;

            dataGridView_Melsel_Interface.ColumnHeadersDefaultCellStyle.BackColor = Color.DimGray;
            dataGridView_Melsel_Interface.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;

            // Column header texts
            dataGridView_Melsel_Interface.Columns[0].Name = "0~199";
            dataGridView_Melsel_Interface.Columns[1].Name = "0~199";
            dataGridView_Melsel_Interface.Columns[2].Name = "0~199";
            dataGridView_Melsel_Interface.Columns[3].Name = "0~199";
            dataGridView_Melsel_Interface.Columns[4].Name = "200~399";
            dataGridView_Melsel_Interface.Columns[5].Name = "200~399";
            dataGridView_Melsel_Interface.Columns[6].Name = "200~399";
            dataGridView_Melsel_Interface.Columns[7].Name = "200~399";
            dataGridView_Melsel_Interface.Columns[8].Name = "400~599";
            dataGridView_Melsel_Interface.Columns[9].Name = "400~599";
            dataGridView_Melsel_Interface.Columns[10].Name = "400~599";
            dataGridView_Melsel_Interface.Columns[11].Name = "400~599";

            dataGridView_Melsel_Interface.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.EnableResizing;
            dataGridView_Melsel_Interface.ColumnHeadersHeight = this.dataGridView_Melsel_Interface.ColumnHeadersHeight * 2;
            dataGridView_Melsel_Interface.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.BottomCenter;
            dataGridView_Melsel_Interface.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // Attach paint events
            dataGridView_Melsel_Interface.CellPainting += dataGridView_Melsel_Interface_CellPainting;
            dataGridView_Melsel_Interface.Paint += dataGridView_Melsel_Interface_Paint;
            dataGridView_Melsel_Interface.Scroll += dataGridView_Melsel_Interface_Scroll;
            dataGridView_Melsel_Interface.ColumnWidthChanged += dataGridView_Melsel_Interface_ColumnWidthChanged;
            dataGridView_Melsel_Interface.CellMouseClick += dataGridView_Melsel_Interface_CellMouseClick;

            dataGridView_Melsel_Interface.ColumnHeadersHeight = (int)Melsec_Define.enumDataGridView_Melsel_Size.COLUMN_HEIGHT;

            // Column widths
            dataGridView_Melsel_Interface.Columns[0].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.ADDRESS_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[1].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.DATA_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[4].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.ADDRESS_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[5].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.DATA_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[8].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.ADDRESS_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[9].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.DATA_COLUMN_WIDTH;

            dataGridView_Melsel_Interface.Columns[2].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.POSITION_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[3].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.COMMAND_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[6].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.POSITION_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[7].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.COMMAND_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[10].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.POSITION_COLUMN_WIDTH;
            dataGridView_Melsel_Interface.Columns[11].Width = (int)Melsec_Define.enumDataGridView_Melsel_Size.COMMAND_COLUMN_WIDTH;

            // Disable sorting & resizing for all columns
            for (int i = 0; i < dataGridView_Melsel_Interface.Columns.Count; i++)
            {
                dataGridView_Melsel_Interface.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
                dataGridView_Melsel_Interface.Columns[i].Resizable = DataGridViewTriState.False;
            }
        }

        /// <summary>
        /// DataGridView 에 구동 PC에 맞게 데이터 셋팅
        /// </summary>
        public void DataGridView_Setting()
        {
            bool colorToggle = false;

            try
            {
                dataGridView_Melsel_Interface.Rows.Clear();

                for (int i = Melsec_Define.i_Start_Address_Value; i < Melsec_Define.i_Start_Address_Value + Melsec_Define.i_Address_Size; i++)
                {
                    dataGridView_Melsel_Interface.Rows.Add(
                        i.ToString(),
                        "0",
                        Melsec_Define.struct_Plc_Output.str_Position[i - Melsec_Define.i_Start_Address_Value],
                        Melsec_Define.struct_Plc_Output.str_Command[i - Melsec_Define.i_Start_Address_Value],
                        (i + Melsec_Define.i_Address_Interval).ToString(),
                        "0",
                        Melsec_Define.struct_Plc_Input.str_Position[i - Melsec_Define.i_Start_Address_Value],
                        Melsec_Define.struct_Plc_Input.str_Command[i - Melsec_Define.i_Start_Address_Value],
                        (i + (Melsec_Define.i_Address_Interval * 2)).ToString(),
                        "0",
                        string.Empty,
                        Melsec_Define.struct_Plc_Cmd.str_Command[i - Melsec_Define.i_Start_Address_Value]
                    );

                    dataGridView_Melsel_Interface.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                }

                for (int idx = 0; idx < Melsec_Define.i_Address_Size; idx++)
                {
                    var row = dataGridView_Melsel_Interface.Rows[idx];

                    row.Cells[0].Style.BackColor = Color.DarkGray;
                    row.Cells[4].Style.BackColor = Color.DarkGray;
                    row.Cells[8].Style.BackColor = Color.DarkGray;

                    row.Cells[1].Style.BackColor = Color.Silver;
                    row.Cells[5].Style.BackColor = Color.Silver;
                    row.Cells[9].Style.BackColor = Color.Silver;

                    row.Cells[2].Style.ForeColor = Color.White;
                    row.Cells[3].Style.ForeColor = Color.White;
                    row.Cells[6].Style.ForeColor = Color.White;
                    row.Cells[7].Style.ForeColor = Color.White;
                    row.Cells[10].Style.ForeColor = Color.White;
                    row.Cells[11].Style.ForeColor = Color.White;

                    if (idx % 10 == 0)
                        colorToggle = !colorToggle;

                    Color blockColor = colorToggle ? Color.DarkKhaki : Color.DimGray;

                    row.Cells[2].Style.BackColor = blockColor;
                    row.Cells[3].Style.BackColor = blockColor;
                    row.Cells[6].Style.BackColor = blockColor;
                    row.Cells[7].Style.BackColor = blockColor;
                    row.Cells[10].Style.BackColor = blockColor;
                    row.Cells[11].Style.BackColor = blockColor;
                }

                // Start interface loop using Task + CancellationToken
                StartInterfaceLoop();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MainDefine.__SYSTEM_PATH} DataGridView_Setting 함수 에러: {ex.Message}", "경고", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public bool DataGridView_Config_Load()
        {
            bool success = false;

            switch (MainDefine.__OPERATION_PROGRAMNUMBER)
            {
                case 1: ClsINI._PATH = MainDefine.__MELSEC_CONFIG_PATH_PC01; break;
                case 2: ClsINI._PATH = MainDefine.__MELSEC_CONFIG_PATH_PC02; break;
                case 3: ClsINI._PATH = MainDefine.__MELSEC_CONFIG_PATH_PC03; break;
            }

            try
            {
                Melsec_Define.i_Start_Address_Value = ClsINI.GetInt32("PLC START ADDRESS", "Plc_Address_Start_Number", 1);
                Melsec_Define.i_Start_SubAddress_Value = ClsINI.GetInt32("PLC START ADDRESS", "Plc_Sub_Address_Start_Number", 0);

                for (int i = Melsec_Define.i_Start_Address_Value; i < Melsec_Define.i_Start_Address_Value + Melsec_Define.i_Address_Size; i++)
                {
                    Melsec_Define.struct_Plc_Output.str_Position[i - Melsec_Define.i_Start_Address_Value] = ClsINI.GetString("PLC POSITION DATA_OUTPUT", "Position_AddressIndex_" + i.ToString());
                    Melsec_Define.struct_Plc_Output.str_Command[i - Melsec_Define.i_Start_Address_Value] = ClsINI.GetString("PLC COMMAND DATA_OUTPUT", "Command_AddressIndex_" + i.ToString());

                    Melsec_Define.struct_Plc_Input.str_Position[i - Melsec_Define.i_Start_Address_Value] = ClsINI.GetString("PLC POSITION DATA_INPUT", "Position_AddressIndex_" + (i + Melsec_Define.i_Address_Interval).ToString());
                    Melsec_Define.struct_Plc_Input.str_Command[i - Melsec_Define.i_Start_Address_Value] = ClsINI.GetString("PLC COMMAND DATA_INPUT", "Command_AddressIndex_" + (i + Melsec_Define.i_Address_Interval).ToString());

                    Melsec_Define.struct_Plc_Cmd.str_Command[i - Melsec_Define.i_Start_Address_Value] = ClsINI.GetString("PLC COMMAND DATA_CMD", "Command_AddressIndex_" + (i + (Melsec_Define.i_Address_Interval * 2)).ToString());
                }

                success = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{MainDefine.__SYSTEM_PATH} DataGridView_Config_Load 함수 에러: {ex.Message}", "경고", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return success;
        }

        #endregion

        #region Interface Loop (Task 기반)

        private void StartInterfaceLoop()
        {
            // 이미 실행 중이면 무시
            if (_ctsInterface != null) return;

            _ctsInterface = new CancellationTokenSource();
            var token = _ctsInterface.Token;

            _interfaceTask = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        // UI 스레드에 안전하게 수행
                        if (!this.IsDisposed && this.IsHandleCreated)
                        {
                            try
                            {
                                this.BeginInvoke(new Action(UpdateGridValues));
                            }
                            catch (ObjectDisposedException)
                            {
                                break;
                            }
                        }

                        // 업데이트 주기: 환경에 따라 조정 가능 (기본 200ms)
                        await Task.Delay(200, token).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException) { /* 정상 종료 */ }
                catch (Exception) { /* 필요시 로깅 */ }
            }, token);
        }

        private void UpdateGridValues()
        {
            try
            {
                int count = Math.Min(Melsec_Define.i_Address_Size, dataGridView_Melsel_Interface.Rows.Count);
                for (int i = 0; i < count; i++)
                {
                    dataGridView_Melsel_Interface[1, i].Value = Melsec_Define._PC_WRITE_ADDRESS[i];
                    dataGridView_Melsel_Interface[5, i].Value = Melsec_Define._PLC_WRITE_ADDRESS[i];
                    dataGridView_Melsel_Interface[9, i].Value = Melsec_Define._PLC_CMD_ADDRESS[i];
                }
            }
            catch (Exception) { /* 필요시 로깅 */ }
        }

        private async Task StopInterfaceLoopAsync(int timeoutMs = 500)
        {
            if (_ctsInterface == null) return;

            try
            {
                _ctsInterface.Cancel();
                if (_interfaceTask != null)
                {
                    var t = Task.WhenAny(_interfaceTask, Task.Delay(timeoutMs));
                    await t.ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                try { _ctsInterface.Dispose(); } catch { }
                _ctsInterface = null;
                _interfaceTask = null;
            }
        }

        #endregion

        #region Address Utilities

        public string ConvertAddress(string inputAddress, int inputNum)
        {
            if (string.IsNullOrWhiteSpace(inputAddress))
                throw new ArgumentNullException(nameof(inputAddress));

            string s = inputAddress.Trim();
            if (s.StartsWith("D", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(1);

            if (!int.TryParse(s, out int addr))
                throw new FormatException($"Address format invalid: {inputAddress}");

            int newAddr = (addr - (int)Melsec_Define.PC_INDEX.Status) + inputNum;
            return $"D{newAddr}";
        }

        #endregion

        #region DataGridView Draw Events

        private void dataGridView_Melsel_Interface_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            Rectangle rtHeader = this.dataGridView_Melsel_Interface.DisplayRectangle;
            rtHeader.Height = this.dataGridView_Melsel_Interface.ColumnHeadersHeight / 2;
            this.dataGridView_Melsel_Interface.Invalidate(rtHeader);
        }

        private void dataGridView_Melsel_Interface_Scroll(object sender, ScrollEventArgs e)
        {
            Rectangle rtHeader = this.dataGridView_Melsel_Interface.DisplayRectangle;
            rtHeader.Height = this.dataGridView_Melsel_Interface.ColumnHeadersHeight / 2;
            this.dataGridView_Melsel_Interface.Invalidate(rtHeader);
        }

        private void dataGridView_Melsel_Interface_Paint(object sender, PaintEventArgs e)
        {
            string[] headers = {
                $"Vision->PLC  {Melsec_Define.i_Start_SubAddress_Value}~{Melsec_Define.i_Start_SubAddress_Value + 99}",
                "200~399",
                $"PLC->Vision  {Melsec_Define.i_Start_SubAddress_Value + 100}~{Melsec_Define.i_Start_SubAddress_Value + 199}",
                "600~799",
                $"Command  {Melsec_Define.i_Start_SubAddress_Value + 200}~{Melsec_Define.i_Start_SubAddress_Value + 299}",
                "200~299",
                "1000~1199"
            };

            for (int j = 0; j < 12; )
            {
                Rectangle r1 = this.dataGridView_Melsel_Interface.GetCellDisplayRectangle(j, -1, true);
                int w2 = this.dataGridView_Melsel_Interface.GetCellDisplayRectangle(j + 1, -1, true).Width;
                int w3 = this.data
