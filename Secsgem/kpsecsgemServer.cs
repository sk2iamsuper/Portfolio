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

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
        private static readonly object SECSGEM_LOCK = new();

        private readonly ISecsGemLogger _logger;
        private readonly SimpleEventBus _eventBus = SimpleEventBus.GetDefaultEventBus();

        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private HsmsConnection? _connector;
        private SecsGem? _secsGem;

        private TaskCompletionSource<SecsMessage?>? _rcmdResponseTcs;
        private TaskCompletionSource<SecsMessage?>? _dataResponseTcs;

        public string IpAddress { get; }
        public SecsGemStatus SecsGemStatus { get; private set; } = SecsGemStatus.LOAD;
        public static string USER_ID = "AUTO";

        public SecsGemServer(string ipAddressPrefix)
        {
            _logger = new SecsLogger();
            IpAddress = ResolveLocalIp(ipAddressPrefix);
        }

        #region Start / Stop

        public void Start(CONNECTION_MODE mode, int port, ushort deviceId, int bufferSize = 65535)
        {
            try
            {
                _cts?.Dispose();
                _cts = new CancellationTokenSource();

                var options = Options.Create(new SecsGemOptions
                {
                    IsActive = mode == CONNECTION_MODE.ACTIVE,
                    IpAddress = IpAddress,
                    Port = port,
                    SocketReceiveBufferSize = bufferSize,
                    DeviceId = deviceId,
                    LinkTestInterval = 15000
                });

                _connector = new HsmsConnection(options, _logger)
                {
                    LinkTestEnabled = true
                };

                _secsGem = new SecsGem(options, _connector, _logger);

                _connector.ConnectionChanged += Connector_ConnectionChanged;
                _connector.Start(_cts.Token);

                SetStatus(SecsGemStatus.CONNECTING);
                LOGGER.Info($"Start SECS/GEM Server at {IpAddress}:{port}, Mode: {mode}, DeviceId: {deviceId}");

                _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
            }
            catch (Exception ex)
            {
                LOGGER.Error(ex, "Failed to start SecsGemServer");
                throw;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _cts.Cancel();
                }

                if (_listenerTask != null)
                {
                    try
                    {
                        await _listenerTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { }
                }

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

                _secsGem?.Dispose();
                _secsGem = null;

                ClearResponseTcs();
                SetStatus(SecsGemStatus.LOAD);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void Stop() => StopAsync().GetAwaiter().GetResult();

        #endregion

        #region Listener

        private void Connector_ConnectionChanged(object? sender, ConnectionState state)
        {
            try
            {
                LOGGER.Info($"SECS/GEM Connection Changed: {state}");
                SetStatus(state == ConnectionState.Selected ? SecsGemStatus.CONNECTED : SecsGemStatus.CONNECTING);
            }
            catch (Exception ex)
            {
                LOGGER.Error(ex, "Error in ConnectionChanged handler");
            }
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            Thread.CurrentThread.Name = "Secs/Gem Event Listener";
            LOGGER.Info("SECS/GEM Listener Start");

            if (_secsGem == null) return;

            try
            {
                await foreach (var message in _secsGem.GetPrimaryMessageAsync(ct).WithCancellation(ct))
                {
                    try
                    {
                        var p = message.PrimaryMessage;
                        if (p == null) continue;

                        switch (p.S, p.F)
                        {
                            case (1, 13):
                                await ReplyAsync(message, new S1F13().GetReply(p)).ConfigureAwait(false);
                                SetStatus(SecsGemStatus.INITIALIZING);
                                break;

                            case (2, 31):
                                await ReplyAsync(message, new S2F31().GetReply(p)).ConfigureAwait(false);
                                break;

                            case (2, 33):
                                await ReplyAsync(message, new S2F33().GetReply(p)).ConfigureAwait(false);
                                break;

                            case (2, 35):
                                await ReplyAsync(message, new S2F35().GetReply(p)).ConfigureAwait(false);
                                break;

                            case (2, 37):
                                await ReplyAsync(message, new S2F37().GetReply(p)).ConfigureAwait(false);
                                break;

                            case (5, 3):
                                await ReplyAsync(message, new S5F3().GetReply(p)).ConfigureAwait(false);
                                SetStatus(SecsGemStatus.INITIALIZED);
                                break;

                            case (2, 41):
                                await ReplyAsync(message, new S2F41().GetReply(p)).ConfigureAwait(false);
                                LOGGER.Info(p.ToSml());
                                _rcmdResponseTcs?.TrySetResult(p);
                                break;

                            case (14, 3):
                                await ReplyAsync(message, new S14F3(p).GetReply()).ConfigureAwait(false);
                                LOGGER.Info(p.ToSml());
                                _dataResponseTcs?.TrySetResult(p);
                                break;

                            case (10, 3):
                                await HandleTerminalDisplayAsync(message, p).ConfigureAwait(false);
                                break;
                        }
                    }
                    catch (OperationCanceledException) { throw; }
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

        private async Task HandleTerminalDisplayAsync(PrimaryMessageWrapper message, SecsMessage p)
        {
            var s10f3 = new S10F3().Parse(p);
            await ReplyAsync(message, s10f3.GetReply()).ConfigureAwait(false);
            LOGGER.Info(@$"Terminal Display TID: {s10f3.GetTID()} Message: {s10f3.GetMessage()}");

            _eventBus.Post(new DisplayEvent(s10f3.GetMessage()), TimeSpan.Zero);
            _dataResponseTcs?.TrySetResult(p);
        }

        #endregion

        #region Send / Reply

        public async Task<Response> SendMessageAsync(S6F11 message, bool needToResponse = false, int timeoutSeconds = 30)
        {
            if (_secsGem == null) throw new InvalidOperationException("SecsGem not initialized");
            if (_connector == null) throw new InvalidOperationException("Connector not initialized");

            lock (SECSGEM_LOCK)
            {
                if (needToResponse)
                    _rcmdResponseTcs = CreateTcs();
                else
                    _dataResponseTcs = CreateTcs();
            }

            var sw = Stopwatch.StartNew();
            SecsMessage? primaryReply = null;

            try
            {
                primaryReply = await _secsGem.SendAsync(message.GetPrimaryMessage()).ConfigureAwait(false);

                SecsMessage? deviceResult = needToResponse
                    ? await AwaitResponseAsync(_rcmdResponseTcs, timeoutSeconds, "RCMD").ConfigureAwait(false)
                    : await AwaitResponseAsync(_dataResponseTcs, timeoutSeconds, "data").ConfigureAwait(false);

                sw.Stop();
                LOGGER.Info($"Elapsed Time: {sw.Elapsed}");

                return new Response(primaryReply, deviceResult, null);
            }
            finally
            {
                if (needToResponse) _rcmdResponseTcs = null;
                else _dataResponseTcs = null;
            }
        }

        public Response SendMessage(S6F11 message, bool needToResponse = false, int timeoutSeconds = 30)
            => SendMessageAsync(message, needToResponse, timeoutSeconds).GetAwaiter().GetResult();

        private Task<bool> ReplyAsync(PrimaryMessageWrapper primaryMessage, SecsMessage reply)
            => primaryMessage.TryReplyAsync(reply);

        private static TaskCompletionSource<SecsMessage?> CreateTcs()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private async Task<SecsMessage?> AwaitResponseAsync(
            TaskCompletionSource<SecsMessage?>? tcs,
            int timeoutSeconds,
            string label)
        {
            if (tcs == null) return null;

            var delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            var winner = await Task.WhenAny(tcs.Task, delayTask).ConfigureAwait(false);

            if (winner == tcs.Task) return tcs.Task.Result;

            LOGGER.Warn($"Timeout waiting for {label} result");
            return null;
        }

        #endregion

        #region Helpers & Cleanup

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
            catch
            {
                return "Unknown";
            }
        }

        private void SetStatus(SecsGemStatus status)
        {
            SecsGemStatus = status;
            _eventBus.Post(new ConnectionChanged(SecsGemStatus, GetDeviceIPAddress()), TimeSpan.Zero);
        }

        private void ClearResponseTcs()
        {
            try { _rcmdResponseTcs?.TrySetCanceled(); } catch { }
            _rcmdResponseTcs = null;

            try { _dataResponseTcs?.TrySetCanceled(); } catch { }
            _dataResponseTcs = null;
        }

        private static string ResolveLocalIp(string ipAddressPrefix)
        {
            var hostName = Dns.GetHostName();
            var entry = Dns.GetHostEntry(hostName);

            foreach (var addr in entry.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork &&
                    addr.ToString().StartsWith(ipAddressPrefix))
                {
                    return addr.ToString();
                }
            }

            return "127.0.0.1";
        }

        public void Dispose()
        {
            Stop();
            _secsGem?.Dispose();
            _secsGem = null;

            _connector = null;
            ClearResponseTcs();
        }

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
        public SecsMessage? PrimaryReply { get; }
        public SecsMessage? DeviceResult { get; }
        public SecsMessage? DataMessage { get; }

        public Response(SecsMessage? primary, SecsMessage? deviceResult, SecsMessage? data)
        {
            PrimaryReply = primary;
            DeviceResult = deviceResult;
            DataMessage = data;
        }
    }
}
