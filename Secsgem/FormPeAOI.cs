using System.IO.Ports;
using EventBus;
using MySql.Data.MySqlClient;


namespace SecsGemManager.Form;

/// <summary>
/// P-AOI(Post Automated Optical Inspection) 장비 관리 폼 클래스
/// 주요 기능:
/// 1. 파일 시스템 감시를 통한 검사 결과 파일 자동 처리
/// 2. 시리얼 포트를 통한 P-AOI 장비 제어
/// 3. 검사 결과 데이터 파싱 및 SECS/GEM 메시지 전송
/// 4. MySQL 데이터베이스에 검사 결과 저장
/// </summary>
public class FormPAOI : FormBase
{
    #region 상수 정의
    /// <summary>정상 운영 모드 명령 (0x03)</summary>
    private const byte CMD_NORMAL = 0x03;
    
    /// <summary>정지 모드 명령 (0x00)</summary>
    private const byte CMD_STOP = 0x00;
    
    /// <summary>파일 사용 대기 최대 시간 (밀리초)</summary>
    private const int FILE_WAIT_TIMEOUT_MS = 10000;
    
    /// <summary>파일 사용 대기 간격 (밀리초)</summary>
    private const int FILE_WAIT_INTERVAL_MS = 100;
    #endregion

    #region 필드 선언
    /// <summary>P-AOI 장비 정지/재개 제어용 시리얼 포트</summary>
    private readonly SerialPort? _stopHandlerSerialPort = new();
    
    /// <summary>처리 완료된 파일 백업 디렉토리</summary>
    private DirectoryInfo _backupDirectory;
    
    /// <summary>검사 결과 파일 모니터링용 파일 시스템 감시자</summary>
    private FileSystemWatcher? _fileSystemWatcher;
    
    /// <summary>시리얼 포트 쓰기 작업 동기화 객체</summary>
    private readonly object _serialPortLock = new();
    #endregion

    #region 이벤트 핸들러
    /// <summary>
    /// 폼 로드 시 초기화 작업 수행
    /// </summary>
    /// <param name="sender">이벤트 발생 객체</param>
    /// <param name="e">이벤트 인자</param>
    public override void FormBase_Load(object sender, EventArgs e)
    {
        try
        {
            // 부모 클래스의 초기화 작업 수행
            base.FormBase_Load(sender, e);
            
            // 백업 디렉토리 설정 및 생성
            InitializeBackupDirectory();
            
            // 파일 시스템 감시자 설정
            InitializeFileSystemWatcher();
            
            // 시리얼 포트 초기화 및 열기
            InitializeSerialPort();
            
            // 장비 초기화 (정상 모드 설정)
            Reset();
            
            LOGGER.Info("FormPAOI 초기화 완료");
        }
        catch (Exception ex)
        {
            LOGGER.Error($"FormPAOI 초기화 실패: {ex.Message}", ex);
            MessageBox.Show($"폼 초기화 중 오류가 발생했습니다: {ex.Message}", 
                          "초기화 오류", 
                          MessageBoxButtons.OK, 
                          MessageBoxIcon.Error);
            throw; // 상위 호출자에게 예외 전파
        }
    }
    
    /// <summary>
    /// 폼 닫힘 시 리소스 정리 작업 수행
    /// </summary>
    /// <param name="sender">이벤트 발생 객체</param>
    /// <param name="e">폼 닫힘 이벤트 인자</param>
    protected override void FormBase_FormClosing(object sender, FormClosingEventArgs e)
    {
        try
        {
            // 부모 클래스의 정리 작업 수행
            base.FormBase_FormClosing(sender, e);
            
            if (!e.Cancel)
            {
                LOGGER.Info("FormPAOI 종료 작업 시작");
                
                // 장비 정지 명령 전송
                StopWork();
                
                // 파일 시스템 감시자 중지
                CleanupFileSystemWatcher();
                
                // 시리얼 포트 닫기
                CleanupSerialPort();
                
                LOGGER.Info("FormPAOI 종료 작업 완료");
            }
        }
        catch (Exception ex)
        {
            LOGGER.Error($"FormPAOI 종료 중 오류: {ex.Message}", ex);
            // 예외 발생 시에도 계속 정리 작업 수행
        }
    }
    
    /// <summary>
    /// 파일 생성 이벤트 핸들러
    /// 새 검사 결과 파일이 생성되면 자동으로 처리
    /// </summary>
    /// <param name="sender">이벤트 발생 객체</param>
    /// <param name="e">파일 시스템 이벤트 인자</param>
    private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
    {
        // 폼이 준비 상태가 아닌 경우 처리 건너뜀
        if (!IsReady) 
        {
            LOGGER.Debug($"폼이 준비되지 않아 파일 처리 건너뜀: {e.FullPath}");
            return;
        }
        
        LOGGER.Info($"새 파일 생성 감지: {e.FullPath}");
        
        try
        {
            // 1. 파일 사용 가능 여부 대기
            if (!WaitForFileRelease(e.FullPath))
            {
                LOGGER.Warn($"파일이 다른 프로세스에서 사용 중입니다: {e.FullPath}");
                return;
            }
            
            // 2. 파일 파싱 및 데이터 처리
            ProcessPaoiFile(e.FullPath, e.Name);
            
            LOGGER.Info($"파일 처리 완료: {e.FullPath}");
        }
        catch (Exception ex)
        {
            LOGGER.Error($"파일 처리 중 오류 발생: {e.FullPath}", ex);
            // 파일 처리 실패 시 장비 정지
            StopWork();
        }
    }
    
    /// <summary>
    /// 디스플레이 이벤트 핸들러
    /// 화면 표시 시 장비 정지
    /// </summary>
    /// <param name="e">디스플레이 이벤트</param>
    [EventSubscriber]
    public override void OnDisplay(DisplayEvent e)
    {
        LOGGER.Info("디스플레이 이벤트 수신, 장비 정지");
        StopWork();
        base.OnDisplay(e);
    }
    #endregion

    #region 공개 메서드
    /// <summary>
    /// P-AOI 장비 초기화 (정상 모드 설정)
    /// </summary>
    public override void Reset()
    {
        try
        {
            LOGGER.Info("P-AOI 장비 초기화 시작");
            SendMessageToSerialPort(CMD_NORMAL);
            base.Reset();
            LOGGER.Info("P-AOI 장비 초기화 완료");
        }
        catch (Exception ex)
        {
            LOGGER.Error($"P-AOI 장비 초기화 실패: {ex.Message}", ex);
            throw;
        }
    }
    
    /// <summary>
    /// P-AOI 장비 정지 명령 전송
    /// </summary>
    public void StopWork()
    {
        try
        {
            LOGGER.Info("P-AOI 장비 정지 명령 전송");
            SendMessageToSerialPort(CMD_STOP);
        }
        catch (Exception ex)
        {
            LOGGER.Error($"P-AOI 장비 정지 명령 전송 실패: {ex.Message}", ex);
            throw;
        }
    }
    #endregion

    #region 비공개 메서드
    /// <summary>
    /// 백업 디렉토리 초기화
    /// </summary>
    private void InitializeBackupDirectory()
    {
        try
        {
            string backupPath = Program.Configuration["PAOI"]["LOG_BACKUP_PATH"].StringValue;
            
            if (string.IsNullOrWhiteSpace(backupPath))
            {
                LOGGER.Warn("백업 디렉토리 경로가 설정되지 않았습니다.");
                return;
            }
            
            _backupDirectory = new DirectoryInfo(backupPath);
            
            if (!_backupDirectory.Exists)
            {
                LOGGER.Info($"백업 디렉토리 생성: {backupPath}");
                _backupDirectory.Create();
            }
            else
            {
                LOGGER.Debug($"백업 디렉토리 사용: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            LOGGER.Error($"백업 디렉토리 초기화 실패: {ex.Message}", ex);
            throw;
        }
    }
    
    /// <summary>
    /// 파일 시스템 감시자 초기화
    /// </summary>
    private void InitializeFileSystemWatcher()
    {
        try
        {
            string monitorPath = Program.Configuration["PAOI"]["MONITORING_PATH"].StringValue;
            
            if (string.IsNullOrWhiteSpace(monitorPath))
            {
                throw new InvalidOperationException("모니터링 경로가 설정되지 않았습니다.");
            }
            
            if (!Directory.Exists(monitorPath))
            {
                throw new DirectoryNotFoundException($"모니터링 경로가 존재하지 않습니다: {monitorPath}");
            }
            
            _fileSystemWatcher = new FileSystemWatcher(monitorPath)
            {
                Filter = "*.txt",                    // .txt 파일만 감시
                IncludeSubdirectories = true,        // 하위 디렉토리 포함
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true           // 이벤트 활성화
            };
            
            _fileSystemWatcher.Created += FileSystemWatcher_Created;
            
            LOGGER.Info($"파일 시스템 감시자 초기화 완료: {monitorPath}");
        }
        catch (Exception ex)
        {
            LOGGER.Error($"파일 시스템 감시자 초기화 실패: {ex.Message}", ex);
            throw;
        }
    }
    
    /// <summary>
    /// 시리얼 포트 초기화 및 연결
    /// </summary>
    private void InitializeSerialPort()
    {
        try
        {
            string portName = Program.Configuration["PAOI"]["STOP_HANDLER_PORT"].StringValue;
            
            if (string.IsNullOrWhiteSpace(portName))
            {
                throw new InvalidOperationException("시리얼 포트 이름이 설정되지 않았습니다.");
            }
            
            _stopHandlerSerialPort.PortName = portName;
            _stopHandlerSerialPort.BaudRate = 9600;          // 통신 속도
            _stopHandlerSerialPort.Parity = Parity.None;     // 패리티 비트 없음
            _stopHandlerSerialPort.DataBits = 8;             // 데이터 비트 8
            _stopHandlerSerialPort.StopBits = StopBits.One;  // 정지 비트 1
            _stopHandlerSerialPort.ReadTimeout = 500;        // 읽기 타임아웃 500ms
            _stopHandlerSerialPort.WriteTimeout = 500;       // 쓰기 타임아웃 500ms
            
            _stopHandlerSerialPort.Open();
            
            LOGGER.Info($"시리얼 포트 연결 성공: {portName}");
        }
        catch (Exception ex)
        {
            LOGGER.Error($"시리얼 포트 연결 실패: {ex.Message}", ex);
            MessageBox.Show($"시리얼 포트 연결 실패: {ex.Message}", 
                          "통신 오류", 
                          MessageBoxButtons.OK, 
                          MessageBoxIcon.Error);
            throw;
        }
    }
    
    /// <summary>
    /// 파일 시스템 감시자 정리
    /// </summary>
    private void CleanupFileSystemWatcher()
    {
        try
        {
            if (_fileSystemWatcher != null)
            {
                _fileSystemWatcher.Created -= FileSystemWatcher_Created;
                _fileSystemWatcher.EnableRaisingEvents = false;
                _fileSystemWatcher.Dispose();
                _fileSystemWatcher = null;
                LOGGER.Info("파일 시스템 감시자 정리 완료");
            }
        }
        catch (Exception ex)
        {
            LOGGER.Error($"파일 시스템 감시자 정리 실패: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// 시리얼 포트 정리
    /// </summary>
    private void CleanupSerialPort()
    {
        try
        {
            if (_stopHandlerSerialPort != null && _stopHandlerSerialPort.IsOpen)
            {
                _stopHandlerSerialPort.Close();
                _stopHandlerSerialPort.Dispose();
                LOGGER.Info("시리얼 포트 정리 완료");
            }
        }
        catch (Exception ex)
        {
            LOGGER.Error($"시리얼 포트 정리 실패: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// P-AOI 검사 결과 파일 처리
    /// </summary>
    /// <param name="filePath">처리할 파일 경로</param>
    /// <param name="fileName">원본 파일 이름</param>
    private void ProcessPaoiFile(string filePath, string fileName)
    {
        // 1. 파일 파싱
        var pFramemap = P_FRAMEMAP.Parse(filePath, EQP_ID);
        
        // 2. SECS/GEM 메시지 전송
        _server.SendMessage(pFramemap);
        
        // 3. 데이터베이스에 검사 결과 저장
        SaveToDatabase(pFramemap);
        
        // 4. 파일 정리 (백업 또는 삭제)
        CleanupProcessedFile(filePath, fileName);
    }
    
    /// <summary>
    /// MySQL 데이터베이스에 검사 결과 저장
    /// SQL Injection 방지를 위한 파라미터화된 쿼리 사용
    /// </summary>
    /// <param name="pFramemap">파싱된 P-AOI 데이터</param>
    private void SaveToDatabase(P_FRAMEMAP pFramemap)
    {
        try
        {
            // PCB 측면에 따른 컬럼명 결정
            string type = pFramemap.pcbSide == "FRONT" ? "paoi_front" : "paoi_back";
            
            // 파라미터화된 쿼리로 SQL Injection 방지
            string query = @"
                INSERT INTO tb_mos_aoi_results (pcbserial, @type) 
                VALUES (@pcbSerial, @mapInfo) 
                ON DUPLICATE KEY UPDATE @type = @mapInfo";
            
            // 쿼리 파라미터화 (동적 컬럼명은 별도 처리 필요)
            query = query.Replace("@type", type);
            
            var parameters = new[]
            {
                new MySqlParameter("@pcbSerial", pFramemap.pcbSerial.Substring(1)),
                new MySqlParameter("@mapInfo", pFramemap.pcbMapInfo)
            };
            
            int affectedRows = MySqlHelper.ExecuteNonQuery(MYSQL_CONNECTION_STRING, query, parameters);
            
            LOGGER.Debug($"데이터베이스 저장 완료: {affectedRows} 행 영향 받음");
        }
        catch (MySqlException ex)
        {
            LOGGER.Error($"데이터베이스 저장 실패: {ex.Message}", ex);
            throw;
        }
        catch (Exception ex)
        {
            LOGGER.Error($"데이터베이스 저장 중 예외 발생: {ex.Message}", ex);
            throw;
        }
    }
    
    /// <summary>
    /// 처리 완료된 파일 정리 (백업 이동 또는 삭제)
    /// </summary>
    /// <param name="filePath">원본 파일 경로</param>
    /// <param name="fileName">원본 파일 이름</param>
    private void CleanupProcessedFile(string filePath, string fileName)
    {
        try
        {
            if (_backupDirectory != null && _backupDirectory.Exists)
            {
                // 백업 디렉토리로 파일 이동
                string backupPath = Path.Combine(_backupDirectory.FullName, fileName);
                File.Move(filePath, backupPath, true); // overwrite: true
                LOGGER.Debug($"파일 백업 완료: {backupPath}");
            }
            else
            {
                // 백업 디렉토리가 없으면 파일 삭제
                File.Delete(filePath);
                LOGGER.Debug($"파일 삭제 완료: {filePath}");
            }
        }
        catch (Exception ex)
        {
            LOGGER.Error($"파일 정리 실패: {filePath}", ex);
            throw;
        }
    }
    
    /// <summary>
    /// 파일이 다른 프로세스에서 사용 중인지 확인하고 해제될 때까지 대기
    /// </summary>
    /// <param name="filePath">확인할 파일 경로</param>
    /// <returns>파일 사용 가능 여부 (true: 사용 가능, false: 타임아웃)</returns>
    private bool WaitForFileRelease(string filePath)
    {
        DateTime startTime = DateTime.Now;
        
        while ((DateTime.Now - startTime).TotalMilliseconds < FILE_WAIT_TIMEOUT_MS)
        {
            try
            {
                // 파일 열기 시도로 사용 중인지 확인
                using (var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    // 성공적으로 열리면 사용 중이 아님
                    fileStream.Close();
                    LOGGER.Debug($"파일 사용 가능: {filePath}");
                    return true;
                }
            }
            catch (IOException)
            {
                // 파일이 사용 중이거나 잠겨있음, 잠시 대기
                Thread.Sleep(FILE_WAIT_INTERVAL_MS);
            }
            catch (Exception ex)
            {
                LOGGER.Error($"파일 접근 확인 중 오류: {filePath}", ex);
                return false;
            }
        }
        
        // 타임아웃 발생
        LOGGER.Warn($"파일 대기 시간 초과: {filePath}");
        return false;
    }
    
    /// <summary>
    /// 시리얼 포트로 단일 바이트 명령 전송 (스레드 안전)
    /// </summary>
    /// <param name="command">전송할 명령 바이트</param>
    private void SendMessageToSerialPort(byte command)
    {
        lock (_serialPortLock)
        {
            try
            {
                if (_stopHandlerSerialPort != null && _stopHandlerSerialPort.IsOpen)
                {
                    byte[] buffer = { command };
                    _stopHandlerSerialPort.Write(buffer, 0, buffer.Length);
                    LOGGER.Debug($"시리얼 포트 명령 전송: 0x{command:X2}");
                }
                else
                {
                    LOGGER.Warn("시리얼 포트가 열려있지 않아 명령을 전송할 수 없습니다.");
                }
            }
            catch (InvalidOperationException ex)
            {
                LOGGER.Error($"시리얼 포트 상태 오류: {ex.Message}", ex);
                throw;
            }
            catch (TimeoutException ex)
            {
                LOGGER.Error($"시리얼 포트 전송 타임아웃: {ex.Message}", ex);
                throw;
            }
            catch (Exception ex)
            {
                LOGGER.Error($"시리얼 포트 전송 오류: {ex.Message}", ex);
                throw;
            }
        }
    }
    #endregion
}
