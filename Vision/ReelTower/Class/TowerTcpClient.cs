using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReelTW.TowerCommunication
{
    public sealed class TowerTcpClient : IDisposable
    {
        private readonly Encoding _encoding;
        private readonly StringBuilder _receiveBuffer;
        private TcpClient _client;
        private NetworkStream _stream;

        public event Action<string> MessageReceived;
        public event Action<Exception> CommunicationError;
        public event Action<bool> ConnectionChanged;

        public bool IsConnected
        {
            get { return _client != null && _client.Connected; }
        }

        public TowerTcpClient()
        {
            _encoding = Encoding.UTF8;
            _receiveBuffer = new StringBuilder();
        }

        public async Task ConnectAsync(string ipAddress, int port, CancellationToken ct)
        {
            // 재연결 시 이전 소켓 자원을 먼저 정리하고 새 TCP 세션을 생성한다.
            Close();

            _client = new TcpClient();
            await _client.ConnectAsync(IPAddress.Parse(ipAddress), port).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            _stream = _client.GetStream();
            RaiseConnectionChanged(true);
        }

        public async Task ListenLoopAsync(CancellationToken ct)
        {
            // 수신 루프는 XML 조각을 누적한 뒤 완성된 REELSYS 메시지만 상위로 전달한다.
            if (_stream == null)
                return;

            byte[] buffer = new byte[4096];

            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    int read = await _stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                    if (read <= 0)
                        break;

                    string chunk = _encoding.GetString(buffer, 0, read);
                    foreach (string message in AppendAndExtractMessages(chunk))
                        RaiseMessageReceived(message);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                RaiseCommunicationError(ex);
            }
            finally
            {
                Close();
            }
        }

        public async Task SendAsync(string xml, CancellationToken ct)
        {
            // 사양서 XML 문자열을 그대로 UTF-8 바이트로 변환해 전송한다.
            if (_stream == null || !IsConnected)
                throw new InvalidOperationException("Tower TCP client is not connected.");

            byte[] data = _encoding.GetBytes(xml);
            await _stream.WriteAsync(data, 0, data.Length, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }

        public void Close()
        {
            try
            {
                if (_stream != null)
                    _stream.Close();
            }
            catch { }

            try
            {
                if (_client != null)
                    _client.Close();
            }
            catch { }

            _stream = null;
            _client = null;
            RaiseConnectionChanged(false);
        }

        private string[] AppendAndExtractMessages(string chunk)
        {
            // TCP는 메시지 경계를 보장하지 않으므로 종료 태그 기준으로 프레이밍한다.
            _receiveBuffer.Append(chunk);
            string all = _receiveBuffer.ToString();
            const string endTag = "</REELSYS>";
            int end = all.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
                return new string[0];

            System.Collections.Generic.List<string> messages = new System.Collections.Generic.List<string>();
            while (end >= 0)
            {
                int messageEnd = end + endTag.Length;
                messages.Add(all.Substring(0, messageEnd));
                all = all.Substring(messageEnd);
                end = all.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
            }

            _receiveBuffer.Length = 0;
            _receiveBuffer.Append(all);
            return messages.ToArray();
        }

        private void RaiseMessageReceived(string message)
        {
            Action<string> handler = MessageReceived;
            if (handler != null)
                handler(message);
        }

        private void RaiseCommunicationError(Exception ex)
        {
            Action<Exception> handler = CommunicationError;
            if (handler != null)
                handler(ex);
        }

        private void RaiseConnectionChanged(bool connected)
        {
            Action<bool> handler = ConnectionChanged;
            if (handler != null)
                handler(connected);
        }

        public void Dispose()
        {
            Close();
        }
    }
}
