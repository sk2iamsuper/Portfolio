
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using tcp_socket.Services;
using tcp_socket.Models;

namespace tcp_socket
{
    public partial class Form1 : Form
    {
        // Settings and services
        private readonly FtpService _ftp;
        private readonly FileService _fileService;
        private readonly WipParser _parser;
        private readonly string[] _folders;
        private DbService _dbService;

        // Socket kept for compatibility with original UI
        private Socket _socket;

        public Form1()
        {
            InitializeComponent();

            // default credentials (same as original)
            _ftp = new FtpService("its", "vpvits");
            _fileService = new FileService();
            _parser = new WipParser();

            // configure local folder list (same meaning as original)
            _folders = new string[] {
                @"D:\\SFTP\\oms",
                @"D:\\SFTP\\HMV_Backup",
                @"D:\\SFTP\\HMV_in",
                @"D:\\SFTP\\Backup",
                @"D:\\SFTP\\in\\edshs",
                @"D:\\SFTP\\in"
            };

            // ensure folders exist at startup
            _fileService.EnsureFolders(_folders);

            // initialize DB service lazily when needed (pass a real connection string)
            // _dbService = new DbService("server=...;uid=...;pwd=...;database=...");
        }

        private void btn_Connect_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                // Create and connect TCP socket (blocking). Consider async for production.
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.Connect(txt_IpAddress.Text.Trim(), int.Parse(txt_IpPort.Text.Trim()));

                txt_TcpContents.Text = "서버에 연결되었습니다.";
                btn_Connect.BackColor = Color.GreenYellow;

                // FTP health check - get files root list
                var ftpUrl = $@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/";
                var files = _ftp.GetFileList(ftpUrl);
                if (files.Length > 0)
                    MessageBox.Show(files[0]);

                // start timer which drives local file processing
                timer1_1.Interval = 1300;
                timer1_1.Start();
            }
            catch (Exception ex)
            {
                DisposeSocket();
                txt_TcpContents.Text = "서버 연결에 실패하였습니다." + " " + ex.Message;
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        public void DisposeSocket()
        {
            if (_socket == null) return;
            try
            {
                if (_socket.Connected)
                {
                    _socket.Shutdown(SocketShutdown.Both);
                    _socket.Close();
                }
            }
            catch { }
            finally { _socket = null; }
        }

        // Timer tick acts as main loop: scan folders and handle files
        private void timer1_1_Tick(object sender, EventArgs e)
        {
            timer1_1.Stop(); // stop while processing to avoid reentrancy
            try
            {
                ProcessSecIn();       // files coming from SEC (in folder)
                ProcessHmvIn();       // files in HMV_in -> upload to Hana
                ProcessMapFiles();     // map files in edshs
                ProcessHmvBackup();    // HMV_Backup files (from Hana) -> DB & OMS
            }
            catch (Exception ex)
            {
                // Log to textbox for visibility
                txt_TcpContents.AppendText("\r\nError: " + ex.Message);
            }
            finally
            {
                timer1_1.Interval = 13000; // longer polling after first run
                timer1_1.Start();
            }
        }

        // Handle files from D:\SFTP\in (SEC)
        private void ProcessSecIn()
        {
            var folderSec = _folders[5]; // D:\SFTP\in
            var files = Directory.GetFiles(folderSec, "*.*");
            foreach (var file in files)
            {
                // For SEC files we simply move/backup then (optionally) send to Hana
                var backup = _fileService.CopyToBackup(file, Path.Combine(_folders[3], DateTime.Now.ToString("yyyyMMdd")));
                // example: if need to then upload to Hana, use _ftp.Upload(...)
                // for simplicity we delete original after backup
                _fileService.SafeDelete(file);
            }
        }

        // Handle files in HMV_in -> upload to Hana FTP and delete
        private void ProcessHmvIn()
        {
            var folderHmvIn = _folders[2];
            var files = Directory.GetFiles(folderHmvIn, "*.*");
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var targetFtp = $@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/in/{name}";
                try
                {
                    _ftp.Upload(targetFtp, file);
                    _fileService.SafeDelete(file);
                }
                catch (Exception ex)
                {
                    txt_TcpContents.AppendText($"\r\nUpload failed: {file} - {ex.Message}");
                }
            }
        }

        // Handle map files in edshs folder
        private void ProcessMapFiles()
        {
            var folderMap = _folders[4];
            var files = Directory.GetFiles(folderMap, "*.*");
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                var targetFtp = $@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/map/{name}";
                try
                {
                    _ftp.Upload(targetFtp, file);
                    // backup map file and delete original
                    _fileService.CopyToBackup(file, Path.Combine(_folders[3], DateTime.Now.ToString("yyyyMMdd"), "Map"));
                    _fileService.SafeDelete(file);
                }
                catch (Exception ex)
                {
                    txt_TcpContents.AppendText($"\r\nMap upload failed: {file} - {ex.Message}");
                }
            }
        }

        // Handle HMV_Backup files (from Hana) - parse WIP and insert to DB, then move to backup
        private void ProcessHmvBackup()
        {
            var folderBackup = _folders[1];
            var files = Directory.GetFiles(folderBackup, "*.*");
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                // If file represents CMWIPINF, parse and update DB
                if (name.Contains("CMWIPINF"))
                {
                    try
                    {
                        using var sr = new StreamReader(file, Encoding.ASCII);
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (line.Length >= 1000)
                            {
                                var model = _parser.Parse(line);
                                // Lazy init DB service (please set connection string below)
                                if (_dbService == null)
                                {
                                    // TODO: set real connection string here or via config
                                    _dbService = new DbService("server=127.0.0.1;uid=root;pwd=;database=ufd;Charset=utf8;");
                                }
                                _dbService.InsertWipInfo(model);
                            }
                        }

                        // after processing, move file to dated backup folder
                        var datedBackup = Path.Combine(_folders[3], DateTime.Now.ToString("yyyyMMdd"));
                        _fileService.EnsureFolders(datedBackup);
                        _fileService.MoveToUnique(file, datedBackup);

                        // create CMWIPINF file for OMS if needed (omitted here — implement if required)
                    }
                    catch (Exception ex)
                    {
                        txt_TcpContents.AppendText($"\r\nProcessHmvBackup error: {ex.Message}");
                    }
                }
                else
                {
                    // Non-CMWIPINF files: just archive
                    var datedBackup = Path.Combine(_folders[3], DateTime.Now.ToString("yyyyMMdd"));
                    _fileService.EnsureFolders(datedBackup);
                    _fileService.MoveToUnique(file, datedBackup);
                }
            }
        }

        // Clean up on form closing
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer1_1?.Stop();
            _dbService?.Dispose();
            DisposeSocket();
        }
    }
}
