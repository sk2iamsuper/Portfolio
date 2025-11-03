using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace ReelTW
{
    public partial class FormPLC : Form, IDisposable
    {
        private CancellationTokenSource _cts;
        private Task _plcTask;
        private bool _monitorMode;
        private bool _isConnected;

        private readonly ClassMelsecPLC _plc = new();
        private short[] _plcStatus, _plcTrigger, _vsStatus, _vsTrigger, _vsResult1, _vsResult2;

        private const int LoopDelayMs = 30;
        private const int AddrStep = 2;

        public bool IsConnectedPLC => _isConnected;

        public FormPLC() => InitializeComponent();

        private void FormPLC_Load(object sender, EventArgs e) => ToggleMonitorMode();

        #region Monitor / Connection
        public async void OpenConnectPLC()
        {
            try
            {
                var ip = ClassSystemConfig.Ins.m_ClsCommon.m_strIpPLC;
                var port = ClassSystemConfig.Ins.m_ClsCommon.m_iPortPLC;

                if (!ClassSystemConfig.Ins.m_ClsCommon.PingRespond(ip, 100, 5))
                {
                    Log($"PLC Ping Fail: {ip}");
                    return;
                }

                _plc.Initialize(ip, port);
                _isConnected = _plc.objPLC_Client[0].Connected;
                Log(_isConnected ? $"Connected PLC {ip}:{port}" : $"Connect Fail {ip}:{port}");

                if (_isConnected)
                {
                    _cts = new CancellationTokenSource();
                    _plcTask = Task.Run(() => MonitorPLCAsync(_cts.Token));
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Log($"OpenConnectPLC Error: {ex.Message}");
            }
        }

        public async void CloseConnectPLC()
        {
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                    await Task.WhenAny(_plcTask ?? Task.CompletedTask, Task.Delay(500));
                    _cts.Dispose();
                }

                _plc.DisConnect();
                _isConnected = false;
                Log("Disconnected PLC");
            }
            catch (Exception ex)
            {
                Log($"CloseConnectPLC Error: {ex.Message}");
            }
        }

        private async Task MonitorPLCAsync(CancellationToken token)
        {
            var writeCount = 0;
            while (!token.IsCancellationRequested && _isConnected)
            {
                try
                {
                    _plc.ReadWordFromPLC(0, "D6000", 1, ref _plcStatus);
                    _plc.ReadWordFromPLC(0, "D6002", 4, ref _plcTrigger);

                    InvokeSafe(UpdateUI_PLCStatus);
                    InvokeSafe(UpdateUI_PLCTrigger);

                    CheckResetSignal();

                    if (++writeCount >= 2)
                    {
                        UpdateVisionStatus();
                        writeCount = 0;
                    }

                    await Task.Delay(LoopDelayMs, token);
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    Log($"PLC Monitor Error: {ex.Message}");
                }
            }
        }
        #endregion

        #region UI Helpers
        private void InvokeSafe(Action action)
        {
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        private static void Log(string msg)
        {
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(
                ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                msg,
                ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local,
                true);
        }
        #endregion

        #region Vision Status
        private void UpdateVisionStatus()
        {
            var status = ClassSystemConfig.Ins.m_ClsCommon._Status;
            SetBit(ref _vsStatus[0], status.VISION_Alive ? 0 : -1);
            SetBit(ref _vsStatus[0], stat_
