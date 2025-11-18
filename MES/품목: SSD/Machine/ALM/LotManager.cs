/// <summary>
/// LOT(생활) 정보의 전체 생명주기를 관리하는 클래스
/// LOT 로드, 검증, 설정 및 생산 준비 담당
/// </summary>
public class LotManager
{
    #region Private Fields
    private static readonly Logger Logger = LogManager.GetLogger("LotManager");
    
    private readonly WorkerManager _workerManager;  // 장비 관리자 참조
    private readonly IMosService _mosService;       // MOS 서비스 (조건부 컴파일)
    private readonly AppConfiguration _config;      // 애플리케이션 설정
    #endregion

    #region Public Properties
    /// <summary>
    /// 현재 로드된 LOT 정보
    /// </summary>
    public LotInformation CurrentLot { get; private set; }
    #endregion

    #region Constructor
    /// <summary>
    /// LotManager 생성자 - 의존성 주입
    /// </summary>
    public LotManager(WorkerManager workerManager, IMosService mosService, AppConfiguration config)
    {
        _workerManager = workerManager;
        _mosService = mosService;
        _config = config;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// LOT ID로 LOT 정보 로드
    /// </summary>
    /// <param name="lotId">로드할 LOT ID</param>
    /// <param name="userName">현재 사용자 이름</param>
    /// <returns>LOT 로드 성공 여부</returns>
    public bool LoadLot(string lotId, string userName)
    {
        // LOT ID 형식 검증
        if (!ValidateLotId(lotId)) return false;

        // LOT ID를 대문자로 표준화
        lotId = lotId.ToUpper();

        // 컴파일 모드에 따라 적절한 LOT 로드 방법 선택
#if MOS
        return LoadLotFromMOS(lotId, userName);
#else
        return LoadLotFromDatabase(lotId, userName);
#endif
    }

    /// <summary>
    /// 현재 LOT 정보 초기화
    /// </summary>
    public void ClearCurrentLot()
    {
        CurrentLot = null;
    }

    /// <summary>
    /// 생산 시작 전 LOT 정보 검증
    /// </summary>
    /// <returns>검증 성공 여부</returns>
    public bool ValidateLotForProduction()
    {
        // LOT 정보 존재 여부 확인
        if (CurrentLot == null)
        {
            ShowErrorMessage("Unable to start because no information has been entered.");
            _workerManager.SetToLed(ControlBoard.LED.RED, true);
            return false;
        }

#if !MOS
        // 비-MOS 모드에서 CCS 정보 검증
        if (!CurrentLot.CCS)
        {
            ShowErrorMessage("CCS information does not match.");
            _workerManager.SetToLed(ControlBoard.LED.RED, true);
            return false;
        }
#endif

        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// LOT ID 형식 검증
    /// </summary>
    private bool ValidateLotId(string lotId)
    {
        if (lotId.Length != Constants.LOT_ID_LENGTH)
        {
            MessageBox.Show(Constants.Messages.INVALID_LOT_ID, "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        return true;
    }

    /// <summary>
    /// MOS 시스템에서 LOT 정보 로드
    /// </summary>
    private bool LoadLotFromMOS(string lotId, string userName)
    {
        try
        {
            // MOS에 LOT 정보 요청
            var lotInfoResponse = _mosService.SendMessage(
                new LOTINFO(_config.EquipmentName, lotId, userName));
            
            if (!lotInfoResponse.Result.GetResult())
            {
                ShowErrorMessage(Constants.Messages.LOT_NOT_FOUND);
                return false;
            }

            // LOT 작업 단계 검증
            if (lotInfoResponse.Info.GetValue(LOTINFO.RESPONSE_ATTRIBUTE.STEPSEQ.ToString()) 
                != _config.OperationStepName)
            {
                ShowErrorMessage(Constants.Messages.LOT_STEP_MISMATCH);
                return false;
            }

            // PGM 정보 요청
            var pgmInfoResponse = _mosService.SendMessage(
                new LOTINFO(_config.EquipmentName, lotId, userName));
            
            if (!pgmInfoResponse.Result.GetResult())
            {
                ShowErrorMessage("PGM information not found from MOS.");
                return false;
            }

            // LOT 정보 객체 생성
            CurrentLot = new LotInformation(_workerManager, pgmInfoResponse.Info, 
                pgmInfoResponse.Info, lotId);
            
            return InitializeRobots();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load LOT from MOS");
            ShowErrorMessage("Failed to load LOT information from MOS.");
            return false;
        }
    }

    /// <summary>
    /// 데이터베이스에서 LOT 정보 로드
    /// </summary>
    private bool LoadLotFromDatabase(string lotId, string userName)
    {
        try
        {
            // 데이터베이스에서 LOT 기본 정보 조회
            var lotInformation = MySqlHelper.GetLotInformation(lotId);
            if (lotInformation == null)
            {
                ShowErrorMessage(Constants.Messages.LOT_NOT_FOUND);
                return false;
            }

            // LOT에 해당하는 라벨 목록 조회
            var queue = MySqlHelper.GetLabelList(lotId);
            CurrentLot = new LotInformation(_workerManager, lotInformation)
            {
                LabelQueue = queue
            };

            // 라벨 수량 검증 및 보정
            var totalCount = int.Parse(CurrentLot.totalCount);
            if (queue.Count < totalCount)
            {
                CurrentLot.LabelQueue = MySqlHelper.GetLabelList(lotId);
                if (CurrentLot.LabelQueue == null)
                {
                    ShowErrorMessage("Serial number generation failed. Please start again after generating the label.");
                    return false;
                }
            }

            return InitializeRobots();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load LOT from database");
            ShowErrorMessage("Failed to load LOT information from database.");
            return false;
        }
    }

    /// <summary>
    /// LOT 정보에 따라 로봇 프로파일 초기화
    /// </summary>
    private bool InitializeRobots()
    {
        // 모델에 해당하는 로봇 프로파일 조회
        var robotProfile = MySqlHelper.GetRobotProfile(CurrentLot.model);
        if (robotProfile == null)
        {
            ShowErrorMessage("Could not find robot profile data.");
            _workerManager.SetToLed(ControlBoard.LED.RED, true);
            return false;
        }

        // 전후면 로봇 프로파일 파싱 및 설정
        _workerManager.FrontProfile = ModelProfile.Parse(robotProfile["front_param"].ToString());
        _workerManager.RearProfile = ModelProfile.Parse(robotProfile["rear_param"].ToString());

        // 프로파일 파싱 결과 검증
        if (_workerManager.FrontProfile == null || _workerManager.RearProfile == null)
        {
            ShowErrorMessage("Invalid robot parameters. Please check on databases.");
            _workerManager.SetToLed(ControlBoard.LED.RED, true);
            return false;
        }

        // LOT 정보 로깅
        LogLotInformation();
        return true;
    }

    /// <summary>
    /// LOT 정보를 로그에 기록
    /// </summary>
    private void LogLotInformation()
    {
        Logger.Info($"LOT : {CurrentLot.name}");
        Logger.Info($"Product code : {CurrentLot.productCode}");
        Logger.Info($"Model : {CurrentLot.model}");
        Logger.Info($"Capacity : {CurrentLot.capacity}");
        Logger.Info($"Quantity : {CurrentLot.totalCount}");
    }

    /// <summary>
    /// 오류 메시지 표시 (헬퍼 메서드)
    /// </summary>
    private void ShowErrorMessage(string message)
    {
        MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    #endregion
}
