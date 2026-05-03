using System;




namespace ReelTW
{
    public partial class Main : Form
    {
        #region Init variables 
        public Queue<ClsSaveImage> m_QueSaveImage;
        public Queue<ClsSaveLog> m_QueSaveLog;
        private Thread m_ThreadUpdate = null;
        private Thread m_ThreadSave = null;
        public int[] m_ListResultBuffer = new int[ClassCommon.MaxUserControl];
        public bool[] m_ListResultFlag = new bool[ClassCommon.MaxUserControl];
        //public eRunningMode m_eRunningMode = eRunningMode.AUTO;
        public bool m_IsLicenseActived = true;
        public const bool IS_DEBUG = true;
        public Bitmap[] m_ListImageBMP = new Bitmap[ClassCommon.MaxDevice];
        public eRunMode m_eRunningMode = eRunMode.AUTO;
        private bool m_bTowerCommunicationStarted = false;

        //public enum eRunningMode
        //{
        //    AUTO,
        //    SEMIAUTO
        //}
        #endregion
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern SafeFileHandle CreateFile(string lpFileName, FileAccess dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, FileMode dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        //[DllImport("kernel32.dll")]
        //private static extern int GetPrivateProfileString(    // GetIniValue 를 위해
        //    String section,
        //    String key,
        //    String def,
        //    StringBuilder retVal,
        //    int size,
        //    String filePath);

        public enum eRunMode
        {
            AUTO,
            MANUAL
        }

        public enum eServerType
        {
            LOADER = 0,
            INSPECT = 1,
            //UNLOADER = 1,
            NONE = 2
        }

        #region Main / Main_Load
        public Main()
        {
            CheckLastProcessRunning(true);

            StartLoadingForm();

            InitializeComponent();

            this.MaximizedBounds = Screen.FromHandle(this.Handle).WorkingArea;
            InitResizeForm();

            // Reset Buffer
            Array.Clear(m_ListResultFlag, 0, m_ListResultFlag.Length);
            Array.Clear(m_ListResultBuffer, 0, m_ListResultBuffer.Length);

            WriteXML();
            //UserCtrlInfoPC CtrlInfo = new UserCtrlInfoPC();
            //panelPCInfo.Controls.Add(CtrlInfo);
            //CtrlInfo.Dock = DockStyle.Fill;

        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            // 프로그램 기동 시 UI, 로그인, 카운터 등 기본 운전 환경을 먼저 구성한다.
            InitializeUI();
            ClassSystemConfig.Ins.m_ClsCommon.m_bLogInMode[(int)ClassCommon.LOGIN_LEVEL.ADMIN] = true;
            ShowUILogIn();
            this.WindowState = FormWindowState.Maximized;
            StopLoadingForm();
            ClassCommon.ControlTextInvoke(lbRunningState, "PROGRAM STARTED");
            LoadCounter();
            UpdateResultCount(false);

            // DB 사용 옵션이 켜진 경우 시작 시점에 검사 이력 저장 경로를 준비한다.
            if (!ClassSystemConfig.Ins.m_ClsSqlServer.IsConnectedDB_SQL && ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUseDB)
            {
                ClassSystemConfig.Ins.m_ClsSqlServer.ConnectDatabase(ClassSystemConfig.Ins.m_ClsCommon.m_strDataSource, ClassSystemConfig.Ins.m_ClsCommon.m_strDatabase, ClassSystemConfig.Ins.m_ClsCommon.m_strDBUser, ClassSystemConfig.Ins.m_ClsCommon.m_strDBPass);
                ClassSystemConfig.Ins.m_ClsSqlServer.DoProcessUpdateTable(DateTime.Now.ToString("yyyy_MM_dd"), "");
            }

            // 자재타워 IF는 설정값으로만 시작 여부를 판단해 기존 운전 로직과 분리한다.
            StartTowerCommunicationAsync();
        }

        #region Tower TCP/IP Interface
        private async void StartTowerCommunicationAsync()
        {
            if (!ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUseTower)
                return;

            if (m_bTowerCommunicationStarted && ClassSystemConfig.Ins.m_TowerService.IsConnected)
                return;

            // IP/Port 설정이 비정상이면 통신을 시작하지 않고 로그만 남긴다.
            string towerIp;
            int towerPort;
            if (!TryGetTowerEndpoint(out towerIp, out towerPort))
                return;

            try
            {
                // 이벤트 중복 등록을 방지한 뒤 타워 XML 메시지 처리 로그를 연결한다.
                ClassSystemConfig.Ins.m_TowerService.MessageHandled -= TowerService_MessageHandled;
                ClassSystemConfig.Ins.m_TowerService.MessageHandled += TowerService_MessageHandled;

                await ClassSystemConfig.Ins.m_TowerService.StartAsync(towerIp, towerPort);
                m_bTowerCommunicationStarted = true;

                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                        "Connected Tower TCP/IP " + towerIp + " : " + towerPort,
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
            catch (Exception ex)
            {
                m_bTowerCommunicationStarted = false;
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                        "StartTowerCommunicationAsync -> " + ex.Message,
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
        }

        private void StopTowerCommunication()
        {
            try
            {
                // 프로그램 정지 시 타워 통신 이벤트와 소켓을 함께 정리한다.
                ClassSystemConfig.Ins.m_TowerService.MessageHandled -= TowerService_MessageHandled;

                if (m_bTowerCommunicationStarted || ClassSystemConfig.Ins.m_TowerService.IsConnected)
                {
                    ClassSystemConfig.Ins.m_TowerService.Stop();
                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                            "Disconnected Tower TCP/IP",
                                                            ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                }
            }
            catch (Exception ex)
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                        "StopTowerCommunication -> " + ex.Message,
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
            finally
            {
                m_bTowerCommunicationStarted = false;
            }
        }

        private bool TryGetTowerEndpoint(out string towerIp, out int towerPort)
        {
            // 설정 파일에서 읽은 타워 접속 정보를 실제 연결 전에 최소 검증한다.
            towerIp = ClassSystemConfig.Ins.m_ClsCommon.m_strIpTower == null ? "" : ClassSystemConfig.Ins.m_ClsCommon.m_strIpTower.Trim();
            towerPort = ClassSystemConfig.Ins.m_ClsCommon.m_iPortTower;

            if (towerIp == "" || towerPort <= 0 || towerPort > 65535)
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                        "Invalid Tower TCP/IP endpoint " + towerIp + " : " + towerPort,
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                return false;
            }

            return true;
        }

        private void TowerService_MessageHandled(TowerCommunication.TowerMessage message)
        {
            // 타워 명령 처리 결과를 운전 로그에 남겨 추후 설비 이슈 추적에 사용한다.
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.DATA_RECEIVE,
                                                    "Tower handled: " + message.ToString(),
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        private void SaveTowerConfig(StreamWriter objWriter)
        {
            // 타워 TCP/IP 설정은 기존 DeviceConfig.ini 흐름에 맞춰 저장한다.
            objWriter.WriteLine("[TOWER_IP]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strIpTower);
            objWriter.WriteLine("[TOWER_PORT]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iPortTower);
            objWriter.WriteLine("[TOWER_ENABLE_USE]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUseTower ? 1 : 0));
        }

        private void LoadTowerConfig(string fileName)
        {
            // 타워 설정이 없을 때는 기본값으로 로딩해 신규 설치 환경에서도 실행되게 한다.
            ClassSystemConfig.Ins.m_ClsCommon.m_strIpTower = ClassCommon.GetConfig(fileName, "TOWER_IP", "192.168.10.50");
            ClassSystemConfig.Ins.m_ClsCommon.m_iPortTower = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(fileName, "TOWER_PORT", "5000"), 5000);
            ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUseTower = (ClassCommon.GetConfig(fileName, "TOWER_ENABLE_USE", "0").Trim() == "1") ? true : false;
        }
        #endregion


        private void InitResizeForm()
        {
            this.MinimumSize = new Size(1400, 700);
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);

        }
        private void InitializeUI()
        {
            LoadDeviceConfig(false);

            LoadPasLogin("User.xml");
            ClassSystemConfig.Ins.m_ClsCommon._Status.PROGRAM_LOOP_RUNNING = true;
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM,
                                                    string.Format("Program Started {0} - {1} - Released {2}", ClassCommon.ProgramName, ClassCommon.ProgramVersion, ClassCommon.ProgramReleasedDate),
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local,
                                                    true);

            for (int iCtrl = 0; iCtrl < ClassCommon.MaxUserControl; iCtrl++)
           
             {
                ClassSystemConfig.Ins.m_UserDisplay[iCtrl] = new UserCtrlDisplay(iCtrl, ClassSystemConfig.Ins.m_ClsCommon.m_ListNameControl[iCtrl], this);
            }

            tableLayoutPanelReel.Controls.Add(ClassSystemConfig.Ins.m_UserDisplay[0], 0, 0);
            tableLayoutPanelReel.Controls.Add(ClassSystemConfig.Ins.m_UserDisplay[1], 1, 0);

            tableLayoutPanelReel.Controls.Add(ClassSystemConfig.Ins.m_UserDisplay[2], 0, 1);
            tableLayoutPanelReel.Controls.Add(ClassSystemConfig.Ins.m_UserDisplay[3], 1, 1);

            for (int iCtrl = 0; iCtrl < 2; iCtrl++)
                //for (int iCtrl = 0; iCtrl < ClassCommon.MaxUserControl; iCtrl++)
                {
                ClassSystemConfig.Ins.m_UserDisplay[iCtrl].Dock = DockStyle.Fill;
            }

        

            UserCtrlInfoPC CtrlInfo = new UserCtrlInfoPC();
            panelPCInfo.Controls.Add(CtrlInfo);
            CtrlInfo.Dock = DockStyle.Fill;

            for (int iCam = 0; iCam < ClassCommon.MaxDevice; iCam++)
                ClassSystemConfig.Ins.m_ClsHIK[iCam] = new ClsHIK(this, iCam);

            ClassSystemConfig.Ins.m_FrmVision.Owner = this;
            ClassSystemConfig.Ins.m_FrmSetting.Owner = this;
            ClassSystemConfig.Ins.m_FrmLogin.Owner = this;
            ClassSystemConfig.Ins.m_FrmPLC.Owner = this;
            ClassSystemConfig.Ins.m_FrmVision.InitializeUI(this);
            ClassSystemConfig.Ins.m_FrmSetting.InitializeUI(this);
            ClassSystemConfig.Ins.m_FrmLogin.InitializeUI(this);
            ClassSystemConfig.Ins.m_FrmPLC.InitializeUI(this);

            panelBottom_.Controls.Add(ClassSystemConfig.Ins.m_UserLog);
            ClassSystemConfig.Ins.m_UserLog.Dock = DockStyle.Fill;

            // Load Config Model
            try
            {
                //ClassSystemConfig.Ins.m_FrmSpec.LoadModelConfig();
            }
            catch
            {

            }

            Thread.Sleep(100);


            if (ClassSystemConfig.Ins.m_FrmVision.LoadRecipeStartup(ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName, false))
            {
                ClassSystemConfig.Ins.m_FrmVision.SetModelSpec();
            }

            //this.lbTitleHeader.Text = String.Format("{0} {1} Released {2}", ClassCommon.ProgramName, ClassCommon.ProgramVersion, ClassCommon.ProgramReleasedDate);
            this.Text = String.Format("{0} {1} Released {2}", ClassCommon.ProgramName, ClassCommon.ProgramVersion, ClassCommon.ProgramReleasedDate); ;
            lbProgramName.Text = ClassCommon.ProgramName;
            lbProgramVersion.Text = ClassCommon.ProgramVersion;
            lbReleasedDate.Text = "DATE: " + ClassCommon.ProgramReleasedDate;

            lblIns.Dock = DockStyle.Fill;
            lblIns2.Dock = DockStyle.Fill;

            this.panelLeftHide.Visible = false;
            //this.panelRight.Visible = false;
            m_QueSaveImage = new Queue<ClsSaveImage>();
            m_QueSaveLog = new Queue<ClsSaveLog>();

            cbSaveLog.Checked = ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local;
            cbSaveGraphicImg.Checked = ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local;
            cbSaveOriginImg.Checked = ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local;
            if (ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSavingMode == 1)
                radioJPG.Checked = true;
            else
                radioBMP.Checked = true;

            cbAutoReconnect.Checked = ClassSystemConfig.Ins.m_ClsCommon.m_bAutoReconnect;
            cbShowProgressStatus.Checked = ClassSystemConfig.Ins.m_ClsCommon.m_bShowProgressStatus;
            cbShowGraphic.Checked = ClassSystemConfig.Ins.m_ClsCommon.m_bShowGraphic;

            InitListView();
            SetToolTip();
            //btnRefresh_Click(null, null);
            UpdateCurrentModel();

            // Register Event ReceivedData
            ClassSystemConfig.Ins.m_ClsHIK[0].ImageReceived += CAM1_ImageCallback;
            ClassSystemConfig.Ins.m_ClsHIK[1].ImageReceived += CAM2_ImageCallback;
            ClassSystemConfig.Ins.m_ClsClient = new ClassClient();
            ClassSystemConfig.Ins.m_ClsClient.EventReceived += EventReceivedData;
            ClassSystemConfig.Ins.m_ClsClient.EventClosed += EventServerClosed;

            m_ThreadUpdate = new Thread(ThreadUpdateStatus);
            m_ThreadUpdate.Start();

            m_ThreadSave = new Thread(ThreadSaveData);
            m_ThreadSave.Start();

            //m_ThreadRespondData = new Thread(ThreadRespondToPLC);
            //m_ThreadRespondData.Start();

        }
        #endregion

        public bool m_bEnableDryRun = false;
        private void cbShowRawImage_CheckedChanged(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_ClsCommon.m_bShowGraphic = cbShowGraphic.Checked;
        }
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

        #region check Last Process & Login Enable
        private void CheckLastProcessRunning(bool bShowMessage)
        {
            if (System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                if (bShowMessage)
                {
                    MessageBox.Show("Another process is running, Please End last process", ClassCommon.ProgramName + " " + ClassCommon.ProgramVersion);
                }

                this.Close();
                Application.ExitThread();
                Environment.Exit(0);
            }
        }
        public void ShowUILogIn()
        {
            if (ClassSystemConfig.Ins.m_ClsCommon.m_bLogInMode[(int)ClassCommon.LOGIN_LEVEL.OPERATOR])
            {
                btnLive_.Enabled = false;
                btnVision.Enabled = false;
                btnMainUI.Enabled = false;
            }
            else
                if (ClassSystemConfig.Ins.m_ClsCommon.m_bLogInMode[(int)ClassCommon.LOGIN_LEVEL.ENGINEER])
                {
                    btnLive_.Enabled = true;
                    btnVision.Enabled = false;
                    btnMainUI.Enabled = true;
                }
                else
                    if (ClassSystemConfig.Ins.m_ClsCommon.m_bLogInMode[(int)ClassCommon.LOGIN_LEVEL.ADMIN])
                    {
                        btnLive_.Enabled = true;
                        btnVision.Enabled = true;
                        btnMainUI.Enabled = true;
                    }
                    else
                    {
                        btnLive_.Enabled = false;
                        btnVision.Enabled = false;
                        btnMainUI.Enabled = false;
                    }

        }
        #endregion

        #region Check Save/Load Counter
        public void LoadCounter()
        {
            try
            {
                string Path = string.Format("{0}\\SystemCount.xml", Directory.GetCurrentDirectory());
                if (File.Exists(Path))
                    ClassSystemConfig.Ins.m_ClsCommon.ClsCount = ClassCommon.ReadFromXmlFile<ClassResultCounter>(Path);
            }
            catch { }
        }
        public void SaveCounter()
        {
            try
            {
                string Path = string.Format("{0}\\SystemCount.xml", Directory.GetCurrentDirectory());
                ClassCommon.WriteToXmlFile<ClassResultCounter>(Path, ClassSystemConfig.Ins.m_ClsCommon.ClsCount);
            }
            catch { }
        }

        double[] m_ListPercentCount = new double[3];
        public void UpdateResultCount(bool reset)
        {
            if (ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult == null)
                ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult = new int[3];

            if (reset)
                Array.Clear(ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult, 0, ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult.Length);

            int Total = ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[(int)ClassResultCounter.COUNT.TOTAL] = ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[(int)ClassResultCounter.COUNT.OK] + ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[(int)ClassResultCounter.COUNT.NG];
            if (Total > 0)
            {
                m_ListPercentCount[0] = ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[(int)ClassResultCounter.COUNT.OK] * 100.0 / (double)Total;
                m_ListPercentCount[1] = 100 - m_ListPercentCount[0];
            }
            else
            {
                m_ListPercentCount[0] = m_ListPercentCount[1] = 0;
            }

            if (listView1.InvokeRequired)
            {
                listView1.Invoke(new MethodInvoker(delegate
                {
                    UpdateRawListview(listView1, 0, new string[3] { "", ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[(int)ClassResultCounter.COUNT.OK] + "", m_ListPercentCount[(int)ClassResultCounter.COUNT.OK].ToString("F2") });
                    UpdateRawListview(listView1, 1, new string[3] { "", ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[(int)ClassResultCounter.COUNT.NG] + "", m_ListPercentCount[(int)ClassResultCounter.COUNT.NG].ToString("F2") });
                    UpdateRawListview(listView1, 2, new string[3] { "", Total + "", Total > 0 ? "100" : "0" });
                }));
            }
            else
            {
                UpdateRawListview(listView1, 0, new string[3] { "", ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[(int)ClassResultCounter.COUNT.OK] + "", m_ListPercentCount[(int)ClassResultCounter.COUNT.OK].ToString("F2") });
                UpdateRawListview(listView1, 1, new string[3] { "", ClassSystemConfig.Ins.m_ClsCommon.ClsCount.ListCountResult[(int)ClassResultCounter.COUNT.NG] + "", m_ListPercentCount[(int)ClassResultCounter.COUNT.NG].ToString("F2") });
                UpdateRawListview(listView1, 2, new string[3] { "", Total + "", Total > 0 ? "100" : "0" });
            }


        }
        private void UpdateRawListview(ListView listview, int iIndex, string[] rowData)
        {
            try
            {
                if (listView1.InvokeRequired)
                {
                    listView1.Invoke(new MethodInvoker(delegate
                    {
                        if (listview.Items.Count > iIndex)
                        {
                            if (iIndex >= 0 && iIndex <= ClassCommon.MaxGradeNumber)
                            {
                                if (rowData != null)
                                {
                                    listview.Items[iIndex].SubItems[1].Text = (rowData.Length > 1) ? rowData[1] : "0";
                                    listview.Items[iIndex].SubItems[2].Text = (rowData.Length > 2) ? rowData[2] : "0";
                                }
                                else
                                {
                                    listview.Items[iIndex].SubItems[1].Text = "0";
                                    listview.Items[iIndex].SubItems[2].Text = "0";
                                }

                            }

                        }
                    }));
                }
                else
                {
                    if (listview.Items.Count > iIndex)
                    {
                        if (iIndex >= 0 && iIndex <= ClassCommon.MaxGradeNumber)
                        {
                            if (rowData != null)
                            {
                                listview.Items[iIndex].SubItems[1].Text = (rowData.Length > 1) ? rowData[1] : "0";
                                listview.Items[iIndex].SubItems[2].Text = (rowData.Length > 2) ? rowData[2] : "0";
                            }
                            else
                            {
                                listview.Items[iIndex].SubItems[1].Text = "0";
                                listview.Items[iIndex].SubItems[2].Text = "0";
                            }

                        }

                    }
                }


            }
            catch
            {

            }
        }
        #endregion

        #region Thread m_threadLoadingForm;
        Thread m_threadLoadingForm;
        FormStartupLoading m_FrmLoad;

        public void StartLoadingForm()
        {
            m_threadLoadingForm = new Thread(new ThreadStart(LoadingForm));
            m_threadLoadingForm.Start();
        }
        private void LoadingForm()
        {
            try
            {
                m_FrmLoad = new FormStartupLoading();
                Application.Run(m_FrmLoad);

            }
            catch (System.Threading.ThreadAbortException ex)
            {
                Console.WriteLine("=>Throw: " + ex.Message);
                throw ex;

            }


        }

        public void StopLoadingForm()
        {
            try
            {
                m_FrmLoad.Invoke(new MethodInvoker(delegate { m_FrmLoad.Hide(); }));

                if (m_threadLoadingForm != null)
                {
                    m_threadLoadingForm.Abort();
                }

            }
            catch
            {

            }

        }
        #endregion

        #region CLick Control Handler

        bool bHideLeftPanel = false;
        private void btnMainUI_MouseHover(object sender, EventArgs e)
        {
            this.toolTip1.SetToolTip(this.btnMainUI, "HOME");
        }

        private void btnConfig_MouseHover(object sender, EventArgs e)
        {
            this.toolTip1.SetToolTip(this.btnConfig, "SETTING");
        }

        private void btnSaveConfig_MouseHover(object sender, EventArgs e)
        {
            //this.toolTip3.SetToolTip(this.btnSaveConfig_, "SAVE");
        }

        private void btnMainUI_Click_1(object sender, EventArgs e)
        {
            //ChangeButtonColorCLick(sender);
            if (bHideLeftPanel == true)
                //bHideLeftPanel = !bHideLeftPanel;
                //panelLeftHide.Visible = bHideLeftPanel;
                panelLeftHide.Visible = false;

            //groupBoxCAM.Visible = true;
            //groupBoxSetting.Visible = true;
            //groupBoxSettingVision.Visible = false;
            //lbQuickSetting.Visible = true;
        }


        private void btnExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("Do you want to exit Application?", "Exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
            {
                ClassSystemConfig.Ins.m_ClsCommon._Status.PROGRAM_LOOP_RUNNING = false;
                ClassSystemConfig.Ins.m_ClsCommon.m_bAutoReconnect = false;
                if (m_ThreadUpdate != null)
                    m_ThreadUpdate.Abort();

                if (m_ThreadSave != null)
                    m_ThreadSave.Abort();

                btnStop_Click(this, null);

                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.PROGRAM, "Program Stopped", ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                SaveCounter();

                Thread.Sleep(100);
                Application.ExitThread();
                Environment.Exit(0);
            }
            else
            {
                e.Cancel = true;
            }
        }



        private void btnVision_Click(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_FrmVision.Show();
            ClassSystemConfig.Ins.m_FrmVision.ShowOnScreen();
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Vision Setting",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        private const int cGrip = 16;      // Grip size
        private const int cCaption = 32;   // Caption bar height;
        protected override void WndProc(ref Message m)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                if (m.Msg == 0x84)
                {  // Trap WM_NCHITTEST
                    Point pos = new Point(m.LParam.ToInt32());
                    pos = this.PointToClient(pos);
                    if (pos.Y < cCaption)
                    {
                        m.Result = (IntPtr)2;  // HTCAPTION
                        return;
                    }
                    if (pos.X >= this.ClientSize.Width - cGrip && pos.Y >= this.ClientSize.Height - cGrip)
                    {
                        m.Result = (IntPtr)17; // HTBOTTOMRIGHT
                        return;
                    }
                }
            }
            base.WndProc(ref m);
        }

        private void btnMaximum_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
        }

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }



        public void LoadPasLogin(string fileSaving)
        {
            List<string> listPassTemp = new List<string>();
            try
            {
                string pathSaving = Directory.GetCurrentDirectory() + "\\" + "Config Setting";
                if (File.Exists(pathSaving + "\\" + fileSaving))
                {
                    XmlDataDocument xmlDoc = new XmlDataDocument();
                    XmlNodeList xmlNode;


                    FileStream fs = new FileStream(pathSaving + "\\" + fileSaving, FileMode.Open, FileAccess.Read);
                    xmlDoc.Load(fs);
                    xmlNode = xmlDoc.GetElementsByTagName("Users");
                    for (int iElem = 0; iElem <= xmlNode.Count - 1; iElem++)
                    {
                        for (int iChil = 0; iChil < xmlNode[iElem].ChildNodes.Count; iChil++)
                        {

                            listPassTemp.Add(xmlNode[iElem].ChildNodes.Item(iChil).InnerText.Trim());

                        }
                    }

                    fs.Close();
                }
            }
            catch
            {

            }

            try
            {
                ClassSystemConfig.Ins.m_ClsCommon.m_ListPasswordLogin[0] = (listPassTemp.Count > 0) ? ClassSystemConfig.Ins.m_ClsCommon.ConvertByteStringToASCII(listPassTemp[0]) : "1";
                ClassSystemConfig.Ins.m_ClsCommon.m_ListPasswordLogin[1] = (listPassTemp.Count > 1) ? ClassSystemConfig.Ins.m_ClsCommon.ConvertByteStringToASCII(listPassTemp[1]) : "123";
                ClassSystemConfig.Ins.m_ClsCommon.m_ListPasswordLogin[2] = (listPassTemp.Count > 2) ? ClassSystemConfig.Ins.m_ClsCommon.ConvertByteStringToASCII(listPassTemp[2]) : "123!";
            }
            catch
            {

            }
        }
        #endregion

        [XmlRoot("Request ID", Namespace = "HKTM",
        IsNullable = false)]
        public class RequestID
        {
            public Toplevel REELSYS;
            
            //public String BASEID;
            //public String BARCODETYPE;
            //public String RETRY;
            //public String REQUESTID;
        }

        //public class reelsys
        public class Toplevel
        {
            // The XmlAttribute attribute instructs the XmlSerializer to serialize the
            // Name field as an XML attribute instead of an XML element (XML element is
            // the default behavior).
            [XmlAttribute]
            public string ID;
            public string NAME;

            // Setting the IsNullable property to false instructs the
            // XmlSerializer that the XML attribute will not appear if
            // the City field is set to a null reference.

            [XmlElement(IsNullable = false)]
            public String BASEID;
            public String BARCODETYPE;
            public String RETRY;
            public String REQUESTID;
        }

        public static void WriteXML()
        {
            RequestID overview = new RequestID();
            DateTime thisDate1 = DateTime.Now;

            Toplevel rid = new Toplevel();
            rid.ID = "s6f7";
            rid.NAME = "REELLOADCHECK";
            rid.BASEID = "a";
            rid.BARCODETYPE = "b";
            rid.RETRY = "N";
            rid.REQUESTID = thisDate1.ToString("yyyyMMddHHmmssff");

            overview.REELSYS = rid;

            //overview.BASEID = "B";
            //overview.BARCODETYPE = "C";
            //overview.RETRY = "D";
            //overview.REQUESTID = thisDate1.ToString("yyyyMMddHHmmssff");

            System.Xml.Serialization.XmlSerializer writer =
                new System.Xml.Serialization.XmlSerializer(typeof(RequestID));

            var path = Directory.GetCurrentDirectory() + @"\Request ReelID.xml";
            //var path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "//Request ReelID.xml";
            System.IO.FileStream file = System.IO.File.Create(path);

            writer.Serialize(file, overview);
            file.Close();
        }


        #region Listview
        string[] ListFieldName = new string[] { "Name", "Count", "Rate(%)" };
        private void InitListView()
        {
            // Listview 1
            listView1.GridLines = false;
            listView1.View = View.Details;


            for (int i = 0; i < ListFieldName.Length; i++)
            {
                listView1.Columns.Add(ListFieldName[i]);
            }

            int iWidthTemp = 0;
            for (int i = 0; i < listView1.Columns.Count; i++)
            {
                listView1.Columns[i].TextAlign = HorizontalAlignment.Center;
                listView1.Columns[i].Width = listView1.Width / ListFieldName.Length;
                if (i < ListFieldName.Length - 1)
                {
                    iWidthTemp += listView1.Columns[i].Width;
                }
            }
            listView1.Columns[ListFieldName.Length - 1].Width = listView1.Width - iWidthTemp;
            SetHeightListView(listView1, 32);

            // Init value Listview
            string[] strRow = new string[3];
            strRow = new string[] { "OK", "0", "0.00" };
            AddRowListView(listView1, strRow, true);
            strRow = new string[] { "NG", "0", "0.00" };
            AddRowListView(listView1, strRow, true);
            strRow = new string[] { "TOTAL", "0", "0.00" };
            AddRowListView(listView1, strRow, true);

            if (listView1.Items.Count > 0)
                listView1.Items[listView1.Items.Count - 1].ForeColor = Color.Gold;
        }

        private void AddRowListView(ListView listview, string[] rowData, bool IsChangeColorRow)
        {
            try
            {
                ListViewItem item = new ListViewItem(rowData);
                listview.Items.Add(item);

                if (IsChangeColorRow)
                    ChangeColorListView(listview);
            }
            catch
            {

            }
        }

        private void ChangeColorListView(ListView listview)
        {
            int lines = listview.Items.Count;
            for (int i = 0; i < lines; i++)
            {
                if (i % 2 == 0)
                    listview.Items[i].BackColor = Color.FromArgb(25, 25, 28);

                else
                    listview.Items[i].BackColor = Color.FromArgb(32, 32, 38);
            }

        }

        private void SetHeightListView(ListView listView, int height)
        {
            ImageList imgList = new ImageList();
            imgList.ImageSize = new Size(1, height);
            listView.SmallImageList = imgList;
        }
        #endregion

        #region Thread Save Data
        private void ThreadSaveData()
        {
            while (ClassSystemConfig.Ins.m_ClsCommon._Status.PROGRAM_LOOP_RUNNING)
            {
                try
                {
                    if (m_QueSaveImage != null && m_QueSaveImage.Count > 0)
                    {
                        ClsSaveImage dataBuff = m_QueSaveImage.Dequeue();
                        if (dataBuff == null)
                            return;

                        if (dataBuff.CamIndex >= 0)
                        {
                            ClassSystemConfig.Ins.m_ClsFunc.SaveImage(dataBuff.StrOKNG, dataBuff.Name, dataBuff.OriginImage, dataBuff.GraphicImage);
                        }

                        // Export CSV File
                        //if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local)
                        //{
                        //    ClassSystemConfig.Ins.m_ClsFunc.SaveCSVLog(dataBuff.SavingTime, dataBuff.RunMode, dataBuff.StrCodeID, dataBuff.PocketIndex, dataBuff.FinalResult
                        //                                                , dataBuff.ListPointResult, dataBuff.ListLogHeader, dataBuff.ListLogValue);
                        //}
                    }
                }
                catch (Exception ex)
                {
                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "EX -> ThreadSaveData " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                }
                Thread.Sleep(50);
            }
        }
        #endregion

        #region Event Client(TCP) Callback
        public void EventServerClosed()
        {

        }

        public void EventReceivedData(string data)
        {
            try
            {
                if (data == null)
                    return;
                else
                    if (data.Contains("\0"))
                    data = data.Replace("\0", "");

                data = data.Trim().TrimEnd('\0');
                m_TimeLastAlive = DateTime.Now;
                ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Alive = !ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Alive;
                UpdateAliveStatus(ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Alive);

                // DO ST After get data


            }
            catch (Exception ex)
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "EventReceivedData -> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
        }
        #endregion

        #region Camera Callback

        bool m_bIsManualTrigger = false;
        private void btnTrigger1_Click(object sender, EventArgs e)
        {
            
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                            "Clicked TRIGGER CAMERA " + 0,
                                            ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            

            //ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
            //                            "Clicked TRIGGERB CAMERA",
            //                            ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);

            m_bIsManualTrigger = true;
            m_eRunningMode = eRunMode.MANUAL;

            DoTriggerCamera(index_cam: 0);
        }

        private void DoTriggerCamera(int index_cam)
        {
            // Check condition
            if (index_cam < 0 && index_cam >= ClassCommon.MaxDevice) //ClassSystemConfig.Ins.m_ClsHIK.Length
            {
                return;
            }

            

            // Do trigger 
            if (ClassSystemConfig.Ins.m_ClsHIK[index_cam].IsConnected)
            {
                ClassSystemConfig.Ins.m_ClsHIK[index_cam].SetExposure((float)ClassSystemConfig.Ins.m_ClsModelConfig.ListExposure[index_cam]);
                ClassSystemConfig.Ins.m_ClsHIK[index_cam].SetGain((float)ClassSystemConfig.Ins.m_ClsModelConfig.ListGain[index_cam]);

                ClassSystemConfig.Ins.m_ClsHIK[index_cam].SetTriggerSoftware(true);
            }
        }

        bool m_bIsBusyCam1 = false, m_bIsBusyCam2 = false;
        private void CAM1_ImageCallback(Bitmap imageBmp)
        {
            if (!IsLiveViewMode)
            {
                if(!m_bIsBusyCam1)
                {
                    // Show Image
                    ClassSystemConfig.Ins.m_UserDisplay[0].SetImage(imageBmp);

                    // Do process
                    Bitmap inputImg = (Bitmap)imageBmp.Clone();
                    ProcessDataCAM1(inputImg);
                    imageBmp.Dispose();
                }

            }
            else
            {
                
                m_bIsBusyCam1 = true;
                ClassSystemConfig.Ins.m_UserDisplay[0].SetImage(imageBmp);
                Thread.Sleep(2);
                m_bIsBusyCam1 = false;
                GC.Collect();
            }
        }

        private void CAM2_ImageCallback(Bitmap imageBmp)
        {
            if (!IsLiveViewMode)
            {
                if (!m_bIsBusyCam2)
                {
                    // Show Image
                    ClassSystemConfig.Ins.m_UserDisplay[1].SetImage(imageBmp);

                    // Do process
                    ProcessDataCAM2(imageBmp);
                }

            }
            else
            {

                m_bIsBusyCam2 = true;
                ClassSystemConfig.Ins.m_UserDisplay[1].SetImage(imageBmp);
                Thread.Sleep(2);
                m_bIsBusyCam2 = false;
                GC.Collect();
            }
        }
        #endregion

        bool m_bIsLiveOn = false;
        private void CAM2_Unloader_ImageReceived(Bitmap inputImage)
        {
            try
            {
                if (m_bIsLiveOn)
                {
                    ClassSystemConfig.Ins.m_UserDisplay[1].SetImage(inputImage);
                }
                else
                    if (m_bIsManualTrigger)
                {
                    Bitmap bmpImage = (Bitmap)inputImage.Clone();
                    ProcessDataQRCode(1, bmpImage, "14");
                    inputImage.Dispose();
                    m_bIsManualTrigger = false;
                }
            }
            catch { }
            GC.Collect();
        }


        Mutex m_mutexLoader = new Mutex();
        private void ProcessDataQRCode(int indexCam, Bitmap inputImage, string cmd_value)
        {
            if (indexCam < 0 || indexCam > 1)
            {
                return;
            }

            m_mutexLoader.WaitOne();

            // Show Image
            ClassSystemConfig.Ins.m_UserDisplay[indexCam].SetImage(inputImage);

            // Run VisionTool
            string strCodeID = "";
            int iResult = 0;
            bool IsTrayID = false;
            string strData = "";


            switch (cmd_value.Trim())
            {
                case "11":
                    IsTrayID = true;
                    ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, indexCam, indexCam, true, false, IsTrayID, ref strCodeID, ref iResult);
                    strData = string.Format("{0}", strCodeID);
                    break;
                case "12":
                    ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, indexCam, indexCam, false, true, IsTrayID, ref strCodeID, ref iResult);
                    strData = string.Format("{0}", iResult);
                    break;
                case "13":
                    ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, indexCam, indexCam, true, false, IsTrayID, ref strCodeID, ref iResult);
                    strData = string.Format("{0}", strCodeID);
                    break;
                case "14":
                    ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, indexCam, indexCam, true, true, IsTrayID, ref strCodeID, ref iResult);
                    strData = string.Format("{0};{1}", iResult, strCodeID);
                    break;
                default:
                    ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, indexCam, indexCam, true, true, IsTrayID, ref strCodeID, ref iResult);
                    break;

                
            }

            // Send Data
            SendToPLC((int)eServerType.LOADER, strData);

            m_mutexLoader.ReleaseMutex();
        }

        

        public void SendToPLC(int index_server, string data, int time_delay = 0, bool show_log = true)
        {
            try
            {
                //if (index_server >= 0 && index_server < ClassSystemConfig.Ins.m_ClsClient.Length)
                    if (index_server >= 0)
                        if (ClassSystemConfig.Ins.m_ClsClient.ClientConnected)
                    {
                        if (time_delay > 0)
                            Thread.Sleep(time_delay);
                        ClassSystemConfig.Ins.m_ClsClient.SendData(data);

                        if (show_log)
                        {
                            ClassSystemConfig.Ins.m_UserLog.SetLogOnline(string.Format("VS -> PLC: {0}", data));
                            switch (index_server)
                            {
                                case (int)eServerType.LOADER:
                                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.LOADER,
                                                        string.Format("VS -> PLC: {0}", data),
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, false);
                                    break;
                                //case (int)eServerType.UNLOADER:
                                //    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.UNLOADER,
                                //                        string.Format("VS -> PLC: {0}", data),
                                //                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, false);
                                //    break;
                                case (int)eServerType.INSPECT:
                                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.INSP_CNT,
                                                        string.Format("VS -> PLC: {0}", data),
                                                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, false);
                                    break;
                            }
                        }
                    }
            }
            catch (Exception ex)
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "SendToPLC -> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
        }

        #region Processing Data
        private void ProcessDataCAM1(Bitmap inputBmp)
        {
            // Check Condition

            // Init variables
            int iResult = 0;
            string strQRCode = "";
            double angle = 0;

            // Run Vpro (as ToolBlock)
            Cognex.VisionPro.ICogImage cogImage = new Cognex.VisionPro.CogImage8Grey(inputBmp);
            ClassSystemConfig.Ins.m_FrmVision.RunProcessCAM1(cogImage, 0, 0, ref iResult, ref strQRCode, ref angle);


            // Process result and send to MES
            // 1. Send PLC Result
            ClassSystemConfig.Ins.m_FrmPLC.WriteResultToPLC_CAM1(iResult, angle, strQRCode);

            bool bCompleted = false;
            // 2. Send To MES ...


            // Wait respond (feedback)
            DateTime time_start = DateTime.Now;
            while(!bCompleted)
            {
                // Do Waiting...

                var delta = (DateTime.Now - time_start).TotalMilliseconds;
                if(delta > 2500) // Over 2500ms -> break
                {
                    break;
                }
                Thread.Sleep(5);
            }
            


            // Print data Zebra

            // ProduceNo, qty - from vision tool, strQRCode
            // ReelID,  StockLocation,    MSL - from MES
            string strData = string.Format("{0}", strQRCode);

            string[] words = strQRCode.Split('!');

            string ProduceNo = "";
            string Qty = "";

            ProduceNo = words[0].ToString();
            Qty = words[4].ToString();
            //Do_Zebra(ProduceNo,  ReelID,  StockLocation,  Qty,  MSL)


        }

        private void ProcessDataCAM2(Bitmap inputBmp)
        {
            // Check Condition
            // Respond PLC that Camera trigger done
            if(ClassSystemConfig.Ins.m_FrmPLC.IsConnectedPLC)
            {
                ClassSystemConfig.Ins.m_FrmPLC.WriteToPLC_TriggerDone(0, 1, true);
            }

            // Init variables
            int iResult = 0;
            string strQRCode = "";

            // Run Vpro (as ToolBlock)
            Cognex.VisionPro.ICogImage cogImage = new Cognex.VisionPro.CogImage8Grey(inputBmp);
            ClassSystemConfig.Ins.m_FrmVision.RunProcessCAM2(cogImage, 0, 0, ref iResult, ref strQRCode);

            // Process result and send to MES
            // Reset Trigger Done
            if (ClassSystemConfig.Ins.m_FrmPLC.IsConnectedPLC)
            {
                ClassSystemConfig.Ins.m_FrmPLC.WriteToPLC_TriggerDone(0, 0, true);
            }

            // 1. Send PLC Result
            ClassSystemConfig.Ins.m_FrmPLC.WriteResultToPLC_CAM2(iResult);

            // Process result 

            // Save Data to Buffer

        }
        #endregion

        #region Tooltip and Load/Save Device Config
        private void SetToolTip()
        {
            ToolTip _toolTip = new ToolTip();
            _toolTip.AutomaticDelay = 100;
            //_toolTip.SetToolTip(btnLoginIcon, "Log In");
            _toolTip.SetToolTip(btnExit, "Exit");
            _toolTip.SetToolTip(btnMinimize, "Minimize");
            _toolTip.SetToolTip(btnMaximum, "Maximum");
           
            _toolTip.SetToolTip(btnSaveConfig_, "Save Configs");
        
        }

      




        private void Do_Zebra(string ProduceNo, string ReelID, string StockLocation, string Qty, string MSL)
        {
            string DATA = "";
            string qr_date = DateTime.Now.ToString("yyyyMMdd");
            //string print_path = Directory.GetCurrentDirectory() + @"\Config Setting\DeviceConfig.ini";
            ClassFileCtl cl = new ClassFileCtl();
            //string print_path = ClassSystemConfig.Ins.m_ClsFileCtl.GetConfigValues("BARCODE", "PRINT");
            string print_path = cl.GetConfigValues("BARCODE", "PRINT");

            //string ProduceNo, ReelID, StockLocation, Qty, MSL;


            DATA = "^XA";
            DATA = DATA + "^CF0,40";
            DATA = DATA + "^FO20,30^FDLGE P/N : 0RH0332B622^FS";    //ProduceNo
            DATA = DATA + "^FO20,80^FDReel ID : RL201512260397^FS"; //ReelID
            DATA = DATA + "^FO20,130^FDStock Loc : A-19-05^FS"; //StockLocation
            DATA = DATA + "^FO20,180^FDQTY: 10000^FS";  //Qty

            DATA = DATA + "^FO240,180^FDMSL : Non^FS";  //MSL
            DATA = DATA + "^FO520,20";
            DATA = DATA + "^BQN,2,7";
            DATA = DATA + "^FDMM,ARL201512260397^FS";

            //DATA = DATA + "^FO530,190^FD20220120^FS";
            DATA = DATA + "^FO510,190^FD " + qr_date + "^FS";


            

            DATA = DATA + "^XZ";

            // Create a buffer with the command
            Byte[] buffer = new byte[DATA.Length];
          

            //serial port
            buffer = System.Text.Encoding.ASCII.GetBytes(DATA);
            // Use the CreateFile external func to connect to the LPT1 port
            //SafeFileHandle printer = CreateFile(txtprint_path.Text + @"\11", FileAccess.ReadWrite, 0, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
            SafeFileHandle printer = CreateFile(print_path, FileAccess.ReadWrite, 0, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

            if (printer.IsInvalid == true)
            {
                return;
            }

            FileStream lpt1 = new FileStream(printer, FileAccess.ReadWrite);
            lpt1.Write(buffer, 0, buffer.Length);
            lpt1.Close();
            

        

        }

        public void SaveDeviceConfig(bool ShowMessage)
        {
            string file_name = Directory.GetCurrentDirectory() + @"\Config Setting\DeviceConfig.ini";

            if (!System.IO.File.Exists(file_name))
            {
                System.IO.Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"\Config Setting");
            }

            try
            {
                using (StreamWriter objWriter = new StreamWriter(file_name))
                {
                    // IP CAM VISION
                    for (int iControl = 0; iControl < ClassCommon.MaxDevice; iControl++)
                        objWriter.WriteLine(String.Format("[IP CAM VISION {0}]  {1}", iControl + 1, ClassSystemConfig.Ins.m_ClsCommon.m_ListIPCAM[iControl]));

                    // USER CONTROL
                    for (int iControl = 0; iControl < ClassCommon.MaxUserControl; iControl++)
                        objWriter.WriteLine(String.Format("[CONTROL NAME {0}]  {1}", iControl + 1, ClassSystemConfig.Ins.m_ClsCommon.m_ListNameControl[iControl]));

                    for (int iControl = 0; iControl < ClassCommon.MaxDevice; iControl++)
                        objWriter.WriteLine(String.Format("[CONTROL ENABLE {0}]  {1}", iControl + 1, ClassSystemConfig.Ins.m_ClsCommon.m_ListEnableControl[iControl] ? 1 : 0));

                    // Exposure & Gain
                    for (int iControl = 0; iControl < ClassCommon.MaxDevice; iControl++)
                        objWriter.WriteLine(String.Format("[CAM EXPOSURE {0}]  {1}", iControl + 1, ClassSystemConfig.Ins.m_ClsCommon.m_dListExposure[iControl]));
                    for (int iControl = 0; iControl < ClassCommon.MaxDevice; iControl++)
                        objWriter.WriteLine(String.Format("[CAM GAIN {0}]  {1}", iControl + 1, ClassSystemConfig.Ins.m_ClsCommon.m_dListGain[iControl]));

                    objWriter.WriteLine("[MODE_MODEL_SIZE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_IModeModelSize);

                    // LOGIN
                    objWriter.WriteLine("[LOGIN USER]  " + ClassSystemConfig.Ins.m_ClsCommon.m_ListUserLogin[0]);
                    objWriter.WriteLine("[LOGIN PASS]  " + "***");

                    // SAVING
                    objWriter.WriteLine("[IS SAVE IMG_LOCAL]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE GRAPHIC IMG LOCAL]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE LOG LOCAL]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE IMG_FTP]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_FTP ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE GRAPHIC IMG FTP]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_FTP ? 1 : 0));
                    objWriter.WriteLine("[IS SAVE LOG FTP]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_FTP ? 1 : 0));

                    objWriter.WriteLine("[IS DELETE AUTO]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bAutoDeleteImg ? 1 : 0));
                    objWriter.WriteLine("[PERIOD DELETE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iPeriodDelete);
                    objWriter.WriteLine("[COMMON PATH]  " + ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath);

                    objWriter.WriteLine("[SHOW GRAPHIC]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bShowGraphic ? "1" : "0"));
                    objWriter.WriteLine("[SHOW ORIGIN]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bShowOrigin ? "1" : "0"));
                    objWriter.WriteLine("[SHOW PROGRESS STATUS]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bShowProgressStatus ? "1" : "0"));

                    // Client/Server
                    objWriter.WriteLine("[IP_CLIENT_1]  " + ClassSystemConfig.Ins.m_ClsCommon.m_ListIPClient[0]);
                    objWriter.WriteLine("[PORT_CLIENT_1]  " + ClassSystemConfig.Ins.m_ClsCommon.m_ListPortClient[0]);
                    objWriter.WriteLine("[IP_SERVER_1]  " + ClassSystemConfig.Ins.m_ClsCommon.m_ListIPServer[0]);
                    objWriter.WriteLine("[PORT_SERVER_1]  " + ClassSystemConfig.Ins.m_ClsCommon.m_ListPortServer[0]);

                    objWriter.WriteLine("[AUTO RECONNECT]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bAutoReconnect ? 1 : 0));

                    objWriter.WriteLine("[FTP_SAVING_PATH]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strFTPPath);
                    objWriter.WriteLine("[IS_FTP_SAVING]  " + (ClassSystemConfig.Ins.m_ClsCommon.IsSaveByFTP ? 1 : 0));
                    objWriter.WriteLine("[USER_FTP]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strUserFTP);
                    objWriter.WriteLine("[PASSWORD_FTP]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strPasswordFTP);

                    // PLC
                    objWriter.WriteLine("[PLC_IP]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strIpPLC);
                    objWriter.WriteLine("[PLC_PORT]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iPortPLC);
                    objWriter.WriteLine("[PLC_ENABLE_USE]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUsePLC ? 1 : 0));

                    // 자재타워 TCP/IP 설정 저장
                    SaveTowerConfig(objWriter);

                    //ZEBRA
                    objWriter.WriteLine("[ZEBRA_IP]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strIpZEBRA);
                    objWriter.WriteLine("[ZEBRA_ENABLE_USE]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bEnableZEBRA ? 1 : 0));

                    // Light Controller + IO Board
                    objWriter.WriteLine("[CONTROLLER_IP]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strCOMController);
                    objWriter.WriteLine("[CONTROLLER_PORT]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iBaudController);
                    objWriter.WriteLine("[IO_BOARD_NAME]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strIOBoardName);
                    objWriter.WriteLine("[IO_BOARD_CH1]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iBoardChannel1);
                    objWriter.WriteLine("[IO_BOARD_CH2]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iBoardChannel2);

                    // RECIPE
                    objWriter.WriteLine("[RECIPE NAME]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName);
                    objWriter.WriteLine("[RECIPE PATH]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath);

                    objWriter.WriteLine("[TRIGGER LIGHT MODE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iModeTriggerLight);
                    objWriter.WriteLine("[TIMEOUT GET IMAGE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iTimeDelayTrigger);
                    objWriter.WriteLine("[DELAY TRIGGER CAM MS]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iTimeDelayTriggerCAM);
                    objWriter.WriteLine("[TIMEOUT CONFIRM]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iTimeoutConfirm);
                    objWriter.WriteLine("[THRESHOLD_LIMIT]  " + ClassSystemConfig.Ins.m_ClsCommon.m_dThresholdLimit.ToString("F3"));
                    objWriter.WriteLine("[SAVING JPG MODE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSavingMode);


                    // RESULT RETURN
                    for (int i = 0; i < ClassSystemConfig.Ins.m_ClsCommon.m_iModeResultReturn.Length; i++)
                        objWriter.WriteLine(String.Format("[MODE RESULT RETURN {0}]  {1}", i + 1, ClassSystemConfig.Ins.m_ClsCommon.m_iModeResultReturn[i]));


                    objWriter.WriteLine("[ADDR PLC STATUS]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strAddrPLCStatus);
                    objWriter.WriteLine("[ADDR PLC TRIGGER]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strAddrPLCTriggerIndex);
                    objWriter.WriteLine("[ADDR VS STATUS]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strAddrVSStatus);
                    objWriter.WriteLine("[ADDR VS DONE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strAddrVSTriggerDone);
                    objWriter.WriteLine("[ADDR VS COMPLETE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strAddrVSComplete);
                    objWriter.WriteLine("[ADDR VS INSPECT DATA]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strAddrVSInspectData);
                    //objWriter.WriteLine("[QUICK_ENABLE_USE]  " + (m_bEnableDryRun ? 1 : 0));

                    // Database
                    objWriter.WriteLine("[DB_DATASOURCE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strDataSource);
                    objWriter.WriteLine("[DB_DATABASE]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strDatabase);
                    objWriter.WriteLine("[DB_USER]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strDBUser);
                    objWriter.WriteLine("[DB_PASS]  " + ClassSystemConfig.Ins.m_ClsCommon.m_strDBPass);
                    objWriter.WriteLine("[DB_ENABLE_USE]  " + (ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUseDB ? 1 : 0));


                    objWriter.Close();
                }
                if (ShowMessage)
                {
                    MessageBox.Show("Saved Configurations");
                }
            }
            catch
            {
                if (ShowMessage)
                {
                    MessageBox.Show("Save Fail");
                }
            }

        }
        private void LoadDeviceConfig(bool ShowMessage)
        {
            string file_name = Directory.GetCurrentDirectory() + @"\Config Setting\DeviceConfig.ini";

            if (!System.IO.Directory.Exists(Directory.GetCurrentDirectory() + @"\Config Setting"))
            {
                System.IO.Directory.CreateDirectory(Directory.GetCurrentDirectory() + @"\Config Setting");
            }
            else
            {
                try
                {
                    try
                    {
                        for (int iControl = 0; iControl < ClassCommon.MaxDevice; iControl++)
                        {
                            ClassSystemConfig.Ins.m_ClsCommon.m_ListIPCAM[iControl] = ClassCommon.GetConfig(file_name, "IP CAM VISION " + (iControl + 1), "192.168.100." + 10 * iControl);
                        }

                        ClassSystemConfig.Ins.m_ClsCommon.m_ListUserLogin[0] = ClassCommon.GetConfig(file_name, "LOGIN USER", "Admin");
                        ClassSystemConfig.Ins.m_ClsCommon.m_ListPasswordLogin[0] = ClassCommon.GetConfig(file_name, "LOGIN PASS", "");

                        for (int iControl = 0; iControl < ClassCommon.MaxUserControl; iControl++)
                        {
                            ClassSystemConfig.Ins.m_ClsCommon.m_ListNameControl[iControl] = ClassCommon.GetConfig(file_name, "CONTROL NAME " + (iControl + 1), "");
                        }
                        for (int iControl = 0; iControl < ClassCommon.MaxDevice; iControl++)
                        {
                            ClassSystemConfig.Ins.m_ClsCommon.m_ListEnableControl[iControl] = ClassCommon.GetConfig(file_name, "CONTROL ENABLE " + (iControl + 1), "1") == "1" ? true : false;
                        }
                        //
                        for (int iControl = 0; iControl < ClassCommon.MaxDevice; iControl++)
                        {
                            ClassSystemConfig.Ins.m_ClsCommon.m_dListExposure[iControl] = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringDouble(ClassCommon.GetConfig(file_name, "CAM EXPOSURE " + (iControl + 1), "0"), 0);
                            ClassSystemConfig.Ins.m_ClsCommon.m_dListGain[iControl] = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringDouble(ClassCommon.GetConfig(file_name, "CAM GAIN " + (iControl + 1), "0"), 0);
                        }

                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_Local = (ClassCommon.GetConfig(file_name, "IS SAVE IMG_LOCAL", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_Local = (ClassCommon.GetConfig(file_name, "IS SAVE GRAPHIC IMG LOCAL", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local = (ClassCommon.GetConfig(file_name, "IS SAVE LOG LOCAL", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgOKNG_FTP = false; // (ClassCommon.GetConfig(file_name, "IS SAVE IMG_FTP", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveImgGraphic_FTP = (ClassCommon.GetConfig(file_name, "IS SAVE GRAPHIC IMG FTP", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_FTP = false; // (ClassCommon.GetConfig(file_name, "IS SAVE LOG FTP", "0").Trim() == "1") ? true : false;

                        ClassSystemConfig.Ins.m_ClsCommon.m_bAutoDeleteImg = (ClassCommon.GetConfig(file_name, "IS DELETE AUTO", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.m_iPeriodDelete = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "PERIOD DELETE", "30"), 15);
                        ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath = ClassCommon.GetConfig(file_name, "COMMON PATH", Directory.GetCurrentDirectory());

                        ClassSystemConfig.Ins.m_ClsCommon.m_bShowGraphic = (ClassCommon.GetConfig(file_name, "SHOW GRAPHIC", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.m_bShowOrigin = false; // (ClassCommon.GetConfig(file_name, "SHOW ORIGIN", "1").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.m_bShowProgressStatus = (ClassCommon.GetConfig(file_name, "SHOW PROGRESS STATUS", "0").Trim() == "1") ? true : false;

                        ClassSystemConfig.Ins.m_ClsCommon.m_ListIPClient[0] = ClassCommon.GetConfig(file_name, "IP_CLIENT_1", "192.168.100.100");
                        ClassSystemConfig.Ins.m_ClsCommon.m_ListIPServer[0] = ClassCommon.GetConfig(file_name, "IP_SERVER_1", "192.168.100.200");
                        ClassSystemConfig.Ins.m_ClsCommon.m_ListPortClient[0] = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "PORT_CLIENT_1", "0"), 0);
                        ClassSystemConfig.Ins.m_ClsCommon.m_ListPortServer[0] = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "PORT_SERVER_1", "0"), 0);


                        ClassSystemConfig.Ins.m_ClsCommon.m_bAutoReconnect = (ClassCommon.GetConfig(file_name, "AUTO RECONNECT", "0").Trim() == "1") ? true : false;
                        ClassSystemConfig.Ins.m_ClsCommon.m_strFTPPath = ClassCommon.GetConfig(file_name, "FTP_SAVING_PATH", "");
                        ClassSystemConfig.Ins.m_ClsCommon.IsSaveByFTP = (ClassCommon.GetConfig(file_name, "IS_FTP_SAVING", "1").Trim() == "1") ? true : false;

                        ClassSystemConfig.Ins.m_ClsCommon.m_strUserFTP = ClassCommon.GetConfig(file_name, "USER_FTP", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strPasswordFTP = ClassCommon.GetConfig(file_name, "PASSWORD_FTP", "");


                        ClassSystemConfig.Ins.m_ClsCommon.m_strIpPLC = ClassCommon.GetConfig(file_name, "PLC_IP", "192.168.10.40");
                        ClassSystemConfig.Ins.m_ClsCommon.m_iPortPLC = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "PLC_PORT", "4000"), 0);
                        ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUsePLC = (ClassCommon.GetConfig(file_name, "PLC_ENABLE_USE", "0").Trim() == "1") ? true : false;

                        LoadTowerConfig(file_name);

                        ClassSystemConfig.Ins.m_ClsCommon.m_strIpZEBRA = ClassCommon.GetConfig(file_name, "ZEBRA_IP", "192.168.10.41");
                        ClassSystemConfig.Ins.m_ClsCommon.m_bEnableZEBRA = (ClassCommon.GetConfig(file_name, "ZEBRA_ENABLE_USE", "0").Trim() == "1") ? true : false;


                        ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName = ClassCommon.GetConfig(file_name, "RECIPE NAME", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strRecipePath = ClassCommon.GetConfig(file_name, "RECIPE PATH", Directory.GetCurrentDirectory() + "\\RECIPE");

                        ClassSystemConfig.Ins.m_ClsCommon.m_iTimeDelayTrigger = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "TIMEOUT GET IMAGE", "1000"), 1000);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iModeTriggerLight = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "TRIGGER LIGHT MODE", "1"), 1);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iTimeDelayTriggerCAM = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "DELAY TRIGGER CAM MS", "100"), 100);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iTimeoutConfirm = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "TIMEOUTCONFIRM", "10"), 0);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iFormatSavingMode = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "SAVING JPG MODE", "0"), 0);

                        for (int i = 0; i < ClassSystemConfig.Ins.m_ClsCommon.m_iModeResultReturn.Length; i++)
                        {
                            ClassSystemConfig.Ins.m_ClsCommon.m_iModeResultReturn[i] = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "MODE RESULT RETURN " + (i + 1), "0"), 0);
                        }

                        ClassSystemConfig.Ins.m_ClsCommon.m_dThresholdLimit = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringDouble(ClassCommon.GetConfig(file_name, "THRESHOLD_LIMIT", "0.5"), 0.5);

                        ClassSystemConfig.Ins.m_ClsCommon.m_strAddrPLCStatus = ClassCommon.GetConfig(file_name, "ADDR PLC STATUS", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strAddrPLCTriggerIndex = ClassCommon.GetConfig(file_name, "ADDR PLC TRIGGER", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strAddrVSStatus = ClassCommon.GetConfig(file_name, "ADDR VS STATUS", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strAddrVSTriggerDone = ClassCommon.GetConfig(file_name, "ADDR VS DONE", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strAddrVSComplete = ClassCommon.GetConfig(file_name, "ADDR VS COMPLETE", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strAddrVSInspectData = ClassCommon.GetConfig(file_name, "ADDR VS INSPECT DATA", "");
                        //m_bEnableDryRun = (ClassCommon.GetConfig(file_name, "QUICK_ENABLE_USE", "1").Trim() == "1") ? true : false;


                        ClassSystemConfig.Ins.m_ClsCommon.m_strCOMController = ClassCommon.GetConfig(file_name, "CONTROLLER_IP", "192.168.0.45");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strIOBoardName = ClassCommon.GetConfig(file_name, "IO_BOARD_NAME", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_iBaudController = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "CONTROLLER_PORT", "50000"), 0);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iBoardChannel1 = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "IO_BOARD_CH1", "1"), 1);
                        ClassSystemConfig.Ins.m_ClsCommon.m_iBoardChannel2 = ClassSystemConfig.Ins.m_ClsCommon.ConvertStringToInt(ClassCommon.GetConfig(file_name, "IO_BOARD_CH2", "2"), 2);

                        ClassSystemConfig.Ins.m_ClsCommon.m_strDataSource = ClassCommon.GetConfig(file_name, "DB_DATASOURCE", @"MSI_HUNG_LK\EMSDATABASE");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strDatabase = ClassCommon.GetConfig(file_name, "DB_DATABASE", "FMS_HOLE");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strDBUser = ClassCommon.GetConfig(file_name, "DB_USER", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_strDBPass = ClassCommon.GetConfig(file_name, "DB_PASS", "");
                        ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUseDB = (ClassCommon.GetConfig(file_name, "DB_ENABLE_USE", "0").Trim() == "1") ? true : false;
                    }
                    catch { }

                }
                catch
                {
                    if (ShowMessage)
                    {
                        MessageBox.Show("Load Fail");
                    }
                }
            }

        }
        #endregion



        public void SendToPLC(string data, bool IsShowLog = true)
        {
            try
            {
                if (ClassSystemConfig.Ins.m_ClsClient.ClientConnected)
                {
                    ClassSystemConfig.Ins.m_ClsClient.SendData(data);

                    if (IsShowLog)
                        ClassSystemConfig.Ins.m_UserLog.SetLogOnline(string.Format("VS -> PLC: {0}", data));
                }
            }
            catch (Exception ex)
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "SendToPLC -> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            }
        }




       
        public void UpdateAliveStatus(bool aLive)
        {
            if (ClassSystemConfig.Ins.m_ClsClient.ClientConnected && aLive)
                ClassCommon.ControlStatusInvoke(statusAlive, ClassCommon.STATUS_COLOR.GREEN);
            else
                ClassCommon.ControlStatusInvoke(statusAlive, ClassCommon.STATUS_COLOR.GREY);

        }


        #region Update Status
        DateTime m_TimelastDelete = DateTime.Now;
        DateTime m_TimeLastAlive = DateTime.Now;
        DateTime m_TimeLastReconnect = DateTime.Now;
        DateTime m_TimeCheckLicense = DateTime.Now;
        bool m_bFlipFlag = false;
        private void ThreadUpdateStatus()
        {
            while (ClassSystemConfig.Ins.m_ClsCommon._Status.PROGRAM_LOOP_RUNNING)
            {
                try
                {
                    // Update Date & Time
                    ClassCommon.ControlTextInvoke(lbTime, DateTime.Now.ToString("HH:mm:ss"));
                    ClassCommon.ControlTextInvoke(lbDate, DateTime.Now.ToString("MMM d, ddd"));

                    // Update Connection Status
                    if (ClassSystemConfig.Ins.m_ClsHIK[0].IsConnected)
                        ClassCommon.ControlStatusInvoke(statusCAM1, ClassCommon.STATUS_COLOR.GREEN);
                    else
                        ClassCommon.ControlStatusInvoke(statusCAM1, ClassCommon.STATUS_COLOR.GREY);

                    if (ClassSystemConfig.Ins.m_ClsHIK[1].IsConnected)
                        ClassCommon.ControlStatusInvoke(statusCAM2, ClassCommon.STATUS_COLOR.GREEN);
                    else
                        ClassCommon.ControlStatusInvoke(statusCAM2, ClassCommon.STATUS_COLOR.GREY);

                    // Vison Busy
                    if (ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Busy)
                        ClassCommon.ControlStatusInvoke(statusVisionBusy, ClassCommon.STATUS_COLOR.RED);
                    else
                        ClassCommon.ControlStatusInvoke(statusVisionBusy, ClassCommon.STATUS_COLOR.GREY);

                    if (ClassSystemConfig.Ins.m_ClsClient.ClientConnected)
                        ClassCommon.ControlStatusInvoke(statusZebra, ClassCommon.STATUS_COLOR.RED);
                    else
                        ClassCommon.ControlStatusInvoke(statusZebra, ClassCommon.STATUS_COLOR.GREY);


                    if (ClassSystemConfig.Ins.m_ClsClient.ClientConnected)
                        ClassSystemConfig.Ins.m_ClsClient.GetConnectionStatus();


                    if (ClassSystemConfig.Ins.m_FrmPLC.IsConnectedPLC)
                        ClassCommon.ControlStatusInvoke(statusPLC, ClassCommon.STATUS_COLOR.GREEN);
                    else
                    {
                        ClassCommon.ControlStatusInvoke(statusPLC, ClassCommon.STATUS_COLOR.GREY);
                    }

                    if (ClassSystemConfig.Ins.m_ClsCommon._Status.PLC_Alive)
                        ClassCommon.ControlStatusInvoke(statusAlive, ClassCommon.STATUS_COLOR.GREEN);
                    else
                    {
                        ClassCommon.ControlStatusInvoke(statusAlive, ClassCommon.STATUS_COLOR.GREY);
                    }


                    // Check READY SIgnal (Camera connected AND PLC Connected && Zebra Connected)
                    if (ClassSystemConfig.Ins.m_ClsHIK[0].IsConnected && ClassSystemConfig.Ins.m_ClsHIK[1].IsConnected && 
                        ClassSystemConfig.Ins.m_FrmPLC.IsConnectedPLC)
                    {
                        ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Ready = true;
                    }
                    else
                    {
                        ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Ready = false;
                    }

                    if (ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Ready)
                        ClassCommon.ControlStatusInvoke(statusReady, ClassCommon.STATUS_COLOR.GREEN);
                    else
                        ClassCommon.ControlStatusInvoke(statusReady, ClassCommon.STATUS_COLOR.RED);

                    if (ClassSystemConfig.Ins.m_ClsHIK[0].IsConnected)
                        ClassSystemConfig.Ins.m_ClsHIK[0].IsConnected = ClassSystemConfig.Ins.m_ClsHIK[0].GetConnectedStatus();
                    if (ClassSystemConfig.Ins.m_ClsHIK[1].IsConnected)
                        ClassSystemConfig.Ins.m_ClsHIK[1].IsConnected = ClassSystemConfig.Ins.m_ClsHIK[1].GetConnectedStatus();
                }
                catch (Exception ex)
                {
                    ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "ThreadUpdateStatus() Update status light-> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                }


                // Delete Images Auto
                if (ClassSystemConfig.Ins.m_ClsCommon.m_bAutoDeleteImg)
                {
                    try
                    {
                        var timeDelete = (DateTime.Now - m_TimelastDelete).TotalMinutes;
                        if (timeDelete > 30)
                        {
                            AutoDeleteFromFolder(ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Images");
                            m_TimelastDelete = DateTime.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "ThreadUpdateStatus() AutoDeleteFolder-> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                    }
                }

                // Auto Reconnect
                if (ClassSystemConfig.Ins.m_ClsCommon.m_bAutoReconnect)
                {
                    try
                    {
                        var time_Reconnect = (DateTime.Now - m_TimeLastReconnect).TotalMilliseconds;
                        var time_checkPlc = (DateTime.Now - m_TimeLastAlive).TotalSeconds;
                        if (time_Reconnect >= 2000)
                        {
                            if (!ClassSystemConfig.Ins.m_ClsClient.ClientConnected)
                            {
                                ClassSystemConfig.Ins.m_ClsClient.OpenClientSocket(ClassSystemConfig.Ins.m_ClsCommon.m_strIpPLC, ClassSystemConfig.Ins.m_ClsCommon.m_iPortPLC);
                                Thread.Sleep(50);
                            }

                            m_TimeLastReconnect = DateTime.Now;
                        }

                        if (ClassSystemConfig.Ins.m_ClsClient.ClientConnected && time_checkPlc > 15)
                        {
                            ClassSystemConfig.Ins.m_ClsClient.CloseSocket();
                            Thread.Sleep(50);
                            ClassSystemConfig.Ins.m_ClsClient.OpenClientSocket(ClassSystemConfig.Ins.m_ClsCommon.m_strIpPLC, ClassSystemConfig.Ins.m_ClsCommon.m_iPortPLC);
                            m_TimeLastAlive = DateTime.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.EXCEPTION,
                                                    "ThreadUpdateStatus() AutoReconnect-> " + ex.Message,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
                    }
                }

                if (!IS_DEBUG)
                {
                    var time_check_license = (DateTime.Now - m_TimeCheckLicense).TotalSeconds;
                    if (time_check_license > 10)
                    {
                        try
                        {
                            //m_IsLicenseActived = DKLIB.LicenseHW.CheckLicenseAvailable();
                        }
                        catch
                        {
                            m_IsLicenseActived = false;
                        }
                        m_TimeCheckLicense = DateTime.Now;
                    }
                }
                Thread.Sleep(150);
            }
        }

       
        #endregion

        private void AutoDeleteFromFolder(string parentFolder)
        {
            if (Directory.Exists(parentFolder))
            {
                List<string> _listMonthPath = new List<string>();
                List<string> _listDatePath = new List<string>();

                string[] listFolder = Directory.GetDirectories(parentFolder);
                if (listFolder != null && listFolder.Length > 0)
                {
                    for (int i = 0; i < listFolder.Length; i++)
                    {
                        string[] listFolder2 = Directory.GetDirectories(listFolder[i]);
                        if (listFolder2 != null && listFolder2.Length > 0)
                        {
                            _listMonthPath.AddRange(listFolder2);

                            for (int j = 0; j < listFolder2.Length; j++)
                            {
                                string[] listFolder3 = Directory.GetDirectories(listFolder2[j]);
                                if (listFolder3 != null && listFolder3.Length > 0)
                                    _listDatePath.AddRange(listFolder3);
                            }
                        }
                    }
                }

                for (int iPath = 0; iPath < _listDatePath.Count; iPath++)
                {
                    if (Directory.Exists(_listDatePath[iPath]))
                        AutoDeleteFile(_listDatePath[iPath], 1, DeleteMode.CreatedTime);
                }
                for (int iPath = 0; iPath < _listMonthPath.Count; iPath++)
                {
                    if (Directory.Exists(_listMonthPath[iPath]))
                        AutoDeleteFile(_listMonthPath[iPath], 1, DeleteMode.LastWriteTime);
                }

                //for (int iMonth = 1; iMonth <= 12; iMonth++)
                //{
                //    string folderImage1 = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Images\" + DateTime.Now.ToString("yyyy") + @"\" + DateTime.Now.ToString("yyyy_") + iMonth.ToString("D2");
                //    if (Directory.Exists(folderImage1))
                //        AutoDeleteFile(folderImage1, 1, DeleteMode.CreatedTime);
                //}
                //string folderImage2 = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath + @"\Images\" + DateTime.Now.ToString("yyyy");
                //AutoDeleteFile(folderImage2, 1, DeleteMode.LastWriteTime);
            }
        }

        public enum DeleteMode
        {
            CreatedTime = 0,
            LastAccessTime,
            LastWriteTime
        }

        private void btnLive_Click(object sender, EventArgs e)
        {

        }

        private void AutoDeleteFile(string path, int limitDay, DeleteMode mode)
        {
            if (ClassSystemConfig.Ins.m_ClsCommon.m_bAutoDeleteImg)
            {
                int iPeriodDele = limitDay;
                if (ClassSystemConfig.Ins.m_ClsCommon.m_iPeriodDelete < limitDay)
                    iPeriodDele = limitDay + 1;
                else
                    iPeriodDele = ClassSystemConfig.Ins.m_ClsCommon.m_iPeriodDelete;

                if (Directory.Exists(path))
                {
                    string[] listFolder = Directory.GetDirectories(path);
                    foreach (var folder in listFolder)
                    {
                        try
                        {
                            DirectoryInfo direcInfo = new DirectoryInfo(folder);

                            switch (mode)
                            {
                                case DeleteMode.CreatedTime:
                                    if (direcInfo.CreationTime <= DateTime.Now.AddDays(1 - iPeriodDele))
                                    {
                                        Directory.Delete(folder, true);
                                        ClassSystemConfig.Ins.m_UserLog.SetLogOnline(String.Format("Deleted Folder {0}, Created Time: {1}", folder, direcInfo.CreationTime.ToShortDateString()));
                                    }
                                    break;
                                case DeleteMode.LastAccessTime:
                                    if (direcInfo.LastAccessTime <= DateTime.Now.AddDays(1 - iPeriodDele))
                                    {
                                        Directory.Delete(folder, true);
                                        ClassSystemConfig.Ins.m_UserLog.SetLogOnline(String.Format("Deleted Folder {0}, Last Access Time: {1}", folder, direcInfo.LastAccessTime.ToShortDateString()));
                                    }
                                    break;
                                case DeleteMode.LastWriteTime:
                                    if (direcInfo.LastWriteTime <= DateTime.Now.AddDays(1 - iPeriodDele))
                                    {
                                        Directory.Delete(folder, true);
                                        ClassSystemConfig.Ins.m_UserLog.SetLogOnline(String.Format("Deleted Folder {0}, Last Write Time: {1}", folder, direcInfo.LastWriteTime.ToShortDateString()));
                                    }
                                    break;
                                default:
                                    if (direcInfo.LastWriteTime <= DateTime.Now.AddDays(1 - iPeriodDele))
                                    {
                                        Directory.Delete(folder, true);
                                        ClassSystemConfig.Ins.m_UserLog.SetLogOnline(String.Format("Deleted Folder {0}, Last Write Time: {1}", folder, direcInfo.LastWriteTime.ToShortDateString()));
                                    }
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            ClassSystemConfig.Ins.m_UserLog.SetLogOnline("Delete Folder FAIL " + folder + " (" + ex.Message + ")");
                        }
                    }
                }
            }
        }

        private void SettingDevice_Click(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_FrmSetting.Show();
            ClassSystemConfig.Ins.m_FrmSetting.ShowOnScreen();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Start",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);

            ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Busy = false;
            bool bComConnected = false;
            for (int iCam = 0; iCam < ClassCommon.MaxDevice; iCam++)
            {
                if (!ClassSystemConfig.Ins.m_ClsHIK[iCam].IsConnected && ClassSystemConfig.Ins.m_ClsCommon.m_ListEnableControl[iCam])
                {
                    ClassSystemConfig.Ins.m_ClsHIK[iCam].ConnectDevice(ClassSystemConfig.Ins.m_ClsCommon.m_ListIPCAM[iCam]);
                    bComConnected |= ClassSystemConfig.Ins.m_ClsHIK[iCam].IsConnected;
                }
            }

            if (!ClassSystemConfig.Ins.m_ClsSqlServer.IsConnectedDB_SQL && ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUseDB)
            {
                // Here is local database
                ClassSystemConfig.Ins.m_ClsSqlServer.ConnectDatabase(ClassSystemConfig.Ins.m_ClsCommon.m_strDataSource, ClassSystemConfig.Ins.m_ClsCommon.m_strDatabase, ClassSystemConfig.Ins.m_ClsCommon.m_strDBUser, ClassSystemConfig.Ins.m_ClsCommon.m_strDBPass);
            }

            if (!ClassSystemConfig.Ins.m_FrmPLC.IsConnectedPLC && ClassSystemConfig.Ins.m_ClsCommon.m_bEnableUsePLC)
            {
                ClassSystemConfig.Ins.m_FrmPLC.OpenConnectPLC();
            }

            if (!ClassSystemConfig.Ins.m_ClsClient.ClientConnected && ClassSystemConfig.Ins.m_ClsCommon.m_bEnableZEBRA)
            {
                ClassSystemConfig.Ins.m_ClsClient.OpenClientSocket(ClassSystemConfig.Ins.m_ClsCommon.m_strIpZEBRA, ClassSystemConfig.Ins.m_ClsCommon.m_iPortPLC);
            }


            if (ClassSystemConfig.Ins.m_FrmPLC.IsConnectedPLC || bComConnected || ClassSystemConfig.Ins.m_ClsClient.ClientConnected)
            {
                if (ClassSystemConfig.Ins.m_FrmPLC.IsConnectedPLC && bComConnected)
                {
                    ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Ready = true;
                    ClassCommon.ControlTextInvoke(lbRunningState, "READY");
                }
                else
                    if (!ClassSystemConfig.Ins.m_FrmPLC.IsConnectedPLC)
                {
                    ClassCommon.ControlTextInvoke(lbRunningState, "PLC UNCONNECT");
                }
                else
                {
                    ClassCommon.ControlTextInvoke(lbRunningState, "CAM UNCONNECT");
                }

                btnStart.Enabled = false;

                ClassSystemConfig.Ins.m_ClsCommon._Status.PROGRAM_STARTED = true;

            }

            else
            {
                ClassCommon.ControlTextInvoke(lbRunningState, "NOT READY");
                ClassSystemConfig.Ins.m_ClsCommon._Status.PROGRAM_STARTED = false;
            }

            ClassSystemConfig.Ins.m_ClsCommon.m_bAutoReconnect = cbAutoReconnect.Checked;

            Array.Clear(m_ListResultFlag, 0, m_ListResultFlag.Length);
            ClearGraphicView();
            btnStop.Enabled = true;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_ClsCommon._Status.PROGRAM_STARTED = false;
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Stop",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);

            ClassSystemConfig.Ins.m_ClsCommon.m_bAutoReconnect = false;
            ClassSystemConfig.Ins.m_ClsCommon._Status.VISION_Busy = false;

            for (int iCam = 0; iCam < ClassCommon.MaxDevice; iCam++)
            {
                if (ClassSystemConfig.Ins.m_ClsHIK[iCam].IsConnected)
                {
                    ClassSystemConfig.Ins.m_ClsHIK[iCam].DisConnectDevice();
                }
            }

            if (ClassSystemConfig.Ins.m_FrmPLC.IsConnectedPLC)
                ClassSystemConfig.Ins.m_FrmPLC.CloseConnectPLC();

            if (ClassSystemConfig.Ins.m_ClsClient.ClientConnected)
                ClassSystemConfig.Ins.m_ClsClient.CloseSocket();

            StopTowerCommunication();

            if (ClassSystemConfig.Ins.m_ClsSqlServer.IsConnectedDB_SQL)
                ClassSystemConfig.Ins.m_ClsSqlServer.CloseDatabase();

            btnStart.Enabled = true;
            btnStop.Enabled = false;
            btnTrigger1.Enabled = true;
            ClassCommon.ControlTextInvoke(lbRunningState, "PROGRAM STOPPED!");
        }

        private void btnTrigger2_Click(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                            "Clicked TRIGGER CAMERA " + 1,
                                            ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);

            //ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
            //                            "Clicked TRIGGERB CAMERA",
            //                            ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
            m_bIsManualTrigger = true;
            m_eRunningMode = eRunMode.MANUAL;

            DoTriggerCamera(index_cam: 1);
        }

        private void btnConfig_Click(object sender, EventArgs e)
        {


            bHideLeftPanel = !bHideLeftPanel;
            panelLeftHide.Visible = bHideLeftPanel;


            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Config",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        private void btnLightSetting_Click(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_FrmLight.Show();
            ClassSystemConfig.Ins.m_FrmLight.ShowOnScreen();
            ClassSystemConfig.Ins.m_FrmLight.InittializeUi();
        }

        public void UpdateCurrentModel()
        {
            ClassCommon.ControlTextInvoke(lbRescipe, ClassSystemConfig.Ins.m_ClsCommon.m_strRecipeName);
        }

        private void btnVision_Click_1(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_FrmVision.Show();
            ClassSystemConfig.Ins.m_FrmVision.ShowOnScreen();
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Vision Setting",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        private void btnClearTest_Click(object sender, EventArgs e)
        {
            ClearGraphicView();
        }

        private void btnLoadImage1_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            if (Directory.Exists(ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath))
            {
                fileDialog.InitialDirectory = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath;
            }
            fileDialog.Filter = "Images |*.bmp;*.jpg;*.png;*.jpeg;";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                //ClassSystemConfig.Ins.m_UserDisplay[0].SetImage((Bitmap)Bitmap.FromFile(fileDialog.FileName));
                //m_ListImageBMP[0] = (Bitmap)Bitmap.FromFile(fileDialog.FileName);
                //int iResult = 0;
                //Cognex.VisionPro.CogImage8Grey cogImage = new Cognex.VisionPro.CogImage8Grey(bmpImage);
                //Cognex.VisionPro.CogImage8Grey cogImage = new Cognex.VisionPro.CogImage8Grey((Bitmap)Bitmap.FromFile(fileDialog.FileName));
                //ClassSystemConfig.Ins.m_FrmVision.ProcessAsTool(1,cogImage);

                

                // Run VisionTool
                string strCodeID = "";
                int iResult = 0;
                bool IsTrayID = false;
                string strData = "";
                Bitmap inputImage = (Bitmap)Bitmap.FromFile(fileDialog.FileName);

                ClassSystemConfig.Ins.m_UserDisplay[0].SetImage(inputImage);


                
                ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, 0, 0, true, false, IsTrayID, ref strCodeID, ref iResult);
                strData = string.Format("{0}", strCodeID);

                string[] words = strCodeID.Split('!');

                string CustomerPo = "";
                //string MakerPo = "";
                //string lot = "";
                string Qty = "";

                CustomerPo = words[0].ToString();
                Qty = words[4].ToString();

             
                lblIns.Text = "Customer P/No: "+ CustomerPo +"           "+ "Qty: "+ Qty;
                
           

                ClassSystemConfig.Ins.m_UserDisplay[2].Controls.Add(lblIns);
                //lblIns.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                lblIns.Visible = true;
                lblIns.Dock = DockStyle.Top;
                //this.Controls.Add(ClassSystemConfig.Ins.m_UserDisplay[2]);

                //ClassSystemConfig.Ins.m_UserDisplay[2].ResumeLayout(false);
                //ClassSystemConfig.Ins.m_UserDisplay[2].Visible = true;

                m_ListResultBuffer[0] = iResult;
                m_ListResultFlag[0] = true;
              
                Thread.Sleep(2);
            }


        }

        private void btnLoadImage2_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            if (Directory.Exists(ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath))
            {
                //fileDialog.InitialDirectory = ClassSystemConfig.Ins.m_ClsCommon.m_CommonPath;
            }
            fileDialog.Filter = "Images |*.bmp;*.jpg;*.png;*.jpeg;";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                //m_ListImageBMP[1] = (Bitmap)Bitmap.FromFile(fileDialog.FileName);
                // Run VisionTool
                string strCodeID = "";
                int iResult = 0;
                bool IsTrayID = false;
                string strData = "";
                Bitmap inputImage = (Bitmap)Bitmap.FromFile(fileDialog.FileName);

                ClassSystemConfig.Ins.m_UserDisplay[1].SetImage(inputImage);


                ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, 0, 1, true, false, IsTrayID, ref strCodeID, ref iResult);
                strData = string.Format("{0}", strCodeID);

                string[] words = strCodeID.Split('!');

                string CustomerPo = "";
                //string MakerPo = "";
                //string lot = "";
                string Qty = "";

                CustomerPo = words[0].ToString();
                Qty = words[4].ToString();

              
                lblIns2.Text = "Customer P/No: " + CustomerPo + "           " + "Qty: " + Qty;

                

                ClassSystemConfig.Ins.m_UserDisplay[3].Controls.Add(lblIns2);
                //lblIns2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
                lblIns2.Visible = true;
                lblIns2.Dock = DockStyle.Top;
                //IsTrayID = true;
                //ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, 1, 1, true, false,  ref strCodeID, ref iResult);
                //ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, 1, 1, true, false, IsTrayID, ref strCodeID, ref iResult);
                //strData = string.Format("{0}", strCodeID);

                m_ListResultBuffer[1] = iResult;
                m_ListResultFlag[1] = true;
             
                Thread.Sleep(2);
            }
        }

        private void ClearGraphicView()
        {
            

            for (int i = 0; i < ClassCommon.MaxUserControl; i++)
            {
                ClassSystemConfig.Ins.m_UserDisplay[i].ClearImage();
                ClassSystemConfig.Ins.m_UserDisplay[i].ClearGraphic();
            }

            lblIns.Text = "";
            lblIns2.Text = "";

        }

        public bool IsLiveViewMode = false;
        private void btnLive_Click_1(object sender, EventArgs e)
        {
            if (btnLive.Text == "LIVE ON")
            {
                //btnLive.BackColor = Color.FromArgb(209, 21, 89);  //symbolizes light turned on

                btnLive.Text = "LIVE OFF";
                btnLive.BackColor = Color.FromArgb(209, 21, 89);  //symbolizes light turned on

                IsLiveViewMode = true;
                if (!ClassSystemConfig.Ins.m_ClsHIK[0].IsConnected || !ClassSystemConfig.Ins.m_ClsHIK[1].IsConnected)
                {
                    IsLiveViewMode = false;
                    btnLive.BackColor = Color.Red;
                    return;
                }

                for (int i = 0; i < ClassSystemConfig.Ins.m_ClsHIK.Length; i++)
                {
                    try
                    {
                        if (ClassSystemConfig.Ins.m_ClsHIK[i].IsConnected)
                        {
                            switch (i)
                            {
                                case 0:
                                    ClassSystemConfig.Ins.m_ClsHIK[i].SetExposure((float)ClassSystemConfig.Ins.m_ClsModelConfig.Exposure1);
                                    ClassSystemConfig.Ins.m_ClsHIK[i].SetGain((float)ClassSystemConfig.Ins.m_ClsModelConfig.Gain1);
                                    break;
                                case 1:
                                    ClassSystemConfig.Ins.m_ClsHIK[i].SetExposure((float)ClassSystemConfig.Ins.m_ClsModelConfig.Exposure2);
                                    ClassSystemConfig.Ins.m_ClsHIK[i].SetGain((float)ClassSystemConfig.Ins.m_ClsModelConfig.Gain2);
                                    break;
                            }
                        }

                        if (ClassSystemConfig.Ins.m_ClsHIK[i].IsConnected)
                            ClassSystemConfig.Ins.m_ClsHIK[i].SetContinousMode(true);

                        ClassSystemConfig.Ins.m_ClsHIK[i].ImageBMPQueue.Clear();
                    }
                    catch { }
                }
                IsLiveViewMode = true;
            }

            else if (btnLive.Text == "LIVE OFF")
            {
                //btnLive.BackColor = Color.FromArgb(209, 21, 89); ; //symbolizes light turned off
                IsLiveViewMode = false;

                for (int i = 0; i < ClassSystemConfig.Ins.m_ClsHIK.Length; i++)
                {
                    try
                    {
                        if (ClassSystemConfig.Ins.m_ClsHIK[i].IsConnected)
                            ClassSystemConfig.Ins.m_ClsHIK[i].SetContinousMode(false);
                    }
                    catch { }
                }

                btnLive.Text = "LIVE ON";
                btnLive.BackColor = Color.FromArgb(209, 21, 89); ; //symbolizes light turned off

                if (cogRecordDisplayCAM1 is null)
                {
                    //cogRecordDisplayCAM1.Image = null;
                    //cogRecordDisplayCAM2.Image = null;
                    return;
                }
                else
                {
                    cogRecordDisplayCAM1.Image = null;
                    cogRecordDisplayCAM2.Image = null;
                }
            }

            
        }

        

        //Mutex m_mutexLoader = new Mutex();
        private void ProcessDataLoader(int indexCam, string cmd_value)
        {
            if (indexCam < 0 || indexCam > 1)
            {
                return;
            }

            m_mutexLoader.WaitOne();
            m_eRunningMode = eRunMode.AUTO;
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.LOADER,
                                                    "PLC Sent Trigger CMD = " + cmd_value,
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, false);
            Bitmap inputImage = ClassSystemConfig.Ins.m_ClsFunc.GetImageFromTrigger(indexCam, 2, 1000);

            // Show Image
            ClassSystemConfig.Ins.m_UserDisplay[indexCam].SetImage(inputImage);

            // Run VisionTool
            string strCodeID = "";
            int iResult = 0;
            bool IsTrayID = false;
            string strData = "";
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.LOADER,
                                                    "Begin Run ProcessDataLoader",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, false);

            switch (cmd_value.Trim())
            {
                case "11":
                    IsTrayID = true;
                    ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, indexCam, indexCam, true, false, IsTrayID, ref strCodeID, ref iResult);
                    strData = string.Format("{0}", strCodeID);
                    break;
                case "12":
                    ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, indexCam, indexCam, false, true, IsTrayID, ref strCodeID, ref iResult);
                    strData = string.Format("{0}", iResult);
                    break;
                case "13":
                    ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, indexCam, indexCam, true, false, IsTrayID, ref strCodeID, ref iResult);
                    strData = string.Format("{0}", strCodeID);
                    break;
                case "14":
                    ClassSystemConfig.Ins.m_FrmVision.RunProcessCodeID(inputImage, indexCam, indexCam, true, true, IsTrayID, ref strCodeID, ref iResult);
                    strData = string.Format("{0};{1}", iResult, strCodeID);
                    break;
            }

            // Send Data
            SendToPLC((int)eServerType.LOADER, strData);

            if (ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local)
            {
                ClsSaveLog buffLog = new ClsSaveLog();
                buffLog.Name = "LOADER";
                buffLog.CamIndex = indexCam;
                buffLog.RunMode = m_eRunningMode.ToString();
                buffLog.SavingTime = DateTime.Now;
                buffLog.LogContent = string.Format("CMD={0}, {1}, {2}, {3}", cmd_value, iResult, strCodeID, IsTrayID ? "Yes" : "-");
                buffLog.LogHeader = "CMD CODE, RESULT, QR CODE, JIG ID";
                m_QueSaveLog.Enqueue(buffLog);
            }

            m_mutexLoader.ReleaseMutex();
        }
        private void radioBtnModeSetup_CheckedChanged(object sender, EventArgs e)
        {
            //groupBoxSettingVision.Enabled = true;
            //groupBoxCAM.Enabled = true;
            //groupBoxSetting.Enabled = true;

            groupBoxSettingVision.Visible = true;
            groupBoxCAM.Visible = true;
            groupBoxSetting.Visible = true;
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked SETUP",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        private void radioBtnModeMonitor_CheckedChanged(object sender, EventArgs e)
        {
            groupBoxSettingVision.Visible = false;
            //groupBoxSettingVision.Enabled = false;
            groupBoxCAM.Visible = false;
            groupBoxSetting.Visible = false;
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked MONITOR",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        private void btnZebratest_Click(object sender, EventArgs e)
        {
            // Command to be sent to the printer
            string command = "^XA^FO10,10,^AO,30,20^FD ^FS^FO10,30^BY3^BCN,100,Y,N,N^FDTesting^FS^XZ";
            ClassFileCtl cl = new ClassFileCtl();
            //string print_path = ClassSystemConfig.Ins.m_ClsFileCtl.GetConfigValues("BARCODE", "PRINT");
            string print_path = cl.GetConfigValues("BARCODE", "PRINT");

            // Create a buffer with the command
            Byte[] buffer = new byte[command.Length];
            buffer = System.Text.Encoding.ASCII.GetBytes(command);
            // Use the CreateFile external func to connect to the LPT1 port
            SafeFileHandle printer = CreateFile(print_path, FileAccess.ReadWrite, 0, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
            //SafeFileHandle printer = CreateFile(txtprint_path.Text + @"\11", FileAccess.ReadWrite, 0, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
            // Aqui verifico se a impressora é válida
            if (printer.IsInvalid == true)
            {
                return;
            }

            // Open the filestream to the lpt1 port and send the command
            FileStream lpt1 = new FileStream(printer, FileAccess.ReadWrite);
            lpt1.Write(buffer, 0, buffer.Length);
            // Close the FileStream connection
            lpt1.Close();

        }

        private void button1_Click(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_FrmPLC.Show();
            ClassSystemConfig.Ins.m_FrmPLC.ShowOnScreen();
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked PLC Setting",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        private void btnLoginIcon_Click(object sender, EventArgs e)
        {
            ClassSystemConfig.Ins.m_FrmLogin.ShowDialog();
            ClassSystemConfig.Ins.m_ClsFunc.SaveLog(ClassFunction.SAVING_LOG_TYPE.HANDLER_CLICKED,
                                                    "Clicked Login",
                                                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local, true);
        }

        public void SavePassLogIn(string fileSaving, string[] strPass)
        {
            string pathSaving = Directory.GetCurrentDirectory() + "\\" + "Config Setting";
            if (!Directory.Exists(pathSaving))
            {
                Directory.CreateDirectory(pathSaving);
            }
            //if (!File.Exists(pathSaving + "\\" + fileSaving))
            //    File.Create(pathSaving + "\\" + fileSaving);

            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                XmlNode rootNode = xmlDoc.CreateElement("Users");
                xmlDoc.AppendChild(rootNode);


                for (int i = 0; i < strPass.Length; i++)
                {
                    string strTemp = "";
                    switch (i)
                    {
                        case 0:
                            strTemp = "OP"; break;
                        case 1:
                            strTemp = "EGN"; break;
                        case 2:
                            strTemp = "MAS"; break;
                        default:
                            strTemp = ""; break;
                    }
                    byte[] bytesArr = Encoding.ASCII.GetBytes(strPass[i]);
                    string strWrite = "";
                    for (int iChar = 0; iChar < bytesArr.Length; iChar++)
                        strWrite += bytesArr[iChar].ToString("x");
                    XmlNode userNode = xmlDoc.CreateElement("Name");
                    XmlAttribute attribute = xmlDoc.CreateAttribute(strTemp);
                    attribute.Value = "Level " + i;
                    userNode.Attributes.Append(attribute);
                    userNode.InnerText = strWrite;
                    rootNode.AppendChild(userNode);

                }

                xmlDoc.Save(pathSaving + "\\" + fileSaving);
            }
            catch { }


        }

        private void cbTestImage_CheckedChanged(object sender, EventArgs e)
        {
            btnLoadImage1.Visible = cbTestImage.Checked;
            btnLoadImage2.Visible = cbTestImage.Checked;
            btnClearTest.Visible = cbTestImage.Checked;
        }

    }
    public class ClsSaveImage
    {
        public int CamIndex = -1;
        public string Name = "";
        public string RunMode = "";
        public DateTime SavingTime = DateTime.Now;
        public Bitmap OriginImage = null;
        public Bitmap GraphicImage = null;
        public string StrOKNG = "";
    }

    public class ClsSaveLog
    {
        public int CamIndex = -1;
        public string Name = "";
        public string RunMode = "";
        public DateTime SavingTime = DateTime.Now;
        public string LogHeader;
        public string LogContent;
    }

}

