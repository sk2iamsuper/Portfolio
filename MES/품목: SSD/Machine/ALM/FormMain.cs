/// <summary>
/// ALM 시스템의 메인 폼 - 전체 애플리케이션의 진입점 및 코디네이터
/// 모든 하위 관리자들을 통합하고 사용자 인터페이스 제공
/// </summary>
public partial class FormMain : Form
{
    #region Private Fields
    private static Logger Logger;
    
    // 핵심 관리자들 - 단일 책임 원칙에 따라 분리
    private readonly SoundManager _soundManager;    // 사운드 담당
    private readonly LotManager _lotManager;        // LOT 담당  
    private readonly StatusManager _statusManager;  // 상태 담당
    private readonly AppConfiguration _config;      // 설정 담당
    
    // 원본 시스템 컴포넌트
    private WorkerManager manager;                  // 장비 하드웨어 제어
    private bool onExitProcess;                     // 종료 진행 상태
    private DateTime starTime;                      // 작업 시작 시간
    #endregion

    #region Public Static Properties
    /// <summary>
    /// 전역 사용자 정보 - 세션 전체에서 공유
    /// </summary>
    public static DateTime StartTime = DateTime.MinValue;
    public static int UserId = 70;
    public static string UserName = "admin";
    public static string Department = "IT";
    
    /// <summary>
    /// 현재 LOT 정보 - 여러 컴포넌트에서 공유 접근
    /// </summary>
    public static LotInformation _lotInformation;
    public static CultureInfo currentCulture;
    public static bool isTestMode;
    #endregion

    #region Constructor
    /// <summary>
    /// FormMain 생성자 - 모든 관리자 클래스들을 초기화하고 의존성 주입
    /// </summary>
    public FormMain()
    {
        // 1. UI 컴포넌트 초기화
        InitializeComponent();
        InitializeUI();
        
        // 2. 이벤트 핸들러 등록
        InitializeEventHandlers();
        
        // 3. 관리자 클래스들 초기화 (의존성 주입)
        _soundManager = new SoundManager(currentCulture);
        _config = AppConfiguration.Load();
        
        // 4. 하위 관리자들 생성 (실제로는 DI 컨테이너 사용 권장)
        _lotManager = new LotManager(manager, _mosService, _config);
        _statusManager = new StatusManager(manager, _soundManager, _lotManager, this);
        
        // 5. MOS 시스템 초기화 (조건부 컴파일)
#if MOS
        _server = new SecsGemServer(Program.Configuration["General"]["EQP_IPADDRESS_PREFIX"].StringValue);
#endif
    }
    #endregion

    #region Public Methods - 다른 클래스에서 호출되는 메서드들
    /// <summary>
    /// StatusManager에서 호출 - 상태 표시 업데이트
    /// </summary>
    public void UpdateStatusLabels(bool ready, bool running, bool doorOpened, bool emergency)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<bool, bool, bool, bool>(UpdateStatusLabels), 
                ready, running, doorOpened, emergency);
            return;
        }

        toolStripStatusLabelStateReady.Enabled = ready;
        toolStripStatusLabelStateRunning.Enabled = running;
        toolStripStatusLabelStateDoorOpened.Enabled = doorOpened;
        toolStripStatusLabelStateEmergency.Enabled = emergency;
    }

    /// <summary>
    /// StatusManager에서 호출 - LOT PropertyGrid 활성화/비활성화
    /// </summary>
    public void SetLotPropertyGridEnabled(bool enabled)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<bool>(SetLotPropertyGridEnabled), enabled);
            return;
        }
        propertyGridLot.Enabled = enabled;
    }

    /// <summary>
    /// StatusManager에서 호출 - LOT PropertyGrid 초기화
    /// </summary>
    public void ClearLotPropertyGrid()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(ClearLotPropertyGrid));
            return;
        }
        propertyGridLot.SelectedObject = null;
    }

    /// <summary>
    /// 작업 이력 업데이트 - 여러 곳에서 호출
    /// </summary>
    public void UpdateHistories()
    {
        // 데이터베이스에서 최근 작업 이력 조회 및 UI 업데이트
        // ... 구현 내용
    }
    #endregion

    #region Event Handlers - 사용자 액션 처리
    /// <summary>
    /// LOT 입력 메뉴 클릭 처리 - LotManager에 위임
    /// </summary>
    private void inputLOTToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _soundManager.StopAll();
        
        var input = Interaction.InputBox("Please enter LOT id", "LOT id");
        if (string.IsNullOrEmpty(input)) return;

        // LotManager에 LOT 로드 위임
        if (_lotManager.LoadLot(input, UserName))
        {
            _soundManager.PlayReady();
            InitializeLotUI();
        }
    }

    /// <summary>
    /// 준비 설정 메뉴 클릭 처리
    /// </summary>
    private void setReadyToolStripMenuItem_Click(object sender, EventArgs e)
    {
        _soundManager.StopAll();
        
        Logger.Info("Set to ready.");
        _soundManager.PlayReady();
        
        if (!manager.SetToReady(true))
        {
            Logger.Warn("Could not initialize. Please check alarms.");
            MessageBox.Show("Could not initialize. Please check alarms.", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            ResetSystem();
        }
    }

    /// <summary>
    /// 애플리케이션 종료 처리 - 모든 리소스 정리
    /// </summary>
    private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (onExitProcess) return;

        if (MessageBox.Show("Do you really want to close this?", "Exit", 
            MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
        {
            ShutdownApplication();
        }
        else
        {
            e.Cancel = true;
        }
    }
    #endregion

    #region Private Methods - 내부 헬퍼 메서드들
    /// <summary>
    /// LOT 로드 성공 후 UI 초기화
    /// </summary>
    private void InitializeLotUI()
    {
        PropertyGridHelper.RefreshPropertyGrid(propertyGridLot, _lotManager.CurrentLot);
        propertyGridLot.Enabled = true;
        
        manager.frontScaraRobot.Init();
        manager.SetToReady(false);
        
        aoiControlFront.Clear();
        aoiControlLabel.Clear();
        aoiControlRear.Clear();
        
        PropertyGridHelper.SelectPropertyGridItemByName(propertyGridLot, "Top label");
    }

    /// <summary>
    /// 시스템 리셋 - LOT 정보 및 UI 초기화
    /// </summary>
    private void ResetSystem()
    {
        aoiControlFront.Clear();
        aoiControlLabel.Clear();
        aoiControlRear.Clear();
        _lotManager.ClearCurrentLot();
        ClearLotPropertyGrid();
    }

    /// <summary>
    /// 애플리케이션 종료 절차 - 모든 관리자 정리
    /// </summary>
    private void ShutdownApplication()
    {
        _soundManager.StopAll();
#if MOS
        _server?.Dispose();
#endif
        
        SimpleEventBus.GetDefaultEventBus().Post(new ExitAppEvent(), TimeSpan.Zero);
        
        var closeDown = new Thread(() =>
        {
            manager.Dispose();
            _soundManager.Dispose();
        });
        
        closeDown.Start();
        closeDown.Join();
        
        onExitProcess = true;
        Close();
        Application.ExitThread();
        Environment.Exit(0);
    }
    #endregion

    // ... 기타 많은 메서드들
}
