using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace CopySDS
{
    public partial class Form1 : Form
    {
        private Timer timer;

        public Form1()
        {
            InitializeComponent();

            txtTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            timer = new Timer { Interval = 1000 };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            txtTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 로그 라인 수 제한
            TrimLogLines();

            // 트리거 시간: 07:30:00 (allow 0..1초) and 14:00:00
            if (DateTime.Now.Hour == 7 && DateTime.Now.Minute == 30 && DateTime.Now.Second <= 1)
            {
                Task.Run(() => Copy_107_120_201_201());
            }

            if (DateTime.Now.Hour == 14 && DateTime.Now.Minute == 0 && DateTime.Now.Second <= 1)
            {
                Task.Run(() => Copy_107_120_201_202());
            }
        }

        #region Helpers (로그, 비밀번호)
        private void Log(string message)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => Log(message)));
                return;
            }
            string entry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine;
            txtLog.Text = entry + txtLog.Text;
        }

        private void TrimLogLines()
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(TrimLogLines));
                return;
            }
            try
            {
                var lines = txtLog.Lines;
                if (lines.Length > 50000)
                {
                    txtLog.Clear();
                }
            }
            catch { }
        }

        private string GetDecryptedPassword()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "encrypted.txt");
                if (!File.Exists(path))
                {
                    Log("encrypted.txt 파일이 없습니다.");
                    return string.Empty;
                }
                string encrypted = File.ReadAllText(path).Trim();
                return EncryptionHelper.Decrypt(encrypted);
            }
            catch (Exception ex)
            {
                Log("비밀번호 복호화 오류: " + ex.Message);
                return string.Empty;
            }
        }
        #endregion

        #region SSH/SFTP 유틸리티
        private void DownloadDirectory(SftpClient sftp, string remotePath, string localPath)
        {
            // 원격 경로가 디렉토리인지 확인
            if (!Directory.Exists(localPath))
                Directory.CreateDirectory(localPath);

            var files = sftp.ListDirectory(remotePath);
            foreach (var file in files)
            {
                if (file.Name == "." || file.Name == "..") continue;
                string remoteFilePath = CombineRemotePath(remotePath, file.Name);
                string localFilePath = Path.Combine(localPath, file.Name);

                if (file.IsDirectory)
                {
                    DownloadDirectory(sftp, remoteFilePath, localFilePath);
                }
                else if (file.IsRegularFile)
                {
                    // 파일 다운로드
                    using (var fs = File.OpenWrite(localFilePath))
                    {
                        sftp.DownloadFile(remoteFilePath, fs);
                    }
                }
            }
        }

        private string CombineRemotePath(string basePath, string name)
        {
            if (basePath.EndsWith("/")) return basePath + name;
            return basePath + "/" + name;
        }
        #endregion

        #region 작업 1: 107.120.201.201 (/purge/SDS) 처리
        private void Copy_107_120_201_201()
        {
            string host = "107.120.201.201";
            string username = "root";
            string password = GetDecryptedPassword();
            if (string.IsNullOrEmpty(password))
            {
                Log("비밀번호가 비어있어 107.120.201.201 작업을 건너뜁니다.");
                return;
            }

            string remoteBasePath = "/purge/SDS";
            string localBasePath = @"D:\purge\SDS";

            try
            {
                using (var sftp = new SftpClient(host, 22, username, password))
                using (var ssh  = new SshClient(host, 22, username, password))
                {
                    sftp.Connect();
                    ssh.Connect();

                    var entries = sftp.ListDirectory(remoteBasePath)
                                      .Where(e => e.IsDirectory && e.Name != "." && e.Name != "..")
                                      .ToList();

                    foreach (var dir in entries)
                    {
                        // 폴더명이 yyyyMMdd 형식인 경우 처리
                        if (DateTime.TryParseExact(dir.Name, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime folderDate))
                        {
                            // 최근 1일치(어제) 복사
                            if (folderDate >= DateTime.Today.AddDays(-1) && folderDate < DateTime.Today)
                            {
                                string remoteFolder = CombineRemotePath(remoteBasePath, dir.Name);
                                string localFolder = Path.Combine(localBasePath, dir.Name);

                                if (!Directory.Exists(localFolder))
                                {
                                    Directory.CreateDirectory(localFolder);
                                    Log($"{host} : {remoteFolder} -> {localFolder} 다운로드 시작");
                                    DownloadDirectory(sftp, remoteFolder, localFolder);
                                    Log($"{host} : {dir.Name} 폴더 다운로드 완료");
                                }
                                else
                                {
                                    Log($"{host} : {localFolder} 이미 존재, 다운로드 건너뜀");
                                }
                            }

                            // 3개월(≈90일) 이상된 폴더 삭제 (원격)
                            if (folderDate < DateTime.Today.AddMonths(-3))
                            {
                                // SSH로 안전하게 find로 삭제
                                string cmd = $"find {remoteBasePath} -maxdepth 1 -type d -name \"{dir.Name}\" -exec rm -rf {{}} \\;";
                                var result = ssh.RunCommand(cmd);
                                if (!string.IsNullOrEmpty(result.Error))
                                {
                                    Log($"{host} 삭제 에러: {result.Error}");
                                }
                                else
                                {
                                    Log($"{host} : {remoteBasePath}/{dir.Name} 삭제 완료");
                                }
                            }
                        }
                    }

                    ssh.Disconnect();
                    sftp.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Log($"Error Copy_107_120_201_201: {ex.Message}");
            }
        }
        #endregion

        #region 작업 2: 107.120.201.202 (/purge) 처리 - .sql 파일
        private void Copy_107_120_201_202()
        {
            string host = "107.120.201.202";
            string username = "root";
            string password = GetDecryptedPassword();
            if (string.IsNullOrEmpty(password))
            {
                Log("비밀번호가 비어있어 107.120.201.202 작업을 건너뜁니다.");
                return;
            }

            string remoteBasePath = "/purge";
            string localBasePath = @"D:\purge\DB2_purge";

            try
            {
                using (var sftp = new SftpClient(host, 22, username, password))
                using (var ssh  = new SshClient(host, 22, username, password))
                {
                    sftp.Connect();
                    ssh.Connect();

                    var entries = sftp.ListDirectory(remoteBasePath)
                                      .Where(e => e.IsRegularFile)
                                      .ToList();

                    var copiedDates = new HashSet<string>();

                    foreach (var file in entries)
                    {
                        if (!file.Name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                            continue;

                        int lastUnderscore = file.Name.LastIndexOf('_');
                        if (lastUnderscore > -1 && file.Name.Length >= lastUnderscore + 9)
                        {
                            string datePart = file.Name.Substring(lastUnderscore + 1, 8);
                            if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                            {
                                // 어제 파일만 복사
                                if (fileDate >= DateTime.Today.AddDays(-1) && fileDate < DateTime.Today)
                                {
                                    string folderPath = Path.Combine(localBasePath, datePart);
                                    if (!Directory.Exists(folderPath))
                                    {
                                        Directory.CreateDirectory(folderPath);
                                    }

                                    string remoteFilePath = CombineRemotePath(remoteBasePath, file.Name);
                                    string localFilePath = Path.Combine(folderPath, file.Name);

                                    using (var fs = File.OpenWrite(localFilePath))
                                    {
                                        sftp.DownloadFile(remoteFilePath, fs);
                                    }

                                    copiedDates.Add(datePart);
                                    Log($"{host} : {file.Name} -> D:\\purge\\DB2_purge\\{datePart} 복사완료");
                                }

                                // 3개월 이전 파일은 원격에서 삭제
                                if (fileDate < DateTime.Today.AddMonths(-3))
                                {
                                    // SSH로 한 번에 삭제 (또는 sftp.DeleteFile(remoteFilePath))
                                    string remoteFilePath = CombineRemotePath(remoteBasePath, file.Name);
                                    string delCmd = $"rm -f {remoteFilePath}";
                                    var delResult = ssh.RunCommand(delCmd);
                                    if (!string.IsNullOrEmpty(delResult.Error))
                                        Log($"{host} 삭제 에러: {delResult.Error}");
                                }
                            }
                        }
                    }

                    ssh.Disconnect();
                    sftp.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Log($"Error Copy_107_120_201_202: {ex.Message}");
            }
        }
        #endregion
    }
}
