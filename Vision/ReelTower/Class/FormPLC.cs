using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;



namespace ReelTW
{
    public partial class FormPLC : Form
    {
        public Main main;
        private Thread m_ThreadPLC;
        private bool bLoopPLC = true;
        public ClassMelsecPLC m_ClsMelsec;

        public static int m_BuffSizePLC_Trigger = 4;
        public static int m_BuffSizeVS_Trigger = 4;
        public static int m_BuffSizeVS_Result1 = 24;
        public static int m_BuffSizeVS_Result2 = 2;
        public static string m_AddrPLC_Status = "D6000";
        public static string m_AddrPLC_Trigger = "D6002";
        public static string m_AddrVS_Status = "D6500";
        public static string m_AddrVS_Trigger = "D6502";
        public static string m_AddrVS_Result1 = "D6510";
        public static string m_AddrVS_Result2 = "D6536";
        private short[] m_ValuePLC_Status;
        private short[] m_ValuePLC_Trigger;

        private short[] m_ValueVS_Status;
        private short[] m_ValueVS_Trigger;
        private short[] m_ValueVS_ResultCAM1;
        private short[] m_ValueVS_ResultCAM2;


        private bool m_bPLC_Connected;
        private bool m_bMonitorMode = false;
        public bool IsConnectedPLC
        {
            get { return m_bPLC_Connected; }
            set { m_bPLC_Connected = value; }
        }
        public FormPLC()
        {
            InitializeComponent();
        }

        private void FormPLC_Load(object sender, EventArgs e)
        {
            switchMonitor_Click(null, null);
        }
        public void InitializeUI(Main obj)
        {
            main = obj;
            InitPLC();
        }

        #region Minimize, maximum
        bool bLastStateNormal = true;
        public void ShowOnScreen()
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = bLastStateNormal ? FormWindowState.Normal : FormWindowState.Maximized;

            this.BringToFront();
        }
        private void btnMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnMaximum_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
                bLastStateNormal = true;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
                bLastStateNormal = false;
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Hide();
        }
        #endregion

        #region Enable Drag Winform
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        private void FormMain_MouseDown(object sender, MouseEventArgs e)
        {
            dragging = true;
            dragCursorPoint = Cursor.Position;
            dragFormPoint = this.Location;
        }

        private void FormMain_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        private void FormMain_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }
        #endregion

        #region UI Control handler Event
        private void FormPLC_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
        }
        private void switchMonitor_Click(object sender, EventArgs e)
        {
            m_bMonitorMode = switchMonitor.Value;
            panelSignal1.Enabled = switchMonitor.Value;
            panelSignal2.Enabled = switchMonitor.Value;
        }
        #endregion

        #region PLC Side
        public enum PLC_STATUS
        {
            ALIVE = 0,
            RESET = 15
        }
        public enum VISION_STATUS
        {
            ALIVE = 0,
            VS_BUSY = 1,
            READY = 2,
            RESET_CFM = 15
        }
        public enum VISION_TRIGGER
        {
            TRG_DONE1 = 0,
            TRG_DONE2 = 1,
            COMPLETE1 = 2,
            COMPLETE2 = 3
        }
        public enum VISION_RESULT1
        {
            RESULT = 0,
            ANGLE = 1,
            QR_CODE = 3,
        }
        public enum VISION_RESULT2
        {
            RESULT = 0,
            SPARE = 1
        }
        private void InitPLC()
        {
            // PLC와 Vision 사이에서 주고받을 Word 버퍼를 운전 시작 전에 한 번 초기화한다.
            m_ClsMelsec = new ClassMelsecPLC();

            // PLC Data Buffer
            m_ValuePLC_Status = new short[1];
            m_ValuePLC_Trigger = new short[m_BuffSizePLC_Trigger];

            // VS Data Buffer
            m_ValueVS_Status = new short[1];
            m_ValueVS_Trigger = new short[m_BuffSizePLC_Trigger];
            m_ValueVS_ResultCAM1 = new short[m_BuffSizeVS_Result1];
            m_ValueVS_ResultCAM2 = new short[m_BuffSizeVS_Result2];
        }

        #region Connect/Disconnect PLC
        public bool StartPlcInterface()
        {
            // Main에서는 이 메서드만 호출해 PLC 연결과 백그라운드 Read/Write 루프를 시작한다.
            if (m_bPLC_Connected)
                return true;

            OpenConnectPLC();
            return m_bPLC_Connected;
        }

        public void StopPlcInterface()
        {
            // Main 정지 흐름에서 PLC 통신과 Vision 출력 버퍼를 함께 정리하는 진입점이다.
            CloseConnectPLC();
            ResetAllData();
        }

        public void OpenConnectPLC()
        {
            try
            {
                // PLC는 Ping 확인 후 Melsec TCP 세션을 열고 주기 Read/Write 스레드를 시작한다.
                if (ClassSystemConfig.Ins.m_ClsCommon.PingRespond(ClassSystemConfig.Ins.m_ClsCommon.m_strIpPLC, 100, 5))
                {
                    m_ClsMelsec.Initialize(ClassSystemConfig.Ins.m_ClsCommon.m_strIpPLC, ClassSystemConfig.Ins.m_ClsCommon.m_iPortPLC);
                    m_bPLC_Connected = m_ClsMelsec.objPLC_Client[0].Connected;
                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                    (m_bPLC_Connected ? "Connected PLC " : "Connect PLC Fail ") + ClassSystemConfig.Ins.m_ClsCommon.m_strIpPLC + " : " + ClassSystemConfig.Ins.m_ClsCommon.m_iPortPLC,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);

                    if (m_bPLC_Connected)
                    {
                        bLoopPLC = true;
                        m_ThreadPLC = new Thread(ThreadReadWritePLC);
                        m_ThreadPLC.Start();
                    }
                }
            }
            catch
            {
                m_bPLC_Connected = false;
            }

        }
        public void CloseConnectPLC()
        {
            try
            {
                // 통신 종료 시 루프 플래그를 먼저 내리고 PLC 소켓을 닫는다.
                if (m_ThreadPLC != null)
                {
                    bLoopPLC = false;

                    m_ThreadPLC.Abort();
                    Thread.Sleep(50);
                    m_ClsMelsec.DisConnect();
                    m_bPLC_Connected = false;
                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                    "Disconnected PLC",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                }
            }
            catch
            {
                m_bPLC_Connected = false;
            }
        }

        int iCntWrite = 0;
        public void ThreadReadWritePLC()
        {
            while (bLoopPLC)
            {
                if (m_bPLC_Connected)
                {
                    // PLC 입력 영역을 읽어 Alive/Reset/Trigger 상태를 내부 상태와 UI에 반영한다.
                    m_ClsMelsec.ReadWordFromPLC(0, m_AddrPLC_Status, m_ValuePLC_Status.Length, ref m_ValuePLC_Status);
                    Thread.Sleep(5);
                    m_ClsMelsec.ReadWordFromPLC(0, m_AddrPLC_Trigger, m_ValuePLC_Trigger.Length, ref m_ValuePLC_Trigger);
                    UpdateUI_PLCStatus();
                    UpdateUI_PLCTrigger();
                    CheckResetSignal();
                }

                // Vision 상태 영역은 주기적으로 PLC에 써서 설비 핸드셰이크를 유지한다.
                iCntWrite++;
                if (iCntWrite > 1)
                {
                    ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Alive = ClassSystemConfig.Ins.m_ClsCommon._Status.PLC_Alive; // GetBit(m_ValuePLC_Status[0], (int)PLC_STATUS.ALIVE);
                    // ALIVE
                    if (ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Alive)
                    {
                        SetBit(ref m_ValueVS_Status[0], (int)VISION_STATUS.ALIVE);
                    }
                    else
                    {
                        ResetBit(ref m_ValueVS_Status[0], (int)VISION_STATUS.ALIVE);
                    }
                    // VISION BUSY
                    if (ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Busy)
                    {
                        SetBit(ref m_ValueVS_Status[0], (int)VISION_STATUS.VS_BUSY);
                    }
                    else
                    {
                        ResetBit(ref m_ValueVS_Status[0], (int)VISION_STATUS.VS_BUSY);
                    }
                    // VISION READY
                    if (ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Ready)
                    {
                        SetBit(ref m_ValueVS_Status[0], (int)VISION_STATUS.READY);
                    }
                    else
                    {
                        ResetBit(ref m_ValueVS_Status[0], (int)VISION_STATUS.READY);
                    }

                    m_ClsMelsec.WriteWordFromPLC(0, m_AddrVS_Status, m_ValueVS_Status.Length, m_ValueVS_Status);
                    UpdateUI_VS_Status();
                    iCntWrite = 0;
                }

                Thread.Sleep(30);
            }
        }

        private void UpdateUI_PLCStatus()
        {
            try
            {
                ClassSystemConfig.Ins.m_ClsCommon._Status.PLC_Alive = GetBit(m_ValuePLC_Status[0], (int)PLC_STATUS.ALIVE);
                ClassSystemConfig.Ins.m_ClsCommon._Status.PLC_Reset = GetBit(m_ValuePLC_Status[0], (int)PLC_STATUS.RESET);

                if (ClassSystemConfig.Ins.m_ClsCommon._Status.PLC_Alive)
                {
                    ClassCommon.ControlStatusInvoke(statusPLC_ALIVE, ClassCommon.STATUS_COLOR.GREEN);
                    //main.UpdateAliveStatus(true);
                }
                else
                {
                    ClassCommon.ControlStatusInvoke(statusPLC_ALIVE, ClassCommon.STATUS_COLOR.GREY);
                    //main.UpdateAliveStatus(false);
                }


                if (ClassSystemConfig.Ins.m_ClsCommon._Status.PLC_Reset)
                    ClassCommon.ControlStatusInvoke(statusPLC_RESET, ClassCommon.STATUS_COLOR.GREEN);
                else
                    ClassCommon.ControlStatusInvoke(statusPLC_RESET, ClassCommon.STATUS_COLOR.GREY);
            }
            catch { }

        }
        public void UpdateUI_PLCTrigger()
        {
            try
            {
                ClassCommon.ControlStatusInvoke(statusPLC_Trigger1, m_ValuePLC_Trigger[0] > 0 ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
                ClassCommon.ControlStatusInvoke(statusPLC_Trigger2, m_ValuePLC_Trigger[1] > 0 ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
                ClassCommon.ControlStatusInvoke(statusPLC_Confirm1, m_ValuePLC_Trigger[2] > 0 ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
                ClassCommon.ControlStatusInvoke(statusPLC_Confirm2, m_ValuePLC_Trigger[3] > 0 ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);

                ClassCommon.CheckBoxInvoke(statusPLC_Trigger1, m_ValuePLC_Trigger[0] > 0);
                ClassCommon.CheckBoxInvoke(statusPLC_Trigger2, m_ValuePLC_Trigger[1] > 0);
                ClassCommon.CheckBoxInvoke(statusPLC_Confirm1, m_ValuePLC_Trigger[2] > 0);
                ClassCommon.CheckBoxInvoke(statusPLC_Confirm2, m_ValuePLC_Trigger[3] > 0);

            }
            catch { }
        }
        private void UpdateUI_VS_Status()
        {
            try
            {
                ClassCommon.ControlStatusInvoke(statusVS_ALIVE, ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Alive ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
                ClassCommon.ControlStatusInvoke(statusVS_READY, ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Ready ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
                ClassCommon.ControlStatusInvoke(statusVS_Busy, ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Busy ? ClassCommon.STATUS_COLOR.RED : ClassCommon.STATUS_COLOR.GREY);
                ClassCommon.ControlStatusInvoke(statusVS_RESET, ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Reset ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
            }
            catch { }
        }
        public void UpdateUI_VS_Trigger()
        {
            try
            {
                ClassCommon.ControlStatusInvoke(statusVS_DONE1, m_ValueVS_Trigger[(int)VISION_TRIGGER.TRG_DONE1] > 0 ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
                ClassCommon.ControlStatusInvoke(statusVS_DONE2, m_ValueVS_Trigger[(int)VISION_TRIGGER.TRG_DONE2] > 0 ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
                ClassCommon.ControlStatusInvoke(statusVS_COMPLETE1, m_ValueVS_Trigger[(int)VISION_TRIGGER.COMPLETE1] > 0 ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
                ClassCommon.ControlStatusInvoke(statusVS_COMPLETE2, m_ValueVS_Trigger[(int)VISION_TRIGGER.COMPLETE2] > 0 ? ClassCommon.STATUS_COLOR.GREEN : ClassCommon.STATUS_COLOR.GREY);
            }
            catch { }
        }
        public void UpdateUI_VS_Result1(int result, double angle, string qr_code)
        {
            try
            {
                ClassCommon.ControlTextInvoke(statusVS_RESULT1, result + "");
                ClassCommon.ControlTextInvoke(statusVS_ANGLE, angle.ToString("F3"));
                ClassCommon.ControlTextInvoke(statusVS_QRCode, qr_code);
            }
            catch { }
        }
        public void UpdateUI_VS_Result2()
        {
            try
            {
                ClassCommon.ControlTextInvoke(statusVS_RESULT2, m_ValueVS_ResultCAM2[(int)VISION_RESULT2.RESULT] + "");
            }
            catch { }
        }
        private void ResetAllData()
        {
            // PLC/Vision 버퍼 전체를 초기 상태로 돌려 다음 사이클 오동작을 막는다.
            Array.Clear(m_ValuePLC_Status, 0, m_ValuePLC_Status.Length);
            Array.Clear(m_ValuePLC_Trigger, 0, m_ValuePLC_Trigger.Length);

            // Clear Vision Buffer
            Array.Clear(m_ValueVS_Status, 0, m_ValueVS_Status.Length);
            Array.Clear(m_ValueVS_Trigger, 0, m_ValueVS_Trigger.Length);
            Array.Clear(m_ValueVS_ResultCAM1, 0, m_ValueVS_ResultCAM1.Length);
            Array.Clear(m_ValueVS_ResultCAM2, 0, m_ValueVS_ResultCAM2.Length);
        }
        public void CheckResetSignal()
        {
            if (ClassSystemConfig.Ins.m_ClsCommon._Status.PLC_Reset)
            {
                // PLC Reset 신호가 들어오면 Vision 출력과 검사 결과 버퍼를 모두 초기화한다.
                ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Error = false;
                ClassSystemConfig.Ins.m_ClsCommon._Status.PLC_Reset = false;
                ResetBit(ref m_ValuePLC_Status[0], (int)PLC_STATUS.RESET);

                if (m_bPLC_Connected)
                {
                    // Clear Vision Buffer
                    Array.Clear(m_ValueVS_Status, 0, m_ValueVS_Status.Length);
                    Array.Clear(m_ValueVS_Trigger, 0, m_ValueVS_Trigger.Length);
                    Array.Clear(m_ValueVS_ResultCAM1, 0, m_ValueVS_ResultCAM1.Length);
                    Array.Clear(m_ValueVS_ResultCAM2, 0, m_ValueVS_ResultCAM2.Length);

                    UpdateUI_VS_Status();
                    UpdateUI_VS_Trigger();
                    UpdateUI_VS_Result1(0, 0, "");
                    UpdateUI_VS_Result2();
                }
            }
        }
        #endregion

        private void btnManualReset_Click(object sender, EventArgs e)
        {
            ResetAllData();
            if (m_bPLC_Connected)
            {
                m_ClsMelsec.WriteWordFromPLC(0, m_AddrVS_Status, m_ValueVS_Status.Length, m_ValueVS_Status);
                m_ClsMelsec.WriteWordFromPLC(0, m_AddrVS_Trigger, m_ValueVS_Trigger.Length, m_ValueVS_Trigger);
                m_ClsMelsec.WriteWordFromPLC(0, m_AddrVS_Result1, m_ValueVS_ResultCAM1.Length, m_ValueVS_ResultCAM1);
                m_ClsMelsec.WriteWordFromPLC(0, m_AddrVS_Result2, m_ValueVS_ResultCAM2.Length, m_ValueVS_ResultCAM2);
            }


            UpdateUI_VS_Status();
            UpdateUI_VS_Trigger();
            UpdateUI_VS_Result1(0, 0, "");
            UpdateUI_VS_Result2();
        }
        public void SetBit(ref short shValue, int bitPosition)
        {
            shValue = (short)(shValue | (1 << bitPosition));
        }
        public void ResetBit(ref short shValue, int bitPosition)
        {
            shValue = (short)(shValue & (~(1 << bitPosition)));
        }
        public bool GetBit(short shValue, int bitPosition)
        {
            int temp = (shValue >> bitPosition) & 0x01;
            if (temp == 1)
                return true;
            else
                return false;
        }
        public enum eDATA
        {
            STATUS,
            TRIGGER,
            RESULT_CAM1,
            RESULT_CAM2
        }
        public void WriteDataToPLC(eDATA type, bool update_ui = true)
        {
            // Main/Vision 로직은 데이터 종류만 지정하고 실제 PLC 주소 매핑은 이 메서드에 모은다.
            if(IsConnectedPLC)
            switch (type)
            {
                case eDATA.STATUS:
                    m_ClsMelsec.WriteWordFromPLC(0, m_AddrVS_Status, m_ValueVS_Status.Length, m_ValueVS_Status);
                    if (update_ui)
                        UpdateUI_VS_Status();
                    break;
                case eDATA.TRIGGER:
                    m_ClsMelsec.WriteWordFromPLC(0, m_AddrVS_Trigger, m_ValueVS_Trigger.Length, m_ValueVS_Trigger);
                    if (update_ui)
                        UpdateUI_VS_Trigger();
                    break;
                case eDATA.RESULT_CAM1:
                    m_ClsMelsec.WriteWordFromPLC(0, m_AddrVS_Result1, m_ValueVS_ResultCAM1.Length, m_ValueVS_ResultCAM1);
                    break;
                case eDATA.RESULT_CAM2:
                    m_ClsMelsec.WriteWordFromPLC(0, m_AddrVS_Result2, m_ValueVS_ResultCAM2.Length, m_ValueVS_ResultCAM2);
                    break;
            }
        }
        public void WriteResultToPLC_CAM1(int result, double angle, string qr_code, bool update_ui = true)
        {
            // CAM1 결과는 OK/NG, 각도, QR 문자열을 Word 버퍼로 변환해 PLC에 전달한다.
            m_ValueVS_ResultCAM1[(int)VISION_RESULT1.RESULT] = (short)result;

            // Angle
            short[] buff1 = ConvertDoubleToShortArray(angle);
            m_ValueVS_ResultCAM1[(int)VISION_RESULT1.ANGLE] = buff1[0];
            m_ValueVS_ResultCAM1[(int)VISION_RESULT1.ANGLE + 1] = buff1[1];

            // QR Code
            short[] buff2 = GetShortArrayFromString(qr_code);
            if (buff2 != null && buff2.Length > 1)
            for (int i = 0; i < buff2.Length; i++ )
            {
                int index = i + (int)VISION_RESULT1.QR_CODE;
                if (index >= m_ValueVS_ResultCAM1.Length)
                    break;

                m_ValueVS_ResultCAM1[index] = buff2[i];
            }

            WriteDataToPLC(eDATA.RESULT_CAM1);

            if (update_ui)
                UpdateUI_VS_Result1(result, angle, qr_code);

            // 결과 쓰기 후 완료 비트를 올려 PLC가 다음 Confirm을 보낼 수 있게 한다.
            m_ValueVS_Trigger[(int)VISION_TRIGGER.COMPLETE1] = 1;
            WriteDataToPLC(eDATA.TRIGGER);

            if (update_ui)
                UpdateUI_VS_Trigger();
        }
        public void WriteResultToPLC_CAM2(int result, bool update_ui = true)
        {
            // CAM2는 단순 결과 중심으로 PLC Result 영역과 완료 비트를 갱신한다.
            m_ValueVS_ResultCAM2[(int)VISION_RESULT2.RESULT] = (short)result;

            WriteDataToPLC(eDATA.RESULT_CAM2);

            if (update_ui)
                UpdateUI_VS_Result2();


            // Write Complete Signal
            m_ValueVS_Trigger[(int)VISION_TRIGGER.COMPLETE2] = 1;
            WriteDataToPLC(eDATA.TRIGGER);

            if (update_ui)
                UpdateUI_VS_Trigger();

        }
        public void WriteToPLC_TriggerDone(int indexCam, int iDone, bool update_ui = true)
        {
            // 카메라 트리거를 수신했다는 응답 비트를 PLC에 반환한다.
            if (indexCam == 0)
                m_ValueVS_Trigger[(int)VISION_TRIGGER.TRG_DONE1] = (short)iDone;
            else
                if (indexCam == 1)
                m_ValueVS_Trigger[(int)VISION_TRIGGER.TRG_DONE2] = (short)iDone;

            WriteDataToPLC(eDATA.TRIGGER);

            if (update_ui)
                UpdateUI_VS_Trigger();
        }
        private short[] ConvertDoubleToShortArray(double double_input)
        {
            short[] data = new short[2];
            try
            {
                float fWrite = (float)double_input;
                byte[] res = BitConverter.GetBytes(fWrite);

                data[0] = (short)(res[0] | ((res[1] << 8) & 0xFF00));
                data[1] = (short)(res[2] | ((res[3] << 8) & 0xFF00));
            }
            catch
            { }
            return data;
        }
        public short[] GetShortArrayFromString(string strData)
        {
            short[] shArray = null;
            try
            {
                if (strData != null && strData.Length > 0)
                {
                    int iSizeArray = (int)Math.Round(strData.Length / 2.0, MidpointRounding.AwayFromZero);
                    shArray = new short[iSizeArray];
                    char[] charArray = strData.ToCharArray();
                    int iIncs = 0;
                    for (int iCnt = 0; iCnt < charArray.Length; )
                    {
                        short shLow = (short)charArray[iCnt];
                        short shHigh = 0;
                        if (iCnt + 1 < charArray.Length)
                            shHigh = (short)charArray[iCnt + 1];
                        shArray[iIncs] = (short)(shLow | (shHigh << 8));
                        iIncs++;
                        iCnt += 2;
                    }
                }

            }
            catch
            {

            }
            return shArray;
        }
        #endregion

        #region Event CAM1
        private void statusPLC_Trigger1_CheckedChanged(object sender, EventArgs e)
        {
            // Do st when Trigger CAM1
            if(statusPLC_Trigger1.Checked)
            {
               // chck condition
               if(ClassSystemConfig.Ins.m_ClsHIK[0].IsConnected)
                {
                    ClassSystemConfig.Ins.m_ClsHIK[0].SetTriggerSoftware(true);

                    // Respond Trigger Done

                    m_ValueVS_Trigger[(int)VISION_TRIGGER.TRG_DONE1] = 1;
                    WriteDataToPLC(eDATA.TRIGGER, true);
                }
            }
        }

        private void statusPLC_Confirm1_CheckedChanged(object sender, EventArgs e)
        {
            // Do st 
            if (statusPLC_Confirm1.Checked)
            {
                // Reset Vison signal of CAM1
                m_ValueVS_Trigger[(int)VISION_TRIGGER.TRG_DONE1] = 0;
                m_ValueVS_Trigger[(int)VISION_TRIGGER.COMPLETE1] = 0;
                WriteDataToPLC(eDATA.TRIGGER, true);
                UpdateUI_VS_Trigger();

                // Reset data
                // Array.Clear(m_ValueVS_ResultCAM1, 0, m_ValueVS_ResultCAM1.Length);
                //WriteResultToPLC_CAM1(0, 0, "", true);
                //WriteDataToPLC(eDATA.RESULT_CAM1, true);
            }
        }
        #endregion

        #region Event CAM2
        private void statusPLC_Trigger2_CheckedChanged(object sender, EventArgs e)
        {
            // Do st when Trigger CAM2
            if (statusPLC_Trigger2.Checked)
            {
                if(ClassSystemConfig.Ins.m_ClsHIK[1].IsConnected)
                ClassSystemConfig.Ins.m_ClsHIK[1].SetTriggerSoftware(true);

                m_ValueVS_Trigger[(int)VISION_TRIGGER.TRG_DONE2] = 1;
                WriteDataToPLC(eDATA.TRIGGER, true);
            }
        }

        private void statusPLC_Confirm2_CheckedChanged(object sender, EventArgs e)
        {
            // Do st 
            if (statusPLC_Confirm2.Checked)
            {
                // Reset Signal
                m_ValueVS_Trigger[(int)VISION_TRIGGER.TRG_DONE2] = 0;
                m_ValueVS_Trigger[(int)VISION_TRIGGER.COMPLETE2] = 0;
                WriteDataToPLC(eDATA.TRIGGER, true);
                UpdateUI_VS_Trigger();
            }
        }
        #endregion
    }
}
