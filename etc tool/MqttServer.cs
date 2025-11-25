using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client.Receiving;
using MQTTnet.Server;
using NLog;

namespace Mqtt2TibcoRV;

/// <summary>
/// MQTT 서버 클래스 - MQTT 메시지를 수신하여 Tibco RV로 전송하는 브리지 역할
/// 싱글톤 패턴으로 구현되어 애플리케이션 내에서 단일 인스턴스로 동작
/// </summary>
public class MqttServer : IDisposable
{
    #region Fields and Properties
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    
    // 싱글톤 인스턴스 (Lazy initialization)
    private static MqttServer _instance;
    private static readonly object _lockObject = new object();
    
    // MQTT 서버 인스턴스
    private IMqttServer _mqttServer;
    
    // Tibco RV 발행자 - MQTT 메시지를 RV로 전송
    private RvPublisher _publisher;
    
    // 리소스 해제 여부 추적
    private bool _disposed = false;
    
    /// <summary>
    /// MQTT 서버 실행 상태 확인
    /// </summary>
    public bool IsRunning => _mqttServer?.IsStarted == true;
    #endregion

    #region Constructor and Singleton Pattern
    /// <summary>
    /// private 생성자 - 싱글톤 패턴 적용
    /// </summary>
    private MqttServer()
    {
        Logger.Debug("MQTT 서버 인스턴스 생성");
    }

    /// <summary>
    /// MQTT 서버 싱글톤 인스턴스 반환
    /// </summary>
    public static MqttServer Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lockObject)
                {
                    _instance ??= new MqttServer();
                }
            }
            return _instance;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// MQTT 서버 시작 - Tibco RV 발행자와 함께 서버 초기화 및 실행
    /// </summary>
    /// <param name="publisher">Tibco RV 메시지 발행자 인스턴스</param>
    /// <returns>시작된 MQTT 서버 인스턴스</returns>
    public static MqttServer Start(RvPublisher publisher)
    {
        if (publisher == null)
        {
            throw new ArgumentNullException(nameof(publisher), "RV Publisher는 null일 수 없습니다.");
        }

        var server = Instance;
        
        // RV 발행자 설정
        server._publisher = publisher;
        
        // 별도 스레드에서 MQTT 서버 실행 (장기 실행 태스크)
        Task.Factory.StartNew(
            () => server.RunAsync().ConfigureAwait(false), 
            TaskCreationOptions.LongRunning
        );
        
        Logger.Info($"MQTT 서버 시작 요청 완료 - 포트: {Program.MQTT_PORT}");
        return server;
    }

    /// <summary>
    /// MQTT 메시지 발행 - 특정 토픽으로 메시지 전송
    /// </summary>
    /// <param name="topic">발행할 토픽</param>
    /// <param name="messageId">메시지 ID</param>
    /// <param name="message">메시지 본문</param>
    /// <returns>비동기 작업 태스크</returns>
    public static async Task PublishAsync(string topic, string messageId, string message)
    {
        if (_instance == null || !_instance.IsRunning)
        {
            Logger.Warn("MQTT 서버가 실행 중이 아니므로 메시지를 발행할 수 없습니다.");
            return;
        }

        if (string.IsNullOrEmpty(topic))
        {
            Logger.Warn("토픽이 null이거나 비어있어 메시지를 발행할 수 없습니다.");
            return;
        }

        try
        {
            // MQTT 애플리케이션 메시지 빌드
            var applicationMessage = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload($"{messageId}{Program.MQTT_SEPARATOR}{message}")
                .WithAtLeastOnceQoS()  // 최소 한 번 전송 보장
                .Build();

            Logger.Info($"MQTT 발송 : {topic,27} => {messageId}{Program.MQTT_SEPARATOR}{message}");
            
            // 메시지 발행
            await _instance._mqttServer.PublishAsync(applicationMessage).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"MQTT 메시지 발행 중 오류 - 토픽: {topic}, 메시지ID: {messageId}");
        }
    }

    /// <summary>
    /// MQTT 서버 종료 - 리소스 정리 및 서버 중지
    /// </summary>
    public void Close()
    {
        try
        {
            if (_mqttServer != null && _mqttServer.IsStarted)
            {
                // 보관된 메시지 정리
                _mqttServer.ClearRetainedApplicationMessagesAsync().ConfigureAwait(false);
                
                // 서버 중지
                _mqttServer.StopAsync().ConfigureAwait(false);
                
                Logger.Info("MQTT 서버가 종료되었습니다.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MQTT 서버 종료 중 오류 발생");
        }
    }

    /// <summary>
    /// 연결된 클라이언트 수 조회
    /// </summary>
    /// <returns>연결된 클라이언트 수</returns>
    public async Task<int> GetConnectedClientsCountAsync()
    {
        if (_mqttServer == null || !_mqttServer.IsStarted)
            return 0;

        try
        {
            var clients = await _mqttServer.GetClientsAsync().ConfigureAwait(false);
            return clients?.Count ?? 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "연결된 클라이언트 수 조회 중 오류");
            return 0;
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// MQTT 서버 비동기 실행 - 서버 설정 및 이벤트 핸들러 구성
    /// </summary>
    private async Task RunAsync()
    {
        try
        {
            // MQTT 서버 옵션 설정
            var optionsBuilder = new MqttServerOptionsBuilder()
                .WithConnectionBacklog(65535)  // 동시 연결 대기 수
                .WithDefaultEndpointPort(Program.MQTT_PORT)  // 기본 포트
                .WithPersistentSessions()  // 영구 세션 사용
                .WithDefaultCommunicationTimeout(TimeSpan.FromHours(1))  // 통신 타임아웃
                .WithMaxPendingMessagesPerClient(65535)  // 클라이언트당 최대 대기 메시지 수
                .WithClientId(null);  // 브로커 클라이언트 ID (null = 자동 생성)

            // MQTT 서버 인스턴스 생성
            _mqttServer = new MqttFactory().CreateMqttServer();

            // 이벤트 핸들러 설정
            ConfigureEventHandlers();

            // 서버 시작
            await _mqttServer.StartAsync(optionsBuilder.Build()).ConfigureAwait(false);
            
            Logger.Info("애플리케이션 메시지 대기 중...");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "MQTT 서버 실행 중 오류 발생");
            throw;  // 오류 전파 (호출자가 처리할 수 있도록)
        }
    }

    /// <summary>
    /// MQTT 서버 이벤트 핸들러 구성
    /// </summary>
    private void ConfigureEventHandlers()
    {
        // 서버 시작 이벤트
        _mqttServer.StartedHandler = new MqttServerStartedHandlerDelegate(async e =>
        {
            Logger.Info($"MQTT 브로커 시작 완료 : 포트 {Program.MQTT_PORT}");
            await Task.CompletedTask.ConfigureAwait(false);
        });

        // 서버 중지 이벤트
        _mqttServer.StoppedHandler = new MqttServerStoppedHandlerDelegate(async e =>
        {
            Logger.Error("MQTT 브로커가 중지되었습니다.");
            await Task.CompletedTask.ConfigureAwait(false);
        });

        // 클라이언트 연결 이벤트
        _mqttServer.ClientConnectedHandler = new MqttServerClientConnectedHandlerDelegate(async e =>
        {
            Logger.Info($"클라이언트 연결: {e.ClientId} ({e.Endpoint})");
            await Task.CompletedTask.ConfigureAwait(false);
        });

        // 클라이언트 연결 해제 이벤트
        _mqttServer.ClientDisconnectedHandler = new MqttServerClientDisconnectedHandlerDelegate(async e =>
        {
            Logger.Info($"클라이언트 연결 해제: {e.ClientId} ({e.DisconnectType})");
            await Task.CompletedTask.ConfigureAwait(false);
        });

        // 애플리케이션 메시지 수신 이벤트
        _mqttServer.ApplicationMessageReceivedHandler = new MqttApplicationMessageReceivedHandlerDelegate(OnApplicationMessageReceived);
    }

    /// <summary>
    /// MQTT 메시지 수신 이벤트 처리 - 메시지 검증 및 Tibco RV로 전송
    /// </summary>
    /// <param name="e">MQTT 메시지 수신 이벤트 인자</param>
    private void OnApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        // 클라이언트 ID 검증
        if (string.IsNullOrEmpty(e.ClientId))
        {
            Logger.Warn("클라이언트 ID가 없는 메시지는 무시됩니다.");
            return;
        }

        // 메시지 payload 검증
        if (e.ApplicationMessage?.Payload == null || e.ApplicationMessage.Payload.Length == 0)
        {
            Logger.Warn($"클라이언트 {e.ClientId}에서 빈 메시지를 수신했습니다.");
            return;
        }

        try
        {
            // payload를 문자열로 변환 및 정리
            var message = Encoding.UTF8.GetString(e.ApplicationMessage.Payload)
                ?.Trim()
                .Replace("\0", string.Empty);  // null 문자 제거

            // 메시지 내용 검증
            if (string.IsNullOrWhiteSpace(message))
            {
                Logger.Warn($"클라이언트 {e.ClientId}에서 빈 메시지 내용을 수신했습니다.");
                return;
            }

            Logger.Info($"MQTT 수신 : {e.ClientId,26} => {message}");

            // 스레드 풀을 이용한 비동기 처리 (Tibco RV로 전송)
            ThreadPool.QueueUserWorkItem(async state =>
            {
                await ProcessMessageAsync(e.ClientId, message).ConfigureAwait(false);
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"클라이언트 {e.ClientId}의 메시지 처리 중 오류 발생");
        }
    }

    /// <summary>
    /// 메시지 처리 - 메시지 파싱 및 Tibco RV로 발행
    /// </summary>
    /// <param name="clientId">클라이언트 ID</param>
    /// <param name="message">원본 메시지</param>
    private async Task ProcessMessageAsync(string clientId, string message)
    {
        try
        {
            // 메시지 파싱 (구분자로 분리)
            var messageParts = message.Split(Program.MQTT_SEPARATOR);
            
            // 메시지 형식 검증 (최소 메시지 ID + 본문)
            if (messageParts.Length < 2)
            {
                Logger.Warn($"잘못된 메시지 형식 from {clientId}: {message}");
                return;
            }

            // 메시지 ID와 본문 분리
            var messageId = messageParts[0];
            var messageBody = string.Join(Program.MQTT_SEPARATOR.ToString(), messageParts.Skip(1));

            // Tibco RV로 발행
            _publisher.Publish(clientId, messageId, messageBody);
            
            await Task.CompletedTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"메시지 처리 중 오류 - 클라이언트: {clientId}, 메시지: {message}");
        }
    }
    #endregion

    #region IDisposable Implementation
    /// <summary>
    /// 리소스 해제 - 관리/비관리 리소스 정리
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// protected 가상 Dispose 메서드 - 상속 시 재정의 가능
    /// </summary>
    /// <param name="disposing">관리 리소스 해제 여부</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 관리 리소스 해제
                Close();
                _mqttServer?.Dispose();
                _mqttServer = null;
            }

            // 비관리 리소스 해제 (필요한 경우)

            _disposed = true;
            Logger.Debug("MQTT 서버 리소스가 해제되었습니다.");
        }
    }

    /// <summary>
    /// 소멸자 - 비관리 리소스 정리
    /// </summary>
    ~MqttServer()
    {
        Dispose(false);
    }
    #endregion
}
