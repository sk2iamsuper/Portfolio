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

public class PaoiFileProcessor : IDisposable
{
    #region 상수
    private const int FILE_WAIT_TIMEOUT_MS = 10000;
    private const int FILE_WAIT_INTERVAL_MS = 100;
    private const int MAX_RETRY_ATTEMPTS = 3;
    private const int RETRY_DELAY_MS = 1000;
    #endregion

    #region 필드
    private readonly DirectoryInfo _backupDirectory;
    private readonly string _monitoringPath;
    private readonly string _eqpId;
    private readonly ISecsGemServer _server;
    private readonly string _mysqlConnectionString;
    private FileSystemWatcher? _fileWatcher;
    private readonly IPaoiFileEventHandler _eventHandler;
    private readonly ILogger _logger;
    private bool _isDisposed;
    #endregion

    #region 이벤트
    /// <summary>파일 처리 시작 시 발생</summary>
    public event EventHandler<FileProcessingStartedEventArgs>? FileProcessingStarted;
    
    /// <summary>파일 처리 완료 시 발생</summary>
    public event EventHandler<FileProcessingCompletedEventArgs>? FileProcessingCompleted;
    
    /// <summary>파일 처리 실패 시 발생</summary>
    public event EventHandler<FileProcessingFailedEventArgs>? FileProcessingFailed;
    #endregion

    #region 생성자
    /// <summary>
    /// PaoiFileProcessor 생성자
    /// </summary>
    public PaoiFileProcessor(
        string monitoringPath,
        string backupPath,
        string eqpId,
        ISecsGemServer server,
        string mysqlConnectionString,
        IPaoiFileEventHandler? eventHandler = null,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(monitoringPath))
            throw new ArgumentException("모니터링 경로는 필수입니다.", nameof(monitoringPath));
        
        if (string.IsNullOrWhiteSpace(backupPath))
            throw new ArgumentException("백업 경로는 필수입니다.", nameof(backupPath));

        _monitoringPath = monitoringPath;
        _eqpId = eqpId ?? throw new ArgumentNullException(nameof(eqpId));
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _mysqlConnectionString = mysqlConnectionString ?? 
            throw new ArgumentNullException(nameof(mysqlConnectionString));
        
        _backupDirectory = InitializeBackupDirectory(backupPath);
        _eventHandler = eventHandler ?? new DefaultPaoiFileEventHandler();
        _logger = logger ?? new NullLogger();
        
        InitializeFileSystemWatcher();
    }
    #endregion

    #region 공개 메서드
    /// <summary>
    /// 파일 처리기 시작
    /// </summary>
    public void Start()
    {
        if (_fileWatcher != null && !_fileWatcher.EnableRaisingEvents)
        {
            _fileWatcher.EnableRaisingEvents = true;
            _logger.Info($"파일 처리기 시작: {_monitoringPath}");
        }
    }

    /// <summary>
    /// 파일 처리기 중지
    /// </summary>
    public void Stop()
    {
        if (_fileWatcher != null && _fileWatcher.EnableRaisingEvents)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _logger.Info("파일 처리기 중지");
        }
    }

    /// <summary>
    /// 특정 파일 수동 처리
    /// </summary>
    public ProcessingResult ProcessFileManually(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.Warn($"파일이 존재하지 않습니다: {filePath}");
            return ProcessingResult.FileNotFound;
        }

        return ProcessFileInternal(filePath, Path.GetFileName(filePath), isManual: true);
    }

    /// <summary>
    /// 현재 모니터링 중인 경로 반환
    /// </summary>
    public string GetMonitoringPath() => _monitoringPath;

    /// <summary>
    /// 현재 백업 디렉토리 반환
    /// </summary>
    public string GetBackupDirectoryPath() => _backupDirectory.FullName;
    #endregion

    #region 비공개 메서드
    /// <summary>
    /// 파일 시스템 감시자 초기화
    /// </summary>
    private void InitializeFileSystemWatcher()
    {
        try
        {
            if (!Directory.Exists(_monitoringPath))
            {
                _logger.Warn($"모니터링 경로가 존재하지 않아 생성합니다: {_monitoringPath}");
                Directory.CreateDirectory(_monitoringPath);
            }

            _fileWatcher = new FileSystemWatcher(_monitoringPath)
            {
                Filter = "*.txt",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            // 이벤트 핸들러 등록
            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Error += OnFileWatcherError;

            _logger.Info($"파일 시스템 감시자 초기화 완료: {_monitoringPath}");
        }
        catch (Exception ex)
        {
            _logger.Error($"파일 시스템 감시자 초기화 실패: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// 백업 디렉토리 초기화
    /// </summary>
    private DirectoryInfo InitializeBackupDirectory(string backupPath)
    {
        try
        {
            var directory = new DirectoryInfo(backupPath);
            
            if (!directory.Exists)
            {
                _logger.Info($"백업 디렉토리 생성: {backupPath}");
                directory.Create();
            }
            
            return directory;
        }
        catch (Exception ex)
        {
            _logger.Error($"백업 디렉토리 초기화 실패: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// 파일 생성 이벤트 핸들러
    /// </summary>
    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        // 이벤트를 비동기로 처리하여 UI 블로킹 방지
        Task.Run(() => ProcessFileAsync(e.FullPath, e.Name));
    }

    /// <summary>
    /// 파일 변경 이벤트 핸들러 (파일 쓰기 완료 확인용)
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _logger.Debug($"파일 변경 감지: {e.FullPath}");
    }

    /// <summary>
    /// 파일 감시자 오류 이벤트 핸들러
    /// </summary>
    private void OnFileWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.Error($"파일 시스템 감시자 오류: {ex.Message}", ex);
        
        // 오류 발생 시 감시자 재시작
        RestartFileWatcher();
    }

    /// <summary>
    /// 파일 비동기 처리
    /// </summary>
    private async Task ProcessFileAsync(string filePath, string fileName)
    {
        try
        {
            OnFileProcessingStarted(filePath, fileName);

            var result = await Task.Run(() => 
                ProcessFileInternal(filePath, fileName, isManual: false));

            if (result == ProcessingResult.Success)
            {
                OnFileProcessingCompleted(filePath, fileName);
            }
            else
            {
                OnFileProcessingFailed(filePath, fileName, result);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"파일 처리 중 예외 발생: {filePath}", ex);
            OnFileProcessingFailed(filePath, fileName, ProcessingResult.Exception, ex);
        }
    }

    /// <summary>
    /// 파일 처리 내부 로직
    /// </summary>
    private ProcessingResult ProcessFileInternal(string filePath, string fileName, bool isManual)
    {
        try
        {
            _logger.Info($"파일 처리 시작: {filePath} (수동: {isManual})");

            // 1. 파일 사용 가능 여부 확인 (재시도 로직 포함)
            if (!WaitForFileReady(filePath))
            {
                _logger.Warn($"파일 사용 대기 시간 초과: {filePath}");
                return ProcessingResult.FileLocked;
            }

            // 2. 파일 읽기 및 파싱
            var pFramemap = ParsePaoiFileWithRetry(filePath);
            if (pFramemap == null)
            {
                _logger.Error($"파일 파싱 실패: {filePath}");
                return ProcessingResult.ParseFailed;
            }

            // 3. SECS/GEM 메시지 전송
            SendSecsGemMessageWithRetry(pFramemap);

            // 4. 데이터베이스 저장
            SaveToDatabaseWithRetry(pFramemap);

            // 5. 파일 정리
            CleanupProcessedFile(filePath, fileName);

            _logger.Info($"파일 처리 완료: {filePath}");
            return ProcessingResult.Success;
        }
        catch (Exception ex)
        {
            _logger.Error($"파일 처리 실패: {filePath}", ex);
            return ProcessingResult.Exception;
        }
    }

    /// <summary>
    /// 재시도 로직을 포함한 파일 파싱
    /// </summary>
    private P_FRAMEMAP? ParsePaoiFileWithRetry(string filePath)
    {
        for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
        {
            try
            {
                return P_FRAMEMAP.Parse(filePath, _eqpId);
            }
            catch (Exception ex) when (attempt < MAX_RETRY_ATTEMPTS)
            {
                _logger.Warn($"파일 파싱 시도 {attempt} 실패: {ex.Message}");
                Thread.Sleep(RETRY_DELAY_MS);
            }
        }
        
        return null;
    }

    /// <summary>
    /// 재시도 로직을 포함한 SECS/GEM 메시지 전송
    /// </summary>
    private void SendSecsGemMessageWithRetry(P_FRAMEMAP pFramemap)
    {
        for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
        {
            try
            {
                _server.SendMessage(pFramemap);
                _logger.Debug($"SECS/GEM 메시지 전송 성공");
                return;
            }
            catch (Exception ex) when (attempt < MAX_RETRY_ATTEMPTS)
            {
                _logger.Warn($"SECS/GEM 전송 시도 {attempt} 실패: {ex.Message}");
                Thread.Sleep(RETRY_DELAY_MS);
            }
        }
        
        throw new InvalidOperationException($"SECS/GEM 메시지 전송 실패 (최대 재시도 횟수 초과)");
    }

    /// <summary>
    /// 재시도 로직을 포함한 데이터베이스 저장
    /// </summary>
    private void SaveToDatabaseWithRetry(P_FRAMEMAP pFramemap)
    {
        for (int attempt = 1; attempt <= MAX_RETRY_ATTEMPTS; attempt++)
        {
            try
            {
                SaveToDatabase(pFramemap);
                _logger.Debug($"데이터베이스 저장 성공");
                return;
            }
            catch (Exception ex) when (attempt < MAX_RETRY_ATTEMPTS)
            {
                _logger.Warn($"데이터베이스 저장 시도 {attempt} 실패: {ex.Message}");
                Thread.Sleep(RETRY_DELAY_MS);
            }
        }
        
        throw new InvalidOperationException($"데이터베이스 저장 실패 (최대 재시도 횟수 초과)");
    }

    /// <summary>
    /// 파일이 사용 가능한 상태가 될 때까지 대기
    /// </summary>
    private bool WaitForFileReady(string filePath)
    {
        var startTime = DateTime.Now;
        
        while ((DateTime.Now - startTime).TotalMilliseconds < FILE_WAIT_TIMEOUT_MS)
        {
            try
            {
                if (IsFileReady(filePath))
                {
                    return true;
                }
            }
            catch (IOException ex)
            {
                _logger.Debug($"파일 접근 실패 (재시도): {ex.Message}");
            }
            
            Thread.Sleep(FILE_WAIT_INTERVAL_MS);
        }
        
        return false;
    }

    /// <summary>
    /// 파일이 읽기/쓰기 가능한 상태인지 확인
    /// </summary>
    private bool IsFileReady(string filePath)
    {
        try
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // 파일 크기가 0보다 큰지 추가 확인 (쓰기 완료 여부)
                if (stream.Length == 0)
                {
                    return false; // 아직 쓰기 중일 수 있음
                }
                
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 데이터베이스에 검사 결과 저장
    /// </summary>
    private void SaveToDatabase(P_FRAMEMAP pFramemap)
    {
        try
        {
            string type = pFramemap.pcbSide == "FRONT" ? "paoi_front" : "paoi_back";
            string pcbSerial = pFramemap.pcbSerial?.Length > 1 ? 
                pFramemap.pcbSerial.Substring(1) : pFramemap.pcbSerial;

            // 파라미터화된 쿼리로 SQL Injection 방지
            string query = $@"
                INSERT INTO tb_mos_aoi_results (pcbserial, {type}) 
                VALUES (@pcbSerial, @mapInfo) 
                ON DUPLICATE KEY UPDATE {type} = @mapInfo";

            var parameters = new[]
            {
                new MySqlParameter("@pcbSerial", pcbSerial),
                new MySqlParameter("@mapInfo", pFramemap.pcbMapInfo)
            };

            int affectedRows = MySqlHelper.ExecuteNonQuery(_mysqlConnectionString, query, parameters);
            
            _logger.Debug($"데이터베이스 저장 완료: {affectedRows} 행 영향 받음");
        }
        catch (Exception ex)
        {
            _logger.Error($"데이터베이스 저장 실패: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// 처리 완료된 파일 정리
    /// </summary>
    private void CleanupProcessedFile(string filePath, string fileName)
    {
        try
        {
            if (_backupDirectory.Exists)
            {
                // 중복 파일명 처리
                string backupFileName = GetUniqueBackupFileName(fileName);
                string backupPath = Path.Combine(_backupDirectory.FullName, backupFileName);
                
                File.Move(filePath, backupPath, true);
                _logger.Debug($"파일 백업 완료: {backupPath}");
            }
            else
            {
                File.Delete(filePath);
                _logger.Debug($"파일 삭제 완료: {filePath}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"파일 정리 실패: {filePath}", ex);
            throw;
        }
    }

    /// <summary>
    /// 고유한 백업 파일명 생성
    /// </summary>
    private string GetUniqueBackupFileName(string originalFileName)
    {
        string baseName = Path.GetFileNameWithoutExtension(originalFileName);
        string extension = Path.GetExtension(originalFileName);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        return $"{baseName}_{timestamp}{extension}";
    }

    /// <summary>
    /// 파일 감시자 재시작
    /// </summary>
    private void RestartFileWatcher()
    {
        try
        {
            Stop();
            Thread.Sleep(1000); // 잠시 대기
            InitializeFileSystemWatcher();
            Start();
            _logger.Info("파일 감시자 재시작 완료");
        }
        catch (Exception ex)
        {
            _logger.Error($"파일 감시자 재시작 실패: {ex.Message}", ex);
        }
    }
    #endregion

    #region 이벤트 발생 메서드
    private void OnFileProcessingStarted(string filePath, string fileName)
    {
        FileProcessingStarted?.Invoke(this, 
            new FileProcessingStartedEventArgs(filePath, fileName));
    }

    private void OnFileProcessingCompleted(string filePath, string fileName)
    {
        FileProcessingCompleted?.Invoke(this, 
            new FileProcessingCompletedEventArgs(filePath, fileName));
    }

    private void OnFileProcessingFailed(string filePath, string fileName, 
        ProcessingResult result, Exception? exception = null)
    {
        FileProcessingFailed?.Invoke(this, 
            new FileProcessingFailedEventArgs(filePath, fileName, result, exception));
    }
    #endregion

    #region IDisposable 구현
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                _fileWatcher?.Dispose();
                _logger.Info("파일 처리기 리소스 정리 완료");
            }
            
            _isDisposed = true;
        }
    }
    #endregion
}

#region 지원 클래스 및 인터페이스

/// <summary>
/// 파일 처리 결과 열거형
/// </summary>
public enum ProcessingResult
{
    Success,
    FileNotFound,
    FileLocked,
    ParseFailed,
    DatabaseError,
    CommunicationError,
    Exception
}

/// <summary>
/// 파일 처리 이벤트 핸들러 인터페이스
/// </summary>
public interface IPaoiFileEventHandler
{
    void OnFileProcessingStarted(string filePath);
    void OnFileProcessingCompleted(string filePath);
    void OnFileProcessingFailed(string filePath, Exception exception);
}

/// <summary>
/// 기본 파일 처리 이벤트 핸들러
/// </summary>
public class DefaultPaoiFileEventHandler : IPaoiFileEventHandler
{
    private readonly ILogger _logger;

    public DefaultPaoiFileEventHandler(ILogger? logger = null)
    {
        _logger = logger ?? new NullLogger();
    }

    public void OnFileProcessingStarted(string filePath)
    {
        _logger.Info($"파일 처리 시작: {filePath}");
    }

    public void OnFileProcessingCompleted(string filePath)
    {
        _logger.Info($"파일 처리 완료: {filePath}");
    }

    public void OnFileProcessingFailed(string filePath, Exception exception)
    {
        _logger.Error($"파일 처리 실패: {filePath}", exception);
    }
}

/// <summary>
/// SECS/GEM 서버 인터페이스
/// </summary>
public interface ISecsGemServer
{
    void SendMessage(P_FRAMEMAP message);
}

/// <summary>
/// 파일 처리 시작 이벤트 인자
/// </summary>
public class FileProcessingStartedEventArgs : EventArgs
{
    public string FilePath { get; }
    public string FileName { get; }
    public DateTime StartTime { get; }

    public FileProcessingStartedEventArgs(string filePath, string fileName)
    {
        FilePath = filePath;
        FileName = fileName;
        StartTime = DateTime.Now;
    }
}

/// <summary>
/// 파일 처리 완료 이벤트 인자
/// </summary>
public class FileProcessingCompletedEventArgs : EventArgs
{
    public string FilePath { get; }
    public string FileName { get; }
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }
    public TimeSpan Duration => EndTime - StartTime;

    public FileProcessingCompletedEventArgs(string filePath, string fileName)
    {
        FilePath = filePath;
        FileName = fileName;
        EndTime = DateTime.Now;
        StartTime = EndTime; // 실제로는 이전 시작 시간이 필요
    }
}

/// <summary>
/// 파일 처리 실패 이벤트 인자
/// </summary>
public class FileProcessingFailedEventArgs : EventArgs
{
    public string FilePath { get; }
    public string FileName { get; }
    public ProcessingResult Result { get; }
    public Exception? Exception { get; }
    public DateTime FailureTime { get; }

    public FileProcessingFailedEventArgs(string filePath, string fileName, 
        ProcessingResult result, Exception? exception = null)
    {
        FilePath = filePath;
        FileName = fileName;
        Result = result;
        Exception = exception;
        FailureTime = DateTime.Now;
    }
}

#endregion
