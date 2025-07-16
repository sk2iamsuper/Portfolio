using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Net.Mime.MediaTypeNames;
using System.Xml.Linq;
using System.IO.MemoryMappedFiles;
using MySql.Data.MySqlClient;
using System.Security.Policy;
using System.Runtime.InteropServices;

namespace tcp_socket
{   
    public partial class Form1 : Form
    {
        
        public struct CMWIPINF_DATA
        {
            public CMWIPINF_FIELD FIELD;
        }

        private MySqlConnection _connection1, _connection2;
        private String m_RowData;
        private CMWIPINF_DATA CMWIPINF;
        private Socket _socket;
        private const int BUFFER_SIZE = 1278;

        private void Connect1()
        {
            _connection1 = Helper.InitConnection("VPV");
            if (_connection1.State != ConnectionState.Open)
                _connection1.Open();
        }

       
            
        //Test 107.120.201.232
       
        private string _id = "its";
        private string _pw = "vpvits";


        //ServerHana
        //113.160.209.134

        //private string _id = "vpv";
        //private string _pw = "Value+2024";

        public struct CMWIPINF_FIELD
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
            public byte[] START_TAG;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
            public byte[] COMPANY_CODE;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
            public byte[] RUN_ID;      
            
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
            public byte[] PRODUCT_CODE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 15)]
            public byte[] LOT_ID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] LOT_TYPE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] RETURN_TYPE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] PROCESS_ID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
            public byte[] STEP_ID;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] STEP_SEQ_NO;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
            public byte[] STEP_DESC;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
            public byte[] STEP_IN_DTTM;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] AREA_FLAG;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] AREA_ID;         
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] WAFER_QTY;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] CHIP_QTY;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] WAFER_CHIP_FLAG;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] HOLD_FLAG;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] HOLD_CODE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
            public byte[] HOLD_DTTM;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] NCF_CODE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] NCA_CODE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] NCT_CODE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] NCQ_CODE;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] OTHER;
            
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] LOSS_QTY;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
            public byte[] BONUS_QTY;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public byte[] FAB_LINE;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 14)]
            public byte[] CREATE_DTTM;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] CUTOFF_DATE;
            
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1024)]
            public byte[] INKLESS;
     

        }

        

        private String[] FOLDER_LIST = new String[6] { "D:\\SFTP\\oms", "D:\\SFTP\\HMV_Backup", "D:\\SFTP\\HMV_in", "D:\\SFTP\\Backup", "D:\\SFTP\\in\\edshs", "D:\\SFTP\\in" };

        public Form1()
        {
            InitializeComponent();

            //timer1_1.Interval = 1300;
            //timer1_1.Start();
           
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private void btn_Connect_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                if (_socket == null)
                {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
                }
                _socket.Connect(txt_IpAddress.Text, int.Parse(txt_IpPort.Text));
                txt_TcpContents.Text = "서버에 연결되었습니다.";

                Invoke(new Action(() =>
                {
                    btn_Connect.BackColor = Color.GreenYellow;

                }));
            }
            catch (Exception)
            {
                DisposeSocket();
                txt_TcpContents.Text = "서버 연결에 실패하였습니다.";
            }
            finally
            {
                Cursor = Cursors.Arrow;
            }


            
            //TestVPV
            var result = GetFileList($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/", "its", "vpvits");

            //ServerHana
            //var result = GetFileList($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/", "vpv", "Value+2024");

            MessageBox.Show(result[0]);

            timer1_1.Interval = 1300;
            timer1_1.Start();
        }

        public void DisposeSocket()
        {
            Invoke(new Action(() => { txt_TcpContents.BackColor = Color.Red; }));
            if (_socket == null || !_socket.Connected) return;
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            _socket = null;
        }

        private void btn_Disconnect_Click(object sender, EventArgs e)
        {

        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            //DeleteFTPFile($"ftp://{txt_IpAddress.Text}:21/CONFIG/CONFIG7.PTC", "vpv", "Value+2024");
            //UploadFTPFile($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/in/CMWIPINF.ff", $@"D:\OMS\20240813\CMWIPINF.ff", $"{_id}", $"{_pw}");
        }
        

        public string[] GetFileList(string url, string userID, string password)
        {
            string[] downloadFiles;
            StringBuilder result = new StringBuilder();
            WebResponse response = null;
            StreamReader reader = null;
            try
            {
                FtpWebRequest reqFTP;
                reqFTP = (FtpWebRequest)FtpWebRequest.Create(new Uri(url));
                reqFTP.UseBinary = true;
                reqFTP.Credentials = new NetworkCredential(userID, password);
                reqFTP.Method = WebRequestMethods.Ftp.ListDirectory;
                reqFTP.Proxy = null;
                reqFTP.KeepAlive = false;
                reqFTP.UsePassive = false;
                response = reqFTP.GetResponse();
                reader = new StreamReader(response.GetResponseStream());
                string line = reader.ReadLine();
                while (line != null)
                {
                    result.Append(line);
                    result.Append("\n");
                    line = reader.ReadLine();
                }
                // to remove the trailing '\n'
                result.Remove(result.ToString().LastIndexOf('\n'), 1);
                return result.ToString().Split('\n');
            }
            catch (Exception)
            {
                if (reader != null)
                {
                    reader.Close();
                }
                if (response != null)
                {
                    response.Close();
                }
                downloadFiles = null;
                return downloadFiles;
            }
        }
        public void UploadFTPFile(string url, string inputFile, string userID, string password)
        {
            FileInfo fileInf = new FileInfo(inputFile);
            string uri = url;
            FtpWebRequest reqFTP;
            UriBuilder URI = new UriBuilder(uri);
            URI.Scheme = "ftp";
            reqFTP = (FtpWebRequest)FtpWebRequest.Create(URI.Uri);
            reqFTP.Credentials = new NetworkCredential($"{_id}", $"{_pw}");
            reqFTP.KeepAlive = false;
            reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
            reqFTP.UseBinary = true;
            reqFTP.ContentLength = fileInf.Length;
            reqFTP.UsePassive = true;
            int bufflenght = 2048;
            byte[] buff = new byte[bufflenght];
            int contentLen;
            FileStream fs = fileInf.OpenRead();
            try
            {
                Stream strm = reqFTP.GetRequestStream();
                contentLen = fs.Read(buff, 0, bufflenght);
                while (contentLen != 0)
                {
                    strm.Write(buff, 0, contentLen);
                    contentLen = fs.Read(buff, 0, bufflenght);
                }
                strm.Close();
                fs.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
                return;
                //throw;
            }
        }
        public bool DeleteFTPFile(string url, string userID, string password)
        {
            try
            {
                FtpWebRequest ftpWebRequest = WebRequest.Create(url) as FtpWebRequest;

                ftpWebRequest.Credentials = new NetworkCredential(userID, password);
                ftpWebRequest.Method = WebRequestMethods.Ftp.DeleteFile;

                FtpWebResponse ftpWebResponse = ftpWebRequest.GetResponse() as FtpWebResponse;
            }
            catch
            {
                return false;
            }

            return true;
        }

        #region FTP 파일존재 여부

        public bool FtpFileExists(String strFtpAddress, String strFtpId, String strFtpPwd, String FileName)
        {
            bool IsExists = true;

            FtpWebRequest reqFTP = null;
            FtpWebResponse respFTP = null;

            try
            {

                UriBuilder URI = new UriBuilder(strFtpAddress + FileName);

                URI.Scheme = "ftp";

                reqFTP = (FtpWebRequest)WebRequest.Create(URI.Uri);
                reqFTP.Credentials = new NetworkCredential(strFtpId, strFtpPwd);
                reqFTP.Method = WebRequestMethods.Ftp.GetFileSize;
                respFTP = (FtpWebResponse)reqFTP.GetResponse();
                if (respFTP.StatusCode == System.Net.FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    IsExists = false;
                }

            }
            catch
            {
                IsExists = false;
            }
            finally
            {
                if (reqFTP != null)
                {
                    reqFTP = null;
                }
                if (respFTP != null)
                {
                    respFTP = null;
                }
            }
            return IsExists;
        }

        #endregion
        private FileInfo FileExtensionChange(String fullPath)
        {
            FileInfo fileMove = new FileInfo(fullPath);
            try
            {
                if (fileMove.Exists)
                {
                    String ext = fullPath.Substring(fullPath.LastIndexOf('.'));
                    fileMove.MoveTo(fileMove.FullName);
                }
            }
            catch (IOException e)
            {
                //lbMain.Invoke(new LogListboxDele(AddLog), new object[] { string.Format("{0},{1},{2}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), "에러 발생 ", e.Message) });
            }
            return fileMove;
        }
        private void timer1_1_Tick(object sender, EventArgs e)
        {

            string[] files = null;
            string[] hanafiles = null;
            string[] mapfiles = null;
            string[] secfiles = null;

            string folderSEC = @"D:\SFTP\in";
            string folderName = @"D:\SFTP\HMV_in";
            string folderNameFromHana = @"D:\SFTP\HMV_Backup";
            string folderMap = @"D:\SFTP\in\edshs";

            secfiles = Directory.GetFiles(folderSEC, "*.*");
            files = Directory.GetFiles(folderName, "*.*");
            hanafiles = Directory.GetFiles(folderNameFromHana, "*.*");
            mapfiles = Directory.GetFiles(folderMap, "*.*");
            
            //subDirs = Directory.GetDirectories(folderName);

            timer1_1.Stop();

            //sec file bakcup and transfer Hana
            //
            if (secfiles != null)
            {

                foreach (string file in secfiles)
                {
                    //string targetName = file.Substring(15);

                    FileMove(file);

                    //File.Delete(file);
                    //}

                }


            }

            //FTP file move
            //VPV >> Hana, copy and delete
            if (files != null)
            {
                
                foreach (string file in files)
                {
                    string targetName = file.Substring(15);

                    //if (file.Contains("CMWIPINF")
                    //    && (!FtpFileExists($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/in/"
                    //    , $"{_id}", $"{_pw}", targetName)))
                    //{

                        //Upload to ServerHana
                        UploadFTPFile($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/in/{targetName}", file, $"{_id}", $"{_pw}");
                        File.Delete(file);
                    //}

                }

                
            }

            //Map file
            //VPV >> Hana
            //D:\SFTP\in\edshs >> hana\map


            if (mapfiles != null)
            {


                foreach (string mapfile in mapfiles)
                {
                    string targetName = mapfile.Substring(17);

                    //if (file.Contains("CMWIPINF")
                    //    && (!FtpFileExists($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/in/"
                    //    , $"{_id}", $"{_pw}", targetName)))
                    //{

                    //Upload to ServerHana
                    UploadFTPFile($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/map/{targetName}", mapfile, $"{_id}", $"{_pw}");


                    FileMove(mapfile);

                    //File.Delete(mapfile);
                    //}

                }


            }

            //for SEC
            //hana wip >> oms, copy and delete
            if (hanafiles != null)
            {

                foreach (string hanafile in hanafiles)
                {
                    //string targetName = hanafile.Substring(19);

                    //if (file.Contains("CMWIPINF")
                    //    && (!FtpFileExists($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/in/"
                    //    , $"{_id}", $"{_pw}", targetName)))
                    //{
                    //Upload to ServerHana
                    //UploadFTPFile($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/in/{targetName}", file, $"{_id}", $"{_pw}");

                    //File.Delete(hanafile);
                    //}

                    //파일이름이 WIPINF이면 행추가
                    //DB delete and insert

                    if (hanafile.Contains("CMWIPINF"))
                    {
                        Connect1();

                        //Delete info table
                        var sql = "DELETE FROM ufd.tb_ufd_input_info";
                        MySqlHelper.ExecuteNonQuery(_connection1, sql);

                        //Insert info table
                        DB_Update(FileExtensionChange(hanafile));

                        //Create File
                        Create_CMWIPINF();

                    }
                    FileMove(hanafile);

                }

            }

            timer1_1.Interval = 13000;
            timer1_1.Start();

            //string strNowFilePath = folderName + "\\" + fileMove.Name;

            //FileInfo fileExists = new FileInfo(strNowFilePath);

            //if (fileExists.Exists)
            //{
            //    if (folderName.Contains("CMWIPINF"))
            //    {
            //      UploadFTPFile($@"ftp://{txt_IpAddress.Text}:{txt_IpPort.Text}/ftp/", folderName, $"{_id}", $"{_pw}");
            //    }
            //}




        }


        private void CMWIPINF_Parser()
        {
            //byte[] HMIBuffer;
            //CMSALESE = new CMSALESE_DATA();
            //int nStructSize = Marshal.SizeOf(CMSALESE);
            //HMIBuffer = new byte[nStructSize];
            //HMIBuffer = Encoding.UTF8.GetBytes(m_RowData);
            //CMSALESE = (CMSALESE_DATA)RawDeSerialize(HMIBuffer, CMSALESE.GetType());
            // LS1
            // BZ
            // K9AHGD8H0B-LBXQQQ             LURD35         LURD35GA       EE**HBRC3               0006                          6               DIE ATTACH 5            20240928181004ASSYHMVA       706         2CW                         *                   *                   *                   *                   *                            0         0--                                                                2024100512001220241005CS_FLAG= ERINFO= PEGEVT= PURPOSETYPE= WORDER= NCFCODE= ASYSITE=HM START_TIME=20240816152203 NCHCODE= NCBCODE=    
            CMWIPINF.FIELD.START_TAG = Encoding.UTF8.GetBytes(m_RowData.Substring(0, 3));  
            CMWIPINF.FIELD.COMPANY_CODE = Encoding.UTF8.GetBytes(m_RowData.Substring(3, 2));  
            CMWIPINF.FIELD.PRODUCT_CODE = Encoding.UTF8.GetBytes(m_RowData.Substring(5, 30));  //Prod_code
            CMWIPINF.FIELD.RUN_ID = Encoding.UTF8.GetBytes(m_RowData.Substring(35, 15));  //RUN_ID
            CMWIPINF.FIELD.LOT_ID = Encoding.UTF8.GetBytes(m_RowData.Substring(50, 15));
            CMWIPINF.FIELD.LOT_TYPE = Encoding.UTF8.GetBytes(m_RowData.Substring(65, 2));
            CMWIPINF.FIELD.RETURN_TYPE = Encoding.UTF8.GetBytes(m_RowData.Substring(67, 2));
            CMWIPINF.FIELD.PROCESS_ID = Encoding.UTF8.GetBytes(m_RowData.Substring(69, 15));
            CMWIPINF.FIELD.STEP_ID = Encoding.UTF8.GetBytes(m_RowData.Substring(84, 30));
            CMWIPINF.FIELD.STEP_SEQ_NO = Encoding.UTF8.GetBytes(m_RowData.Substring(114, 16));
            CMWIPINF.FIELD.STEP_DESC = Encoding.UTF8.GetBytes(m_RowData.Substring(130, 24));
            //CMWIPINF.FIELD.STEP_IN_DTTM = Encoding.UTF8.GetBytes(m_RowData.Substring(154, 14));
            CMWIPINF.FIELD.STEP_IN_DTTM = Encoding.UTF8.GetBytes(m_RowData.Substring(159, 14));
            CMWIPINF.FIELD.AREA_FLAG = Encoding.UTF8.GetBytes(m_RowData.Substring(173, 4));
            CMWIPINF.FIELD.AREA_ID = Encoding.UTF8.GetBytes(m_RowData.Substring(177, 4));
            CMWIPINF.FIELD.CHIP_QTY = Encoding.UTF8.GetBytes(m_RowData.Substring(181, 10));
            CMWIPINF.FIELD.WAFER_QTY = Encoding.UTF8.GetBytes(m_RowData.Substring(191, 10));
            CMWIPINF.FIELD.WAFER_CHIP_FLAG = Encoding.UTF8.GetBytes(m_RowData.Substring(201, 1));
            CMWIPINF.FIELD.HOLD_FLAG = Encoding.UTF8.GetBytes(m_RowData.Substring(202, 2));
            CMWIPINF.FIELD.HOLD_CODE = Encoding.UTF8.GetBytes(m_RowData.Substring(204, 10));
            CMWIPINF.FIELD.HOLD_DTTM = Encoding.UTF8.GetBytes(m_RowData.Substring(214, 14));
            CMWIPINF.FIELD.NCF_CODE = Encoding.UTF8.GetBytes(m_RowData.Substring(228, 20));
            CMWIPINF.FIELD.NCA_CODE = Encoding.UTF8.GetBytes(m_RowData.Substring(248, 20));
            CMWIPINF.FIELD.NCT_CODE = Encoding.UTF8.GetBytes(m_RowData.Substring(268, 20));
            CMWIPINF.FIELD.NCQ_CODE = Encoding.UTF8.GetBytes(m_RowData.Substring(288, 20));
            CMWIPINF.FIELD.OTHER = Encoding.UTF8.GetBytes(m_RowData.Substring(308, 20));
            CMWIPINF.FIELD.LOSS_QTY = Encoding.UTF8.GetBytes(m_RowData.Substring(328, 10));
            CMWIPINF.FIELD.BONUS_QTY = Encoding.UTF8.GetBytes(m_RowData.Substring(338, 10));
            CMWIPINF.FIELD.FAB_LINE = Encoding.UTF8.GetBytes(m_RowData.Substring(348, 1));
            CMWIPINF.FIELD.CREATE_DTTM = Encoding.UTF8.GetBytes(m_RowData.Substring(414, 14));
            CMWIPINF.FIELD.CUTOFF_DATE = Encoding.UTF8.GetBytes(m_RowData.Substring(428, 8));
            CMWIPINF.FIELD.INKLESS = Encoding.UTF8.GetBytes(m_RowData.Substring(436, 1024));



            //if (m_RowData.Length > BUFFER_SIZE)
            //{
            //    CMWIPINF.FIELD.DATA_MODIFICATION_DATETIME = Encoding.UTF8.GetBytes(m_RowData.Substring(240, 14));
            //    CMWIPINF.FIELD.RETURN_TYPE = Encoding.UTF8.GetBytes(m_RowData.Substring(254, 2));
            //    CMWIPINF.FIELD.FAB_LINE = Encoding.UTF8.GetBytes(m_RowData.Substring(256, 2));
            //    CMWIPINF.FIELD.SALE_OPTION = Encoding.UTF8.GetBytes(m_RowData.Substring(258, 4));
            //    CMWIPINF.FIELD.INKLESS = Encoding.UTF8.GetBytes(m_RowData.Substring(262, 1024));
            //}
            //else
            //{
            //    //CMWIPINF.FIELD.DATA_MODIFICATION_DATETIME = Encoding.UTF8.GetBytes(m_RowData.Substring(240, 14));
            //    CMWIPINF.FIELD.RETURN_TYPE = Encoding.UTF8.GetBytes("");
            //    CMWIPINF.FIELD.FAB_LINE = Encoding.UTF8.GetBytes("");
            //    CMWIPINF.FIELD.SALE_OPTION = Encoding.UTF8.GetBytes("");
            //    CMWIPINF.FIELD.INKLESS = Encoding.UTF8.GetBytes(m_RowData.Substring(240, 1024));
            //}
        }

        //Create file row 4 "BZ20241005"
        string bzdate = "";
        string hhdate = "";

        private void DB_Update(FileInfo fileInfo)
        {

            bzdate = fileInfo.ToString().Substring(30,8);
            hhdate = fileInfo.ToString().Substring(30, 10);
            //var sheet = fileInfo.Name.Split('_')[0];
            //String strRawData = File.ReadAllText(fileInfo.FullName, Encoding.Default);
            //strRawData = strRawData.Replace("\n", "\r\n");


            using (StreamReader sr = new StreamReader(fileInfo.FullName, Encoding.ASCII))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                    //if ((line.Length >= 100 || line.Length == 71) && !line.Contains("PART_NO") && !line.Contains("SALES_CODE")) 
                        if (line.Length >= 1000)
                        {
                            m_RowData = line;

                            CMWIPINF_Parser();
                            CMWIPINF_DataUpdate();
                        

                        }
                    }

                   
                }
            
            

            FileInfo fileinfo = FileExtensionChange(fileInfo.FullName);
        }

        // 맵파일(하나), 재공파일(고객사)
        //하나에서 들어온 재공 파일 처리, 복사 백업, 고객사 전송
        //복사 D:\SFTP\HMV_Backup >> D:\SFTP\Backup
        //고객사 무브 : D:\SFTP\HMV_Backup >> D:\SFTP\oms
        private String FileMove(String fullPath)
        {
            String strNowFilePathIn= null;
            String strNowFilePath = null;  // P_S_BOX_PSSD_R0.DAT  Q_S_BOX_PSSD_R0.DAT
            String strNowFilePathSec = null;
            String strNowFilePathMap = null;
            String strNowFilePathBackupMap = null;
            String strNowFilePathBackup = null;
            String strNowFilePathInHana = null;

            try
            {
                FileInfo fileMove = new FileInfo(fullPath);

                String[] folderIn = null;
                String[] folderName = null;
                String[] folderNameBackupMap = null;
                String[] folderNameBackup = null;
                String[] folderNameSec = null;
                String[] folderMap = null;
                String[] folderInHana = null;
                


                //D:\\SFTP\\in
                //from SEC
                folderIn = new String[1] { FOLDER_LIST[5] };

                //D:\\SFTP\\HMV_in
                folderInHana = new String[1] { FOLDER_LIST[2] };

                //D:\\SFTP\\HMV_Backup
                folderName = new String[1] { FOLDER_LIST[1]};

                // D:\\SFTP\\Backup "Map"
                //folderNameBackupMap = new String[1] { FOLDER_LIST[3] + "\\" + "Map" };
                folderNameBackupMap = new String[1] { FOLDER_LIST[3] + "\\" + "Map" + DateTime.Now.ToString("yyyyMMdd") };

                //D:\\SFTP\\Backup 
                //Backup From hana 
                folderNameBackup = new String[1] { FOLDER_LIST[3] + "\\" + DateTime.Now.ToString("yyyyMMdd") };

                //oms
                folderNameSec = new String[1] { FOLDER_LIST[0] };

                //Map file
                //D:\\SFTP\\in\\edshs
                folderMap = new String[1] { FOLDER_LIST[4] };

                
                //FolderCteate(folderName);
                FolderCteate(folderNameBackupMap);
                FolderCteate(folderNameBackup);

                strNowFilePathIn = folderIn[0] + "\\" + fileMove.Name;
                strNowFilePath = folderName[0] + "\\" + fileMove.Name;
                strNowFilePathBackupMap = folderNameBackupMap[0] + "\\" + fileMove.Name;
                strNowFilePathBackup = folderNameBackup[0] + "\\" + fileMove.Name;
                strNowFilePathSec = folderNameSec[0] + "\\" + fileMove.Name;
                strNowFilePathMap = folderMap[0] + "\\" + fileMove.Name;
                strNowFilePathInHana = folderInHana[0] + "\\" + fileMove.Name;


                FileInfo fileExistsIn = new FileInfo(strNowFilePathIn);
                FileInfo fileExists = new FileInfo(strNowFilePath);
                FileInfo fileExistsSec = new FileInfo(strNowFilePathSec);
                FileInfo fileExistsMap = new FileInfo(strNowFilePathMap);
                FileInfo fileExistsBackupMap = new FileInfo(strNowFilePathBackupMap);
                FileInfo fileExistsBackup = new FileInfo(strNowFilePathBackup);
                FileInfo fileExistsInHana = new FileInfo(strNowFilePathInHana);

                //D:\\sftp\in
                //SEC > Hana, SEC > Backup
                // 백업후 하나 전송
                if (fullPath.Contains("SFTP\\in"))
                {

                    if (fileExistsIn.Exists)
                    {
                        int num = 0;
                        String CopyName = null;

                        String name = strNowFilePathBackup.Substring(0, strNowFilePathBackup.LastIndexOf('.'));
                        //CopyName = strNowFilePathHana.Substring(0, strNowFilePathHana.LastIndexOf('.'));

                        String ext = strNowFilePathBackup.Substring(strNowFilePathBackup.LastIndexOf('.'));
                        CopyName = strNowFilePathInHana.Substring(0, strNowFilePathInHana.LastIndexOf('.'));

                        while (true)
                        {
                            String NameChack = name + "_" + num + ext;
                            String NameCheckHana = CopyName + "_" + num + ext;

                            fileExistsBackup = new FileInfo(NameChack);
                            

                            //대상폴더가 중복확인
                            if (!fileExistsBackup.Exists)
                            {
                                if(fullPath.Contains("CMWIPINF_"))
                                {
                                    //백업폴더로 복사 먼저실행
                                    fileMove.CopyTo(NameChack);
                                    strNowFilePathIn = NameChack;


                                    //하나 전송
                                    fileMove.MoveTo(NameCheckHana);
                                    strNowFilePathInHana = NameCheckHana;
                                    break;
                                }

                                else
                                {
                                    //백업폴더로 복사 먼저실행
                                    fileMove.MoveTo(NameChack);
                                    strNowFilePath = NameChack;
                                    break;
                                    //strNowFilePathIn = NameChack;
                                }


                            }

                            num++;
                        }


                    }
                }

                //D:\\SFTP\\HMV_Backup
                //하나에서 보낸 파일이 있으면 동작
                if (fullPath.Contains("HMV_Backup"))
                {


                    if (fileExists.Exists)
                    {
                        int num = 0;
                        String CopyName = null;

                        String name = strNowFilePathBackup.Substring(0, strNowFilePathBackup.LastIndexOf('.'));
                        //CopyName = strNowFilePathHana.Substring(0, strNowFilePathHana.LastIndexOf('.'));

                        String ext = strNowFilePathBackup.Substring(strNowFilePathBackup.LastIndexOf('.'));
                        CopyName = strNowFilePathSec.Substring(0, strNowFilePathSec.LastIndexOf('.'));

                        while (true)
                        {
                            String NameChack = name + "_" + num +  ext;
                            String NameCheckSec = CopyName  +  ext;

                            fileExists = new FileInfo(NameChack);
                            fileExistsSec = new FileInfo(NameCheckSec);

                            fileExistsBackup = new FileInfo(NameChack);
                            //폴더가 빌때까지 실행.
                            if (!fileExistsBackup.Exists)
                            {

                                //백업폴더로 복사 먼저실행
                                fileMove.MoveTo(NameChack);
                                strNowFilePathBackup = NameChack;

                                //고객사 전송
                                //fileMove.MoveTo(NameCheckSec);
                                //strNowFilePathSec = NameCheckSec;
                                break;
                            }

                            num++;
                        }



                    }
                    else
                    {
                        //if (fileMove.Exists)
                        //    fileMove.MoveTo(strNowFilePath);
                    }
                }

                //Map file 처리
                // 백업후 하나 전송
                if(fullPath.Contains("edshs"))
                {

                    if (fileExistsMap.Exists)
                    {
                        int num = 0;
                        String CopyName = null;

                        String name = strNowFilePathBackupMap.Substring(0, strNowFilePathBackupMap.LastIndexOf('.'));
                        //CopyName = strNowFilePathHana.Substring(0, strNowFilePathHana.LastIndexOf('.'));

                        String ext = strNowFilePathBackupMap.Substring(strNowFilePathBackupMap.LastIndexOf('.'));
                        CopyName = strNowFilePathMap.Substring(0, strNowFilePathMap.LastIndexOf('.'));

                        while (true)
                        {
                            String NameChack = name + "_" + num + ext;
                            String NameCheckMap = CopyName + "_" + num + ext;

                            fileExists = new FileInfo(NameChack);
                            fileExistsBackupMap = new FileInfo(NameCheckMap);

                            //폴더가 빌때까지 실행.
                            if (!fileExistsBackupMap.Exists)
                            {

                                //백업폴더로 복사 먼저실행
                                fileMove.CopyTo(NameChack);
                                strNowFilePath = NameChack;

                                //string targetName = fullPath.Substring(17);

                                File.Delete(fullPath);
                                //고객사 전송
                                //fileMove.MoveTo(NameCheckMap);
                                //strNowFilePathSec = NameCheckMap;
                                break;
                            }

                            num++;
                        }


                    }
                }
                //lbMain.Invoke(new LogListboxDele(AddLog), new object[] { string.Format("{0},{1},{2}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), "파일 백업 ", strNowFilePath) });
            }
            catch (IOException e)
            {
                //lbMain.Invoke(new LogListboxDele(AddLog), new object[] { string.Format("{0},{1},{2}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), "에러 발생 ", e.Message) });
            }
            return strNowFilePath;

        }

        /*  폴더가 없을 경우 생성 합니다. */
        private void FolderCteate(string[] folderList)
        {
            for (int i = 0; i < folderList.Length; i++)
            {
                DirectoryInfo di = new DirectoryInfo(folderList[i]);
                if (di.Exists == false) di.Create();
            }
        }

        private void CMWIPINF_DataUpdate()
        {
            //var optioncode = "";
            //var workcode = "";
            //var returnType = "";
            var inkless = Encoding.Default.GetString(CMWIPINF.FIELD.INKLESS);
            //var saleoption = Encoding.Default.GetString(CMWIPINF.FIELD.SALE_OPTION).Trim();
            //string[] split = inkless.Split(new[] { '{' });

//#if false
//            for (int i = 0; i < split.Length; i++)
//                if (split[i].Split(new[] { ' ' })[0] == "NAME=TIER")
//                    optioncode = optioncode + split[i].Split(new[] { '}' })[0].Split(new[] { '=' })[2]; 
//#else
            //optioncode = optioncode + Encoding.Default.GetString(CMWIPINF.FIELD.PRODUCT_CODE).Substring(21, 1);
            //삼성 물류에서 TIER 사용을 못하는 관계로 SSD 의 펌웨어정보를 담고 있는 PRODUCT CODE 의 마지막번호를 사용함. 기존부터 적용했지만 20170427 재확인.
//#endif

            //for (int i = 0; i < split.Length; i++)
            //    if (split[i].Split(new[] { ' ' })[0] == "NAME=OPTCODE")
            //        optioncode = optioncode + split[i].Split(new[] { '}' })[0].Split(new[] { '=' })[2];

            //for (int i = 0; i < split.Length; i++)
            //    if (split[i].Split(new[] { ' ' })[0] == "NAME=FABSITE")
            //        optioncode = optioncode + split[i].Split(new[] { '}' })[0].Split(new[] { '=' })[2];

            //optioncode = optioncode + "0";

            //for (int i = 0; i < split.Length; i++)
            //    if (split[i].Split(new[] { ' ' })[0] == "NAME=WEEKCODE")
            //        workcode = split[i].Split(new[] { '}' })[0].Split(new[] { '=' })[2];


            /* 2020-01-28 삭제 
            if (Encoding.Default.GetString(CMWIPINF.FIELD.LOT_ID).Substring(0, 2) == "QQ")
            {
                saleoption = "3" + saleoption.Substring(1, 3);
            }
            */
            //for (int i = 0; i < split.Length; i++)
            //    if (split[i].Split(new[] { ' ' })[0] == "NAME=RETURN_TYPE")
            //        returnType = split[i].Split(new[] { '}' })[0].Split(new[] { '=' })[2];

            //var productcode = Encoding.Default.GetString(CMWIPINF.FIELD.PRODUCT_CODE);

            var sql = "INSERT INTO tb_ufd_input_info " +
                            "( start_tag,company_code,prod_code, run_id,  lot_id, lot_type, return_type, process_id, step_id, step_seq_no, " +
                            "step_desc, step_in_dttm, area_flag, area_id, chip_qty, wafer_qty, wafer_chip_flag, " +
                            "hold_flag, hold_code, hold_dttm, ncf_code, nca_code, nct_code, ncq_code, other, loss_qty, " +
                            "bonus_qty, fab_line, create_dttm,cutoff_date, inkless) " +
                            "VALUES ('" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.START_TAG).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.COMPANY_CODE).Trim() + "','" +
                                    //Encoding.Default.GetString(CMWIPINF.FIELD.ISSUE_DATE).Trim() + "','" +
                                    //Encoding.Default.GetString(CMWIPINF.FIELD.SLIP_NUMBER).Trim() + "','" +  /* Replace("0-0", "0-P").Replace("0-2", "0-Q") */
                                    //Encoding.Default.GetString(CMWIPINF.FIELD.PRODUCT_CODE).Trim() + "','" +
                                    //.Replace("MZEM515THALC-000MV-F01", "MZEM515THALC-000MV-P01")
                                    //.Replace("0-1", "0-V")
                                    //.Replace("0-2", "0-V")
                                    //.Replace("0-A", "0-Q")
                                    //.Replace("0-E", "0-Q")
                                    //.Replace("0-G", "0-Q")
                                    //.Replace("0-H", "0-Q")
                                    //.Replace("0-L", "0-Q")
                                    //.Replace("0-Q", "0-Q")
                                    //.Replace("0-R", "0-Q")
                                    //// .Replace("0-V", "0-Q")  1,2 -> V 로 변경코드 추가로 인해 삭제
                                    //.Replace("0-0", "0-P")
                                    //.Replace("0-B", "0-P")
                                    //.Replace("0-F", "0-V") // 2021-12-13 변경
                                    //.Replace("0-J", "0-P")
                                    //.Replace("0-P", "0-P")
                                    //.Replace("0-N", "0-P")
                                    //.Replace("0-T", "0-P")
                                    //.Replace("0-W", "0-P").Trim() + "','" +

                                    Encoding.Default.GetString(CMWIPINF.FIELD.PRODUCT_CODE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.RUN_ID).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.LOT_ID).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.LOT_TYPE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.RETURN_TYPE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.PROCESS_ID).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.STEP_ID).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.STEP_SEQ_NO).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.STEP_DESC).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.STEP_IN_DTTM).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.AREA_FLAG).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.AREA_ID).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.CHIP_QTY).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.WAFER_QTY).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.WAFER_CHIP_FLAG).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.HOLD_FLAG).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.HOLD_CODE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.HOLD_DTTM).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.NCF_CODE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.NCA_CODE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.NCT_CODE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.NCQ_CODE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.OTHER).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.LOSS_QTY).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.BONUS_QTY).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.FAB_LINE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.CREATE_DTTM).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.CUTOFF_DATE).Trim() + "','" +
                                    Encoding.Default.GetString(CMWIPINF.FIELD.INKLESS).Trim() + "')";
                                    //.Replace("HM", "VP") + "')";
                                    
                                    
                                    //saleoption + "','" +
                                    //optioncode + "','" +
                                    //Encoding.Default.GetString(CMWIPINF.FIELD.PRODUCT_CODE).Trim() + "','" +
                                    //workcode + "')";

            try { MySqlHelper.ExecuteNonQuery(_connection1, sql); } catch (Exception e) { }
            //try { MySqlHelper.ExecuteNonQuery(_connection2, sql); } catch (Exception e) { }

        }

        private void Create_CMWIPINF()
        {
            Stream fout;

            var directoryInfo = new DirectoryInfo(string.Format(@"OMS\{0}", bzdate));
            //var directoryInfo = new DirectoryInfo(string.Format(@"OMS\{0}", DateTime.Now.ToString("yyyyMMdd")));
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            String[] folderName = new String[1] { FOLDER_LIST[3] + "\\" + bzdate + "\\SENT" };
            //String[] folderName = new String[1] { FOLDER_LIST[3] + "\\" + DateTime.Now.ToString("yyyyMMdd") + "\\SENT\\CMWIPINF" };
            FolderCteate(folderName);

            var fName = String.Format(@"D:\SFTP\oms\CMWIPINF_BZ{0}.ff", hhdate);
            //var fName = String.Format(@"D:\SFTP\oms\CMWIPINF_BZ{0}.ff", DateTime.Now.ToString("yyyyMMddHH"));
            fout = new FileStream(fName, FileMode.Create, FileAccess.Write);

            var sw = new StreamWriter(fout, Encoding.Default);

            sw.WriteLine(@"CMWIPINF");
            sw.WriteLine(@"VALUEPLUSEVN");
            sw.WriteLine(@"1001");
            //sw.WriteLine(@"BZ" + DateTime.Now.ToString("yyyyMMdd"));
            sw.WriteLine(@"BZ" + bzdate);
            sw.WriteLine(@"MDH1");

            
            //Union query : Hana  + Value 
            var sql = $@"SELECT  start_tag,company_code,run_id, prod_code, lot_id, lot_type, return_type, process_id, step_id, step_seq_no, step_desc,
                 step_in_dttm,
                 area_flag, area_id, chip_qty, wafer_qty, wafer_chip_flag, hold_flag, hold_code, hold_dttm, ncf_code, nca_code, nct_code, ncq_code, other, loss_qty, bonus_qty, fab_line, create_dttm,cutoff_date, inkless
                 FROM tb_ufd_input_info
                union
                select   i.start_tag,i.company_code,i.run_id, w.prod_code, l.lotid as lot_id, l.lot_type, '**',p.oms_step, p.oms_step, p.oms_step, p.oms_description, 
                date_format(l.created_on,'%Y%m%d%H%m%s'),  
                'ASSY', i.area_id, w.CHIP_QTY, '', '',
                 l.flag, '', '', i.ncf_code, i.nca_code, i.nct_code, i.ncq_code, i.other, i.loss_qty, i.bonus_qty, i.fab_line, i.create_dttm,i.cutoff_date, i.inkless
                 from tb_ufd_lotid l, tb_ufd_wafer_info w, tb_ufd_input_info i,tb_ufd_process p where l.comp_id=w.id and l.step_id=p.id and w.WAFER_LOT=i.run_id and i.area_id<>'HMVA'  ";

            //var sql =
            //    $@"SELECT l.lotid, l.lot_type, e.prod_code, '', IF(p.id = 28, (SELECT COUNT(*) FROM tb_mes_dat_setinfo WHERE lot_id = l.id AND status_code != 'VOU'), 
            //    l.start_lot_qty), w.fab_line, '', l.return_type, p.step, p.process_name, REPLACE(w.lot_id, 'QSI-SMT_', ''), SUBSTR(l.comp_k9_opt, 1, 1), l.week, UPPER(l.status), l.hold_code 
            //    FROM tb_mes_lotid l, tb_mes_std_espec e, tb_mes_process p, tb_in_wafer_info w 
            //    WHERE l.espec_id = e.id AND l.next_step_id = p.id AND l.comp_k9_id = w.id AND lot_flag = 'R' AND marge_lot is null AND l.step_id is not null 
            //    AND SUBSTR(p.step, 1, 1) != 'F' AND p.step != 'M100' AND p.step != 'M111' AND p.step != 'M119' AND l.updated_at < DATE_FORMAT(now(), '%Y-%m-%d %H:00:00') 
            //    ORDER BY e.prod_code, l.week, l.lotid ";

            var dataTable = MySqlHelper.ExecuteDataset(_connection1, sql).Tables[0];

            foreach (DataRow result in dataTable.Rows)
            {
                //var START_TAG = "LS1";
                //var OEM_CODE = "BZ";
                var START_TAG = result[0].ToString();
                var OEM_CODE = result[1].ToString();
                var PROD_CODE = result[3].ToString();
                var RUN_ID = result[2].ToString();
                var LOT_ID = result[4].ToString();
                var LOT_TYPE = result[5].ToString();
                var RETURN_TYPE = result[6].ToString();//"RT";  "RM"
                var PROCESS_ID = result[7].ToString();   // "ZTST";//"BMDL";
                //var STEP_ID = (result[8].ToString() == "BFMS") ? "M170" : result[8].ToString();  //result[8].ToString();
                //var STEP_SEQ_NO = (result[8].ToString() == "BFMS") ? "M170" : result[8].ToString();  //result[8].ToString();
                var STEP_ID = result[8].ToString();
                var STEP_SEQ_NO = result[9].ToString();
                var STEP_DESC = result[10].ToString();
                var STEP_IN_DTTM = result[11].ToString();
                var AREA_FLAG = result[12].ToString();
                var AREA_ID = result[13].ToString();
                var CHIP_QTY = result[14].ToString();
                var WAFER_QTY = "";
                var WAFER_CHIP_FLAG = "";  // WAFER-W, CHIP-C
                var HOLD_FLAG = "";        // Hold - H, Run -R, Wait -W

                //var status = "";
                //switch (result[13].ToString())
                //{
                //    case "TERMINATED": HOLD_FLAG = "W"; status = "WAIT"; break;
                //    case "ACTIVE": HOLD_FLAG = "R"; status = "RUN"; break;
                //    case "HOLD": HOLD_FLAG = "H"; status = "HOLD"; break;
                //}

                var HOLD_CODE = "";
                var HOLD_DTTM = "";
                var NCF_CODE = result[20].ToString();
                var NCA_CODE = result[21].ToString(); 
                var NCT_CODE = result[22].ToString();
                var NCQ_CODE = result[23].ToString();
                var OTHER = result[24].ToString();
                var LOSS_QTY = result[25].ToString();
                var BONUS_QTY = result[26].ToString();
                var FAB_LINE = result[27].ToString();
                var WAFER_ID = "";
                var CREATE_DTTM = DateTime.Now.ToString("yyyyMMddHHmmss");
                var CUTOFF_YMD = DateTime.Now.ToString("yyyyMMdd");

                var inkless_details = " " +
                //"{NAME=RETURN_TYPE VALUE=} " +
                //"{NAME=NCFCODE VALUE=} " +
                //"{NAME=WEEKCODE VALUE=" + result[12].ToString() + "} " +
                //"{NAME=WORDER VALUE=} " +
                "{NAME=CS_FLAG VALUE=} " +
                "{NAME=ERINFO VALUE=} " +
                "{NAME=PEGEVT VALUE=} " +
                "{NAME=PURPOSETYPE VALUE=} " +
                "{NAME=WORDER VALUE=" +
                "{NAME=NCFCODE VALUE=} " +
                "{NAME=ASYSITE VALUE=} " + result[13].ToString().Substring(0,2) + "} " +
                "{NAME=START_TIME VALUE=} " + result[11].ToString() + "} " +
                "{NAME=NCHCODE VALUE=} " +
                "{NAME=NCBCODE VALUE=} " + "}";
                //"{NAME=EVALCODE VALUE=} " +
                //"{NAME=PCTESTCNT VALUE=} " +
                //"{NAME=OPTCODE VALUE=} " +
                //"{NAME=ASSYLINE VALUE=} " +
                //"{NAME=PCTESTRESULT VALUE=} " +
                //"{NAME=NEWBULID VALUE=} " +
                //"{NAME=CHGREQNO VALUE=} " +
                //"{NAME=PCTESTKIND VALUE=} " +
                //"{NAME=RUNID VALUE=} " +
                //"{NAME=CASE_CCS_FLAG VALUE=} " +
                //"{NAME=NONPOFLAG VALUE=} " +
                //"{NAME=RECEIVESTEP VALUE=} " +
                //"{NAME=PCB_ARRAY_BASE_QTY VALUE=} " +
                //"{NAME=PCB_ARRAY_QTY VALUE=} " +
                //"{NAME=FABSITE VALUE=" + FAB_LINE + "} " +
                //"{NAME=STATUS VALUE=" + status + "} " +  // RUN, WAIT, HELD, REPAIR
                //"{NAME=TIER VALUE=" + result[11].ToString() + "} " +
                //"{NAME=OPTION VALUE=} " +
                //"{NAME=EVAL VALUE=} " +
                //"{NAME=PROC_ID VALUE=} " +
                //"{NAME=NCB_CODE VALUE=} " +
                //"{NAME=NCE_CODE VALUE=} " +
                //"{NAME=NCH_CODE VALUE=} " +
                //"{NAME=NCM_CODE VALUE=} " +
                //"{NAME=NCP_CODE VALUE=} " +
                //"{NAME=NCR_CODE VALUE=} " +
                //"{NAME=COMMENT VALUE=} " +
                //"{NAME=PURPOSETYPE VALUE=} " +
                //"{NAME=MORDER VALUE=} " +
                //"{NAME=GC_CODE VALUE=} " +
                //"{NAME=WEEKCODE VALUE=" + result[12].ToString() + "}";

                var INKLESS = inkless_details;

                if (INKLESS.Length > 1024)
                    INKLESS = INKLESS.Substring(0, 1024);

                String recode = String.Empty;
                recode += START_TAG.PadRight(3);
                recode += OEM_CODE.PadRight(2);
                recode += PROD_CODE.PadRight(30);
                recode += RUN_ID.PadRight(15);
                recode += LOT_ID.PadRight(15);
                recode += LOT_TYPE.PadRight(2);
                recode += RETURN_TYPE.PadRight(2);
                recode += PROCESS_ID.PadRight(15);
                recode += STEP_ID.PadRight(30);
                recode += STEP_SEQ_NO.PadRight(16);
                recode += STEP_DESC.PadRight(24);
                recode += STEP_IN_DTTM.PadRight(14);
                recode += AREA_FLAG.PadRight(4);
                recode += AREA_ID.PadRight(4);
                recode += CHIP_QTY.PadLeft(10);
                recode += WAFER_QTY.PadLeft(10);
                recode += WAFER_CHIP_FLAG.PadRight(1);
                recode += HOLD_FLAG.PadRight(2);
                recode += HOLD_CODE.PadRight(10);
                recode += HOLD_DTTM.PadRight(14);
                recode += NCF_CODE.PadRight(20);
                recode += NCA_CODE.PadRight(20);
                recode += NCT_CODE.PadRight(20);
                recode += NCQ_CODE.PadRight(20);
                recode += OTHER.PadRight(20);
                recode += LOSS_QTY.PadLeft(10);
                recode += BONUS_QTY.PadLeft(10);
                recode += FAB_LINE.PadRight(1);
                recode += WAFER_ID.PadRight(65);
                recode += CREATE_DTTM.PadRight(14);
                recode += CUTOFF_YMD.PadRight(8);
                recode += INKLESS.PadRight(1024);

                sw.WriteLine(recode);
            }

            sw.WriteLine(@"MDT1");

            sw.Close();
            fout.Close();

            File.Copy(fName, fName.Replace(@"D:\SFTP\oms", folderName[0]));
        }
    }
}


