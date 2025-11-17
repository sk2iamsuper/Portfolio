using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using EventBus;
using Microsoft.Extensions.Options;
using NLog;
using Secs4Net;
using Secs4Net.Sml;
using kpsecsgem.events;
using kpsecsgem.message;
using kpsecsgem.message.common;

namespace kpsecsgem
{
    /// <summary>
    /// SECS/GEM 서버 구현 클래스
    /// 반도체 장비와의 통신을 위한 HSMS 프로토콜 구현
    /// </summary>
    
    public sealed class SecsGemServer : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// 연결 모드 (ACTIVE: 장비에 연결, PASSIVE: 장비의 연결을 대기)
        /// </summary>
        public enum CONNECTION_MODE { ACTIVE, PASSIVE }
        
        /// <summary>
        /// SECS/GEM 상태
        /// </summary>
        public enum SecsGemStatus { LOAD, CONNECTING, CONNECTED, INITIALIZING, INITIALIZED }

        // 정적 필드
        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger(); // NLog 로거
        private static readonly object SECSGEM_LOCK = new(); // 스레드 동기화를 위한 락 객체

        // 의존성 주입된 서비스
        private readonly ISecsGemLogger _logger; // SECS/GEM 로거
        private readonly SimpleEventBus _eventBus = SimpleEventBus.GetDefaultEventBus(); // 이벤트 버스

        // 비동기 작업 관리
        private CancellationTokenSource? _cts; // 작업 취소 토큰 소스
        private Task? _listenerTask; // 메시지 수신 리스너 태스크
        private HsmsConnection? _connector; // HSMS 연결 객체
        private SecsGem? _secsGem; // SECS/GEM 핵심 객체

        // 비동기 응답 대기를 위한 TaskCompletionSource
        private TaskCompletionSource<SecsMessage?>? _rcmdResponseTcs; // 원격 명령 응답 대기
        private TaskCompletionSource<SecsMessage?>? _dataResponseTcs; // 데이터 응답 대기

        // 속성
        public string IpAddress { get; } // 서버 IP 주소
        public SecsGemStatus SecsGemStatus { get; private set; } = SecsGemStatus.LOAD; // 현재 상태
        public static string USER_ID = "AUTO"; // 사용자 ID (기본값: AUTO)

        /// <summary>
        /// 생성자 - IP 주소 접두사를 기반으로 로컬 IP 주소 결정
        /// </summary>
        /// <param name="ipAddressPrefix">IP 주소 접두사 (예: "192.168")</param>
        public SecsGemServer(string ipAddressPrefix)
        {
            _logger = new SecsLogger();

            // 호스트 이름으로부터 IP 주소 목록 조회
            var hostName = Dns.GetHostName();
            var entry = Dns.GetHostEntry(hostName);
            
            // IPv4 주소 중 접두사가 일치하는 첫 번째 주소 선택
            foreach (var addr in entry.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork && 
                    addr.ToString().StartsWith(ipAddressPrefix))
                {
                    IpAddress = addr.ToString();
                    return;
                }
            }

            // 일치하는 주소가 없을 경우 로컬호스트로 폴백
            IpAddress = "127.0.0.1";
        }

        #region Start / Stop

        /// <summary>
        /// SECS/GEM 서버 시작
        /// </summary>
        /// <param name="mode">연결 모드</param>
        /// <param name="port">포트 번호</param>
        /// <param name="deviceId">장비 ID</param>
        /// <param name="bufferSize">소켓 버퍼 크기 (기본값: 65535)</param>
        public void Start(CONNECTION_MODE mode, int port, ushort deviceId, int bufferSize = 65535)
        {
            // Start는 동기적으로 유지하되 내부에서 비동기 태스크 생성
            try
            {
                // 기존 취소 토큰 정리
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                // SECS/GEM 옵션 설정
                var options = Options.Create(new SecsGemOptions
                {
                    IsActive = mode == CONNECTION_MODE.ACTIVE,
                    IpAddress = IpAddress,
                    Port = port,
                    SocketReceiveBufferSize = bufferSize,
                    DeviceId = deviceId,
                    LinkTestInterval = 15000 // 링크 테스트 간격 (15초)
                });

                // HSMS 연결 객체 생성
                _connector = new HsmsConnection(options, _logger)
                {
                    LinkTestEnabled = true // 링크 테스트 활성화
                };

                // SECS/GEM 객체 생성
                _secsGem = new SecsGem(options, _connector, _logger);

                // 연결 상태 변경 이벤트 구독
                _connector.ConnectionChanged += Connector_ConnectionChanged;

                // 커넥터 시작 (비차단)
                _connector.Start(_cts.Token);

                // 상태 업데이트 및 로깅
                SecsGemStatus = SecsGemStatus.CONNECTING;
                LOGGER.Info($"Start SECS/GEM Server at {IpAddress}:{port}, Mode: {mode}, DeviceId: {deviceId}");

                // 메시지 수신 리스너 태스크 시작
                _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
            }
            catch (Exception ex)
            {
                LOGGER.Error(ex, "Failed to start SecsGemServer");
                throw;
            }
        }

        /// <summary>
        /// SECS/GEM 서버 비동기 정지
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                // 취소 토큰으로 작업 중지 신호 전송
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }

                // 리스너 태스크 대기
                if (_listenerTask != null)
                {
                    try
                    {
                        await _listenerTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { } // 취소 예외는 무시
                }

                // 커넥터 비동기 정리
                if (_connector != null)
                {
                    try
                    {
                        await _connector.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LOGGER.Warn(ex, "Exception while disposing connector");
                    }
                    _connector = null;
                }

                // SECS/GEM 객체 정리
                _secsGem?.Dispose();
                _secsGem = null;

                // 응답 대기 객체 정리
                ClearResponseTcs();
                
                // 상태 초기화
                SecsGemStatus = SecsGemStatus.LOAD;
            }
            finally
            {
                // 취소 토큰 정리
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// SECS/GEM 서버 동기 정지
        /// </summary>
        public void Stop()
        {
            // 비동기 정지 메서드를 동기적으로 래핑
            StopAsync().GetAwaiter().GetResult();
        }

        #endregion

        #region Listener

        /// <summary>
        /// 연결 상태 변경 이벤트 핸들러
        /// </summary>
        private void Connector_ConnectionChanged(object? sender, ConnectionState state)
        {
            try
            {
                LOGGER.Info($"SECS/GEM Connection Changed: {state}");
                
                // 상태 매핑: Selected -> CONNECTED, 그 외 -> CONNECTING
                SecsGemStatus = state == ConnectionState.Selected ? 
                    SecsGemStatus.CONNECTED : SecsGemStatus.CONNECTING;
                
                // 연결 상태 변경 이벤트 발행
                _eventBus.Post(new ConnectionChanged(SecsGemStatus, GetDeviceIPAddress()), TimeSpan.Zero);
            }
            catch (Exception ex)
            {
                LOGGER.Error(ex, "Error in ConnectionChanged handler");
            }
        }

        /// <summary>
        /// 메인 메시지 수신 루프
        /// </summary>
        private async Task ListenLoopAsync(CancellationToken ct)
        {
            // 스레드 이름 설정 (디버깅 용이성)
            Thread.CurrentThread.Name = "Secs/Gem Event Listener";
            LOGGER.Info("SECS/GEM Listener Start");
            
            if (_secsGem == null) return;

            try
            {
                // 비동기 메시지 스트림 구독
                await foreach (var message in _secsGem.GetPrimaryMessageAsync(ct).WithCancellation(ct))
                {
                    try
                    {
                        var p = message.PrimaryMessage;
                        if (p == null) continue;

                        // S1F13: 통신 Establish 요청
                        if (p is { S: 1, F: 13 })
                        {
                            var reply = new S1F13().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            SecsGemStatus = SecsGemStatus.INITIALIZING;
                            _eventBus.Post(new ConnectionChanged(SecsGemStatus, GetDeviceIPAddress()), TimeSpan.Zero);
                            continue;
                        }

                        // S2F31: Date and Time Set 요청
                        if (p is { S: 2, F: 31 })
                        {
                            var reply = new S2F31().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }

                        // S2F33: Define Report 요청
                        if (p is { S: 2, F: 33 })
                        {
                            var reply = new S2F33().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }

                        // S2F35: Link Event Report 요청
                        if (p is { S: 2, F: 35 })
                        {
                            var reply = new S2F35().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }

                        // S2F37: Enable/Disable Event Report 요청
                        if (p is { S: 2, F: 37 })
                        {
                            var reply = new S2F37().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }

                        // S5F3: Alarm Report 요청
                        if (p is { S: 5, F: 3 })
                        {
                            var reply = new S5F3().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            SecsGemStatus = SecsGemStatus.INITIALIZED;
                            _eventBus.Post(new ConnectionChanged(SecsGemStatus, GetDeviceIPAddress()), TimeSpan.Zero);
                            continue;
                        }

                        // S2F41: Remote Command 결과
                        if (p is { S: 2, F: 41 })
                        {
                            var reply = new S2F41().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            LOGGER.Info(p.ToSml()); // SML 형식으로 로깅

                            // RCMD 결과를 기다리는 TCS에 설정
                            _rcmdResponseTcs?.TrySetResult(p);
                            continue;
                        }

                        // S14F3: Pass Case 데이터
                        if (p is { S: 14, F: 3 })
                        {
                            var reply = new S14F3(p).GetReply();
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            LOGGER.Info(p.ToSml());
                            _dataResponseTcs?.TrySetResult(p);
                            continue;
                        }

                        // S10F3: Terminal Display (에러 메시지 등)
                        if (p is { S: 10, F: 3 })
                        {
                            var s10f3 = new S10F3().Parse(p);
                            await SendReplyMessageAsync(message, s10f3.GetReply()).ConfigureAwait(false);
                            LOGGER.Info(@$"Terminal Display TID: {s10f3.GetTID()} Message: {s10f3.GetMessage()}");
                            
                            // 디스플레이 이벤트 발행
                            _eventBus.Post(new DisplayEvent(s10f3.GetMessage()), TimeSpan.Zero);
                            _dataResponseTcs?.TrySetResult(p);
                            continue;
                        }
                    }
                    catch (OperationCanceledException) { throw; } // 취소 예외는 재전파
                    catch (Exception ex)
                    {
                        LOGGER.Error(ex, "Error handling incoming SECS message");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LOGGER.Info("SECS/GEM Listener canceled");
            }
            catch (Exception ex)
            {
                LOGGER.Error(ex, "Listener loop error");
            }
        }

        #endregion

        #region Send / Reply

        /// <summary>
        /// 메시지 비동기 전송
        /// </summary>
        /// <param name="message">전송할 S6F11 메시지</param>
        /// <param name="needToResponse">응답 필요 여부</param>
        /// <param name="timeoutSeconds">타임아웃 (초)</param>
        /// <returns>응답 객체</returns>
        public async Task<Response> SendMessageAsync(S6F11 message, bool needToResponse = false, int timeoutSeconds = 30)
        {
            if (_secsGem == null) throw new InvalidOperationException("SecsGem not initialized");
            if (_connector == null) throw new InvalidOperationException("Connector not initialized");

            // 스레드 안전을 위해 락 내에서 TCS 생성
            lock (SECSGEM_LOCK)
            {
                // 응답 유형에 따라 적절한 TCS 생성
                if (needToResponse)
                {
                    _rcmdResponseTcs = new TaskCompletionSource<SecsMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                else
                {
                    _dataResponseTcs = new TaskCompletionSource<SecsMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            var sw = Stopwatch.StartNew(); // 성능 측정용 스톱워치
            SecsMessage? primaryReply = null;
            
            try
            {
                // 메시지 전송 및 1차 응답 수신
                primaryReply = await _secsGem.SendAsync(message.GetPrimaryMessage()).ConfigureAwait(false);

                // 장비의 결과 메시지 대기
                SecsMessage? deviceResult = null;
                if (needToResponse && _rcmdResponseTcs != null)
                {
                    // 타임아웃과 응답 대기 태스크 경쟁
                    var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                    var winner = await Task.WhenAny(_rcmdResponseTcs.Task, delayTask).ConfigureAwait(false);
                    if (winner == _rcmdResponseTcs.Task)
                        deviceResult = _rcmdResponseTcs.Task.Result;
                    else
                        LOGGER.Warn("Timeout waiting for RCMD result");
                }
                else if (!needToResponse && _dataResponseTcs != null)
                {
                    var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
                    var winner = await Task.WhenAny(_dataResponseTcs.Task, delayTask).ConfigureAwait(false);
                    if (winner == _dataResponseTcs.Task)
                        deviceResult = _dataResponseTcs.Task.Result;
                    else
                        LOGGER.Warn("Timeout waiting for data result");
                }

                sw.Stop();
                LOGGER.Info($"Elapsed Time: {sw.Elapsed}");

                return new Response(primaryReply, deviceResult, null);
            }
            finally
            {
                // TCS 누수 방지를 위해 정리
                if (needToResponse) _rcmdResponseTcs = null;
                else _dataResponseTcs = null;
            }
        }

        /// <summary>
        /// 메시지 동기 전송 (주의: 데드락 가능성)
        /// </summary>
        public Response SendMessage(S6F11 message, bool needToResponse = false, int timeoutSeconds = 30)
            => SendMessageAsync(message, needToResponse, timeoutSeconds).GetAwaiter().GetResult();

        /// <summary>
        /// 응답 메시지 비동기 전송
        /// </summary>
        private Task<bool> SendReplyMessageAsync(PrimaryMessageWrapper primaryMessage, SecsMessage reply)
        {
            return primaryMessage.TryReplyAsync(reply);
        }

        #endregion

        #region Helpers & Cleanup

        /// <summary>
        /// 장비 IP 주소 조회
        /// </summary>
        /// <returns>장비 IP 주소 또는 "Unknown"</returns>
        public string GetDeviceIPAddress()
        {
            try
            {
                if (_connector == null) return "Unknown";
                var ip = _connector.DeviceIpAddress;
                if (string.IsNullOrEmpty(ip) || ip.Equals("NA", StringComparison.OrdinalIgnoreCase)) 
                    return "Unknown";
                return ip;
            }
            catch { return "Unknown"; }
        }

        /// <summary>
        /// 응답 대기 객체 정리
        /// </summary>
        private void ClearResponseTcs()
        {
            // RCMD 응답 TCS 정리
            try
            {
                _rcmdResponseTcs?.TrySetCanceled();
            }
            catch { }
            _rcmdResponseTcs = null;

            // 데이터 응답 TCS 정리
            try
            {
                _dataResponseTcs?.TrySetCanceled();
            }
            catch { }
            _dataResponseTcs = null;
        }

        /// <summary>
        /// 동기 리소스 정리
        /// </summary>
        public void Dispose()
        {
            // 서버 정지
            Stop();

            // 관리 리소스 정리
            _secsGem?.Dispose();
            _secsGem = null;

            _connector = null;
            ClearResponseTcs();
        }

        /// <summary>
        /// 비동기 리소스 정리
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await StopAsync().ConfigureAwait(false);
            _logger?.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// SECS 메시지 응답을 캡슐화하는 클래스
    /// </summary>
    public class Response
    {
        public SecsMessage? PrimaryReply { get; } // 1차 응답 메시지
        public SecsMessage? DeviceResult { get; } // 장비 결과 메시지
        public SecsMessage? DataMessage { get; }  // 데이터 메시지

        public Response(SecsMessage? primary, SecsMessage? deviceResult, SecsMessage? data)
        {
            PrimaryReply = primary;
            DeviceResult = deviceResult;
            DataMessage = data;
        }
    }
}
