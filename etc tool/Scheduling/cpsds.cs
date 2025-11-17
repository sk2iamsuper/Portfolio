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

/*
스마트 백업 : 3개월 이상된 db 서버 데이터 자동 백업(low cost storage로 백업) 후 삭제
보안 : SSH, SFTP 연결
*/

namespace CopySDS
{
    public partial class Form1 : Form
    {
        private Timer timer; // 실시간 시간 표시 및 작업 트리거를 위한 타이머

        public Form1()
        {
            InitializeComponent();

            // 초기화 시 현재 시간 표시
            txtTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // 1초 간격으로 실행되는 타이머 설정
            timer = new Timer { Interval = 1000 };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        // 타이머 틱 이벤트 핸들러 - 매초 실행됨
        private void Timer_Tick(object sender, EventArgs e)
        {
            // 현재 시간 업데이트
            txtTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 로그 라인 수 제한 관리
            TrimLogLines();

            // 트리거 시간: 07:30:00 (0-1초 사이에 실행)
            if (DateTime.Now.Hour == 7 && DateTime.Now.Minute == 30 && DateTime.Now.Second <= 1)
            {
                // 비동기로 첫 번째 작업 실행
                Task.Run(() => Copy_107_120_201_201());
            }

            // 트리거 시간: 14:00:00 (0-1초 사이에 실행)
            if (DateTime.Now.Hour == 14 && DateTime.Now.Minute == 0 && DateTime.Now.Second <= 1)
            {
                // 비동기로 두 번째 작업 실행
                Task.Run(() => Copy_107_120_201_202());
            }
        }

        #region Helpers (로그, 비밀번호)
        
        // 로그 출력 메서드 - 스레드 안전하게 구현
        private void Log(string message)
        {
            // UI 스레드에서 실행되어야 하는 경우
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => Log(message)));
                return;
            }
            
            // 타임스탬프와 함께 로그 메시지 생성
            string entry = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " | " + message + Environment.NewLine;
            
            // 새 로그를 상단에 추가 (역순 표시)
            txtLog.Text = entry + txtLog.Text;
        }

        // 로그 라인 수 제한 관리 메서드
        private void TrimLogLines()
        {
            // UI 스레드에서 실행되어야 하는 경우
            if (InvokeRequired)
            {
                this.Invoke(new Action(TrimLogLines));
                return;
            }
            
            try
            {
                var lines = txtLog.Lines;
                // 로그 라인이 50,000줄을 초과하면 모두 지움
                if (lines.Length > 50000)
                {
                    txtLog.Clear();
                }
            }
            catch { } // 예외 발생 시 무시
        }

        // 암호화된 비밀번호 파일에서 복호화하여 비밀번호 가져오기
        private string GetDecryptedPassword()
        {
            try
            {
                // 실행 파일 경로에 있는 encrypted.txt 파일 경로 구성
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "encrypted.txt");
                
                // 파일 존재 여부 확인
                if (!File.Exists(path))
                {
                    Log("encrypted.txt 파일이 없습니다.");
                    return string.Empty;
                }
                
                // 암호화된 텍스트 읽기
                string encrypted = File.ReadAllText(path).Trim();
                
                // 복호화하여 반환
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
        
        // 원격 디렉토리 전체를 로컬로 다운로드 (재귀적)
        private void DownloadDirectory(SftpClient sftp, string remotePath, string localPath)
        {
            // 로컬 디렉토리가 없으면 생성
            if (!Directory.Exists(localPath))
                Directory.CreateDirectory(localPath);

            // 원격 디렉토리 내용 조회
            var files = sftp.ListDirectory(remotePath);
            
            foreach (var file in files)
            {
                // 현재 디렉토리와 상위 디렉토리 건너뛰기
                if (file.Name == "." || file.Name == "..") continue;
                
                // 원격 및 로컬 파일 경로 구성
                string remoteFilePath = CombineRemotePath(remotePath, file.Name);
                string localFilePath = Path.Combine(localPath, file.Name);

                if (file.IsDirectory)
                {
                    // 디렉토리인 경우 재귀적으로 다운로드
                    DownloadDirectory(sftp, remoteFilePath, localFilePath);
                }
                else if (file.IsRegularFile)
                {
                    // 일반 파일인 경우 다운로드
                    using (var fs = File.OpenWrite(localFilePath))
                    {
                        sftp.DownloadFile(remoteFilePath, fs);
                    }
                }
            }
        }

        // 원격 경로 결합 유틸리티 메서드
        private string CombineRemotePath(string basePath, string name)
        {
            if (basePath.EndsWith("/")) 
                return basePath + name;
            return basePath + "/" + name;
        }
        #endregion

        #region 작업 1: 107.120.201.201 (/purge/SDS) 처리
        
        // 첫 번째 서버(107.120.201.201)에서 SDS 데이터 백업 및 정리
        private void Copy_107_120_201_201()
        {
            string host = "107.120.201.201";
            string username = "root";
            string password = GetDecryptedPassword();
            
            // 비밀번호가 없으면 작업 중단
            if (string.IsNullOrEmpty(password))
            {
                Log("비밀번호가 비어있어 107.120.201.201 작업을 건너뜁니다.");
                return;
            }

            string remoteBasePath = "/purge/SDS";
            string localBasePath = @"D:\purge\SDS";

            try
            {
                // SFTP와 SSH 클라이언트 생성 및 연결
                using (var sftp = new SftpClient(host, 22, username, password))
                using (var ssh  = new SshClient(host, 22, username, password))
                {
                    sftp.Connect();
                    ssh.Connect();

                    // 원격 디렉토리 목록 조회 (디렉토리만 필터링)
                    var entries = sftp.ListDirectory(remoteBasePath)
                                      .Where(e => e.IsDirectory && e.Name != "." && e.Name != "..")
                                      .ToList();

                    foreach (var dir in entries)
                    {
                        // 폴더명이 yyyyMMdd 형식인 경우 처리
                        if (DateTime.TryParseExact(dir.Name, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime folderDate))
                        {
                            // 최근 1일치(어제) 데이터 복사 조건
                            if (folderDate >= DateTime.Today.AddDays(-1) && folderDate < DateTime.Today)
                            {
                                string remoteFolder = CombineRemotePath(remoteBasePath, dir.Name);
                                string localFolder = Path.Combine(localBasePath, dir.Name);

                                // 로컬에 해당 폴더가 없을 때만 다운로드
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
                                // SSH로 안전하게 find 명령어로 삭제
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

                    // 연결 종료
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
        
        // 두 번째 서버(107.120.201.202)에서 SQL 파일 백업 및 정리
        private void Copy_107_120_201_202()
        {
            string host = "107.120.201.202";
            string username = "root";
            string password = GetDecryptedPassword();
            
            // 비밀번호가 없으면 작업 중단
            if (string.IsNullOrEmpty(password))
            {
                Log("비밀번호가 비어있어 107.120.201.202 작업을 건너뜁니다.");
                return;
            }

            string remoteBasePath = "/purge";
            string localBasePath = @"D:\purge\DB2_purge";

            try
            {
                // SFTP와 SSH 클라이언트 생성 및 연결
                using (var sftp = new SftpClient(host, 22, username, password))
                using (var ssh  = new SshClient(host, 22, username, password))
                {
                    sftp.Connect();
                    ssh.Connect();

                    // 원격 디렉토리에서 일반 파일만 필터링
                    var entries = sftp.ListDirectory(remoteBasePath)
                                      .Where(e => e.IsRegularFile)
                                      .ToList();

                    // 이미 복사한 날짜를 추적하기 위한 집합
                    var copiedDates = new HashSet<string>();

                    foreach (var file in entries)
                    {
                        // SQL 파일만 처리
                        if (!file.Name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // 파일명에서 날짜 부분 추출 (형식: ..._yyyyMMdd.sql)
                        int lastUnderscore = file.Name.LastIndexOf('_');
                        if (lastUnderscore > -1 && file.Name.Length >= lastUnderscore + 9)
                        {
                            string datePart = file.Name.Substring(lastUnderscore + 1, 8);
                            
                            // 날짜 형식이 유효한지 확인
                            if (DateTime.TryParseExact(datePart, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime fileDate))
                            {
                                // 어제 생성된 파일만 복사
                                if (fileDate >= DateTime.Today.AddDays(-1) && fileDate < DateTime.Today)
                                {
                                    // 날짜별 폴더 생성
                                    string folderPath = Path.Combine(localBasePath, datePart);
                                    if (!Directory.Exists(folderPath))
                                    {
                                        Directory.CreateDirectory(folderPath);
                                    }

                                    // 파일 다운로드
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
                                    string remoteFilePath = CombineRemotePath(remoteBasePath, file.Name);
                                    
                                    // SSH로 파일 삭제
                                    string delCmd = $"rm -f {remoteFilePath}";
                                    var delResult = ssh.RunCommand(delCmd);
                                    
                                    if (!string.IsNullOrEmpty(delResult.Error))
                                        Log($"{host} 삭제 에러: {delResult.Error}");
                                }
                            }
                        }
                    }

                    // 연결 종료
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
