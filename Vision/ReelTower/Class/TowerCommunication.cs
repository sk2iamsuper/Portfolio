using System;
using System.Threading;
using System.Threading.Tasks;


namespace ReelTW.TowerCommunication
{
    public sealed class TowerCommunicationService : IDisposable
    {
        private readonly TowerTcpClient _client;
        private CancellationTokenSource _cts;
        private Task _listenTask;

        public event Action<TowerMessage> MessageHandled;

        public bool IsConnected
        {
            get { return _client.IsConnected; }
        }

        public TowerCommunicationService()
        {
            _client = new TowerTcpClient();
            _client.MessageReceived += OnMessageReceived;
            _client.CommunicationError += OnCommunicationError;
        }

        public async Task StartAsync(string ipAddress, int port)
        {
            // 서비스 시작은 연결 생성과 수신 루프 기동을 한 번에 묶는다.
            Stop();

            _cts = new CancellationTokenSource();
            await _client.ConnectAsync(ipAddress, port, _cts.Token).ConfigureAwait(false);
            _listenTask = Task.Factory.StartNew(
                () => _client.ListenLoopAsync(_cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }

        public void Stop()
        {
            // 중지 시 CancellationToken과 TCP 클라이언트를 함께 정리한다.
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            _client.Close();
            _listenTask = null;
        }

        private async void OnMessageReceived(string xml)
        {
            try
            {
                // 수신 XML을 도메인 모델로 파싱한 뒤 명령별 응답을 생성한다.
                TowerMessage message = TowerMessage.Parse(xml);
                string reply = HandleTowerMessage(message);

                if (!String.IsNullOrEmpty(reply))
                    await _client.SendAsync(reply, CurrentToken()).ConfigureAwait(false);

                RaiseMessageHandled(message);
            }
            catch (Exception ex)
            {
                SaveLog("Tower message handling error: " + ex.Message);
            }
        }

        private string HandleTowerMessage(TowerMessage message)
        {
            string command = (message.Name ?? String.Empty).Trim().ToUpperInvariant();

            // 사양서 NAME 값 기준으로 SECS/GEM 리스너처럼 switch-case 처리한다.
            switch (command)
            {
                case "REELLOADCHECK":
                    return TowerMessageFactory.CreateReelLoadCheckReply(message, CreateOkContext(message));

                case "REELLOADCHECKDONE":
                    return TowerMessageFactory.CreateSimpleResultReply(message, "REELLOADCHECKDONE", CreateOkContext(message));

                case "REELLOADIN":
                    return TowerMessageFactory.CreateSimpleResultReply(message, "REELLOADIN", CreateOkContext(message));

                case "REELLOADDONE":
                    return TowerMessageFactory.CreateSimpleResultReply(message, "REELLOADDONE", CreateOkContext(message));

                case "REELUNLOADOUT":
                    return TowerMessageFactory.CreateUnloadOutConfirm(message, CreateOkContext(message));

                case "REELUNLOADCHECK":
                    return TowerMessageFactory.CreateSimpleResultReply(message, "REELUNLOADCHECK", CreateOkContext(message));

                case "REELUNLOADCHECKDONE":
                    return TowerMessageFactory.CreateSimpleResultReply(message, "REELUNLOADCHECKDONE", CreateOkContext(message));

                case "REELUNLOADDONE":
                    return TowerMessageFactory.CreateBoxUnloadDoneReply(message, CreateOkContext(message));

                default:
                    SaveLog("Unknown tower command: " + command);
                    return String.Empty;
            }
        }

        private TowerReplyContext CreateOkContext(TowerMessage message)
        {
            // 현재 토대 단계에서는 정상 승인 기본값을 만들고, 추후 실제 비즈니스 판정으로 교체한다.
            TowerReplyContext context = new TowerReplyContext();
            context.CommunicationId = String.IsNullOrEmpty(message.CommunicationId) ? "XXX" : message.CommunicationId;
            context.RequestId = String.IsNullOrEmpty(message.RequestId) ? TowerMessage.NewRequestId() : message.RequestId;
            context.ReelId = message.Reels.Count > 0 ? message.Reels[0].Id : String.Empty;
            context.PartNumber = message.Reels.Count > 0 ? message.Reels[0].PartNumber : String.Empty;
            context.BarcodeType = message.BarcodeType;
            context.BoxId = message.BoxId;
            context.Result = TowerResult.OK;
            context.ErrorId = String.Empty;
            context.ErrorDesc = String.Empty;
            return context;
        }

        private CancellationToken CurrentToken()
        {
            return _cts == null ? CancellationToken.None : _cts.Token;
        }

        private void OnCommunicationError(Exception ex)
        {
            SaveLog("Tower TCP/IP error: " + ex.Message);
        }

        private void RaiseMessageHandled(TowerMessage message)
        {
            Action<TowerMessage> handler = MessageHandled;
            if (handler != null)
                handler(message);
        }

        private void SaveLog(string text)
        {
            try
            {
                ClassSystemConfig.Ins.m_ClsFunc.SaveLog(
                    ClassFunction.SAVING_LOG_TYPE.DATA_RECEIVE,
                    text,
                    ClassSystemConfig.Ins.m_ClsCommon.IsSaveLog_Local,
                    true);
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _client.Dispose();
        }
    }
}
