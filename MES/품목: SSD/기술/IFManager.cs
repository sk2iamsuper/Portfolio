using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using NLog;

namespace Interface
{
    /// <summary>
    /// SSD 테스트 장비의 메인 인터페이스 폼
    /// LOT 관리, 슬레이브 제어, 테스트 모니터링 등을 담당
    /// </summary>
    public partial class FormMain : Form
    {
        #region Fields and Properties

        // 로깅: NLog 로거 인스턴스
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        // ==================== 애플리케이션 상태 관리 ====================
        
        /// <summary>현재 로그인한 사용자 이름</summary>
        public static string UserName { get; set; } = "Diagnostics";
        
        /// <summary>현재 처리 중인 LOT 정보 (제품 정보, 설정값 등)</summary>
        public static Dictionary<string, object> LotInfo { get; set; }
        
        /// <summary>장비 설정 정보를 담은 문자열</summary>
        public static string SInfo { get; set; }
        
        /// <summary>LOT IN 상태 여부 (true: LOT 진행 중, false: LOT 없음)</summary>
        public static bool IsLotIn { get; set; }
        
        /// <summary>3rd Party 테스트 모드 여부</summary>
        public static bool IsThirdParty { get; set; }
        
        /// <summary>현재 장비 ID</summary>
        public static string EquipmentId => Settings.Default.EQUIPMENT_ID;
        
        /// <summary>S_INFO에서 추출한 모델 ID</summary>
        public static string SInfoModelId { get; set; } = string.Empty;
        
        /// <summary>S_INFO에서 추출한 펌웨어 이름</summary>
        public static string SInfoFwName { get; set; } = string.Empty;

        // ==================== 테스트 데이터 관리 ====================
        
        /// <summary>테스트 완료된 바코드 목록</summary>
        public static List<string> CompletedBarcodes { get; } = new List<string>();
        
        /// <summary>스크랩 코드 딕셔너리 (키: 시리얼, 값: (HBIN, 스크랩코드))</summary>
        public static Dictionary<string, Tuple<int, int>> ScrapCodeDictionary { get; } = new Dictionary<string, Tuple<int, int>>();
        
        /// <summary>현재 LOT의 수율 정보</summary>
        public static LotYield CurrentYield { get; } = new LotYield();

        // ==================== UI 컴포넌트 및 타이머 ====================
        
        /// <summary>스크롤 이벤트 처리를 위한 타이머</summary>
        private readonly Timer scrollTimer = new Timer();
        
        /// <summary>슬레이브 컨트롤 관리 딕셔너리 (키: 슬레이브 ID, 값: SlaveControl 객체)</summary>
        private readonly Dictionary<string, SlaveControl> slaves = new Dictionary<string, SlaveControl>();
        
        /// <summary>LOT 진행 시간 측정을 위한 타이머</summary>
        private readonly Timer lotTimer = new Timer();
        
        /// <summary>스레드 동기화를 위한 락 객체</summary>
        private readonly object lockObj = new object();
        
        /// <summary>현재 스크롤 중인지 여부</summary>
        private bool isScrolling;
        
        /// <summary>키 입력 버퍼 (포트 제어용)</summary>
        private string lastKeyInput = string.Empty;
        
        /// <summary>마지막으로 처리한 LOT ID</summary>
        private string lastLotId = string.Empty;
        
        /// <summary>LOT 진행 누적 시간 (초)</summary>
        private int totalSeconds;
        
        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// FormMain 생성자 - UI 컴포넌트 초기화 및 이벤트 설정
        /// </summary>
        public FormMain()
        {
            // 1. 문화권 설정 (다국어 지원)
            InitializeCulture();
            
            // 2. UI 컴포넌트 초기화 (Designer에서 생성)
            InitializeComponent();
            
            // 3. 슬레이브 컨트롤 동적 생성 및 배치
            InitializeSlaveControls();
            
            // 4. 이벤트 버스 및 통신 시스템 초기화
            InitializeEventSystem();
            
            // 5. 다양한 타이머 초기화
            InitializeTimers();
        }

        /// <summary>
        /// 애플리케이션 문화권 설정 (다국어 지원)
        /// </summary>
        private void InitializeCulture()
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(Settings.Default.LANGUAGE_STRING);
        }

        /// <summary>
        /// 슬레이브 컨트롤들을 동적으로 생성하고 테이블 레이아웃에 배치
        /// 각 슬레이브는 테스트 포트를 나타냄
        /// </summary>
        private void InitializeSlaveControls()
        {
            // 설정에서 포트 수를 읽어와 컬럼 수 결정 (4 * 포트수)
            tableLayoutPanelSlave.ColumnCount = 4 * Settings.Default.SLAVE_PORT;

            // 3개의 행에 대해 슬레이브 컨트롤 생성
            for (var row = 0; row < tableLayoutPanelSlave.RowCount; row++)
            {
                // 행별 오프셋 계산 (2, 1, 0 순서)
                var rowOffset = GetRowOffset(row);
                
                // 각 열에 슬레이브 컨트롤 생성
                for (var column = 0; column < tableLayoutPanelSlave.ColumnCount; column++)
                {
                    // 포트 번호 계산: 컬럼 + 1 + (행오프셋 * 전체컬럼수)
                    var port = column + 1 + rowOffset * tableLayoutPanelSlave.ColumnCount;
                    
                    // 슬레이브 컨트롤 생성 및 설정
                    var control = CreateSlaveControl(port);
                    
                    // 테이블 레이아웃에 컨트롤 추가
                    tableLayoutPanelSlave.Controls.Add(control, column, row);
                    
                    // 슬레이브 관리 딕셔너리에 추가
                    slaves[control.SlaveId] = control;
                }
            }
        }

        /// <summary>
        /// 행 번호에 따른 오프셋 계산
        /// UI 배치를 위해 역순으로 매핑
        /// </summary>
        /// <param name="row">행 인덱스 (0, 1, 2)</param>
        /// <returns>계산된 오프셋 값</returns>
        private int GetRowOffset(int row)
        {
            return row switch
            {
                0 => 2, // 첫 번째 행은 오프셋 2
                1 => 1, // 두 번째 행은 오프셋 1
                2 => 0, // 세 번째 행은 오프셋 0
                _ => 0  // 기본값
            };
        }

        /// <summary>
        /// 개별 슬레이브 컨트롤 생성 및 초기화
        /// </summary>
        /// <param name="port">할당할 포트 번호</param>
        /// <returns>초기화된 SlaveControl 객체</returns>
        private SlaveControl CreateSlaveControl(int port)
        {
            var control = new SlaveControl();
            control.SetPort(port); // 포트 번호 설정
            Logger.Debug($"Created slave control - Column: {port}, Port: {port}");
            return control;
        }

        /// <summary>
        /// 이벤트 시스템 및 통신 모듈 초기화
        /// </summary>
        private void InitializeEventSystem()
        {
            // UI 더블 버퍼링 설정 (깜빡임 방지)
            DoubleBufferedHelper.SetDoubleBufferedParent(this);
            
            // 이벤트 버스에 현재 폼 등록 (이벤트 수신 가능하도록)
            SimpleEventBus.GetDefaultEventBus().Register(this);
            
            // MOS 모드일 경우 SECS/GEM 서버 초기화
            #if MOS
            InitializeMosServer();
            #endif
        }

        /// <summary>
        /// 다양한 용도의 타이머들 초기화
        /// </summary>
        private void InitializeTimers()
        {
            // LOT 진행 시간 측정 타이머 (1초 간격)
            lotTimer.Interval = 1000;
            lotTimer.Tick += LotTimer_Tick;
            
            // 스크롤 이벤트 처리 타이머 (2초 간격)
            scrollTimer.Interval = 2000;
            scrollTimer.Tick += ScrollTimer_Tick;
        }

        #if MOS
        /// <summary>
        /// MOS 모드에서 SECS/GEM 통신 서버 초기화
        /// </summary>
        private void InitializeMosServer()
        {
            // 창 제목에 MOS 모드 표시
            Text = $"{Text} for MOS ({EquipmentId})";
            
            // SECS/GEM 서버 인스턴스 생성 및 시작
            SecsGemServer = new SecsGemServer(Settings.Default.EQP_IPADDRESS_PREFIX);
            SecsGemServer.Start(CONNECTION_MODE.PASSIVE, Settings.Default.MOS_PORT, Settings.Default.MOS_DEVICE_ID);
        }
        #endif
        
        #endregion

        #region Form Lifecycle Events

        /// <summary>
        /// 폼 로드 이벤트 핸들러 - 애플리케이션 초기화 수행
        /// </summary>
        private void FormMain_Load(object sender, EventArgs e)
        {
            // 1. 애플리케이션 기본 설정 초기화
            InitializeApplication();
            
            // 2. 임시 파일 정리
            CleanupTempFiles();
            
            // 3. 미전송 로그 처리 시작 (별도 스레드)
            StartUnsentLogProcessing();
            
            // 4. 모드에 따른 UI 업데이트
            UpdateUIForMode();
            
            // 디버그 모드에서 테스트용 LOT ID 설정
            #if DEBUG
            txtLot.Text = "L04TP10219";
            #endif
        }

        /// <summary>
        /// 애플리케이션 실행 시 필요한 초기화 작업 수행
        /// </summary>
        private void InitializeApplication()
        {
            // 데이터베이스 컬럼 존재 여부 확인
            MySQLHelper.CheckOcrColumnExist();
            
            // 버전 정보 설정
            SetVersionInfo();
        }

        /// <summary>
        /// 애플리케이션 버전 정보를 폼 제목에 표시
        /// </summary>
        private void SetVersionInfo()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var buildDate = BuildDateHelper.GetLinkerTime(Assembly.GetExecutingAssembly());
            Text += $" v{version} (Build : {buildDate})";
        }

        /// <summary>
        /// 임시 PNG 파일 정리 (이전 실행에서 남은 파일 삭제)
        /// </summary>
        private void CleanupTempFiles()
        {
            foreach (var file in Directory.GetFiles(Path.GetTempPath(), "*.png", SearchOption.TopDirectoryOnly))
            {
                FileHelper.SafeDelete(file, Logger);
            }
        }

        /// <summary>
        /// MOS/Non-MOS 모드에 따른 UI 요소 설정
        /// </summary>
        private void UpdateUIForMode()
        {
            #if !MOS
            // Non-MOS 모드: 장비 정보 및 사용자 표시
            toolStripStatusLabelInfo.Text = $"{EquipmentId} @{UserName} | {DateTime.Now}";
            UpdateLotHistories(); // LOT 히스토리 조회
            #else
            // MOS 모드: 특정 메뉴 비활성화
            changeOperatorToolStripMenuItem.Enabled = false;
            btnLotIn.Enabled = false;
            updateSINFOToolStripMenuItem.Enabled = true;
            issuesToolStripMenuItem.Enabled = false;
            operationStandardToolStripMenuItem.Enabled = false;
            chk3rdParty.Enabled = false;
            chk3rdParty.Checked = true; // 3rd Party 모드 기본 선택
            #endif

            // 그룹박스 원본 텍스트 저장 (나중에 count 추가하기 위해)
            groupBoxLot.Tag = groupBoxLot.Text;
            groupBoxSsd.Tag = groupBoxSsd.Text;
        }

        /// <summary>
        /// 폼 종료 이벤트 핸들러 - 리소스 정리 및 종료 확인
        /// </summary>
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 사용자에게 종료 확인
            if (!ConfirmExit()) 
            {
                e.Cancel = true; // 사용자가 취소하면 종료 중단
                return;
            }
            
            // 리소스 정리
            CleanupResources();
        }

        /// <summary>
        /// 사용자에게 애플리케이션 종료 확인
        /// </summary>
        /// <returns>사용자가 확인하면 true, 취소하면 false</returns>
        private bool ConfirmExit()
        {
            return MessageBoxEx.Show(
                strings.question_exit, 
                strings.exit_application_text, 
                strings.msg_ok, 
                strings.msg_cancel
            ) == DialogResult.OK;
        }

        /// <summary>
        /// 애플리케이션 종료 시 리소스 정리
        /// </summary>
        private void CleanupResources()
        {
            // 타이머 정지
            lotTimer?.Stop();
            scrollTimer?.Stop();
            
            // MOS 모드일 경우 통신 서버 정지
            #if MOS
            SecsGemServer?.Stop();
            #endif
        }
        
        #endregion

        #region Lot Management Core Logic

        /// <summary>
        /// LOT IN/OUT 버튼 클릭 이벤트 핸들러
        /// LOT 진행 상태에 따라 다른 동작 수행
        /// </summary>
        private void btnLotIn_Click(object sender, EventArgs e)
        {
            lastKeyInput = string.Empty; // 키 입력 버퍼 초기화
            var lotId = txtLot.Text.Trim(); // 입력된 LOT ID 읽기

            if (btnLotIn.Text == "LOT In")
            {
                // LOT IN 프로세스 시작
                StartLotProcess(lotId);
            }
            else
            {
                // LOT OUT 프로세스 시작
                EndLotProcess(lotId);
            }
        }

        /// <summary>
        /// LOT IN 프로세스 - LOT 시작 및 초기화
        /// </summary>
        /// <param name="lotId">시작할 LOT ID</param>
        private void StartLotProcess(string lotId)
        {
            // 1. 테스트 데이터 초기화
            ClearTestData();
            
            // 2. LOT 정보 유효성 검사
            if (!ValidateLotInfo(lotId)) return;
            
            // 3. 장비 지원 여부 확인
            if (!ValidateEquipmentSupport()) return;
            
            // 4. 인터페이스 설정 확인
            if (!ValidateInterfaceSettings()) return;
            
            // 5. LOT 데이터 초기화
            InitializeLotData(lotId);
            
            // 6. UI 업데이트
            UpdateLotUI();
            
            // 7. LOT 타이머 시작
            StartLotTimer();
            
            // 8. 상태 설정 및 이벤트 발행
            IsLotIn = true;
            SimpleEventBus.GetDefaultEventBus().Post(new LotStatusEvent(LotEventType.LOT_IN), TimeSpan.Zero);
        }

        /// <summary>
        /// LOT OUT 프로세스 - LOT 종료 및 정리
        /// </summary>
        /// <param name="lotId">종료할 LOT ID</param>
        private void EndLotProcess(string lotId)
        {
            // 1. 사용자에게 LOT OUT 확인
            var result = ConfirmLotOut();
            if (result == DialogResult.Cancel) return;

            // 2. LOT OUT 처리 수행
            if (result == DialogResult.Yes)
            {
                #if !MOS
                ProcessNonMosLotOut(); // Non-MOS 모드 LOT OUT
                #else
                ProcessMosLotOut(lotId); // MOS 모드 LOT OUT
                #endif
            }

            // 3. UI 초기화
            ResetLotUI();
            
            // 4. 상태 설정 및 이벤트 발행
            IsLotIn = false;
            SimpleEventBus.GetDefaultEventBus().Post(new LotStatusEvent(LotEventType.LOT_OUT), TimeSpan.Zero);
        }

        /// <summary>
        /// LOT 정보 유효성 검사 (MOS/Non-MOS 모드별 처리)
        /// </summary>
        /// <param name="lotId">검사할 LOT ID</param>
        /// <returns>유효하면 true, 아니면 false</returns>
        private bool ValidateLotInfo(string lotId)
        {
            #if !MOS
            return ValidateNonMosLotInfo(lotId); // Non-MOS 모드 검증
            #else
            return ValidateMosLotInfo(lotId); // MOS 모드 검증
            #endif
        }

        #if !MOS
        /// <summary>
        /// Non-MOS 모드 LOT 정보 검증
        /// 데이터베이스에서 LOT 정보 조회 및 검증
        /// </summary>
        private bool ValidateNonMosLotInfo(string lotId)
        {
            // 데이터베이스에서 LOT 정보 조회
            var row = MySQLHelper.GetRows(
                $"SELECT DISTINCT E.model_name, E.prod_code, E.interface FROM tb_mes_lotid L " +
                $"JOIN tb_mes_std_espec E ON L.espec_id = E.id WHERE L.lotid = '{lotId}'");

            // LOT 정보가 없으면 에러
            if (row == null || row.Count == 0)
            {
                ShowError(strings.error_lot_info);
                return false;
            }

            // 추가 검증 로직 (스텝 확인, Hold 상태 확인 등)
            // ... (생략된 검증 로직)
            
            return true;
        }
        #endif

        #if MOS
        /// <summary>
        /// MOS 모드 LOT 정보 검증
        /// SECS/GEM 메시지를 통해 MOS 서버에 LOT 정보 확인
        /// </summary>
        private bool ValidateMosLotInfo(string lotId)
        {
            // MOS 서버에 LOT 정보 요청
            var result = SecsGemServer.SendMessage(new LOT_INFO(EquipmentId, lotId, UserName), true);
            var lotState = result.Info.GetValue("LOTSTATE").ToUpper();

            // LOT 상태가 WAIT 또는 RUN이 아니면 에러
            if (!(lotState.Equals("WAIT") || lotState.Equals("RUN")))
            {
                Logger.Error($"Lot state error: {lotState}");
                ShowError(strings.error_lot_in_failed);
                return false;
            }

            // 추가 MOS 검증 로직 (스텝 시퀀스 확인 등)
            // ... (생략된 검증 로직)
            
            return true;
        }
        #endif

        /// <summary>
        /// 장비 지원 모델 확인
        /// 현재 LOT의 모델이 장비에서 지원하는지 확인
        /// </summary>
        /// <returns>지원하면 true, 아니면 false</returns>
        private bool ValidateEquipmentSupport()
        {
            // 데이터베이스에서 장비 지원 모델 정보 조회
            var row = MySQLHelper.GetRows(
                $"SELECT comment FROM new_mes.tb_equipment_list WHERE code = '{EquipmentId}'");
                
            var supportedModels = row[0]["comment"].ToString();
            
            // 모든 모델 지원 (*) 이면 바로 통과
            if (supportedModels.Equals("*")) return true;

            // 현재 LOT의 모델 이름
            var modelName = LotInfo["model_name"].ToString();
            var supportedList = supportedModels.Split(',');
            
            // 지원 모델 목록에 현재 모델이 포함되어 있으면 에러
            // (주석과 반대로 동작하는 것 같으나 원본 코드 유지)
            if (supportedList.Contains(modelName))
            {
                ShowError(strings.error_support_model);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 인터페이스 설정 확인
        /// LOT에 인터페이스 설정이 되어 있는지 확인
        /// </summary>
        /// <returns>설정되어 있으면 true, 아니면 false</returns>
        private bool ValidateInterfaceSettings()
        {
            if (string.IsNullOrEmpty(LotInfo["interface"]?.ToString()))
            {
                ShowError(strings.error_interface_not_set);
                ClearLotInput();
                return false;
            }
            return true;
        }

        /// <summary>
        /// LOT 데이터 초기화 (MOS/Non-MOS 모드별 처리)
        /// </summary>
        /// <param name="lotId">초기화할 LOT ID</param>
        private void InitializeLotData(string lotId)
        {
            #if !MOS
            InitializeNonMosLotData(lotId); // Non-MOS 모드 데이터 초기화
            #else
            InitializeMosLotData(lotId); // MOS 모드 데이터 초기화
            #endif

            // S_INFO에서 필드 값 추출
            ExtractSInfoFields();
            
            // AOI 검사 설정이 되어 있으면 ONNX 모델 실행
            if (Settings.Default.CHECK_AOI)
            {
                OnnxHelper.RunOnnx(LotInfo["ml_execute"].ToString());
            }
        }

        /// <summary>
        /// S_INFO 문자열에서 모델 ID와 펌웨어 이름 추출
        /// </summary>
        private void ExtractSInfoFields()
        {
            SInfoFwName = SInfo.Substring(SInfo.IndexOf("FWNAME=")).Split('\r')[0].Split('=')[1].Trim();
            SInfoModelId = SInfo.Substring(SInfo.IndexOf("MODELID=")).Split('\r')[0].Split('=')[1].Trim();
        }

        /// <summary>
        /// LOT IN 상태의 UI 업데이트
        /// </summary>
        private void UpdateLotUI()
        {
            // 버튼 텍스트 변경
            btnLotIn.Text = "LOT Out";
            txtLot.ReadOnly = true; // LOT ID 입력 잠금
            
            // 제품 정보 표시
            lblCOO.Text = StringHelper.GetCountryOfOrigin(LotInfo["prod_code"].ToString());
            lblProductCode.Text = LotInfo["prod_code"].ToString();
            lblModel.Text = $"{LotInfo["model_name"]} {LotInfo["capacity"]}";
            lblPGM.Text = GetPgmDisplayName();
            
            // 진행률 표시줄 표시
            toolStripProgressBar.Visible = true;
            lastLotId = txtLot.Text.Trim(); // 마지막 LOT ID 저장
        }

        /// <summary>
        /// PGM 파일 표시 이름 생성 (MOS/Non-MOS 모드별 처리)
        /// </summary>
        /// <returns>표시할 PGM 이름</returns>
        private string GetPgmDisplayName()
        {
            #if MOS
            return LotInfo["interface_pgm"].ToString(); // MOS: 전체 경로 표시
            #else
            return Path.GetFileName(LotInfo["interface_pgm"].ToString()).Replace(".pgm", ""); // Non-MOS: 파일명만 표시
            #endif
        }

        /// <summary>
        /// LOT OUT 시 UI 초기화
        /// </summary>
        private void ResetLotUI()
        {
            // 타이머 정지
            lotTimer.Stop();
            toolStripProgressBar.Visible = false;
            
            // 버튼 상태 복원
            btnLotIn.Text = "LOT In";
            
            // 라벨 초기화
            ClearLotLabels();
            
            // 데이터 그리드 초기화
            ClearDataGrid();
            
            // 입력 컨트롤 초기화
            txtLot.ReadOnly = false;
            txtLot.Text = string.Empty;
            txtLot.Focus();

            // 상태 변수 초기화
            LotInfo = null;
            chk3rdParty.Enabled = true;
            ClearSlaveControls();
            
            // S_INFO 필드 초기화
            SInfoModelId = string.Empty;
            SInfoFwName = string.Empty;
            
            // Non-MOS 모드일 경우 LOT 히스토리 업데이트
            #if !MOS
            UpdateLotHistories();
            #endif
        }

        /// <summary>
        /// LOT 정보 표시 라벨들 초기화
        /// </summary>
        private void ClearLotLabels()
        {
            // 수율 정보 라벨 초기화
            lblFail.Text = lblPass.Text = lblTotal.Text = lblWait.Text = lblYield.Text = string.Empty;
            
            // 부가 정보 라벨 초기화
            lblExtra.Text = $"{LotInfo?["lotid"]} Lot out.";
            lblCOO.Text = lblProductCode.Text = lblModel.Text = lblPGM.Text = string.Empty;
        }

        /// <summary>
        /// 테스트 데이터 초기화 (LOT IN 시 실행)
        /// </summary>
        private void ClearTestData()
        {
            SlaveControl.ClearRunningBarcodes(); // 실행 중인 바코드 초기화
            ScrapCodeDictionary.Clear(); // 스크랩 코드 딕셔너리 초기화
            CompletedBarcodes.Clear(); // 완료된 바코드 목록 초기화
        }
        
        #endregion

        #region Timer Events

        /// <summary>
        /// LOT 진행 시간 타이머 틱 이벤트
        /// 1초마다 실행되어 경과 시간 업데이트
        /// </summary>
        private void LotTimer_Tick(object sender, EventArgs e)
        {
            toolStripStatusLabelElapsed.Text = $"{TimeSpan.FromSeconds(++totalSeconds)}";
        }

        /// <summary>
        /// 스크롤 타이머 틱 이벤트
        /// 스크롤이 끝난 후 2초 뒤에 실행되어 스크롤 상태 초기화
        /// </summary>
        private void ScrollTimer_Tick(object sender, EventArgs e)
        {
            scrollTimer.Stop();
            isScrolling = false; // 스크롤 상태 초기화
        }
        
        #endregion

        #region Data Management and UI Updates

        /// <summary>
        /// 수율 정보 업데이트 (MOS/Non-MOS 모드별 처리)
        /// </summary>
        public void UpdateYield()
        {
            #if !MOS
            // Non-MOS: 데이터베이스에서 수율 정보 조회
            var yieldData = MySQLHelper.GetLotYield(LotInfo["id"].ToString());
            CurrentYield.UpdateFrom(yieldData);
            #else
            // MOS: 완료된 바코드와 스크랩 코드로 수율 계산
            CurrentYield.Pass = CompletedBarcodes.Count;
            CurrentYield.Fail = ScrapCodeDictionary.Count;
            #endif

            // UI에 수율 정보 표시
            UpdateYieldUI();
        }

        /// <summary>
        /// 수율 정보를 UI에 표시
        /// 크로스 스레드 호출을 고려하여 Invoke 사용
        /// </summary>
        private void UpdateYieldUI()
        {
            // UI 스레드가 아닌 경우 Invoke로 호출
            if (InvokeRequired)
            {
                Invoke(new Action(UpdateYieldUI));
                return;
            }

            // 수율 정보 라벨 업데이트
            lblTotal.Text = $"{CurrentYield.Total}EA";
            lblPass.Text = $"{CurrentYield.Pass}EA";
            lblFail.Text = $"{CurrentYield.Fail}EA";
            lblWait.Text = $"{CurrentYield.Wait}EA";
            lblYield.Text = $"{(CurrentYield.Yield * 100):F0}%";

            // 진행률 표시줄 업데이트
            toolStripProgressBar.Maximum = CurrentYield.Total;
            toolStripProgressBar.Value = CurrentYield.Pass;
        }

        /// <summary>
        /// SSD 테스트 히스토리 업데이트 (MOS/Non-MOS 모드별 처리)
        /// </summary>
        public void UpdateSsdHistories()
        {
            #if !MOS
            UpdateNonMosSsdHistories(); // Non-MOS 모드 히스토리 업데이트
            #else
            UpdateMosSsdHistories(); // MOS 모드 히스토리 업데이트
            #endif
        }

        #if !MOS
        /// <summary>
        /// Non-MOS 모드 SSD 테스트 히스토리 업데이트
        /// 데이터베이스에서 테스트 결과 조회
        /// </summary>
        private void UpdateNonMosSsdHistories()
        {
            var query = @"SELECT MAX(L.ssdsn) SN, COUNT(Y.label_id) C, GROUP_CONCAT(Y.scrap_code) R 
                         FROM tb_mes_dat_label L 
                         LEFT JOIN tb_li_dat_yield Y ON L.id = Y.label_id 
                         LEFT JOIN tb_mes_dat_setinfo S ON L.id = S.sn_id 
                         WHERE L.lot_id = {0} GROUP BY L.ssdsn";
            
            ExecuteDataGridUpdate(query, LotInfo["id"], groupBoxSsd);
        }
        #endif

        #if MOS
        /// <summary>
        /// MOS 모드 SSD 테스트 히스토리 업데이트
        /// 메모리 내 데이터로 테스트 결과 구성
        /// </summary>
        private void UpdateMosSsdHistories()
        {
            var table = new DataTable();
            table.Columns.Add("Serial", typeof(string));
            table.Columns.Add("HBIN", typeof(int));
            table.Columns.Add("Scrap Code", typeof(int));

            // 스크랩 코드 딕셔너리에서 데이터 추가
            foreach (var scrapCode in ScrapCodeDictionary)
            {
                table.Rows.Add(scrapCode.Key, scrapCode.Value.Item1, scrapCode.Value.Item2);
            }

            // 완료된 바코드 목록에서 데이터 추가 (PASS 제품)
            foreach (var barcode in CompletedBarcodes)
            {
                table.Rows.Add(barcode, 0, 0);
            }

            // 데이터 그리드 업데이트
            UpdateDataGridView(dataGridViewSSDs, table, groupBoxSsd);
        }
        #endif

        /// <summary>
        /// LOT 히스토리 업데이트 (오늘 처리된 LOT 목록 표시)
        /// </summary>
        public void UpdateLotHistories()
        {
            var query = @"SELECT L.lotid LOT, H.total T, H.total - H.fail_qty P, H.fail_qty F 
                         FROM tb_mes_lotid_history H 
                         JOIN tb_mes_lotid L ON H.lot_id = L.id 
                         WHERE H.process_id = 9 AND H.event_id = 2 
                         AND H.location = '{0}' 
                         AND H.created_on BETWEEN CONCAT(CURDATE(),' 00:00:00') AND CONCAT(CURDATE(),' 23:59:59') 
                         ORDER BY H.id DESC";
            
            ExecuteDataGridUpdate(query, EquipmentId, groupBoxLot);
        }

        /// <summary>
        /// 데이터베이스 쿼리 실행 및 데이터 그리드 업데이트
        /// </summary>
        /// <param name="query">실행할 SQL 쿼리 (파라미터 자리표시자 포함)</param>
        /// <param name="parameter">쿼리 파라미터</param>
        /// <param name="groupBox">업데이트할 그룹박스 컨트롤</param>
        private void ExecuteDataGridUpdate(string query, object parameter, Control groupBox)
        {
            try
            {
                using var connection = new MySqlConnection(Settings.Default.CONNECTION_STRING);
                connection.Open();
                
                // 쿼리 파라미터 치환
                var formattedQuery = string.Format(query, parameter);
                var adapter = new MySqlDataAdapter(formattedQuery, connection);
                var table = new DataTable();
                adapter.Fill(table);
                
                // 데이터 그리드 업데이트
                UpdateDataGridView(dataGridViewLots, table, groupBox);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Database query failed");
            }
        }

        /// <summary>
        /// 데이터 그리드 뷰 업데이트 (크로스 스레드 안전)
        /// </summary>
        /// <param name="dataGridView">업데이트할 DataGridView</param>
        /// <param name="table">바인딩할 DataTable</param>
        /// <param name="groupBox">개수 표시를 업데이트할 GroupBox</param>
        private void UpdateDataGridView(DataGridView dataGridView, DataTable table, Control groupBox)
        {
            // UI 스레드가 아닌 경우 Invoke로 호출
            if (InvokeRequired)
            {
                Invoke(new Action<DataGridView, DataTable, Control>(UpdateDataGridView), 
                      dataGridView, table, groupBox);
                return;
            }

            // 데이터 소스 바인딩
            dataGridView.DataSource = new BindingSource { DataSource = table };
            dataGridView.AutoResizeColumns(); // 컬럼 크기 자동 조정
            
            // 그룹박스 텍스트에 개수 표시 (예: "SSD Histories (25 EA)")
            groupBox.Text = $"{groupBox.Tag} ({dataGridView.RowCount} EA)";
        }
        
        #endregion

        #region Event Handlers and Subscribers

        /// <summary>
        /// 수율 업데이트 이벤트 구독자
        /// 테스트 완료 시 호출되어 수율 정보 업데이트
        /// </summary>
        [EventSubscriber]
        public void ReceivedEvent(UpdateYieldEvent e)
        {
            // 스레드 안전성을 위해 lock 사용
            lock(lockObj)
            {
                #if MOS
                UpdateMosSsdHistories(); // MOS 모드 SSD 히스토리 업데이트
                #else
                UpdateSsdHistories(); // Non-MOS 모드 SSD 히스토리 업데이트
                #endif
                UpdateYield(); // 수율 정보 업데이트
            }
        }

        /// <summary>
        /// LOT ID 텍스트 변경 이벤트 핸들러
        /// 입력에 따른 버튼 활성화 상태 제어
        /// </summary>
        private void txtLot_TextChanged(object sender, EventArgs e)
        {
            #if !MOS
            // Non-MOS 모드: LOT ID가 10자일 때만 LOT IN 버튼 활성화
            btnLotIn.Enabled = txtLot.Text.Length == 10;
            #endif
        }

        /// <summary>
        /// LOT ID 입력 키 다운 이벤트 핸들러
        /// 엔터 키로 LOT IN 실행, 위쪽 화살표로 이전 LOT ID 불러오기
        /// </summary>
        private void txtLot_KeyDown(object sender, KeyEventArgs e)
        {
            #if !MOS
            if (!IsLotIn && e.KeyCode == Keys.Enter)
            {
                // LOT IN 상태가 아니고 엔터 키 입력 시 LOT IN 실행
                btnLotIn.PerformClick();
            }
            else if (e.KeyCode == Keys.Up)
            {
                // 위쪽 화살표 키로 마지막 LOT ID 불러오기
                txtLot.Text = lastLotId;
            }
            #endif
        }

        /// <summary>
        /// 폼 키 프레스 이벤트 핸들러
        /// LOT IN 상태에서 특정 키 입력 처리 (포트 제어 등)
        /// </summary>
        private void FormMain_KeyPress(object sender, KeyPressEventArgs e)
        {
            // LOT IN 상태가 아니면 키 입력 버퍼 초기화
            if (!IsLotIn)
            {
                lastKeyInput = string.Empty;
                return;
            }

            // LOT IN 상태에서의 키 입력 처리
            HandleLotInKeyPress(e);
        }

        /// <summary>
        /// LOT IN 상태에서의 키 입력 처리
        /// "LOT Out" 입력 감지 및 포트 리테스트 기능
        /// </summary>
        /// <param name="e">키 프레스 이벤트 인자</param>
        private void HandleLotInKeyPress(KeyPressEventArgs e)
        {
            // 키 입력 버퍼에 문자 추가
            lastKeyInput += e.KeyChar;
            
            // "LOT Out" + 엔터 입력 감지 (LOT OUT 실행)
            if (lastKeyInput.Contains("LOT Out\r"))
            {
                btnLotIn.PerformClick(); // LOT OUT 실행
                e.Handled = true; // 이벤트 처리 완료 표시
                lastKeyInput = string.Empty; // 버퍼 초기화
                return;
            }

            // 포트 리테스트 시도 (2자리 숫자 입력 시)
            TryPortRetest();
        }

        /// <summary>
        /// 포트 리테스트 시도
        /// 2자리 숫자 입력 시 해당 포트 리테스트 실행
        /// </summary>
        private void TryPortRetest()
        {
            // 입력이 2자리가 아니면 종료
            if (lastKeyInput.Length != 2) return;

            // 숫자로 변환 시도
            if (int.TryParse(lastKeyInput.Trim(), out int port))
            {
                lastKeyInput = string.Empty; // 버퍼 초기화
                FindAndRetestSlave(port); // 해당 포트 찾아 리테스트
            }
        }

        /// <summary>
        /// 특정 포트 번호의 슬레이브 찾아 리테스트 실행
        /// </summary>
        /// <param name="port">리테스트할 포트 번호</param>
        private void FindAndRetestSlave(int port)
        {
            // 모든 슬레이브 컨트롤 중에서 해당 포트 찾기
            foreach (var control in tableLayoutPanelSlave.Controls.OfType<SlaveControl>())
            {
                if (control.GetPort() == port)
                {
                    control.Retest(); // 리테스트 실행
                    break;
                }
            }
        }
        
        #endregion

        #region Helper Methods

        /// <summary>
        /// 에러 메시지 표시 (공통 처리)
        /// </summary>
        /// <param name="message">표시할 에러 메시지</param>
        private void ShowError(string message)
        {
            MessageBox.Show(message, strings.error_text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            txtLot.Focus(); // LOT ID 입력창으로 포커스 이동
        }

        /// <summary>
        /// LOT OUT 확인 대화상자 표시
        /// 대기 중이거나 실패한 제품이 있을 경우 확인 요청
        /// </summary>
        /// <returns>사용자 선택 결과</returns>
        private DialogResult ConfirmLotOut()
        {
            // 모든 제품이 완료되었으면 바로 확인
            if (lblWait.Text.Equals("0EA") && lblFail.Text.Equals("0EA"))
            {
                return DialogResult.Yes;
            }

            // 대기 중이거나 실패한 제품이 있으면 확인 요청
            MessageBoxManager.Yes = strings.lot_out;
            MessageBoxManager.No = strings.lot_change;
            MessageBoxManager.Register();
            
            var result = MessageBox.Show(
                strings.question_lot_out, 
                strings.alert_text, 
                MessageBoxButtons.YesNoCancel, 
                MessageBoxIcon.Question, 
                MessageBoxDefaultButton.Button2
            );
            
            MessageBoxManager.Unregister();
            return result;
        }

        /// <summary>
        /// LOT 입력 관련 UI 초기화
        /// </summary>
        private void ClearLotInput()
        {
            clearToolStripMenuItem.PerformClick(); // 클리어 메뉴 실행
            txtLot.Text = string.Empty; // LOT ID 초기화
            txtLot.Focus(); // 입력창으로 포커스 이동
        }

        /// <summary>
        /// 모든 슬레이브 컨트롤 초기화
        /// </summary>
        private void ClearSlaveControls()
        {
            SimpleEventBus.GetDefaultEventBus().Post(new SlaveStatusEvent(SlaveEventType.CLEAR), TimeSpan.Zero);
        }

        /// <summary>
        /// SSD 데이터 그리드 초기화
        /// </summary>
        private void ClearDataGrid()
        {
            dataGridViewSSDs.DataSource = null; // 데이터 소스 제거
            groupBoxSsd.Text = groupBoxSsd.Tag.ToString(); // 원본 텍스트 복원
        }

        /// <summary>
        /// LOT 진행 타이머 시작
        /// </summary>
        private void StartLotTimer()
        {
            totalSeconds = 0; // 경과 시간 초기화
            lotTimer.Start(); // 타이머 시작
        }

        /// <summary>
        /// 미전송 로그 처리 스레드 시작
        /// </summary>
        private void StartUnsentLogProcessing()
        {
            new Thread(ProcessUnsentLogs).Start();
        }
        
        #endregion

        #region Unsent Log Processing

        /// <summary>
        /// 미전송 로그 처리 메인 메서드
        /// 별도 스레드에서 실행되어 로그 파일 업로드 및 정리
        /// </summary>
        private void ProcessUnsentLogs()
        {
            try
            {
                // 1. 미전송 로그 파일 목록 조회
                var logFiles = GetUnsentLogFiles();
                
                // 2. 로그 파일 처리 (업로드 및 삭제)
                ProcessLogFiles(logFiles);
                
                // 3. 빈 로그 디렉토리 정리
                CleanupLogDirectories();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unsent log processing failed");
            }
        }

        /// <summary>
        /// 미전송 로그 파일 목록 조회 (MOS/Non-MOS 모드별 경로)
        /// </summary>
        /// <returns>로그 파일 경로 배열</returns>
        private string[] GetUnsentLogFiles()
        {
            #if MOS
            // MOS 모드: MOS 하위 디렉토리의 TXT 파일
            var path = Path.Combine(UploadHelper.ApplicationLogPath, "MOS");
            return Directory.GetFiles(path, "*.TXT", SearchOption.AllDirectories);
            #else
            // Non-MOS 모드: 로그 디렉토리의 log 파일
            var path = UploadHelper.ApplicationLogPath;
            return Directory.GetFiles(path, "*.log", SearchOption.AllDirectories);
            #endif
        }

        /// <summary>
        /// 로그 파일들 처리 (업로드 및 삭제)
        /// </summary>
        /// <param name="files">처리할 로그 파일 경로 배열</param>
        private void ProcessLogFiles(string[] files)
        {
            var lotList = new Dictionary<string, object>(); // LOT별 날짜 정보 캐시
            
            foreach (var file in files)
            {
                ProcessSingleLogFile(file, lotList); // 개별 파일 처리
                Thread.Sleep(100); // 리소스 경합 방지를 위한 대기
            }
        }

        /// <summary>
        /// 개별 로그 파일 처리
        /// 파일 읽기, 업로드, 삭제 수행
        /// </summary>
        /// <param name="file">처리할 로그 파일 경로</param>
        /// <param name="lotList">LOT 날짜 정보 캐시 딕셔너리</param>
        private void ProcessSingleLogFile(string file, Dictionary<string, object> lotList)
        {
            try
            {
                var fileName = Path.GetFileName(file); // 파일명 추출
                var lotId = fileName.Split('_')[0]; // LOT ID 추출 (파일명에서)
                var fileContent = File.ReadAllText(file); // 파일 내용 읽기
                
                #if !MOS
                // Non-MOS 모드: LOT IN 시간 조회 및 캐싱
                if (!lotList.ContainsKey(lotId))
                {
                    var lotInTime = MySQLHelper.GetLotInTime(lotId);
                    lotList[lotId] = lotInTime.Split(' ')[0].Replace("-", ""); // YYYYMMDD 형식
                }
                var date = lotList[lotId].ToString();
                UploadUnsentLog(date, fileName, fileContent); // 날짜 정보와 함께 업로드
                #else
                // MOS 모드: 날짜 정보 없이 업로드
                UploadUnsentLog("", fileName, fileContent);
                #endif

                // 파일 처리 후 삭제
                FileHelper.SafeDelete(file, Logger);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to process log file: {file}");
            }
        }

        /// <summary>
        /// 로그 디렉토리 정리 (빈 디렉토리 삭제)
        /// </summary>
        private void CleanupLogDirectories()
        {
            #if MOS
            var path = Path.Combine(UploadHelper.ApplicationLogPath, "MOS");
            #else
            var path = UploadHelper.ApplicationLogPath;
            #endif

            // 모든 하위 디렉토리 삭제 시도
            foreach (var dir in Directory.GetDirectories(path))
            {
                DirectoryHelper.SafeDelete(dir, Logger);
                Thread.Sleep(100); // 리소스 경합 방지
            }
        }

        /// <summary>
        /// 미전송 로그 업로드 (MOS/Non-MOS 모드별 업로드 방식)
        /// </summary>
        /// <param name="date">업로드 날짜 (Non-MOS 모드에서 사용)</param>
        /// <param name="fileName">로그 파일명</param>
        /// <param name="content">로그 파일 내용</param>
        private void UploadUnsentLog(string date, string fileName, string content)
        {
            #if MOS
            // MOS 모드: MOS 전용 업로드
            UploadHelper.UploadMos("MOS", fileName, content, true);
            #else
            // Non-MOS 모드: 날짜/LOT_ID 경로로 업로드
            var lotId = fileName.Split('_')[0];
            var destinationFolder = $@"{date}/{lotId}";
            UploadHelper.Upload(destinationFolder, fileName, content, true);
            #endif
        }
        
        #endregion

        // ... (기타 UI 이벤트 핸들러들 - 도구 메뉴, 슬레이브 제어 등)
    }

    #region Helper Classes

    /// <summary>
    /// LOT 수율 정보를 관리하는 클래스
    /// 총수량, Pass, Fail, 대기수량, 수율률 계산 제공
    /// </summary>
    public class LotYield
    {
        /// <summary>총 제품 수량</summary>
        public int Total { get; set; }
        
        /// <summary>양품 수량</summary>
        public int Pass { get; set; }
        
        /// <summary>불량품 수량</summary>
        public int Fail { get; set; }
        
        /// <summary>대기 중인 제품 수량 (계산 속성)</summary>
        public int Wait => Total - Pass - Fail;
        
        /// <summary>수율률 (계산 속성)</summary>
        public double Yield => Total > 0 ? (double)Pass / Total : 0;

        /// <summary>
        /// 다른 LotYield 객체에서 값 복사
        /// </summary>
        /// <param name="other">복사할 원본 객체</param>
        public void UpdateFrom(LotYield other)
        {
            Total = other.Total;
            Pass = other.Pass;
            Fail = other.Fail;
        }
    }

    /// <summary>
    /// 파일 작업을 위한 안전한 헬퍼 클래스
    /// 예외 처리와 로깅을 포함한 파일 작업 제공
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// 안전한 파일 삭제 (예외 처리 및 로깅 포함)
        /// </summary>
        /// <param name="filePath">삭제할 파일 경로</param>
        /// <param name="logger">로깅을 위한 NLog 인스턴스</param>
        public static void SafeDelete(string filePath, Logger logger)
        {
            try
            {
                File.Delete(filePath);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to delete file: {filePath}");
            }
        }
    }

    /// <summary>
    /// 디렉토리 작업을 위한 안전한 헬퍼 클래스
    /// 예외 처리와 로깅을 포함한 디렉토리 작업 제공
    /// </summary>
    public static class DirectoryHelper
    {
        /// <summary>
        /// 안전한 디렉토리 삭제 (예외 처리 및 로깅 포함)
        /// </summary>
        /// <param name="directoryPath">삭제할 디렉토리 경로</param>
        /// <param name="logger">로깅을 위한 NLog 인스턴스</param>
        public static void SafeDelete(string directoryPath, Logger logger)
        {
            try
            {
                Directory.Delete(directoryPath, true); // 재귀적 삭제
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to delete directory: {directoryPath}");
            }
        }
    }
    
    #endregion
}
