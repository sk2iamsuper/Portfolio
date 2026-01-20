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
using System.Diagnostics;
using Microsoft.Win32;
using MES_WMS.UserCommon;

namespace MES_WMS
{
    public partial class FrmMain : Form
    {
        #region 상수 및 열거형
        private const string CONFIG_FILE_NAME = @"\CMES\config_mes.ini";
        private const string SERVER_SECTION = "SERVER";
        private const string USER_SECTION = "USER";
        private const string FILE_KEY = "File";
        private const string FACTORY_KEY = "Factory";
        private const string SERVER_KEY = "server";
        
        private enum ButtonType
        {
            WH,
            Factory,
            Empno,
            Order,
            Items
        }
        #endregion

        #region 필드
        private readonly CLSORDERLIST _orderList = new CLSORDERLIST();
        private readonly ClsFileCtl _fileController = new ClsFileCtl();
        private PRF13 _childViewForm;
        
        private string _userFactory = Public_Function.user_Factory;
        private string _userDept = string.Empty;
        private readonly string _userEmpno = Public_Function.user_Empno;
        private readonly string _userName = Public_Function.user_Name;
        
        public string PrintPath { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        #endregion

        #region 생성자
        public FrmMain()
        {
            InitializeComponent();
            InitializeApplication();
        }
        #endregion

        #region 초기화 메서드
        private void InitializeApplication()
        {
            CheckForUpdates();
            InitializeForm();
        }

        private void InitializeForm()
        {
            LoadInitialSettings();
            ConfigurePanelLocation();
            ConfigureUserSettings();
            LoadProcessButtons();
            LoadPrinterOptions();
        }
        #endregion

        #region 설정 관리
        private void LoadInitialSettings()
        {
            var settings = new AppSettings();
            btnFactory.Text = settings.Factory ?? "F11";
            btnServer.Text = settings.Server;
        }

        private void SaveUserSettings()
        {
            Public_Function.user_Saup = "01";
            Public_Function.user_Factory = btnFactory.Text;
            Public_Function.user_WH = btnWH.Name;
            Public_Function.user_IP = btnEmpno.Tag?.ToString() ?? string.Empty;
            Public_Function.user_Empno = btnEmpno.Name;
            Public_Function.user_Name = btnEmpno.Text;
            Public_Function.user_Server = "cmvn";

            using (var db = new CmCn())
            {
                _userDept = db.StrResultReturnExecute(
                    $"select dept_code from thb01 where saup_gubn='01' and empno='{btnEmpno.Name}'");
                Public_Function.user_Dept = _userDept;
            }
        }

        private void ConfigureUserSettings()
        {
            _userFactory = btnFactory.Text.Substring(0, 3);
            _orderList.SelFactory = _userFactory;
            btnFactory.Text = _userFactory;
            btnFactory.Name = _userFactory;
            _orderList.SelEmpno = _userEmpno;
            _orderList.SelDept = _userDept;

            if (_userFactory == "F11")
            {
                _userDept = "0104";
                btnWH.Text = "WH Export (A2)";
                btnWH.Name = "A2";
                _orderList.SelWH = "A2";
                btnSetStk.Visible = false;
            }

            ConfigureWarehouseButtons();
        }

        private void ConfigureWarehouseButtons()
        {
            CreateDynamicButtons(
                _orderList.GetProcDS,
                pnlTop,
                Color.WhiteSmoke,
                Color.Black,
                ButtonType.WH);
        }
        #endregion

        #region 파일 업데이트 관리
        private void CheckForUpdates()
        {
            try
            {
                var updater = new ApplicationUpdater();
                updater.CheckAndUpdateApplication();
            }
            catch (Exception ex)
            {
                LogError($"파일 업데이트 실패: {ex.Message}");
                // 실패해도 애플리케이션은 계속 실행
            }
        }
        #endregion

        #region 동적 UI 생성
        private void CreateDynamicButtons(
            DataSet dataSource,
            FlowLayoutPanel container,
            Color backColor,
            Color foreColor,
            ButtonType buttonType)
        {
            container.Controls.Clear();
            container.Padding = new Padding(5);

            if (dataSource == null || dataSource.Tables[0].Rows.Count == 0)
                return;

            var buttonSize = CalculateButtonSize(dataSource, container);
            var font = GetButtonFont(buttonType);

            foreach (DataRow row in dataSource.Tables[0].Rows)
            {
                var button = CreateButton(row, buttonSize, backColor, foreColor, font, buttonType);
                container.Controls.Add(button);
            }

            AdjustContainerSize(container, buttonSize, dataSource);
        }

        private Size CalculateButtonSize(DataSet dataSource, FlowLayoutPanel container)
        {
            int rowCount = dataSource.Tables[0].Rows.Count;
            int width, height;

            if (rowCount < 4)
            {
                width = container.Width / 4 - 5;
                height = container.Height / 4 - 15;
            }
            else if (rowCount < 8)
            {
                width = container.Width / 4 - 5;
                height = container.Height / 4 - 17;
            }
            else
            {
                width = container.Width / 7 - 25;
                height = container.Height / 8 - 10;
            }

            return new Size(width, Math.Max(height, 68));
        }

        private Font GetButtonFont(ButtonType buttonType)
        {
            return buttonType switch
            {
                ButtonType.Items => new Font("Gulim", 10F, FontStyle.Bold),
                ButtonType.Order => new Font("Gulim", 17F, FontStyle.Bold),
                _ => new Font("Gulim", 8F, FontStyle.Bold)
            };
        }

        private Button CreateButton(
            DataRow row,
            Size size,
            Color backColor,
            Color foreColor,
            Font font,
            ButtonType buttonType)
        {
            var button = new Button
            {
                Text = row[1].ToString(),
                Name = row[0].ToString(),
                Tag = row[2].ToString(),
                Size = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = backColor,
                ForeColor = foreColor,
                Font = font,
                Location = new Point(10, 15)
            };

            button.Click += GetButtonClickHandler(buttonType);
            return button;
        }

        private EventHandler GetButtonClickHandler(ButtonType buttonType)
        {
            return buttonType switch
            {
                ButtonType.WH => WHBtn_Click,
                ButtonType.Factory => FactBtn_Click,
                ButtonType.Empno => EmpBtn_Click,
                _ => EmpBtn_Click
            };
        }

        private void AdjustContainerSize(FlowLayoutPanel container, Size buttonSize, DataSet dataSource)
        {
            int rows = (int)Math.Ceiling(dataSource.Tables[0].Rows.Count / 4.0);
            container.Height = (buttonSize.Height * rows) + 20;
        }
        #endregion

        #region 이벤트 핸들러
        private void FactBtn_Click(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                UpdateFactorySelection(button);
            }
        }

        private void EmpBtn_Click(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                ValidateAndUpdateEmployee(button);
            }
        }

        private void WHBtn_Click(object sender, EventArgs e)
        {
            if (sender is Button button)
            {
                UpdateWarehouseSelection(button);
            }
        }

        private void btnFactory_Click(object sender, EventArgs e)
        {
            ShowSelectionPanel(_orderList.GetFactDS, ButtonType.Factory, Color.LightBlue);
        }

        private void btnWH_Click(object sender, EventArgs e)
        {
            ShowSelectionPanel(_orderList.GetWHDS, ButtonType.WH, SystemColors.Control);
        }

        private void btnEmpno_Click(object sender, EventArgs e)
        {
            ShowSelectionPanel(_orderList.GetEmpDS, ButtonType.Empno, SystemColors.Control);
        }

        private void picPgm05_Click(object sender, EventArgs e)
        {
            if (!ValidateUserAccess("QC", "You can't use this picking program!"))
                return;

            if (!ValidateFactoryAccess())
                return;

            LaunchForm<PRF05>();
        }

        private void picPgm07_Click(object sender, EventArgs e)
        {
            SaveUserSettings();
            LaunchForm<PRF07>();
        }

        private void btnSetStk_Click(object sender, EventArgs e)
        {
            SaveUserSettings();
            LaunchForm<PRF10>(FormWindowState.Normal);
        }

        private void btnConfig_Click(object sender, EventArgs e)
        {
            ShowPasswordConfigurationDialog();
        }

        private void picPRF08_Click(object sender, EventArgs e)
        {
            PrintPath = GetSelectedPrinterPath();
            SaveUserSettings();
            LaunchForm<PRF08>();
        }

        private void picPRF12_Click(object sender, EventArgs e)
        {
            if (!ValidateUserAccess("QC", "This program is allowed to QC member!", true))
                return;

            if (!ValidateFactoryAccess())
                return;

            PrintPath = GetSelectedPrinterPath();
            SaveUserSettings();
            LaunchForm<PRF12>();
        }

        private void childFrom_OnNotifyParent_LotNo_new1(object sender, ChildFormEventArgs e)
        {
            if (sender is PRF13 childForm)
            {
                lblps.Text = e.Message[0].ToString();
            }
        }
        #endregion

        #region 도우미 메서드
        private void UpdateFactorySelection(Button button)
        {
            btnFactory.Text = button.Text;
            btnFactory.Name = button.Name;
            _orderList.SelFactory = button.Name;
            pnlTop.Controls.Clear();
            SaveSettings();
            ShowProgramPanelIfValid();
        }

        private void ValidateAndUpdateEmployee(Button button)
        {
            using (var db = new CmCn())
            {
                string query = $"select goout_gubn, g_ip from cmv.dbo.thb01 " +
                             $"where empno='{button.Name}' and goout_gubn='1'";
                
                if (db.ResultReturnDataSet(query).Tables[0].Rows.Count == 0)
                {
                    MessageBox.Show("You can't use it!");
                    return;
                }
            }

            UpdateEmployeeInfo(button);
        }

        private void UpdateEmployeeInfo(Button button)
        {
            btnEmpno.Text = button.Text;
            btnEmpno.Name = button.Name;
            btnEmpno.Tag = button.Tag?.ToString();

            _orderList.SeluserIP = button.Tag?.ToString() ?? string.Empty;
            _orderList.SelKname = button.Text;
            _orderList.SelEmpno = button.Name;

            pnlTop.Controls.Clear();
            ShowProgramPanelIfValid();
            btnSetStk.Visible = (btnEmpno.Text == "root");
        }

        private void UpdateWarehouseSelection(Button button)
        {
            btnWH.Text = button.Text;
            btnWH.Name = button.Name;
            _orderList.SelWH = button.Name;
            pnlTop.Controls.Clear();
            ShowProgramPanelIfValid();
        }

        private void ShowSelectionPanel(DataSet dataSet, ButtonType buttonType, Color backColor)
        {
            pnlPGM.Visible = false;
            CreateDynamicButtons(dataSet, pnlTop, backColor, Color.Black, buttonType);
        }

        private bool ValidateUserAccess(string restrictedRole, string errorMessage, bool requireRole = false)
        {
            if ((requireRole && btnWH.Text != restrictedRole) || 
                (!requireRole && btnWH.Text == restrictedRole))
            {
                MessageBox.Show(errorMessage);
                return false;
            }
            return true;
        }

        private bool ValidateFactoryAccess()
        {
            _userFactory = btnFactory.Text.Substring(0, 3);
            
            using (var db = new CmCn())
            {
                string query = $"select factory from tst16c where opt_type='03' " +
                             $"and rtrim(remark)='{_orderList.SelEmpno}'";
                
                var result = db.ResultReturnDataSet(query);
                
                if (result.Tables[0].Rows.Count > 0 && 
                    _userFactory != result.Tables[0].Rows[0][0].ToString())
                {
                    MessageBox.Show($"Factory: {result.Tables[0].Rows[0][0]}, Selected {_userFactory}");
                    return false;
                }
            }
            
            return true;
        }

        private void ShowProgramPanelIfValid()
        {
            pnlPGM.Visible = !string.IsNullOrEmpty(btnEmpno.Text) && btnEmpno.Text != "Worker";
        }

        private string GetSelectedPrinterPath()
        {
            return rb_ip1.Checked ? rb_ip1.Text : rb_ip2.Text;
        }

        private void ShowPasswordConfigurationDialog()
        {
            _childViewForm = new PRF13(btnEmpno.Name);
            _childViewForm.OnNotifyParent += childFrom_OnNotifyParent_LotNo_new1;
            _childViewForm.ShowDialog();
            lblps.Text = _childViewForm.GetEmpPass(btnEmpno.Name);
        }

        private void LaunchForm<T>(FormWindowState windowState = FormWindowState.Maximized) where T : Form, new()
        {
            var form = new T();
            if (Public_Function.Validate_Form(form))
            {
                form.WindowState = windowState;
                form.Show();
            }
        }

        private void LoadPrinterOptions()
        {
            using (var db = new CmCn())
            {
                string query = $"select remark from tst16c where factory='{_userFactory}' " +
                             $"and opt_type='05' order by opt_code";
                
                var result = db.ResultReturnDataSet(query);
                
                if (result.Tables[0].Rows.Count > 0)
                {
                    rb_ip1.Text = result.Tables[0].Rows[0][0].ToString();
                    
                    if (result.Tables[0].Rows.Count > 1)
                    {
                        rb_ip2.Text = result.Tables[0].Rows[1][0].ToString();
                    }
                    else
                    {
                        rb_ip2.Visible = false;
                    }
                }
            }
        }

        private void ConfigurePanelLocation()
        {
            pnlPGM.Location = new Point(200, 200);
        }

        private void SaveSettings()
        {
            var settings = new AppSettings();
            settings.Factory = btnFactory.Text;
            settings.Save();
        }

        private void LogError(string message)
        {
            // 실제 구현에서는 로깅 프레임워크를 사용하거나 파일에 기록
            Debug.WriteLine($"[ERROR] {DateTime.Now}: {message}");
        }
        #endregion

        #region 지원 클래스
        private class AppSettings
        {
            private readonly ClsinitUtil _iniUtil;

            public AppSettings()
            {
                string configPath = GetConfigPath();
                _iniUtil = new ClsinitUtil(configPath);
            }

            private string GetConfigPath()
            {
                string programFilesPath = Environment.Is64BitOperatingSystem
                    ? @"C:\Program Files (x86)\Chemi_MES"
                    : @"C:\Program Files\Chemi_MES";
                
                return Path.Combine(programFilesPath, "CMES", "config_mes.ini");
            }

            public string Factory
            {
                get => _iniUtil.GetIniValue(USER_SECTION, FACTORY_KEY);
                set => _iniUtil.SetIniValue(USER_SECTION, FACTORY_KEY, value);
            }

            public string Server
            {
                get => _iniUtil.GetIniValue(SERVER_SECTION, SERVER_KEY);
                set => _iniUtil.SetIniValue(SERVER_SECTION, SERVER_KEY, value);
            }

            public void Save()
            {
                // 설정이 자동으로 저장됨 (ClsinitUtil이 내부적으로 처리)
            }
        }

        private class ApplicationUpdater
        {
            public void CheckAndUpdateApplication()
            {
                var config = new AppSettings();
                string serverPath = config.GetFileServerPath();
                string localPath = GetLocalApplicationPath();

                if (IsUpdateRequired(serverPath, localPath))
                {
                    ExecuteUpdate();
                }
            }

            private bool IsUpdateRequired(string serverPath, string localPath)
            {
                if (!File.Exists(serverPath) || !File.Exists(localPath))
                    return false;

                var serverFile = new FileInfo(serverPath);
                var localFile = new FileInfo(localPath);

                return serverFile.LastWriteTime != localFile.LastWriteTime;
            }

            private void ExecuteUpdate()
            {
                string updateToolPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"CMES\AsyncMES.exe");

                if (File.Exists(updateToolPath))
                {
                    Process.Start(updateToolPath);
                    Environment.Exit(0);
                }
            }
        }
        #endregion

        #region 폼 이벤트
        private void FrmMain_Load(object sender, EventArgs e)
        {
            // 추가 초기화 코드
        }
        #endregion
    }
}
