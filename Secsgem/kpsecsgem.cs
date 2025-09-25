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
    public sealed class SecsGemServer : IDisposable, IAsyncDisposable
    {
        public enum CONNECTION_MODE { ACTIVE, PASSIVE }
        public enum SecsGemStatus { LOAD, CONNECTING, CONNECTED, INITIALIZING, INITIALIZED }

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();
        private static readonly object SECSGEM_LOCK = new();

        private readonly ISecsGemLogger _logger;
        private readonly SimpleEventBus _eventBus = SimpleEventBus.GetDefaultEventBus();

        private CancellationTokenSource? _cts;
        private Task? _listenerTask;
        private HsmsConnection? _connector;
        private SecsGem? _secsGem;

        // async waiters for responses
        private TaskCompletionSource<SecsMessage?>? _rcmdResponseTcs;
        private TaskCompletionSource<SecsMessage?>? _dataResponseTcs;

        public string IpAddress { get; }
        public SecsGemStatus SecsGemStatus { get; private set; } = SecsGemStatus.LOAD;
        public static string USER_ID = "AUTO";

        public SecsGemServer(string ipAddressPrefix)
        {
            _logger = new SecsLogger();

            // Use GetHostEntry (GetHostByName is obsolete)
            var hostName = Dns.GetHostName();
            var entry = Dns.GetHostEntry(hostName);
            foreach (var addr in entry.AddressList)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork && addr.ToString().StartsWith(ipAddressPrefix))
                {
                    IpAddress = addr.ToString();
                    return;
                }
            }

            IpAddress = "127.0.0.1"; // fallback
        }

        #region Start / Stop

        public void Start(CONNECTION_MODE mode, int port, ushort deviceId, int bufferSize = 65535)
        {
            // keep Start synchronous but create async tasks inside
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

                // start connector (non-blocking)
                _connector.Start(_cts.Token);

                SecsGemStatus = SecsGemStatus.CONNECTING;
                LOGGER.Info($"Start SECS/GEM Server at {IpAddress}:{port}, Mode: {mode}, DeviceId: {deviceId}");

                // listener
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

                // clear event subscriptions
                // (Connector removed; just in case)
                // nothing to unsubscribe from _secsGem here because we didn't subscribe.

                ClearResponseTcs();
                SecsGemStatus = SecsGemStatus.LOAD;
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        public void Stop()
        {
            // synchronous wrapper
            StopAsync().GetAwaiter().GetResult();
        }

        #endregion

        #region Listener

        private void Connector_ConnectionChanged(object? sender, ConnectionState state)
        {
            try
            {
                LOGGER.Info($"SECS/GEM Connection Changed: {state}");
                SecsGemStatus = state == ConnectionState.Selected ? SecsGemStatus.CONNECTED : SecsGemStatus.CONNECTING;
                _eventBus.Post(new ConnectionChanged(SecsGemStatus, GetDeviceIPAddress()), TimeSpan.Zero);
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

                        // handle common messages
                        if (p is { S: 1, F: 13 })
                        {
                            var reply = new S1F13().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            SecsGemStatus = SecsGemStatus.INITIALIZING;
                            _eventBus.Post(new ConnectionChanged(SecsGemStatus, GetDeviceIPAddress()), TimeSpan.Zero);
                            continue;
                        }

                        if (p is { S: 2, F: 31 })
                        {
                            var reply = new S2F31().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }

                        if (p is { S: 2, F: 33 })
                        {
                            var reply = new S2F33().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }

                        if (p is { S: 2, F: 35 })
                        {
                            var reply = new S2F35().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }

                        if (p is { S: 2, F: 37 })
                        {
                            var reply = new S2F37().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            continue;
                        }

                        if (p is { S: 5, F: 3 })
                        {
                            var reply = new S5F3().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            SecsGemStatus = SecsGemStatus.INITIALIZED;
                            _eventBus.Post(new ConnectionChanged(SecsGemStatus, GetDeviceIPAddress()), TimeSpan.Zero);
                            continue;
                        }

                        if (p is { S: 2, F: 41 }) // RCMD (result)
                        {
                            var reply = new S2F41().GetReply(p);
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            LOGGER.Info(p.ToSml());

                            // set TCS for RCMD result
                            _rcmdResponseTcs?.TrySetResult(p);
                            continue;
                        }

                        if (p is { S: 14, F: 3 }) // Pass Case
                        {
                            var reply = new S14F3(p).GetReply();
                            await SendReplyMessageAsync(message, reply).ConfigureAwait(false);
                            LOGGER.Info(p.ToSml());
                            _dataResponseTcs?.TrySetResult(p);
                            continue;
                        }

                        if (p is { S: 10, F: 3 }) // Fail Reason
                        {
                            var s10f3 = new S10F3().Parse(p);
                            await SendReplyMessageAsync(message, s10f3.GetReply()).ConfigureAwait(false);
                            LOGGER.Info(@$"Terminal Display TID: {s10f3.GetTID()} Message: {s10f3.GetMessage()}");
                            _eventBus.Post(new DisplayEvent(s10f3.GetMessage()), TimeSpan.Zero);
                            _dataResponseTcs?.TrySetResult(p);
                            continue;
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

        #endregion

        #region Send / Reply

        public async Task<Response> SendMessageAsync(S6F11 message, bool needToResponse = false, int timeoutSeconds = 30)
        {
            if (_secsGem == null) throw new InvalidOperationException("SecsGem not initialized");
            if (_connector == null) throw new InvalidOperationException("Connector not initialized");

            lock (SECSGEM_LOCK)
            {
                // recreate the TCS inside lock to prevent races
                if (needToResponse)
                {
                    _rcmdResponseTcs = new TaskCompletionSource<SecsMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
                else
                {
                    _dataResponseTcs = new TaskCompletionSource<SecsMessage?>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            var sw = Stopwatch.StartNew();
            SecsMessage? primaryReply = null;
            try
            {
                // Send and get primary reply (await correctly)
                primaryReply = await _secsGem.SendAsync(message.GetPrimaryMessage()).ConfigureAwait(false);

                // wait for device's resulting message
                SecsMessage? deviceResult = null;
                if (needToResponse && _rcmdResponseTcs != null)
                {
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
                // reset/clear TCSs so they don't leak
                if (needToResponse) _rcmdResponseTcs = null;
                else _dataResponseTcs = null;
            }
        }

        // optional synchronous wrapper (use carefully)
        public Response SendMessage(S6F11 message, bool needToResponse = false, int timeoutSeconds = 30)
            => SendMessageAsync(message, needToResponse, timeoutSeconds).GetAwaiter().GetResult();

        private Task<bool> SendReplyMessageAsync(PrimaryMessageWrapper primaryMessage, SecsMessage reply)
        {
            return primaryMessage.TryReplyAsync(reply);
        }

        #endregion

        #region Helpers & Cleanup

        public string GetDeviceIPAddress()
        {
            try
            {
                if (_connector == null) return "Unknown";
                var ip = _connector.DeviceIpAddress;
                if (string.IsNullOrEmpty(ip) || ip.Equals("NA", StringComparison.OrdinalIgnoreCase)) return "Unknown";
                return ip;
            }
            catch { return "Unknown"; }
        }

        private void ClearResponseTcs()
        {
            try
            {
                _rcmdResponseTcs?.TrySetCanceled();
            }
            catch { }
            _rcmdResponseTcs = null;
            try
            {
                _dataResponseTcs?.TrySetCanceled();
            }
            catch { }
            _dataResponseTcs = null;
        }

        public void Dispose()
        {
            // synchronous dispose: try to stop and dispose resources
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

    // Response class kept similar to your original code (adjust as necessary)
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
