using System;
using System.IO;
using System.Linq;
using System.Net;

namespace tcp_socket.Services
{
    public class FtpService
    {
        private readonly string _user;
        private readonly string _pw;

        public FtpService(string user, string pw)
        {
            _user = user;
            _pw = pw;
        }

        public string[] GetFileList(string url)
        {
            try
            {
                var uri = new Uri(url);
                var req = (FtpWebRequest)WebRequest.Create(uri);
                req.Method = WebRequestMethods.Ftp.ListDirectory;
                req.Credentials = new NetworkCredential(_user, _pw);
                req.UsePassive = false;
                req.KeepAlive = false;

                using var resp = (FtpWebResponse)req.GetResponse();
                using var sr = new StreamReader(resp.GetResponseStream());
                var text = sr.ReadToEnd();
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                return lines;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public void Upload(string ftpUrl, string localFilePath)
        {
            using var client = new WebClient();
            client.Credentials = new NetworkCredential(_user, _pw);
            client.UploadFile(ftpUrl, WebRequestMethods.Ftp.UploadFile, localFilePath);
        }

        public bool Delete(string ftpUrl)
        {
            try
            {
                var req = (FtpWebRequest)WebRequest.Create(ftpUrl);
                req.Method = WebRequestMethods.Ftp.DeleteFile;
                req.Credentials = new NetworkCredential(_user, _pw);
                using var resp = (FtpWebResponse)req.GetResponse();
                return true;
            }
            catch { return false; }
        }

        public bool Exists(string ftpUrl)
        {
            try
            {
                var req = (FtpWebRequest)WebRequest.Create(ftpUrl);
                req.Method = WebRequestMethods.Ftp.GetFileSize;
                req.Credentials = new NetworkCredential(_user, _pw);
                using var resp = (FtpWebResponse)req.GetResponse();
                return true;
            }
            catch { return false; }
        }
    }
}
