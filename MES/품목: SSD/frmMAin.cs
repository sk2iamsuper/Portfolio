using mes_.Properties;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Net.Mail;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using mes_._20._step_in_out;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Management;

namespace mes_
{
    public partial class frmMain : Form
    {
        public static string dbsite = string.Empty;
        private string informTime = "";
        private string version;
        public static int userID = 1;
        public static string authority = "";
        public static string bookmark = "";
        public static string userName = "";
        public static string user_ID = "";
        public static string user_PW = "";
        public static string department = "";
        public static string language = "";
        public static string login_language = "";
        public static readonly SoundPlayer SoundPlayerFail = new SoundPlayer { Stream = Resources.buzz };
        public static readonly SoundPlayer SoundPlayerPass = new SoundPlayer { Stream = Resources.click };
        public static readonly SoundPlayer SoundPlayerEffect5 = new SoundPlayer { Stream = Resources.effect5 };
        private MySqlConnection _connection;
        public static string check_update = "";

        List<string> _lstTreeState;
        public static string lineName = "";

        public static Dictionary<string, string> dictionary = new Dictionary<string, string>();

        public frmMain(string input)
        {
            InitializeComponent();
            DoubleBufferedHelper.SetDoubleBufferedParent(this);

            panel1.Dock = DockStyle.Fill;

            check_update = input;
        }

        private List<string> GetMainBoardSerialNumberList()
        {
            List<string> list = new List<string>();

            string query = "SELECT * FROM Win32_BaseBoard";

            ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher(query);

            foreach (ManagementObject managementObject in managementObjectSearcher.Get())
            {
                list.Add(managementObject.GetPropertyValue("SerialNumber").ToString());
            }

            return list;
        }

        private void frmLogin_Load(object sender, EventArgs e)
        {
            cbDBsite.Text = Settings.Default.conn_site;
            txtID.Text = Settings.Default.user_name;
            cbLanguage.Text = Settings.Default.language;
            version = GetLinkerTime(Assembly.GetExecutingAssembly()).ToString("yyyy.MM.dd HH:mm:ss"); // Properties.Settings.Default.MES_VERSION;
            txtVersion.Text = "[Build Version] " + version;

            panel2.BackgroundImage = Properties.Resources.MES_3;
            //panel2.BackgroundImage = Properties.Resources.MES_3;

            switch (txtID.Text)
            {
                case "현황판": txtPW.Text = "1234"; break;
                case "AUTO": txtPW.Text = "2"; break;
                case "AUTOCASE": txtPW.Text = "1234"; break;
            }

            informTime = DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        private void Initialize_RestoreTreeview()
        {
            _lstTreeState = new List<string>();

            try
            {
                string contents = Properties.Settings.Default.treenode_path;
                if (contents != string.Empty)
                {
                    foreach (string item in contents.Split(','))
                    {
                        _lstTreeState.Add(item);
                    }
                }

                tvMain.CollapseAll();

                if (_lstTreeState.Count > 0)
                {
                    RestoreTreeState(tvMain.Nodes, _lstTreeState);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void sendEmail(string _message, string email)
        {
            MailMessage message = new MailMessage();
            message.To.Add(email);

            message.From = new MailAddress("elitebug@naver.com", "mes", System.Text.Encoding.UTF8);
            MailAddress bcc = new MailAddress("mes@valueplus.co.kr");//참조 메일계정
            message.Bcc.Add(bcc);

            MailAddress nguyen = new MailAddress("cuong.nguyen@valueplus.vn");//베트남 전산담당자
            if (dbsite.Contains("SPV"))
                message.Bcc.Add(nguyen);

            if (_message.Contains("DELAY"))
                message.Subject = $"[{dbsite}] OMS Info.";
            else if (_message.Contains("CALL MESSAGE"))
                message.Subject = $"[{dbsite}] MES Call.";
            else if (_message.Contains("WATCH-CON"))
                message.Subject = $"[{dbsite}] WATCH-CON Info.";
            else
                message.Subject = $"[{dbsite}] MES Info.";

            message.SubjectEncoding = UTF8Encoding.UTF8;
            message.Body = _message;
            message.BodyEncoding = UTF8Encoding.UTF8;
            message.IsBodyHtml = true; //메일내용이 HTML형식임
            message.Priority = MailPriority.High; //중요도 높음
            message.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure; //메일 배달 실패시 알림
            //Attachment attFile = new Attachment(E"d\\image1.jpg");//첨부파일

            SmtpClient client = new SmtpClient();
            client.Host = "smtp.naver.com";//"smtp.worksmobile.com"; //SMTP(발송)서버 도메인
            client.Port = 587; //25, SMTP서버 포트
            client.EnableSsl = true; //SSL 사용
            client.Timeout = 10000;
            client.DeliveryMethod = SmtpDeliveryMethod.Network;
            client.UseDefaultCredentials = false;
            client.Credentials = new System.Net.NetworkCredential("elitebug@naver.com", "hantel81");//보내는 사람 메일 서버접속계정, 암호, Anonymous이용시 생략
            client.Send(message);

            message.Dispose();
        }

        public static string menuname(string msgcode, string krmsg)
        {
            if (msgcode.Substring(0, 1) == "E")
                return dictionary[msgcode];

            //return msgcode + "_" + dictionary[msgcode];

            return dictionary[msgcode] + " .[" + msgcode + "]";
        }


        private void treeview_spk_1()
        {
            TreeNode productsupport = new TreeNode(menuname("E0001", "생산지원"));
            productsupport.Nodes.Add(menuname("T001", "반출입신청서"));
            productsupport.Nodes.Add(menuname("T002", "반출입관리"));
            productsupport.Nodes.Add(menuname("T003", "일괄승인"));
            productsupport.Nodes.Add(menuname("T005", "방문객"));
            productsupport.Nodes.Add(menuname("T007", "태블릿모드"));
            productsupport.Nodes.Add(menuname("T006", "원가절감OT양식"));
            TreeNode auto = new TreeNode(menuname("E0024", "AUTO"));
            auto.Nodes.Add(menuname("AT01", "로그취합"));
            //auto.Nodes.Add(menuname("AT03", "WATCHCON"));
            auto.Nodes.Add(menuname("AT02", "라벨DOE"));
            auto.Nodes.Add(menuname("AT04", "ZEBRA FINDER"));
            auto.Nodes.Add(menuname("AT05", "LOT CARD"));
            auto.Nodes.Add(menuname("AT06", "PARTHOLD"));
            auto.Nodes.Add(menuname("AT07", "TCP"));

            TreeNode warehouse = new TreeNode(menuname("E0002", "자재관리"));  // 자재관리
            TreeNode warehouseSearch = new TreeNode(menuname("E0005", "조회"));
            warehouseSearch.Nodes.Add(menuname("M001", "COMP 재공"));
            warehouseSearch.Nodes.Add(menuname("M002", "COMP 이력"));
            warehouseSearch.Nodes.Add(menuname("M003", "자재 이력"));
            //warehouseSearch.Nodes.Add(menuname("M004", "PCB 추적"));



            warehouseSearch.Nodes.Add(menuname("M005", "COMP출고현황"));
            warehouseSearch.Nodes.Add(menuname("S004", "E-SPEC"));
            warehouseSearch.Nodes.Add(menuname("S001", "BOM 리스트"));
            warehouseSearch.Nodes.Add(menuname("S002", "모듈 BOM"));
            warehouseSearch.Nodes.Add(menuname("S003", "이원화 BOM"));
            warehouseSearch.Nodes.Add(menuname("S025", "ERP BOM"));
            warehouseSearch.Nodes.Add(menuname("M006", "IC-CONT.출고현황"));
            warehouseSearch.Nodes.Add(menuname("M007", "창고별재고"));
            warehouseSearch.Nodes.Add(menuname("M008", "자재유수명"));
            warehouseSearch.Nodes.Add(menuname("M009", "자재이동보기"));
            warehouseSearch.Nodes.Add(menuname("PP18", "출고통제"));
            warehouse.Nodes.Add(warehouseSearch);
            TreeNode warehouseIn = new TreeNode(menuname("E0003", "입고"));
            warehouseIn.Nodes.Add(menuname("MA01", "신규입고"));
            warehouseIn.Nodes.Add(menuname("MA02", "기구물입고"));
            warehouseIn.Nodes.Add(menuname("MA03", "COMP 입고"));
            warehouseIn.Nodes.Add(menuname("MA04", "IC-CONT. 입고"));
            //warehouseIn.Nodes.Add(menuname("MA05", "양품반납"));
            //warehouseIn.Nodes.Add(menuname("MA06", "불량반납"));
            //warehouseIn.Nodes.Add(menuname("MA07", "차용반납"));
            //warehouseIn.Nodes.Add(menuname("MA08", "부자재입고"));
            warehouseIn.Nodes.Add(menuname("MA09", "반품"));
            warehouseIn.Nodes.Add(menuname("MB13", "릴 라벨 확인WH"));
            warehouse.Nodes.Add(warehouseIn);
            TreeNode warehouseOut = new TreeNode(menuname("E0004", "출고"));
            warehouseOut.Nodes.Add(menuname("MB01", "COMP 출고예약"));
            warehouseOut.Nodes.Add(menuname("MB02", "COMP SPLIT/MERGE"));
            warehouseOut.Nodes.Add(menuname("MB03", "COMP Loss"));
            warehouseOut.Nodes.Add(menuname("MB04", "COMP Adjust"));
            warehouseOut.Nodes.Add(menuname("MB05", "자재 SPLIT/MERGE"));
            warehouseOut.Nodes.Add(menuname("MB06", "자재요청"));
            warehouseOut.Nodes.Add(menuname("MB16", "ERP 자재요청"));
            warehouseOut.Nodes.Add(menuname("MB07", "자재출고"));
            warehouseOut.Nodes.Add(menuname("MB08", "릴 라벨 확인MH"));
            warehouseOut.Nodes.Add(menuname("MB12", "릴 변경"));
            warehouseOut.Nodes.Add(menuname("MB09", "기구물출고"));
            warehouseOut.Nodes.Add(menuname("MB10", "MH 이동"));
            warehouseOut.Nodes.Add(menuname("MB14", "SPV 이동"));
            warehouseOut.Nodes.Add(menuname("PP15", "WH(M)-OUT"));
            warehouseOut.Nodes.Add(menuname("MB15", "REPAIR 출고"));
            //warehouseOut.Nodes.Add(menuname("MB11", "외부차용"));
            warehouse.Nodes.Add(warehouseOut);
            TreeNode warehouseRecode = new TreeNode(menuname("E0006", "등록"));
            warehouseRecode.Nodes.Add(menuname("MC01", "수입검사의뢰서"));
            //warehouseRecode.Nodes.Add("제습함보관");
            warehouseRecode.Nodes.Add(menuname("MC02", "베이킹"));
            warehouseRecode.Nodes.Add(menuname("MC03", "COMP 보관함"));
            warehouseRecode.Nodes.Add(menuname("MC04", "ROM F/W"));
            warehouseRecode.Nodes.Add(menuname("Q008", "입고정보등록"));

            warehouse.Nodes.Add(warehouseRecode);


            TreeNode product = new TreeNode(menuname("E0007", "생산관리")); // 생산관리
            TreeNode SmtProductSearch = new TreeNode(menuname("E0005", "조회"));
            SmtProductSearch.Nodes.Add(menuname("P001", "일단위계획(MO)"));
            SmtProductSearch.Nodes.Add(menuname("P002", "SSD 재공"));
            SmtProductSearch.Nodes.Add(menuname("M001", "COMP 재공"));
            SmtProductSearch.Nodes.Add(menuname("P003", "MOUNTER 현황"));
            SmtProductSearch.Nodes.Add(menuname("P004", "가동률"));
            SmtProductSearch.Nodes.Add(menuname("P016", "실적현황"));
            //SmtProductSearch.Nodes.Add(menuname("P005", "작업자별현황"));
            //SmtProductSearch.Nodes.Add(menuname("P006", "실시간현황"));
            SmtProductSearch.Nodes.Add(menuname("P007", "완제품조회"));
            SmtProductSearch.Nodes.Add(menuname("P008", "출하품조회"));
            SmtProductSearch.Nodes.Add(menuname("A021", "파트체인지조회"));
            SmtProductSearch.Nodes.Add(menuname("P010", "공정별수율"));
            SmtProductSearch.Nodes.Add(menuname("P011", "설비별수율"));
            SmtProductSearch.Nodes.Add(menuname("P012", "장비현황"));
            SmtProductSearch.Nodes.Add(menuname("P013", "SMT 라인별현황"));
            SmtProductSearch.Nodes.Add(menuname("P014", "공지사항"));
            SmtProductSearch.Nodes.Add(menuname("P015", "ERP파일"));
            SmtProductSearch.Nodes.Add(menuname("Q001", "3S/8D REPORT"));

            product.Nodes.Add(SmtProductSearch);

            TreeNode scoreboard = new TreeNode(menuname("E0008", "현황판"));
            scoreboard.Nodes.Add(menuname("PA01", "SMT현황판"));
            scoreboard.Nodes.Add(menuname("PA02", "iArts현황판"));
            scoreboard.Nodes.Add(menuname("PA03", "조립현황판"));
            scoreboard.Nodes.Add(menuname("PA04", "라우터현황판"));
            scoreboard.Nodes.Add(menuname("PA05", "포장현황판"));
            scoreboard.Nodes.Add(menuname("PA06", "사무실현황판"));
            scoreboard.Nodes.Add(menuname("PA07", "온습도현황판"));
            scoreboard.Nodes.Add(menuname("PA08", "온습도(실시간)"));
            product.Nodes.Add(scoreboard);


            TreeNode smtproduct = new TreeNode(menuname("E0009", "작업지시서"));
            smtproduct.Nodes.Add(menuname("PP01", "[주간]계획등록"));
            smtproduct.Nodes.Add(menuname("PP02", "[일일]계획등록"));
            smtproduct.Nodes.Add(menuname("PP03", "[일일]SMT 계획"));
            smtproduct.Nodes.Add(menuname("PP04", "[일일]현황판계획"));
            smtproduct.Nodes.Add(menuname("PP05", "[일일]출하계획"));
            smtproduct.Nodes.Add(menuname("PP19", "[주간]ERP계획"));
            smtproduct.Nodes.Add(menuname("PP06", "[공지]인폼등록"));
            smtproduct.Nodes.Add(menuname("PP07", "[SEC]IC-CONT.요청"));
            smtproduct.Nodes.Add(menuname("PP08", "자재출고요청"));
            smtproduct.Nodes.Add(menuname("PP17", "현장자재요청"));
            smtproduct.Nodes.Add(menuname("PP09", "반제품SEC출고"));
            smtproduct.Nodes.Add(menuname("PP10", "반제품입고"));
            smtproduct.Nodes.Add(menuname("PP11", "SEC재작업"));
            smtproduct.Nodes.Add(menuname("PP12", "SMT계획변경"));
            smtproduct.Nodes.Add(menuname("PP13", "수불데이터"));
            smtproduct.Nodes.Add(menuname("PP14", "재경팀결산"));
            product.Nodes.Add(smtproduct);

            TreeNode productmanager = new TreeNode(menuname("E0010", "관리"));
            productmanager.Nodes.Add(menuname("T004", "작업자인증"));
            product.Nodes.Add(productmanager);

            TreeNode process = new TreeNode(menuname("E0012", "공정관리"));
            TreeNode process_search = new TreeNode(menuname("E0005", "조회"));
            process_search.Nodes.Add(menuname("P002", "SSD 재공"));
            process_search.Nodes.Add(menuname("P006", "실시간현황"));
            process_search.Nodes.Add(menuname("A006", "진도현황"));
            process_search.Nodes.Add(menuname("QM03", "HOLD MEMO"));
            process_search.Nodes.Add(menuname("A004", "STEP MOVE"));
            process_search.Nodes.Add(menuname("A001", "LOT추적"));
            process_search.Nodes.Add(menuname("A002", "SET추적"));
            process_search.Nodes.Add(menuname("A019", "SCRAPCODE 등록"));
            process_search.Nodes.Add(menuname("A020", "PDA 이력"));
            process_search.Nodes.Add(menuname("A021", "파트체인지이력"));
            process_search.Nodes.Add(menuname("S001", "BOM 리스트"));
            process_search.Nodes.Add(menuname("PP16", "WH(P)-IN"));
            process.Nodes.Add(process_search);

            TreeNode process_m015 = new TreeNode(menuname("E0013", "점검"));
            process_m015.Nodes.Add(menuname("S006", "ASSY 도면보기"));
            process_m015.Nodes.Add(menuname("A055", "모델변경 SMT"));
            process_m015.Nodes.Add(menuname("A056", "SCREEN PRINTER NEW"));
            process_m015.Nodes.Add(menuname("A058", "PM 계획"));
            process_m015.Nodes.Add(menuname("QC08", "공정검사_SPCN (생산)"));

            TreeNode Sparepart = new TreeNode(menuname("E0029", "SPARE PART"));
            Sparepart.Nodes.Add(menuname("A059", "SPARE PART 입고"));
            Sparepart.Nodes.Add(menuname("A060", "SPARE PART 출고"));
            process_m015.Nodes.Add(Sparepart);
            TreeNode SmtProductMetal = new TreeNode(menuname("E0030", "STENCIL"));
            SmtProductMetal.Nodes.Add(menuname("A063", "STENCIL TENSION 측정"));
            SmtProductMetal.Nodes.Add(menuname("A064", "STENCIL 육안검사"));
            process_m015.Nodes.Add(SmtProductMetal);
            TreeNode SmtSubPortTable = new TreeNode(menuname("E0031", "S-TABLE"));
            SmtSubPortTable.Nodes.Add(menuname("A065", "S-TABLE 평탄도 측정"));
            SmtSubPortTable.Nodes.Add(menuname("A066", "S-TABLE 육안검사"));
            process_m015.Nodes.Add(SmtSubPortTable);
            TreeNode SmtProductSqueegee = new TreeNode(menuname("E0048", "SQUEEGEE"));
            SmtProductSqueegee.Nodes.Add(menuname("A057", "SQUEEGEE 검사"));
            SmtProductSqueegee.Nodes.Add(menuname("A075", "BLADE 교체"));
            process_m015.Nodes.Add(SmtProductSqueegee);
            TreeNode SmtProductSolder = new TreeNode(menuname("E0032", "SOLDER PASTE"));
            SmtProductSolder.Nodes.Add(menuname("A067", "SOLDER 사용등록"));
            SmtProductSolder.Nodes.Add(menuname("A068", "SOLDER 교반등록"));
            process_m015.Nodes.Add(SmtProductSolder);

            TreeNode SmtProductMagazine = new TreeNode(menuname("E0049", "OTHER"));
            SmtProductMagazine.Nodes.Add(menuname("A077", "MAGAZINE 검사"));
            SmtProductMagazine.Nodes.Add(menuname("A078", "NOZZLE 검사"));
            SmtProductMagazine.Nodes.Add(menuname("A079", "CLEANER 검사"));
            process_m015.Nodes.Add(SmtProductMagazine);

            process_m015.Nodes.Add(menuname("S019", "FEEDER 신규등록"));
            process_m015.Nodes.Add(menuname("A069", "PROFILE"));
            process_m015.Nodes.Add(menuname("A070", "PCB 세척"));
            process_m015.Nodes.Add(menuname("A076", "M/S AOI 등록"));
            process_m015.Nodes.Add(menuname("A071", "LOT 누락검사"));
            process_m015.Nodes.Add(menuname("A072", "LOT 세트검사"));
            process_m015.Nodes.Add(menuname("A073", "점검세트등록"));
            process_m015.Nodes.Add(menuname("A074", "파손세트등록"));
            process_m015.Nodes.Add(menuname("A082", "CCS SHEET (SMT)"));
            process_m015.Nodes.Add(menuname("A083", "SMT,AOI Monitoring"));
            process.Nodes.Add(process_m015);

            TreeNode assyproduct = new TreeNode(menuname("E0014", "공정"));
            assyproduct.Nodes.Add(menuname("A022", "M010 (SMT 1차)"));
            assyproduct.Nodes.Add(menuname("A023", "M015 (SMT 2차)"));
            assyproduct.Nodes.Add(menuname("A024", "M031 (iARTs)"));
            assyproduct.Nodes.Add(menuname("A025", " └ PCB 분리"));
            assyproduct.Nodes.Add(menuname("A026", "M033 (Router)"));
            assyproduct.Nodes.Add(menuname("A027", " └ M033 불량등록"));
            assyproduct.Nodes.Add(menuname("A028", " └ M033 불량분리"));
            assyproduct.Nodes.Add(menuname("A029", "PART CHANGE"));
            assyproduct.Nodes.Add(menuname("A030", "M100 (Initial) [개발중]"));
            assyproduct.Nodes.Add(menuname("A031", "M100 로그 업로더"));
            assyproduct.Nodes.Add(menuname("A032", "M111 (Aging) [개발중]"));
            assyproduct.Nodes.Add(menuname("A033", "M120 (Case/Label)"));
            assyproduct.Nodes.Add(menuname("A034", " └ BRAND 라벨"));
            assyproduct.Nodes.Add(menuname("A035", "M121 (Label)"));
            assyproduct.Nodes.Add(menuname("A036", " └ SUB ASSY"));
            assyproduct.Nodes.Add(menuname("A080", " └ LEAK 테스트"));
            assyproduct.Nodes.Add(menuname("A084", "M125 (LED Inspection)"));
            assyproduct.Nodes.Add(menuname("A037", "M130 (Interface)"));
            assyproduct.Nodes.Add(menuname("A039", " └ MOQ 불량분리"));
            assyproduct.Nodes.Add(menuname("A038", "M160 (FVI)"));
            assyproduct.Nodes.Add(menuname("A040", " └ MOQ 합치기"));
            assyproduct.Nodes.Add(menuname("A041", " └ MOQ 나누기"));
            assyproduct.Nodes.Add(menuname("A042", " └ MOQ 일부이동"));
            assyproduct.Nodes.Add(menuname("A043", " └ LOT-CHANGE"));
            assyproduct.Nodes.Add(menuname("A044", "M165 (QA-GATE)"));
            assyproduct.Nodes.Add(menuname("A045", "M170 (Packing)"));
            assyproduct.Nodes.Add(menuname("A046", " └ 유통 라벨"));
            assyproduct.Nodes.Add(menuname("A047", " └ MANUAL/TRAY 삽입"));
            assyproduct.Nodes.Add(menuname("A048", " └ T/K 용량 확인"));
            assyproduct.Nodes.Add(menuname("A049", " └ 소/대 박스포장"));
            assyproduct.Nodes.Add(menuname("A081", " └ OEM 박스포장"));
            assyproduct.Nodes.Add(menuname("A051", " └ 중량선별기"));
            assyproduct.Nodes.Add(menuname("A052", " └ BOX STACKER"));
            assyproduct.Nodes.Add(menuname("A053", "인파렛트"));
            assyproduct.Nodes.Add(menuname("A054", "제품출하"));
            process.Nodes.Add(assyproduct);

            TreeNode SmtProductRework = new TreeNode(menuname("E0015", "불량"));
            SmtProductRework.Nodes.Add(menuname("A007", "REWORK 조회"));
            SmtProductRework.Nodes.Add(menuname("A008", "불량라벨발행"));
            SmtProductRework.Nodes.Add(menuname("A009", "M100 등록"));
            SmtProductRework.Nodes.Add(menuname("A010", "M100 분리만"));
            SmtProductRework.Nodes.Add(menuname("A011", "SMT REWORK"));
            SmtProductRework.Nodes.Add(menuname("A086", "분리(STEP 유지)"));
            SmtProductRework.Nodes.Add(menuname("A085", "AQL Process"));
            SmtProductRework.Nodes.Add(menuname("A012", "M111 양품이동"));
            SmtProductRework.Nodes.Add(menuname("A013", "P 합치기"));
            SmtProductRework.Nodes.Add(menuname("A014", "매거진매칭"));
            SmtProductRework.Nodes.Add(menuname("A015", "현장출고"));
            SmtProductRework.Nodes.Add(menuname("A016", "SCRAP창고"));
            SmtProductRework.Nodes.Add(menuname("A017", "PROBING 등록"));
            SmtProductRework.Nodes.Add(menuname("A018", "브랜드라벨(재)"));
            process.Nodes.Add(SmtProductRework);

            TreeNode quality = new TreeNode(menuname("E0016", "품질관리"));
            TreeNode qualitySearch = new TreeNode(menuname("E0005", "조회"));
            qualitySearch.Nodes.Add(menuname("P002", "SSD 재공"));
            qualitySearch.Nodes.Add(menuname("A001", "LOT추적"));
            qualitySearch.Nodes.Add(menuname("A002", "SET추적"));
            qualitySearch.Nodes.Add(menuname("Q005", "CCS"));
            qualitySearch.Nodes.Add(menuname("S001", "BOM 리스트"));
            qualitySearch.Nodes.Add(menuname("S002", "모듈 BOM"));
            qualitySearch.Nodes.Add(menuname("S025", "ERP BOM"));
            qualitySearch.Nodes.Add(menuname("S005", "QASH"));
            qualitySearch.Nodes.Add(menuname("Q007", "SMT CPK"));
            qualitySearch.Nodes.Add(menuname("Q009", "SMT Nonadjusted Ratio"));
            qualitySearch.Nodes.Add(menuname("Q006", "자동체결기로그"));
            qualitySearch.Nodes.Add(menuname("Q001", "3S/8D REPORT"));
            quality.Nodes.Add(qualitySearch);

            TreeNode outquality = new TreeNode(menuname("E0017", "QC"));
            outquality.Nodes.Add(menuname("QC01", "수입검사"));
            outquality.Nodes.Add(menuname("QC02", "공정검사_PQC"));
            outquality.Nodes.Add(menuname("QC03", "공정검사_SPCN (품질)"));
            outquality.Nodes.Add(menuname("QC04", "공정검사_iARTs"));
            outquality.Nodes.Add(menuname("QC05", "M165 (QA-GATE)"));
            outquality.Nodes.Add(menuname("Q002", "M165 리포트"));
            outquality.Nodes.Add(menuname("Q003", "M165 수정"));
            outquality.Nodes.Add(menuname("QC06", "OBA 판정"));
            outquality.Nodes.Add(menuname("Q004", "OBA 리포트"));
            outquality.Nodes.Add(menuname("QM01", "QC CONTROL"));
            outquality.Nodes.Add(menuname("QM02", "HOLD LOT 승인"));
            outquality.Nodes.Add(menuname("QC07", "AQL 등록"));
            outquality.Nodes.Add(menuname("Q008", "입고정보등록"));
            outquality.Nodes.Add(menuname("QC09", "BOXLABEL CHECK"));
            outquality.Nodes.Add(menuname("QC10", "GageRaR"));
            outquality.Nodes.Add(menuname("QC11", "ESPEC 승인"));
            outquality.Nodes.Add(menuname("QC12", "반제품발송이력"));
            quality.Nodes.Add(outquality);

            TreeNode espec = new TreeNode(menuname("E0018", "기준정보"));
            TreeNode especAdd = new TreeNode(menuname("E0006", "등록"));
            especAdd.Nodes.Add(menuname("S007", "EVENT 등록"));
            especAdd.Nodes.Add(menuname("S008", "E-SPEC 수정"));
            especAdd.Nodes.Add(menuname("T004", "작업자인증"));
            espec.Nodes.Add(especAdd);

            TreeNode especSearch = new TreeNode(menuname("E0005", "조회"));
            especSearch.Nodes.Add(menuname("S004", "E-SPEC"));
            especSearch.Nodes.Add(menuname("S009", "표준문서"));
            especSearch.Nodes.Add(menuname("S010", "무게분포"));
            especSearch.Nodes.Add(menuname("S011", "LASTLABEL"));
            espec.Nodes.Add(especSearch);

            TreeNode especmaterial = new TreeNode(menuname("E0028", "BOM"));
            //especmaterial.Nodes.Add("BOM 구성
            especmaterial.Nodes.Add(menuname("S001", "BOM 리스트"));
            especmaterial.Nodes.Add(menuname("S002", "모듈 BOM"));
            especmaterial.Nodes.Add(menuname("S025", "ERP BOM"));
            especmaterial.Nodes.Add(menuname("S012", "자재신규등록"));
            especmaterial.Nodes.Add(menuname("S013", "자재기준정보"));
            espec.Nodes.Add(especmaterial);
            TreeNode especEquipment = new TreeNode(menuname("E0025", "설비/치공구"));
            especEquipment.Nodes.Add(menuname("S014", "설비,치공구 관리"));
            especEquipment.Nodes.Add(menuname("S015", "설비,치공구 등록"));
            //especEquipment.Nodes.Add(menuname("S016", "설비공구 승인처리"));
            espec.Nodes.Add(especEquipment);
            TreeNode especBuMaterial = new TreeNode(menuname("E0026", "부자재"));
            especBuMaterial.Nodes.Add(menuname("S017", "STENCIL 신규등록"));
            especBuMaterial.Nodes.Add(menuname("S018", "S-TABLE 신규등록"));
            especBuMaterial.Nodes.Add(menuname("S019", "FEEDER 신규등록"));
            espec.Nodes.Add(especBuMaterial);
            TreeNode especOption = new TreeNode(menuname("E0027", "기타등록"));
            especOption.Nodes.Add(menuname("S020", "공급업체"));
            especOption.Nodes.Add(menuname("S021", "CCS 등록"));
            especOption.Nodes.Add(menuname("S022", "LABEL DESIGN"));
            especOption.Nodes.Add(menuname("S023", "Mounter PGM 등록"));
            espec.Nodes.Add(especOption);

            TreeNode info = new TreeNode(menuname("S024", "사용자정보변경"));

            //treeView1.Nodes.Add(test);
            tvMain.Nodes.Add(productsupport);
            tvMain.Nodes.Add(auto);
            tvMain.Nodes.Add(warehouse);
            tvMain.Nodes.Add(product);
            tvMain.Nodes.Add(process);
            tvMain.Nodes.Add(quality);
            tvMain.Nodes.Add(espec);
            tvMain.Nodes.Add(info);

            productsupport.BackColor = Color.DarkCyan;
            auto.BackColor = Color.DarkCyan;
            warehouse.BackColor = Color.DarkCyan;
            process.BackColor = Color.DarkCyan;
            product.BackColor = Color.DarkCyan;
            quality.BackColor = Color.DarkCyan;
            espec.BackColor = Color.DarkCyan;
        }

        private void treeview_spk_2()
        {
            TreeNode productsupport = new TreeNode("생산지원");
            productsupport.Nodes.Add("반출입신청서");
            productsupport.Nodes.Add("반출입관리");
            productsupport.Nodes.Add("반출입승인");
            tvMain.Nodes.Add(productsupport);

            TreeNode WarehouseManagemant = new TreeNode("자재관리");  // 자재관리
            WarehouseManagemant.Nodes.Add("자재이동(to생산)");
            WarehouseManagemant.Nodes.Add("자재이동조회");
            WarehouseManagemant.Nodes.Add("자재정보등록");
            tvMain.Nodes.Add(WarehouseManagemant);

            /////////////////////////////////////////////////////////////////////
            TreeNode ProductManagemant = new TreeNode("생산관리");
            TreeNode Managemant_Search = new TreeNode("조회");
            Managemant_Search.Nodes.Add("MO 현황");
            Managemant_Search.Nodes.Add("생산진행현황");
            Managemant_Search.Nodes.Add("인벤토리현황");
            Managemant_Search.Nodes.Add("히스토리트래킹");
            Managemant_Search.Nodes.Add("실적관리");
            Managemant_Search.Nodes.Add("ERP파일");
            Managemant_Search.Nodes.Add("P017_SSD 재공");
            Managemant_Search.Nodes.Add("[NEW] 재경팀결산");
            Managemant_Search.Nodes.Add("T7 & T7 Touch 초기화");
            Managemant_Search.Nodes.Add("SerialNumber 확인");
            Managemant_Search.Nodes.Add("LOT CARD PRINT");
            ProductManagemant.Nodes.Add(Managemant_Search);

            TreeNode Managemant_Sch = new TreeNode("작업지시서");
            Managemant_Sch.Nodes.Add("생산계획");
            Managemant_Sch.Nodes.Add("VEH->BT전환");
            Managemant_Sch.Nodes.Add("출하계획");
            ProductManagemant.Nodes.Add(Managemant_Sch);

            tvMain.Nodes.Add(ProductManagemant);

            /////////////////////////////////////////////////////////////////////
            TreeNode ProcessManagemant = new TreeNode("공정관리");
            ProcessManagemant.Nodes.Add("SSD 입고/출고");
            ProcessManagemant.Nodes.Add("양면인식검사");
            ProcessManagemant.Nodes.Add("투입공정");
            ProcessManagemant.Nodes.Add("R-TEST (수동)");
            ProcessManagemant.Nodes.Add("R-TEST (반자동)");
            ProcessManagemant.Nodes.Add("Leak TEST");
            ProcessManagemant.Nodes.Add("SETLABEL REPRINT");
            ProcessManagemant.Nodes.Add("EAN 라벨");
            ProcessManagemant.Nodes.Add("슬리브작업");
            ProcessManagemant.Nodes.Add("무게측정");
            ProcessManagemant.Nodes.Add("FILE CLIENT");
            ProcessManagemant.Nodes.Add("소/대박스포장");
            ProcessManagemant.Nodes.Add("소박스 일치");
            ProcessManagemant.Nodes.Add("인파렛트");
            ProcessManagemant.Nodes.Add("출하(통관)");
            ProcessManagemant.Nodes.Add("반제(온양)품출하");
            ProcessManagemant.Nodes.Add("나누기/합치기");
            ProcessManagemant.Nodes.Add("Lot Merge/Split");
            ProcessManagemant.Nodes.Add("[NEW] 불량등록");
            tvMain.Nodes.Add(ProcessManagemant);

            /////////////////////////////////////////////////////////////////////
            TreeNode QualityManagemant = new TreeNode("품질관리");
            QualityManagemant.Nodes.Add("품질검사");
            QualityManagemant.Nodes.Add("LOT판정결과조회");
            QualityManagemant.Nodes.Add("수입검사등록");
            QualityManagemant.Nodes.Add("수입검사 RAW data");
            QualityManagemant.Nodes.Add("수입검사 이력 조회");
            QualityManagemant.Nodes.Add("AIR LEAK - SPC");
            QualityManagemant.Nodes.Add("BEAM - A/S");
            QualityManagemant.Nodes.Add("BOXLABEL CHECK");
            tvMain.Nodes.Add(QualityManagemant);

            /////////////////////////////////////////////////////////////////////
            TreeNode EspecManagemant = new TreeNode("기준정보");
            EspecManagemant.Nodes.Add("E-SPEC");
            tvMain.Nodes.Add(EspecManagemant);

            /////////////////////////////////////////////////////////////////////
            TreeNode EquipManufacture = new TreeNode("장비사업");
            EquipManufacture.Nodes.Add("(1) 세트라벨");
            EquipManufacture.Nodes.Add("(3) 출하조회");
            EquipManufacture.Nodes.Add("(4) 장비생산 F/W Write");
            EquipManufacture.Nodes.Add("PCBA/PRODUCT 라벨");
            EquipManufacture.Nodes.Add("PCBA 입고");
            EquipManufacture.Nodes.Add("투입");
            EquipManufacture.Nodes.Add("조립(Assemble)");
            EquipManufacture.Nodes.Add("LOT 구성(완제품)");
            EquipManufacture.Nodes.Add("품질 검사 결과");
            EquipManufacture.Nodes.Add("출고 등록");
            EquipManufacture.Nodes.Add("제품 관리(불량/대여)");
            EquipManufacture.Nodes.Add("조회 (이력/재고)");
            EquipManufacture.Nodes.Add("PCB E-SPEC");
            tvMain.Nodes.Add(EquipManufacture);

            /////////////////////////////////////////////////////////////////////
            TreeNode EhddManagemant = new TreeNode("EHDD 창고");
            EhddManagemant.Nodes.Add("제품 입고");
            EhddManagemant.Nodes.Add("출하 등록");
            EhddManagemant.Nodes.Add("재고/출하 현황");
            EhddManagemant.Nodes.Add("시리얼번호 조회");
            EhddManagemant.Nodes.Add("재작업");
            EhddManagemant.Nodes.Add("Carton 확인");
            tvMain.Nodes.Add(EhddManagemant);

            /////////////////////////////////////////////////////////////////////
            TreeNode CableManagemant = new TreeNode("Cable & Hub");
            CableManagemant.Nodes.Add("생산 계획");
            CableManagemant.Nodes.Add("세트라벨 발행");
            CableManagemant.Nodes.Add("펄어비스 유통라벨");
            CableManagemant.Nodes.Add("기프트박스 무게검사");
            CableManagemant.Nodes.Add("카톤 포장");
            CableManagemant.Nodes.Add("카톤박스 무게검사");
            CableManagemant.Nodes.Add("파렛트 라벨");
            CableManagemant.Nodes.Add("출하 검사");
            CableManagemant.Nodes.Add("출하");
            tvMain.Nodes.Add(CableManagemant);

            /////////////////////////////////////////////////////////////////////
            TreeNode EquipmentManagement = new TreeNode("설비/치공구");
            EquipmentManagement.Nodes.Add("설비/치공구 등록");
            EquipmentManagement.Nodes.Add("설비/치공구 관리");
            tvMain.Nodes.Add(EquipmentManagement);

            TreeNode info = new TreeNode("사용자정보변경");
            tvMain.Nodes.Add(info);

            tvMain.ExpandAll();
        }

        private void treeview_spv()
        {
            TreeNode productsupport = new TreeNode(menuname("E0001", "생산지원"));
            productsupport.Nodes.Add(menuname("T005", "방문객"));
            productsupport.Nodes.Add(menuname("T007", "태블릿모드"));
            TreeNode auto = new TreeNode(menuname("E0024", "AUTO"));
            auto.Nodes.Add(menuname("AT01", "로그취합"));
            //auto.Nodes.Add(menuname("AT03", "WATCHCON"));
            auto.Nodes.Add(menuname("AT02", "라벨DOE"));
            auto.Nodes.Add(menuname("AT04", "ZEBRA FINDER"));
            TreeNode warehouse = new TreeNode(menuname("E0002", "자재관리"));  // 자재관리
            TreeNode warehouseSearch = new TreeNode(menuname("E0005", "조회"));
            warehouseSearch.Nodes.Add(menuname("M001", "COMP 재공"));
            warehouseSearch.Nodes.Add(menuname("M002", "COMP 이력"));
            warehouseSearch.Nodes.Add(menuname("M003", "자재 이력"));
            //warehouseSearch.Nodes.Add(menuname("M004", "PCB 추적"));
            warehouseSearch.Nodes.Add(menuname("M005", "COMP출고현황"));
            warehouseSearch.Nodes.Add(menuname("S004", "E-SPEC"));
            warehouseSearch.Nodes.Add(menuname("S001", "BOM 리스트"));
            warehouseSearch.Nodes.Add(menuname("S002", "모듈 BOM"));
            warehouseSearch.Nodes.Add(menuname("S003", "이원화 BOM"));
            warehouseSearch.Nodes.Add(menuname("S025", "ERP BOM"));
            warehouseSearch.Nodes.Add(menuname("M006", "IC-CONT.출고현황"));
            warehouseSearch.Nodes.Add(menuname("M007", "창고별재고"));
            warehouseSearch.Nodes.Add(menuname("M008", "자재유수명"));
            warehouseSearch.Nodes.Add(menuname("M009", "자재이동보기"));
            warehouse.Nodes.Add(warehouseSearch);
            TreeNode warehouseIn = new TreeNode(menuname("E0003", "입고"));
            warehouseIn.Nodes.Add(menuname("MA01", "신규입고"));
            warehouseIn.Nodes.Add(menuname("MA02", "기구물입고"));
            warehouseIn.Nodes.Add(menuname("MA03", "COMP 입고"));
            warehouseIn.Nodes.Add(menuname("MA09", "COMP 반품"));
            warehouseIn.Nodes.Add(menuname("MA04", "IC-CONT. 입고"));
            warehouse.Nodes.Add(warehouseIn);
            TreeNode warehouseOut = new TreeNode(menuname("E0004", "출고"));
            warehouseOut.Nodes.Add(menuname("MB01", "COMP 출고예약"));
            warehouseOut.Nodes.Add(menuname("MB02", "COMP SPLIT/MERGE"));
            warehouseOut.Nodes.Add(menuname("MB03", "COMP Loss"));
            warehouseOut.Nodes.Add(menuname("MB04", "COMP Adjust"));
            warehouseOut.Nodes.Add(menuname("MB05", "자재 SPLIT/MERGE"));
            warehouseOut.Nodes.Add(menuname("MB06", "자재요청"));
            warehouseOut.Nodes.Add(menuname("MB07", "자재출고"));
            warehouseOut.Nodes.Add(menuname("MB09", "기구물출고"));
            warehouseOut.Nodes.Add(menuname("MB08", "릴 라벨 확인"));
            warehouseOut.Nodes.Add(menuname("MB12", "릴 변경"));
            warehouseOut.Nodes.Add(menuname("MB10", "MH 이동"));
            warehouseOut.Nodes.Add(menuname("PP15", "[WH]TEST"));
            warehouseOut.Nodes.Add(menuname("MB15", "REPAIR 출고"));
            warehouse.Nodes.Add(warehouseOut);

            TreeNode warehouseRecode = new TreeNode(menuname("E0006", "등록"));
            warehouseRecode.Nodes.Add(menuname("MC01", "수입검사의뢰서"));
            warehouseRecode.Nodes.Add(menuname("MC02", "베이킹"));
            warehouseRecode.Nodes.Add(menuname("MC03", "COMP"));
            warehouseRecode.Nodes.Add(menuname("Q008", "입고정보등록"));
            warehouse.Nodes.Add(warehouseRecode);

            TreeNode product = new TreeNode(menuname("E0007", "생산관리")); // 생산관리
            TreeNode SmtProductSearch = new TreeNode(menuname("E0005", "조회"));
            SmtProductSearch.Nodes.Add(menuname("P001", "일단위계획(MO)"));
            SmtProductSearch.Nodes.Add(menuname("P002", "SSD 재공"));
            SmtProductSearch.Nodes.Add(menuname("M001", "COMP 재공"));
            SmtProductSearch.Nodes.Add(menuname("P003", "MOUNTER 현황"));
            SmtProductSearch.Nodes.Add(menuname("P004", "가동률"));
            SmtProductSearch.Nodes.Add(menuname("P016", "실적현황"));
            //SmtProductSearch.Nodes.Add(menuname("P005", "작업자별현황"));
            //SmtProductSearch.Nodes.Add(menuname("P006", "실시간현황"));
            SmtProductSearch.Nodes.Add(menuname("P007", "완제품조회"));
            SmtProductSearch.Nodes.Add(menuname("P008", "출하품조회"));
            SmtProductSearch.Nodes.Add(menuname("A021", "파트체인지조회"));
            SmtProductSearch.Nodes.Add(menuname("P010", "공정별수율"));
            SmtProductSearch.Nodes.Add(menuname("P011", "설비별수율"));
            SmtProductSearch.Nodes.Add(menuname("P012", "장비현황"));
            SmtProductSearch.Nodes.Add(menuname("P013", "SMT 라인별현황"));
            SmtProductSearch.Nodes.Add(menuname("P014", "공지사항"));
            SmtProductSearch.Nodes.Add(menuname("P015", "ERP파일"));
            SmtProductSearch.Nodes.Add(menuname("Q001", "3S/8D REPORT"));
            SmtProductSearch.Nodes.Add(menuname("T005", "방문객"));
            product.Nodes.Add(SmtProductSearch);

            TreeNode scoreboard = new TreeNode(menuname("E0008", "현황판"));
            //scoreboard.Nodes.Add(menuname("PA01", "SMT현황판"));
            //scoreboard.Nodes.Add(menuname("PA02", "iArts현황판"));
            //scoreboard.Nodes.Add(menuname("PA03", "조립현황판"));
            //scoreboard.Nodes.Add(menuname("PA04", "라우터현황판"));
            //scoreboard.Nodes.Add(menuname("PA05", "포장현황판"));
            scoreboard.Nodes.Add(menuname("PA06", "사무실현황판"));
            scoreboard.Nodes.Add(menuname("PA07", "종합현황판"));
            product.Nodes.Add(scoreboard);


            TreeNode smtproduct = new TreeNode(menuname("E0009", "작업지시서"));
            smtproduct.Nodes.Add(menuname("PP01", "[주간]계획등록"));
            smtproduct.Nodes.Add(menuname("PP02", "[일일]계획등록"));
            smtproduct.Nodes.Add(menuname("PP03", "[일일]SMT 계획"));
            smtproduct.Nodes.Add(menuname("PP04", "[일일]현황판계획"));
            smtproduct.Nodes.Add(menuname("PP05", "[일일]출하계획"));
            smtproduct.Nodes.Add(menuname("PP06", "[공지]인폼등록"));
            smtproduct.Nodes.Add(menuname("PP07", "[SEC]IC-CONT.요청"));
            smtproduct.Nodes.Add(menuname("PP08", "자재출고요청"));
            smtproduct.Nodes.Add(menuname("PP09", "반제품SEC출고"));
            smtproduct.Nodes.Add(menuname("PP10", "반제품입고"));
            smtproduct.Nodes.Add(menuname("PP11", "SEC재작업"));
            smtproduct.Nodes.Add(menuname("PP12", "SMT계획변경"));
            smtproduct.Nodes.Add(menuname("PP13", "수불데이터"));
            smtproduct.Nodes.Add(menuname("PP14", "재경팀결산"));
            product.Nodes.Add(smtproduct);

            TreeNode productmanager = new TreeNode(menuname("E0010", "관리"));
            productmanager.Nodes.Add(menuname("T004", "작업자인증"));
            product.Nodes.Add(productmanager);

            TreeNode process = new TreeNode(menuname("E0012", "공정관리"));
            TreeNode process_search = new TreeNode(menuname("E0005", "조회"));
            process_search.Nodes.Add(menuname("P002", "SSD 재공"));
            //process_search.Nodes.Add(menuname("P006", "실시간현황"));
            //process_search.Nodes.Add(menuname("A006", "진도현황"));
            process_search.Nodes.Add(menuname("QM03", "HOLD MEMO"));
            process_search.Nodes.Add(menuname("A004", "STEP MOVE"));
            process_search.Nodes.Add(menuname("A001", "LOT추적"));
            process_search.Nodes.Add(menuname("A002", "SET추적"));
            process_search.Nodes.Add(menuname("A019", "SCRAPCODE 등록"));
            process_search.Nodes.Add(menuname("A020", "PDA 이력"));
            process_search.Nodes.Add(menuname("A021", "파트체인지이력"));
            process.Nodes.Add(process_search);

            TreeNode process_m015 = new TreeNode(menuname("E0013", "점검"));
            process_m015.Nodes.Add(menuname("S006", "ASSY 도면보기"));
            process_m015.Nodes.Add(menuname("A055", "모델변경 SMT"));
            process_m015.Nodes.Add(menuname("A056", "SCREEN PRINTER NEW"));
            process_m015.Nodes.Add(menuname("A058", "PM 계획"));
            process_m015.Nodes.Add(menuname("QC08", "공정검사_SPCN (생산)"));

            TreeNode Sparepart = new TreeNode(menuname("E0029", "SPARE PART"));
            Sparepart.Nodes.Add(menuname("A059", "SPARE PART 입고"));
            Sparepart.Nodes.Add(menuname("A060", "SPARE PART 출고"));
            process_m015.Nodes.Add(Sparepart);
            TreeNode SmtProductMetal = new TreeNode(menuname("E0030", "STENCIL"));
            SmtProductMetal.Nodes.Add(menuname("A063", "STENCIL TENSION 측정"));
            SmtProductMetal.Nodes.Add(menuname("A064", "STENCIL 육안검사"));
            process_m015.Nodes.Add(SmtProductMetal);
            TreeNode SmtSubPortTable = new TreeNode(menuname("E0031", "S-TABLE"));
            SmtSubPortTable.Nodes.Add(menuname("A065", "S-TABLE 평탄도 측정"));
            SmtSubPortTable.Nodes.Add(menuname("A066", "S-TABLE 육안검사"));
            process_m015.Nodes.Add(SmtSubPortTable);
            TreeNode SmtProductSqueegee = new TreeNode(menuname("E0048", "SQUEEGEE"));
            SmtProductSqueegee.Nodes.Add(menuname("A057", "SQUEEGEE 검사"));
            SmtProductSqueegee.Nodes.Add(menuname("A075", "BLADE 교체"));
            process_m015.Nodes.Add(SmtProductSqueegee);
            TreeNode SmtProductSolder = new TreeNode(menuname("E0032", "SOLDER PASTE"));
            SmtProductSolder.Nodes.Add(menuname("A067", "SOLDER 사용등록"));
            SmtProductSolder.Nodes.Add(menuname("A068", "SOLDER 교반등록"));
            process_m015.Nodes.Add(SmtProductSolder);

            TreeNode SmtProductMagazine = new TreeNode(menuname("E0049", "OTHER"));
            SmtProductMagazine.Nodes.Add(menuname("A077", "MAGAZINE 검사"));
            SmtProductMagazine.Nodes.Add(menuname("A078", "NOZZLE 검사"));
            SmtProductMagazine.Nodes.Add(menuname("A079", "CLEANER 검사"));
            process_m015.Nodes.Add(SmtProductMagazine);


            process_m015.Nodes.Add(menuname("A069", "PROFILE"));
            process_m015.Nodes.Add(menuname("A070", "PCB 세척"));
            process_m015.Nodes.Add(menuname("A076", "M/S AOI 등록"));
            process_m015.Nodes.Add(menuname("A071", "LOT 누락검사"));
            //process_m015.Nodes.Add(menuname("A072", "LOT 세트검사"));
            process_m015.Nodes.Add(menuname("A073", "점검세트등록"));
            process_m015.Nodes.Add(menuname("A074", "파손세트등록"));
            process_m015.Nodes.Add(menuname("A082", "CCS SHEET (SMT)"));
            process_m015.Nodes.Add(menuname("A083", "SMT,AOI Monitoring"));
            process.Nodes.Add(process_m015);

            TreeNode assyproduct = new TreeNode(menuname("E0014", "공정"));
            assyproduct.Nodes.Add(menuname("A022", "M010 (SMT 1차)"));
            assyproduct.Nodes.Add(menuname("A023", "M015 (SMT 2차)"));
            assyproduct.Nodes.Add(menuname("A024", "M031 (iARTs)"));
            assyproduct.Nodes.Add(menuname("A025", " └ PCB 분리"));
            assyproduct.Nodes.Add(menuname("A026", "M033 (Router)"));
            //assyproduct.Nodes.Add(menuname("A027", " └ M033 불량등록"));
            assyproduct.Nodes.Add(menuname("A028", " └ M033 불량분리"));
            assyproduct.Nodes.Add(menuname("A029", "PART CHANGE"));
            //assyproduct.Nodes.Add(menuname("A030", "M100 (Initial) [개발중]"));
            assyproduct.Nodes.Add(menuname("A031", "M100 로그 업로더"));
            //assyproduct.Nodes.Add(menuname("A032", "M111 (Aging) [개발중]"));
            assyproduct.Nodes.Add(menuname("A033", "M120 (Case/Label)"));
            assyproduct.Nodes.Add(menuname("A034", " └ BRAND 라벨"));
            assyproduct.Nodes.Add(menuname("A035", "M121 (Label)"));
            assyproduct.Nodes.Add(menuname("A036", " └ SUB ASSY"));
            assyproduct.Nodes.Add(menuname("A037", "M130 (Interface)"));
            assyproduct.Nodes.Add(menuname("A039", " └ MOQ 불량분리"));
            assyproduct.Nodes.Add(menuname("A038", "M160 (FVI)"));
            assyproduct.Nodes.Add(menuname("A040", " └ MOQ 합치기"));
            assyproduct.Nodes.Add(menuname("A041", " └ MOQ 나누기"));
            assyproduct.Nodes.Add(menuname("A042", " └ MOQ 일부이동"));
            assyproduct.Nodes.Add(menuname("A043", " └ LOT-CHANGE"));
            assyproduct.Nodes.Add(menuname("A044", "M165 (QA-GATE)"));
            assyproduct.Nodes.Add(menuname("A045", "M170 (Packing)"));
            assyproduct.Nodes.Add(menuname("A046", " └ 유통 라벨"));
            assyproduct.Nodes.Add(menuname("A047", " └ MANUAL/TRAY 삽입"));
            assyproduct.Nodes.Add(menuname("A048", " └ T/K 용량 확인"));
            assyproduct.Nodes.Add(menuname("A049", " └ 소/대 박스포장"));
            assyproduct.Nodes.Add(menuname("A081", " └ OEM 박스포장"));
            assyproduct.Nodes.Add(menuname("A051", " └ 중량선별기"));
            assyproduct.Nodes.Add(menuname("A052", " └ BOX STACKER"));
            assyproduct.Nodes.Add(menuname("A053", "인파렛트"));
            assyproduct.Nodes.Add(menuname("A054", "제품출하"));
            process.Nodes.Add(assyproduct);

            TreeNode SmtProductRework = new TreeNode(menuname("E0015", "불량"));
            SmtProductRework.Nodes.Add(menuname("A007", "REWORK 조회"));
            SmtProductRework.Nodes.Add(menuname("A008", "불량라벨발행"));
            SmtProductRework.Nodes.Add(menuname("A009", "M100 등록"));
            SmtProductRework.Nodes.Add(menuname("A010", "M100 분리만"));
            SmtProductRework.Nodes.Add(menuname("A011", "SMT REWORK"));
            SmtProductRework.Nodes.Add(menuname("A012", "M111 양품이동"));
            SmtProductRework.Nodes.Add(menuname("A013", "P 합치기"));
            SmtProductRework.Nodes.Add(menuname("A014", "매거진매칭"));
            SmtProductRework.Nodes.Add(menuname("A015", "현장출고"));
            SmtProductRework.Nodes.Add(menuname("A016", "SCRAP창고"));
            SmtProductRework.Nodes.Add(menuname("A017", "PROBING 등록"));
            //SmtProductRework.Nodes.Add(menuname("A018", "브랜드라벨(재)"));
            process.Nodes.Add(SmtProductRework);

            TreeNode quality = new TreeNode(menuname("E0016", "품질관리"));
            TreeNode qualitySearch = new TreeNode(menuname("E0005", "조회"));
            qualitySearch.Nodes.Add(menuname("P002", "SSD 재공"));
            qualitySearch.Nodes.Add(menuname("A001", "LOT추적"));
            qualitySearch.Nodes.Add(menuname("A002", "SET추적"));
            qualitySearch.Nodes.Add(menuname("Q005", "CCS"));
            qualitySearch.Nodes.Add(menuname("S001", "BOM 리스트"));
            qualitySearch.Nodes.Add(menuname("S002", "모듈 BOM"));
            qualitySearch.Nodes.Add(menuname("S025", "ERP BOM"));
            qualitySearch.Nodes.Add(menuname("S005", "QASH"));
            qualitySearch.Nodes.Add(menuname("Q007", "CPK"));
            qualitySearch.Nodes.Add(menuname("Q009", "SMT Nonadjusted Ratio"));
            //qualitySearch.Nodes.Add(menuname("Q006", "자동체결기로그"));
            qualitySearch.Nodes.Add(menuname("Q001", "3S/8D REPORT"));
            quality.Nodes.Add(qualitySearch);

            TreeNode outquality = new TreeNode(menuname("E0017", "QC"));
            outquality.Nodes.Add(menuname("QC01", "수입검사"));
            outquality.Nodes.Add(menuname("QC02", "공정검사_PQC"));
            outquality.Nodes.Add(menuname("QC03", "공정검사_SPCN"));
            outquality.Nodes.Add(menuname("QC04", "공정검사_iARTs"));
            outquality.Nodes.Add(menuname("QC05", "M165 (QA-GATE)"));
            outquality.Nodes.Add(menuname("Q002", "M165 리포트"));
            outquality.Nodes.Add(menuname("Q003", "M165 수정"));
            outquality.Nodes.Add(menuname("QC06", "OBA 판정"));
            outquality.Nodes.Add(menuname("Q004", "OBA 리포트"));
            outquality.Nodes.Add(menuname("QM01", "QC CONTROL"));
            outquality.Nodes.Add(menuname("QM02", "HOLD LOT 승인"));
            outquality.Nodes.Add(menuname("QC07", "AQL 등록"));
            outquality.Nodes.Add(menuname("Q008", "입고정보등록"));
            outquality.Nodes.Add(menuname("QC09", "BOXLABEL CHECK"));
            outquality.Nodes.Add(menuname("QC11", "ESPEC 승인"));
            outquality.Nodes.Add(menuname("QC12", "반제품발송이력"));
            quality.Nodes.Add(outquality);

            TreeNode espec = new TreeNode(menuname("E0018", "기준정보"));
            TreeNode especAdd = new TreeNode(menuname("E0006", "등록"));
            especAdd.Nodes.Add(menuname("S007", "EVENT 등록"));
            especAdd.Nodes.Add(menuname("S008", "E-SPEC 수정"));
            especAdd.Nodes.Add(menuname("T004", "작업자인증"));
            espec.Nodes.Add(especAdd);

            TreeNode especSearch = new TreeNode(menuname("E0005", "조회"));
            especSearch.Nodes.Add(menuname("S004", "E-SPEC"));
            especSearch.Nodes.Add(menuname("S009", "표준문서"));
            //especSearch.Nodes.Add(menuname("S010", "무게분포"));
            especSearch.Nodes.Add(menuname("S011", "LASTLABEL"));
            especSearch.Nodes.Add(menuname("AT02", "라벨DOE"));
            espec.Nodes.Add(especSearch);

            TreeNode especmaterial = new TreeNode(menuname("E0028", "BOM"));
            //especmaterial.Nodes.Add("BOM 구성
            especmaterial.Nodes.Add(menuname("S001", "BOM 리스트"));
            especmaterial.Nodes.Add(menuname("S002", "모듈 BOM"));
            especmaterial.Nodes.Add(menuname("S025", "ERP BOM"));
            especmaterial.Nodes.Add(menuname("S012", "자재신규등록"));
            especmaterial.Nodes.Add(menuname("S013", "자재기준정보"));
            espec.Nodes.Add(especmaterial);
            TreeNode especEquipment = new TreeNode(menuname("E0025", "설비/치공구"));
            especEquipment.Nodes.Add(menuname("S014", "설비공구 리스트"));
            especEquipment.Nodes.Add(menuname("S015", "설비공구 신규등록"));
            //especEquipment.Nodes.Add(menuname("S016", "설비공구 승인처리"));
            espec.Nodes.Add(especEquipment);
            TreeNode especBuMaterial = new TreeNode(menuname("E0026", "부자재"));
            especBuMaterial.Nodes.Add(menuname("S017", "STENCIL 신규등록"));
            especBuMaterial.Nodes.Add(menuname("S018", "S-TABLE 신규등록"));
            especBuMaterial.Nodes.Add(menuname("S019", "FEEDER 신규등록"));
            espec.Nodes.Add(especBuMaterial);
            TreeNode especOption = new TreeNode(menuname("E0027", "기타등록"));
            especOption.Nodes.Add(menuname("S020", "공급업체"));
            especOption.Nodes.Add(menuname("S021", "CCS 등록"));
            especOption.Nodes.Add(menuname("S022", "LABEL DESIGN"));
            especOption.Nodes.Add(menuname("S023", "Mounter PGM 등록"));
            espec.Nodes.Add(especOption);

            TreeNode info = new TreeNode(menuname("S024", "사용자정보변경"));

            //treeView1.Nodes.Add(test);
            tvMain.Nodes.Add(productsupport);
            tvMain.Nodes.Add(auto);
            tvMain.Nodes.Add(warehouse);
            tvMain.Nodes.Add(product);
            tvMain.Nodes.Add(process);
            tvMain.Nodes.Add(quality);
            tvMain.Nodes.Add(espec);
            tvMain.Nodes.Add(info);

            productsupport.BackColor = Color.DarkCyan;
            auto.BackColor = Color.DarkCyan;
            warehouse.BackColor = Color.DarkCyan;
            process.BackColor = Color.DarkCyan;
            product.BackColor = Color.DarkCyan;
            quality.BackColor = Color.DarkCyan;
            espec.BackColor = Color.DarkCyan;

            tvMain.ExpandAll();
        }

        private void treeview_bookmark()
        {
            TreeNode warehouse = new TreeNode(menuname("E0002", "자재관리"));
            TreeNode product = new TreeNode(menuname("E0007", "생산관리"));
            TreeNode process = new TreeNode(menuname("E0012", "공정관리"));
            TreeNode quality = new TreeNode(menuname("E0016", "품질관리"));
            TreeNode espec = new TreeNode(menuname("E0018", "기준정보"));
            TreeNode productsupport = new TreeNode(menuname("E0001", "생산지원"));

            즐겨찾기ToolStripMenuItem.DropDownItems.Clear();

            string[] views = bookmark.Split(',');
            foreach (var view in views)
            {
                switch (view.Substring(0, 1))
                {
                    case "M":
                        warehouse.Nodes.Add(menuname(view, "").Replace(" └ ", ""));
                        break;
                    case "P":
                        product.Nodes.Add(menuname(view, "").Replace(" └ ", ""));
                        break;
                    case "A": // 공정관리
                        process.Nodes.Add(menuname(view, "").Replace(" └ ", ""));
                        break;
                    case "Q":
                        quality.Nodes.Add(menuname(view, "").Replace(" └ ", ""));
                        break;
                    case "S":
                        espec.Nodes.Add(menuname(view, "").Replace(" └ ", ""));
                        break;
                    case "T": // 생산지원
                        productsupport.Nodes.Add(menuname(view, "").Replace(" └ ", ""));
                        break;
                }

                //즐겨찾기ToolStripMenuItem.DropDownItems.Add(menuname(view, "").Replace(" └ ", ""));
                //즐겨찾기ToolStripMenuItem.DropDownItems.Add(menuname(view, "").Replace(" └ ", "")).Click += MenuItem_Click;
            }

            tvBookMark.Nodes.Add(productsupport);
            tvBookMark.Nodes.Add(warehouse);
            tvBookMark.Nodes.Add(product);
            tvBookMark.Nodes.Add(process);
            tvBookMark.Nodes.Add(quality);
            tvBookMark.Nodes.Add(espec);

            productsupport.BackColor = Color.DarkCyan;
            warehouse.BackColor = Color.DarkCyan;
            process.BackColor = Color.DarkCyan;
            product.BackColor = Color.DarkCyan;
            quality.BackColor = Color.DarkCyan;
            espec.BackColor = Color.DarkCyan;


            if (bookmark == "")
            {
                tvBookMark.Nodes.Add(menuname("P002", "SSD 재공"));
            }

            tvBookMark.ExpandAll();

            if (bookmark.Length > 6)
            {
                tabMain.SelectedIndex = 1;
                _CallRecursive(tvBookMark);
            }
        }

        private void _CallRecursive(TreeView treeView)
        {
            TreeNodeCollection nodes = treeView.Nodes;
            foreach (TreeNode n in nodes)
            {
                즐겨찾기ToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
                즐겨찾기ToolStripMenuItem.DropDownItems.Add($"{n.Text}");

                _PrintRecursive(n);
            }
        }
        private void _PrintRecursive(TreeNode treeNode)
        {
            foreach (TreeNode tn in treeNode.Nodes)
            {
                즐겨찾기ToolStripMenuItem.DropDownItems.Add("   " + tn.Text).Click += MenuItem_Click;

                _PrintRecursive(tn);
            }
        }

        private void mdiShow(TreeNode SelectedNode)
        {
            if (SelectedNode == null)
                return;

            var nodename = SelectedNode.Text.Split('[')[1].Replace("]", "");

            Form childForm = null;

            switch (nodename)
            {
                case "T001": childForm = new frmSupport_물품_반출입_등록(_connection, ""); break;
                case "T002": childForm = new frmSupport_물품_반출입_관리(_connection); break;
                case "T003": childForm = new frmSupport_물품_일괄승인(_connection); break;
                case "T005": childForm = new frmVisit(_connection); break;
                case "T006": childForm = new frmOTReport(_connection); break;
                case "T007": new frmTablet(_connection).Show(); break;
                case "AT01": childForm = new frm로그수집_smt(); break;
                case "AT02": childForm = new frmLABEL_DOE(_connection); break;
                case "Q008": childForm = new frmPCB_AvlLot(_connection); break;
                case "QC09": childForm = new frmBoxLabelCheck(_connection); break;
                case "QC10": childForm = new frmGageRaR(_connection); break;
                case "QC11": childForm = new frmApproval(_connection); break;
                case "QC12": childForm = new frmReport1(_connection); break;

                //new frmLABEL_DOE(_connection).Show(); break;
                //childForm = new frmLABEL_DOE(_connection); break;
                case "AT03": childForm = new frmWatchCon(_connection); break;
                case "AT04": childForm = new frmZebraFinder(); break;
                case "AT05": childForm = new frmPSSD_LotCard(_connection); break;
                case "AT06": new frmQAPartHold(_connection, "MZQL21T9HCJR-00AAZ-QU2").Show(); break;
                case "AT07": childForm = new frmNewTop(); break;
                //childForm = new frmGageRaR(_connection); break;
                //childForm = new frmWHRequest(_connection); break;
                //childForm = new ftpFile(); break;
                case "PP15": new frmWH_T1(_connection).Show(); break;
                case "PP18": childForm = new frmERP출고통제(_connection); break;
                case "PP16": childForm = new frmWH_T2(_connection); break;
                case "PP17": childForm = new frmWHRequest(_connection); break;

                //========================================================================
                // 자재관리
                case "M001": childForm = new frm재공_자재_콤포넌트(_connection); break;
                case "M002": childForm = new frm조회_콤포넌트(_connection); break;
                case "M003": childForm = new frm추적_자재(); break;
                case "M004": childForm = new frm추적_SMT_INFO(); break;
                case "M005": childForm = new frm재공_공정_콤포넌트(_connection); break;
                case "M006": childForm = new frm재공_공정_컨트롤러(_connection); break;
                case "M007": childForm = new frm창고별재고(_connection); break;
                case "M008": childForm = new frm자재조회_유수명(_connection); break;
                case "M009": childForm = new frm이동조회(_connection); break;

                case "MA01": childForm = new frm자재입고("신규", _connection); break;
                case "MA02": childForm = new frm자재입고_기구물("입고", _connection, "포장"); break;
                case "MA03": childForm = new frm자재입고_콤포넌트(_connection); break;
                case "MA04": childForm = new frm자재입고_컨트롤러(_connection); break;
                case "MA05": childForm = new frm자재입고("양품", _connection); break;
                case "MA06": childForm = new frm자재입고("불량", _connection); break;
                case "MA07": childForm = new frm자재입고("차용", _connection); break;
                case "MA08": MessageBox.Show("개발중"); break;
                case "MA09": childForm = new frm반품_콤포넌트(_connection); break;

                case "MB01": childForm = new frm자재예약_콤포넌트(_connection); break;
                case "MB02": childForm = new frmSplitMergeForComp(_connection); break;
                case "MB03": childForm = new frm자재불출_loss(_connection); break;
                case "MB04": childForm = new frm자재불출_Adjust(_connection); break;
                case "MB05": childForm = new frmSplitMergeForMate(_connection); break;
                case "MB06": childForm = new frm자재요청(_connection); break;
                case "MB07": childForm = new frm자재불출("생산", _connection); break;
                case "MB08": childForm = new frm자재라벨확인(_connection, "MH"); break;
                case "MB13": childForm = new frm자재라벨확인(_connection, "WH"); break;
                case "MB09": childForm = new frm자재입고_기구물("출고", _connection, "포장"); break;
                case "MB10": childForm = new frmMH이동(_connection); break;
                case "MB14": childForm = new frmMoveToVina(_connection); break;
                case "MB11": childForm = new frm자재불출("외부", _connection); break;
                case "MB12": childForm = new frmReelChange(_connection); break;
                case "MB15": childForm = new frmToRepair(_connection); break;
                case "MB16": childForm = new frmERP자재요청(_connection); break;

                case "MC01": childForm = new frm수입검사의뢰(_connection); break;
                case "MC02": childForm = new frm관리_베이킹(_connection); break;
                case "MC03": childForm = new frm보관_콤포넌트(_connection); break;
                case "MC04": childForm = new frmRomWrite(_connection); break;

                //========================================================================
                // 생산관리
                case "P001": childForm = new frmMoList(_connection); break;
                case "P002": childForm = new frm재공_SSD(_connection, dbsite); break;
                case "P003": childForm = new frmSMTMounterMoniter(); break;
                case "P004": childForm = new frmRTMS(); break;
                case "P005": childForm = new frm현황_작업자별생산량(_connection); break;
                case "P006": childForm = new frm실시간조회(_connection); break;
                case "P007": childForm = new frm조회_완제_출하(_connection, "완제품"); break;
                case "P008": childForm = new frm조회_완제_출하(_connection, "출하품"); break;
                //case "A021": childForm = new frm조회_PART_CHANGE(_connection); break;
                case "P010": childForm = new frmYieldReport(); break;
                case "P011": childForm = new frm장비사용현황(); break;
                case "P012": childForm = new frm현황조회_장비별(); break;
                case "P013": childForm = new frm현황조회_SMT라인별(_connection); break;
                case "P014": new frm현황판_공지사항(_connection).Show(); break;
                case "P015": childForm = new frmERP파일(_connection, dbsite); break;
                case "P016": childForm = new frmOverallSstatus(_connection); break;
                case "P017": new frm재공_SSD2().Show(); break;

                case "PA01": new frm현황판_사무실(_connection, "SMT").Show(); break;
                case "PA02": new frm현황판_사무실(_connection, "iArts").Show(); break;
                case "PA03": new frm현황판_사무실(_connection, "조립").Show(); break;
                case "PA04": new frm현황판_사무실(_connection, "라우터").Show(); break;
                case "PA05": new frm현황판_사무실(_connection, "포장").Show(); break;
                case "PA06":
                    if (frmMain.dbsite.Contains("SPK"))
                    {
                        new frm현황판_종합().Show();
                        break;
                    }
                    else
                    {
                        new frm현황판_SPV(_connection).Show();
                        break;
                    }

                case "PA07": childForm = new frmTempHum(_connection); break;
                case "PA08": new frm_온습도(_connection).Show(); break;
                case "PP01": childForm = new frmScheduleWeekly(_connection); break;
                case "PP02": childForm = new frmScheduleDaily(_connection); break;
                case "PP03": childForm = new frmScheduleSMT(_connection); break;
                case "PP04": childForm = new frm현황판계획(_connection); break;
                case "PP05": childForm = new frm출하계획_YMDL(_connection); break;
                case "PP19": childForm = new frmERP생산계획(_connection); break;
                case "PP06": new frm인폼등록(_connection).Show(); break;
                case "PP07": childForm = new frm부족요청_컨트롤러(); break;
                case "PP08": childForm = new frm자재요청(_connection); break;
                case "PP09": childForm = new frm반제품출하(_connection); break;
                case "PP10": childForm = new QSI_입고(_connection); break;
                case "PP11": childForm = new frmSEC_Rework(_connection); break;
                case "PP12": childForm = new frm계획변경_SMT(_connection); break;
                case "PP13": childForm = new frm수불결산(_connection); break;
              
                case "T004": childForm = new frmWorkerCertification(_connection); break;

                //========================================================================
                // 공정관리
                case "A001": childForm = new frm추적_LOT_정방향(_connection); break;
                case "A002": childForm = new frm추적_세트_역방향(); break;
                case "QM03": childForm = new frmMemo_Lot(_connection, ""); break;
                case "A004": childForm = new frmHoldLot("P", _connection, false); break;
                case "A006": childForm = new frm진도현황(_connection); break;
                case "A019": childForm = new frmScrapCode(_connection); break;
                case "A020": childForm = new frmPDAlist(_connection); break;
                case "A021": childForm = new frm조회_PART_CHANGE(_connection); break;

                case "A007": childForm = new frmRework_Search(_connection); break;
                case "A008": childForm = new frmRework_1_LabelPrint(_connection); break;
                case "A009": childForm = new frmRework_2_등록(_connection); break;
                case "A010": childForm = new frmRework_4_SPLIT(_connection, "M100"); break;
                case "A011": childForm = new frmRework_SMT(_connection); break;
                case "A085": childForm = new frmRework_AQL(_connection); break;
                case "A012": childForm = new frmRework_4_SPLIT(_connection, "M111"); break;
                case "A086": childForm = new frmRework_4_SPLIT(_connection, "F$$$"); break;
                case "A013": childForm = new frmRework_5_MERGE(_connection); break;
                case "A014": childForm = new frmRework_6_iArts(_connection); break;
                case "A015": childForm = new frmRework_Release(_connection); break;
                case "A016": childForm = new frmReworkScrap(_connection); break;
                case "A017": childForm = new frmRework_Probing(_connection); break;
                case "A018": childForm = new frm_BrandLabel_Reprint(); break;

                case "A022": childForm = new frmM010_LotIn("M010", _connection); break;
                case "A023": childForm = new frmM010_LotIn("M015", _connection); break;
                case "A024": childForm = new frmM031_iArts(); break;
                case "A025": childForm = new frmM033_MOQ_일부이동(_connection); break;
                case "A026": new frmM033_PCBArrayCheck(_connection, "P").Show(); break;
                case "A027": childForm = new frm불량등록_Router(); break;
                case "A028": childForm = new frmM100_불량분리(); break;
                case "A029": childForm = new frmPartChange(_connection); break;

                case "A030": MessageBox.Show("개발중"); break;
                case "A031": childForm = new frmM100_업로더(); break;
                case "A032": MessageBox.Show("개발중"); break;
                case "A033": childForm = new frmM120_Case(_connection); break;
                case "A034": childForm = new frmM120_BrandLabel(); break;
                case "A035": childForm = new frmM121_Label(_connection); break;
                case "A036": childForm = new frmM120_SubAssy(_connection); break;
                case "A084": childForm = new frmM125_LedInspection(_connection); break;
                case "A037": childForm = new frmM130_Dummy(_connection); break;
                case "A038": childForm = new frmM160_FVI(); break;
                case "A039": childForm = new frmM161_불량분리(); break;
                case "A040": childForm = new frmM161_MOQ_합치기(_connection); break;
                case "A041": childForm = new frmM161_MOQ_나누기(_connection); break;
                case "A042": childForm = new frmM161_MOQ_일부이동(_connection); break;
                case "A043": childForm = new frmM161_MOQ_LOT체인지(_connection); break;
                case "A044": childForm = new frmM165_QA(_connection); break;
                case "A045": childForm = new frmM170_PackingInit(_connection); break;
                case "A046": childForm = new frmM170_유통라벨(); break;
                case "A047": childForm = new frmM170_Manual(); break;
                case "A048": childForm = new frmM170_TK_CAPA(); break;
                case "A049": childForm = new frmM170_TK포장(); break;
                case "A081": childForm = new frmM170_OEM(); break;
                case "A051": childForm = new frmM170_Measure(_connection); break;
                case "A052": childForm = new BoxStackerMachine2(_connection); break;
                case "A053": childForm = new frmM180_Inpallet(); break;
                case "A054": childForm = new frm출하_YMCS(_connection); break;

                case "A055": childForm = new frmSMT모델변경(_connection); break;
                case "A056": childForm = new frmM010_CCS_Tools(); break;
                case "A058": childForm = new frmPMSch(_connection); break;
                case "A059": childForm = new frm자재입고_기구물("입고", _connection, "수리"); break;
                case "A060": childForm = new frm자재입고_기구물("출고", _connection, "수리"); break;
                case "A061": childForm = new frmSMTModelChangeSystem(); break;
                case "A062": childForm = new frmSMTPartChangeSystem(); break;
                case "A063": childForm = new frmMetalMask_tension(); break;
                case "A064": childForm = new frmMetalMask_visual(); break;
                case "A065": childForm = new frmS_TABLE_tension(); break;
                case "A066": childForm = new frmS_TABLE_visual(); break;
                case "A057": childForm = new frmSqueegee_Gauge(); break;
                case "A075": childForm = new frmSqueegee_Blade(); break;

                case "A067": childForm = new frmSMTRecordSolerPaste(); break;
                case "A068": childForm = new frmSolderPaste_agitation(); break;
                case "A069": childForm = new frmSMTProfile(); break;
                case "A070": childForm = new frmPCB세척(_connection); break;
                case "A071": childForm = new frmPCBLOT조회(_connection); break;
                case "A072": childForm = new frm중복스캔확인(_connection); break;
                case "A073": childForm = new frm점검세트(_connection); break;
                case "A074": childForm = new frm파손세트(_connection); break;
                case "A076": childForm = new frmSMT_SM_AOI(_connection); break;
                case "A077": childForm = new frmSMT_Magazines(_connection); break;
                case "A078": childForm = new frmSMT_Nozzle(_connection); break;
                case "A079": childForm = new frmSMT_AutoCleaner(_connection); break;
                case "A080": childForm = new frmSPK1_LeakTest(_connection); break;
                case "A082": childForm = new frmSMT_CCS(_connection); break;
                case "A083": childForm = new frmAOI_Monitoring(_connection); break;

                case "S024": childForm = new frm사용자정보(); break;

                //========================================================================
                // 품질관리
                case "QC01": childForm = new frm수입검사_자재(_connection); break;
                case "QC02": childForm = new frmPQC(_connection); break;
                case "QC03": childForm = new frmSPCN(_connection, "Q"); break;
                case "QC04": childForm = new frmM033_PCBArrayCheck(_connection, "Q"); break;
                case "QC05": childForm = new frmM165_QA(_connection); break;
                case "QC06": childForm = new frmM180_OBA(_connection); break;
                case "QC07": childForm = new frmAQL(_connection); break;
                case "QC08": childForm = new frmSPCN(_connection, "P"); break;

                case "Q001": childForm = new frmHoldLot("Q", _connection, true); break;
                case "Q002": childForm = new frmM165_Report(_connection); break;
                case "Q003": childForm = new frmM165_수정(_connection); break;
                case "Q004": childForm = new frmOQCReport(_connection); break;
                case "Q005": childForm = new frmPQC_SMT(_connection); break;
                case "Q006": childForm = new frmDandyLog(_connection); break;
                case "Q007": childForm = new frmSMT_SPI_CPK(_connection); break;
                case "Q009": childForm = new frmSMT_AOI_REPORT(_connection); break;

                case "QM01": childForm = new frmQCManager(_connection); break;
                case "QM02": childForm = new frmHoldLot("Q", _connection, false); break;

                //========================================================================
                // 기준정보
                case "S001": childForm = new frmMRPBomList(); break;
                case "S002": childForm = new frm모듈BOM(_connection); break;
                case "S003": childForm = new frmAlternateBom(_connection); break;
                case "S004": childForm = new frmEspec(); break;
                case "S005": childForm = new frmQABasic(_connection); break;
                case "S006": childForm = new frmSpec도면불러오기(_connection); break;
                case "S007": new frmPCNList(_connection).Show(); break;
                case "S008": childForm = new frm기준정보등록(_connection); break;
                case "S009": childForm = new frmFTPSearch(); break;
                case "S010": childForm = new frm조회_무게정보(_connection); break;
                case "S011": childForm = new frm조회_Last_Label(_connection); break;
                case "S012": childForm = new frm자재정보등록(_connection); break;
                case "S013": childForm = new frmMaterialsInfo(); break;
                case "S014": childForm = new frmEquipmentList(); break;
                case "S015": childForm = new frmEquipmentRecord(); break;
                //case "S016": childForm = new frmEquipmentApproval(); break;
                case "S017": childForm = new frmMetalMask(); break;
                case "S018": childForm = new frmS_TABLE(); break;
                case "S019": childForm = new frmSMT_Feeder(_connection); break;

                case "S020": childForm = new frm업체정보수정(); break;
                case "S021": childForm = new frmEspecCCS(); break;
                case "S022": new frmLabelDesign().Show(); break;
                case "S023": childForm = new frmPGMHIstory(); break;
                case "S025": childForm = new frmERPBom(); break;
            }

            if (childForm != null)
            {
                childForm.WindowState = FormWindowState.Maximized;
                childForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                childForm.Dock = DockStyle.Fill;
                childForm.TopLevel = false;

                TabPage tbp = new TabPage(SelectedNode.Text);
                tabControl1.TabPages.Add(tbp);
                tbp.Controls.Add(childForm);
                tbp.BorderStyle = BorderStyle.Fixed3D;

                tabControl1.SelectedTab = tbp;
                tabControl1.SelectedTab.Tag = SelectedNode.FullPath;

                childForm.Show();
            }
        }


        private void tvBookMark_DoubleClick(object sender, EventArgs e)
        {
            mdiShow(tvBookMark.SelectedNode);
        }


        private void treeView1_DoubleClick(object sender, EventArgs e)
        {
            if (tvMain.SelectedNode != null)
            {
                int index = 0;
                foreach (Control tab in tabControl1.Controls)
                {
                    if (tvMain.SelectedNode.FullPath == tab.Tag.ToString())
                    {
                        tabControl1.SelectedIndex = index;
                        return;
                    }
                    index = index + 1;
                }

                /*
                if (!tvMain.SelectedNode.FullPath.Contains("S024"))
                {
                    if (!authority.Contains(tvMain.SelectedNode.FullPath.Split('\\')[0].ToString()))
                    {
                        MessageBox.Show("접근할 수 없습니다. 권한을 확인하세요.", "NOTICE", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        return;
                    }
                }
                */

                Form childForm = null;

                if (dbsite == "SPK-02")
                {
                    switch (tvMain.SelectedNode.FullPath)
                    {
                        case "생산지원\\반출입신청서": childForm = new frmSupport_물품_반출입_등록(_connection, ""); break;
                        case "생산지원\\반출입관리": childForm = new frmSupport_물품_반출입_관리(_connection); break;
                        case "생산지원\\반출입승인": childForm = new frmSupport_물품_일괄승인(_connection); break;

                        case "자재관리\\자재이동(to생산)": childForm = new frmWH_T3(_connection); break;
                        case "자재관리\\자재이동조회": childForm = new frmWH_T2(_connection); break;
                        case "자재관리\\자재정보등록": childForm = new frm자재정보등록(_connection); break;

                        // ---------------------------------------------------------------------------------------------------------------------
                        case "생산관리\\조회\\MO 현황": childForm = new frmSSD_MoList(_connection); break;
                        case "생산관리\\조회\\생산진행현황": childForm = new frmSSD_Monitoring(_connection); break;
                        case "생산관리\\조회\\실적관리": childForm = new frmSSD_Inventory(_connection); break;
                        case "생산관리\\조회\\인벤토리현황": childForm = new frmInventoryForSSD(_connection); break;
                        case "생산관리\\조회\\ERP파일": childForm = new frmERP파일(_connection, dbsite); break;
                        case "생산관리\\조회\\P017_SSD 재공": new frm재공_SSD2().Show(); break;

                       
                        case "생산관리\\조회\\T7 & T7 Touch 초기화": new frmSSD_TCGRevert(_connection).Show(); break;
                        case "생산관리\\조회\\SerialNumber 확인": new frmSSD_SNChecker(_connection).Show(); break;
                        case "생산관리\\조회\\LOT CARD PRINT": childForm = new frmPSSD_LotCard(_connection); break;
                        case "생산관리\\조회\\히스토리트래킹": childForm = new frmSSD_HistoryTracking(_connection); break;
                        case "생산관리\\작업지시서\\생산계획": childForm = new frmSSD_AssyPlan(_connection); break;

                        case "생산관리\\작업지시서\\VEH->BT전환": childForm = new frmSSD_BT_Rollback(_connection); break;
                        case "생산관리\\작업지시서\\출하계획": childForm = new frmSSD_ShipPlan(_connection); break;
                        // ---------------------------------------------------------------------------------------------------------------------
                        case "공정관리\\출하(통관)": childForm = new frmSSD_ShipMent(_connection); break;
                        case "공정관리\\반제(온양)품출하": childForm = new frmSSD_AssyShipMent(_connection); break;
                        case "공정관리\\나누기/합치기": childForm = new frmSSD_Split_Merge(_connection); break;
                        case "공정관리\\Lot Merge/Split": childForm = new frmSSD_Merge(_connection); break;
                        case "공정관리\\SSD 입고/출고": childForm = new frmSSD_Incomming(_connection); break;

                        case "공정관리\\투입공정": childForm = new frmSSD_Input(_connection); break;
                        case "공정관리\\SETLABEL REPRINT": childForm = new frmPSSD_SetLabelReprint(_connection); break;
                        case "공정관리\\EAN 라벨": new frmSSD_EanPrint(_connection).Show(); break;
                        case "공정관리\\슬리브작업": new frmSSD_Sleeve(_connection).Show(); break;
                        case "공정관리\\[NEW] 불량등록": childForm = new frmSSD_RegFail(_connection); break;

                        case "공정관리\\양면인식검사": new frmSSD_OppositeChecker("MP", _connection).Show(); break;    /*childForm = new frmSSD_OppositeChecker("MP", _connection); tableLayoutPanel1.ColumnStyles[0].Width = 0; break;*/
                        case "공정관리\\R-TEST (수동)": new frmSSD_SmartData_Renewal("MP", _connection).Show(); break;   /*childForm = new frmSSD_SmartData_Renewal("MP", _connection); tableLayoutPanel1.ColumnStyles[0].Width = 0; break;*/
                        case "공정관리\\R-TEST (반자동)": new frmSSD_SmartData_Auto("MP", _connection).Show(); break; /*childForm = new frmSSD_SmartData_Auto("MP", _connection); tableLayoutPanel1.ColumnStyles[0].Width = 0; break;*/
                        case "공정관리\\Leak TEST": new frmSSD_AirLeak_Renewal(_connection, "LINE").Show(); break;
                        case "공정관리\\무게측정": childForm = new frmSSD_GiftWeight(_connection); break;
                        case "공정관리\\FILE CLIENT": childForm = new frmSSD_MysqlFileClient(_connection); break;
                        case "공정관리\\소/대박스포장": childForm = new frmSSD_Cell1(_connection); break;

                        case "공정관리\\소박스 일치": childForm = new frmSSD_CartonPacking(_connection); break;
                        case "공정관리\\인파렛트": childForm = new frmSSD_Inpallet(_connection); break;
                        // ---------------------------------------------------------------------------------------------------------------------
                        case "품질관리\\LOT판정결과조회": childForm = new FrmSsdQaSearchLotResult(_connection); break;
                        case "품질관리\\품질검사": new frmSSD_OQC_Inspection(_connection).Show(); return;
                        case "품질관리\\수입검사등록": childForm = new FrmSsdIqcInspection(_connection); break;
                        case "품질관리\\수입검사 RAW data": childForm = new FrmSsdIqcReport1(_connection); break;
                        case "품질관리\\수입검사 이력 조회": childForm = new FrmSsdIqcReport2(_connection); break;

                        case "품질관리\\AIR LEAK - SPC": childForm = new frmSSD_AirLeakSPC(_connection); break;
                        case "품질관리\\BEAM - A/S": childForm = new frmBP_AS(_connection); break;
                        case "품질관리\\BOXLABEL CHECK": childForm = new frmBoxLabelCheck(_connection); break;
                        // ---------------------------------------------------------------------------------------------------------------------
                        case "장비사업\\(1) 세트라벨": childForm = new frmEQSetlabel(_connection); break;
                        case "장비사업\\(3) 출하조회": childForm = new frmEQShipSearch(_connection); break;
                        case "장비사업\\(4) 장비생산 F/W Write": childForm = new frmEQManufacturing(); break;
                        case "장비사업\\PCBA/PRODUCT 라벨": childForm = new frmEQ_PcbSnlabel(_connection); break;
                        case "장비사업\\PCBA 입고": childForm = new frmEQ_PcbWareHousing_WithERP(_connection); break;

                        case "장비사업\\투입": childForm = new frmEQ_PcbInput_WithERP(_connection); break;
                        case "장비사업\\조립(Assemble)": childForm = new frmEQ_PcbAssemble_WithERP(_connection); break;
                        case "장비사업\\LOT 구성(완제품)": childForm = new frmEQ_PcbAssembled_LotMaker(_connection); break;
                        case "장비사업\\품질 검사 결과": childForm = new frmEQ_PcbAssembled_QC(_connection); break;
                        case "장비사업\\출고 등록": childForm = new frmEQ_PcbShipment_WithERP(_connection); break;
                        case "장비사업\\제품 관리(불량/대여)": childForm = new frmEQ_PcbAsManage(_connection); break;
                        case "장비사업\\조회 (이력/재고)": childForm = new frmEQ_PcbSearch(_connection); break;
                        case "장비사업\\PCB E-SPEC": childForm = new frmEQ_PcbEspec(_connection); break;
                        // ---------------------------------------------------------------------------------------------------------------------
                        case "EHDD 창고\\제품 입고": childForm = new FrmSpkIncomming(_connection); break;
                        case "EHDD 창고\\출하 등록": childForm = new FrmSpkShipping(_connection); break;
                        case "EHDD 창고\\재고/출하 현황": childForm = new FrmSpkStore(_connection); break;
                        case "EHDD 창고\\시리얼번호 조회": childForm = new frmSPKSearch(_connection); break;
                        case "EHDD 창고\\재작업": childForm = new frmSGReprint(_connection); break;

                        case "EHDD 창고\\Carton 확인": childForm = new frmSPK_CartonCheck(_connection); break;
                        // ---------------------------------------------------------------------------------------------------------------------
                        case "Cable & Hub\\생산 계획": childForm = new frmSECCommit_K(_connection, "Cable"); break;
                        case "Cable & Hub\\세트라벨 발행": childForm = new frmSECSetLabel_K(_connection, "Cable"); break;
                        case "Cable & Hub\\펄어비스 유통라벨": childForm = new frmSECEANLabel(_connection); break;
                        case "Cable & Hub\\기프트박스 무게검사": childForm = new frmSecGiftBoxWeightK(_connection); break;
                        case "Cable & Hub\\카톤 포장": childForm = new frmSECCartonPacking_K(_connection); break;

                        case "Cable & Hub\\카톤박스 무게검사": childForm = new frmSECCartonWeight_K(_connection); break;
                        case "Cable & Hub\\파렛트 라벨": childForm = new frmSECInpallet_K(_connection); break;
                        case "Cable & Hub\\출하 검사": childForm = new frmSECOqc_K(_connection); break;
                        case "Cable & Hub\\출하": childForm = new frmSECShipment_K(_connection, "Cable"); break;
                        // ---------------------------------------------------------------------------------------------------------------------
                        case "설비/치공구\\설비/치공구 등록": childForm = new frmEquipmentRecord_spk2(_connection); break;
                        case "설비/치공구\\설비/치공구 관리": childForm = new frmEquipmentList_spk2(_connection); break;
                        // ---------------------------------------------------------------------------------------------------------------------
                        case "기준정보\\E-SPEC": childForm = new frmSSD_Espec(_connection); break;
                        // ---------------------------------------------------------------------------------------------------------------------
                        case "사용자정보변경": childForm = new frm사용자정보(); break;
                            // ---------------------------------------------------------------------------------------------------------------------
                    }
                }
                else
                {
                    if (!tvMain.SelectedNode.FullPath.Contains("S024") && !tvMain.SelectedNode.FullPath.Contains("AT04") && !tvMain.SelectedNode.FullPath.Contains("생산지원")) // 사용자 정보변경
                    {
                        if (!authority.Contains(tvMain.SelectedNode.FullPath.Split('\\')[0].ToString()))
                        {
                            MessageBox.Show(menuname("MG040", "접근할 수 없습니다. 권한을 확인하세요."), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            return;
                        }
                    }

                    if (tvMain.SelectedNode.Text.Split('.').Length >= 2 || tvMain.SelectedNode.Text == "물류")
                    {
                        mdiShow(tvMain.SelectedNode);
                        return;
                    }
                }

                if (childForm != null)
                {
                    childForm.WindowState = FormWindowState.Maximized;
                    childForm.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                    childForm.Dock = DockStyle.Fill;
                    childForm.TopLevel = false;

                    TabPage tbp = new TabPage(tvMain.SelectedNode.Text);
                    tabControl1.TabPages.Add(tbp);
                    tbp.Controls.Add(childForm);
                    tbp.BorderStyle = BorderStyle.Fixed3D;

                    tabControl1.SelectedTab = tbp;
                    tabControl1.SelectedTab.Tag = tvMain.SelectedNode.FullPath;

                    childForm.Show();
                }
            }
        }

        public DateTime GetLinkerTime(Assembly assembly, TimeZoneInfo target = null)
        {
            var filePath = assembly.Location;
            const int c_PeHeaderOffset = 60;
            const int c_LinkerTimestampOffset = 8;

            var buffer = new byte[2048];

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                stream.Read(buffer, 0, 2048);

            var offset = BitConverter.ToInt32(buffer, c_PeHeaderOffset);
            var secondsSince1970 = BitConverter.ToInt32(buffer, offset + c_LinkerTimestampOffset);
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var linkTimeUtc = epoch.AddSeconds(secondsSince1970);

            var tz = target ?? TimeZoneInfo.Local;
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(linkTimeUtc, tz);

            return localTime;
        }

        private bool RunningProcesses(string processname)
        {
            Process[] processlist = Process.GetProcesses();

            foreach (Process theprocess in processlist)
            {
                if (theprocess.ProcessName == processname)
                    return true;
            }

            return false;
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (panel1.IsDisposed)
                return;

            tvMain.Font = tvBookMark.Font = (cbLanguage.Text == "English" || cbLanguage.Text == "Tiếng việt") ?
                new System.Drawing.Font("Verdana", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)))
                : new System.Drawing.Font("굴림체", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));

            language = (cbLanguage.Text == "한국어") ? cbLanguage.Text : "English";

            cbDBsite.Tag = cbDBsite.Text;

            tableLayoutPanel1.BackColor
                = tvMain.BackColor = tvMainPanel.BackColor = tvBookMark.BackColor = tvBookMarkPanel.BackColor
                = btnFavoriteDelete.BackColor = btnFavoriteAdd.BackColor
                = btnCall.BackColor = btnInform.BackColor = btnVoc.BackColor = btnHelp.BackColor
                = (cbDBsite.Text.Contains("SPV")) ? Color.Olive : Color.SlateGray;

            switch (txtID.Text)
            {
                case "현황판": txtPW.Text = "1234"; break;
                case "AUTO": txtPW.Text = "2"; break;
                case "AUTOCASE": txtPW.Text = "1234"; break;
            }


            if (cbDBsite.Text == string.Empty)
            {
                cbDBsite.Focus();
                return;
            }

            if (cbLanguage.Text == string.Empty)
            {
                cbLanguage.Focus();
                return;
            }

            if (txtID.Text == string.Empty)
            {
                txtID.Focus();
                return;
            }

            if (txtPW.Text == string.Empty)
            {
                txtPW.Focus();
                return;
            }

            dbsite = cbDBsite.Text;
            _connection = Helpers.MySqlHelper.InitConnection(cbDBsite.Text);

            var sql = string.Format("SELECT id, user_name, authority, user_pw, user_id, department, bookmark FROM tb_user where user_id = '{0}' and user_pw = '{1}' ", txtID.Text.ToUpper(), txtPW.Text.ToUpper());
            var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];

            userName = string.Empty;
            foreach (DataRow row in dataTable.Rows)
            {
                userID = int.Parse(row[0].ToString());
                userName = row[1].ToString();
                authority = row[2].ToString();
                user_PW = row[3].ToString();
                user_ID = row[4].ToString().ToUpper();
                department = row[5].ToString();
                bookmark = row[6].ToString();
                //cbDepartment.Text = row[5].ToString();
            }

            if (cbDBsite.Text != "SPK-02")
            {
                dictionary.Clear();
                if (cbLanguage.Text == "English")
                    sql = "SELECT msg_code, msg_en FROM new_mes.b_message ";
                else if (cbLanguage.Text == "Tiếng việt")
                    sql = "SELECT msg_code, msg_vn FROM new_mes.b_message ";
                else
                    sql = "SELECT msg_code, msg_kr FROM new_mes.b_message ";
                dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
                foreach (DataRow row in dataTable.Rows)
                {
                    dictionary.Add(row[0].ToString(), row[1].ToString());
                }

                /*
                if (cbDepartment.Text == string.Empty)
                {
                    MessageBox.Show("부서 정보를 확인하세요. ", "로그인");
                    cbDepartment.Focus();
                    checkDepartment = true;
                    return;
                }
                */
            }

            if (userName == string.Empty)
            {
                var message = "유저가 존재하지 않습니다.";
                if (cbLanguage.Text == "English")
                    message = "Can not find the user data.";
                else if (cbLanguage.Text == "Tiếng việt")
                    message = "Không thể tìm thấy dữ liệu người dùng.";

                MessageBox.Show(message, "LOGIN", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                txtPW.Text = string.Empty;
                txtPW.Focus();

                return;
            }

            if (cbDBsite.Text.Contains("SPV"))
            {
                if (!check_update.Equals("PASS"))
                {
                    MessageBox.Show("Hãy chạy chương trình valueplus mes+\r\n\r\nValueplus mes+ 프로그램 실행 해보세요!", "Message", MessageBoxButtons.OK);
                    return;
                }
            }

            tvMain.Visible = true;
            this.Text = $"VALUEPLUS MES+ System     LOGIN : {userName},     Build Ver:{version}";

            tvMain.Visible = true;

            switch (dbsite)
            {
                case "SPK-01":
                    treeview_spk_1();
                    treeview_bookmark();
                    Initialize_RestoreTreeview();
                    break;

                case "SPK-02":
                    treeview_spk_2();
                    this.AcceptButton = null;
                    //this.CancelButton = null;
                    break;

                case "SPV":
                case "SPV_TEST":
                    treeview_spv();
                    treeview_bookmark();
                    var culture = new CultureInfo("en-US");
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                    break;
            }

            tvMain.SelectedNode = tvMain.Nodes[0];
            tvMain.Refresh();


            if (txtID.Text == "root")
                tvMain.ExpandAll();

            tvMain.Nodes[0].EnsureVisible();

            if (_connection.State == ConnectionState.Closed)
                _connection.Open();

            sql = $"UPDATE tb_user SET updated_at = NOW(), mes_ver = '{version}' WHERE id = {userID} ";
            MySqlHelper.ExecuteNonQuery(_connection, sql);

            //#if !DEBUG
            if (dbsite == "SPK-01" && user_ID != "root" && user_ID != "AUTO" && user_ID != "AUTOCASE")
                button3_Click(null, null);  // Inform Note
                                            //#endif

            if (cbUserIDSave.Checked)
            {
                Properties.Settings.Default.conn_site = cbDBsite.Tag.ToString();
                Properties.Settings.Default.user_name = txtID.Text.ToUpper();
                Properties.Settings.Default.language = cbLanguage.Text;
                Properties.Settings.Default.Save();
            }

            panel1.Dispose();

            //timer1.Interval = 1000;
            //timer1.Start();

            try
            {
                using (var fs = File.Open("SETTING.ini", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    fs.Close();
                    var readData = string.Empty;

                    if (frmMain.user_ID == "root")
r
                        readData = File.ReadAllText("SETTING.ini", Encoding.UTF8);
                    else
                        readData = File.ReadAllText("SETTING.ini", Encoding.Default);

                    lineName = readData.Replace("\r\n", "=").Split('=')[1];
                }

                if (cbDBsite.Tag.ToString() == "TEST")
                {
                    new BoxStackerMachine2(_connection).Show();
                }
            }
            catch (Exception)
            {
            }

            if (lineName == "WH_KEY1")
            {
                tvMain.Font = tvBookMark.Font = (cbLanguage.Text == "English" || cbLanguage.Text == "Tiếng việt") ?
                    new System.Drawing.Font("Verdana", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)))
                    : new System.Drawing.Font("굴림체", 13F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            }

            if (dbsite == "SPK-01")
            {
                /*
                _msconnection = Helpers.MSSqlHelper.InitConnection("ERP");
                _msconnection.Open();
                SqlCommand mssql = new SqlCommand("SELECT COUNT(*) FROM SP_ERP.dbo.p_bom_detail ", _msconnection);
                SqlDataReader sdr = mssql.ExecuteReader();
                while (sdr.Read())
                {
                    MessageBox.Show(sdr[0].ToString(), "MS SQL");
                }
                sdr.Close();
                */
            }

            //textBox1.Focus();


            if (user_ID == "root")
            {
                //new frmAOI_Monitoring(_connection).Show();
            }
            btnSearch.Focus();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void txtPW_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r' && txtPW.Text != string.Empty)
            {
                btnLogin.PerformClick();
            }
        }

        private void 끝내기ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab != null)
            {
                if (tabControl1.SelectedTab.Controls.Count > 0)
                {
                    Form childform = tabControl1.SelectedTab.Controls[0] as Form;
                    childform.Close();
                }

                tabControl1.TabPages.Remove(tabControl1.SelectedTab);

                if (dbsite == "SPK-02")
                {
                    tableLayoutPanel1.ColumnStyles[0].Width = 230;
                }
            }
        }

        private void tabControl1_ControlAdded(object sender, ControlEventArgs e)
        {
            button1.Visible = (tabControl1.TabCount == 0) ? false : true;
        }

        private void tabControl1_ControlRemoved(object sender, ControlEventArgs e)
        {
            button1.Visible = (tabControl1.TabCount == 1) ? false : true;
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            tvMain.SelectedNode.NodeFont = new Font(tvMain.Font, FontStyle.Bold);
            tvMain.SelectedNode.ForeColor = Color.Black;
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (tvMain.SelectedNode != null)
            {
                tvMain.SelectedNode.NodeFont = new Font(tvMain.Font, FontStyle.Regular);
                tvMain.SelectedNode.ForeColor = Color.White;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == "")
                return;

            var windowName = "LOGIN";
            try
            {
                windowName = tabControl1.SelectedTab.Text;
            }
            catch (Exception) { }

            if (textBox1.Text != string.Empty)
                sendEmail($@"[CALL MESSAGE] MES+ Build Ver: {version}, {windowName}, {textBox1.Text.ToUpper()}, {frmMain.userName}", "email@");

            new frmCall(textBox1.Text, _connection).ShowDialog();

            MessageBox.Show("Message Sent Complete", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            _lstTreeState = SaveTreeState(tvMain.Nodes);
            try
            {
                string contents = string.Empty;
                for (int i = 0; i < _lstTreeState.Count; i++)
                {
                    if (i == _lstTreeState.Count - 1)
                    {
                        contents += _lstTreeState[i];
                    }
                    else
                    {
                        contents += _lstTreeState[i] + ',';
                    }
                }

                Properties.Settings.Default.treenode_path = contents;
                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            var message = (frmMain.language.Contains("English")) ? "Do you want to Quit MES+ ?" : "종료 하시겠습니까?";

            //if (MessageBox.Show(new Form { TopMost = true }, menuname("MG035"), "MES+", MessageBoxButtons.OKCancel) == DialogResult.Cancel)
            if (MessageBox.Show(new Form { TopMost = true }, message, "MES+", MessageBoxButtons.OKCancel) == DialogResult.Cancel)
            {
                e.Cancel = true;
            }
        }

        private void Recursive(TreeNodeCollection Nodes)
        {
            foreach (TreeNode node in Nodes)
            {
                if (node.FullPath == _fullpath)
                {
                    node.Expand();
                }

                Recursive(node.Nodes);
            }
        }

        private string _fullpath = string.Empty;
        private void RestoreTreeState(TreeNodeCollection nodes, List<string> treeState)
        {
            foreach (string fullpath in treeState)
            {
                _fullpath = fullpath;
                Recursive(nodes);
            }
        }

        private List<string> SaveTreeState(TreeNodeCollection nodes)
        {
            List<string> lstNodeStates = new List<string>();
            try
            {
                foreach (TreeNode node in nodes)
                {
                    if (node.IsExpanded == true)
                    {
                        lstNodeStates.Add(node.FullPath);
                    }
                    lstNodeStates.AddRange(SaveTreeState(node.Nodes));
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return lstNodeStates;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var message = string.Empty;
            var created_on = string.Empty;
            var user_name = string.Empty;
            var sql = string.Format("SELECT id, txt, created_on, user_name FROM tb_z_score_text ORDER BY created_on DESC limit 1 ");
            var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                message = row[1].ToString().Replace("\n", "\r\n");
                created_on = row[2].ToString();
                user_name = row[3].ToString();
            }

            informTime = created_on;
            isExist = false;
            btnInform.BackColor = Color.SlateGray;
            new frmInformNote(message, user_name, _connection).Show();
        }

        private bool isExist = false;
        private int timerCount = 0;
        private void timer1_Tick(object sender, EventArgs e)
        {
            timerCount = timerCount + 1;

            if (isExist)
            {
                btnInform.BackColor = (btnInform.BackColor == Color.Red) ? Color.SlateGray : Color.Red;
            }

            if (timerCount % 10 == 0)
            {
                var sql = string.Format("SELECT id, txt, created_on FROM tb_z_score_text ORDER BY created_on DESC limit 1 ");
                var dataTable = MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
                foreach (DataRow row in dataTable.Rows)
                {
                    var created_on = row[2].ToString();

                    if (informTime != created_on)
                    {
                        isExist = true;
                    }
                }
            }
        }

        private void btnVoc_Click(object sender, EventArgs e)
        {
            new frmVOC(_connection).Show();
        }

        private void txtPW_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && txtPW.Text != string.Empty)
            {
                btnLogin_Click(null, null);
            }
        }

        private void textBox1_Click(object sender, EventArgs e)
        {
            //if (textBox1.Text == "문제 확인요청시!! 재연을 할수 있도록 LOT ID 와 작업내용을 함께 보내주세요.")
            //    textBox1.Text = "";
        }

        private void btnFavorite_Click(object sender, EventArgs e)
        {
            if (frmMain.dbsite.Contains("SPK-02"))
            {
                MessageBox.Show("지원하지 않는 기능입니다.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            TreeView tv;
            if (tabMain.SelectedIndex == 0)
                tv = tvMain;
            else
                tv = tvBookMark;

            if (tv.SelectedNode == null)
                return;

            if (tv.SelectedNode.Text.Contains("["))
            {
                var viewCode = tv.SelectedNode.Text.Split('[')[1].Replace("]", "");

                if (viewCode.Length != 4)
                {
                    MessageBox.Show("[" + tv.SelectedNode.Text + "] " + menuname("MG031", "즐겨찾기 등록을 할수 없습니다."), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (_connection.State == ConnectionState.Closed)
                    _connection.Open();

                if (!bookmark.Contains(viewCode))
                {
                    if (bookmark == "")
                        bookmark = viewCode;
                    else
                        bookmark = bookmark + "," + viewCode;

                    MessageBox.Show("[" + tv.SelectedNode.Text + "] " + menuname("MG032", "즐겨찾기에 추가했습니다."), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    bookmark = bookmark.Replace("," + viewCode + ",", ",").Replace("," + viewCode, "").Replace(viewCode + ",", "");

                    MessageBox.Show("[" + tv.SelectedNode.Text + "] " + menuname("MG033", "즐겨찾기에서 삭제했습니다."), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                var sql = string.Format("UPDATE tb_user SET bookmark = '{0}' WHERE id = {1} ", bookmark, userID);
                MySqlHelper.ExecuteNonQuery(_connection, sql);

                tvBookMark.Nodes.Clear();
                treeview_bookmark();
            }
            else
            {
                MessageBox.Show("[" + tv.SelectedNode.Text + "] " + menuname("MG034", "즐겨찾기 등록을 위한 준비가 되지 않았습니다."), "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        private void cbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbLanguage.Text == "English")
            {
                btnLogin.Text = "OK";
                btnExit.Text = "Cancel";
            }
            else
            {
                btnLogin.Text = "확인";
                btnExit.Text = "취소";
            }

            txtPW.Focus();
        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            //new frmERP출고통제(_connection).Show();
            //MessageBox.Show(Helpers.UiHelper.week(true));
            new frmHelpPage(_connection).Show();
        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            /*
            this.SuspendLayout();
            if (tabControl1 != null && tabControl1.TabPages.Count > 0)
            {
                if (tabControl1.SelectedTab.Controls.Count > 0)
                {
                    //this.Invalidate();
                    //Application.DoEvents();
                    Form childForm = tabControl1.SelectedTab.Controls[0] as Form;
                    childForm.WindowState = FormWindowState.Normal;
                    childForm.WindowState = FormWindowState.Maximized;
                    //Application.DoEvents();
                    //DoubleBufferedHelper.SetDoubleBufferedParent(this);
                }
            }
            this.ResumeLayout();
            */
        }

        private void MenuItem_Click(object sender, EventArgs e)
        {
            var menuItem = (ToolStripMenuItem)sender;

            bfind = false;

            CallRecursive(tvBookMark, menuItem.Text.TrimStart());
        }

        private void CallRecursive(TreeView treeView, string searchtext)
        {
            // Print each node recursively.
            TreeNodeCollection nodes = treeView.Nodes;
            foreach (TreeNode n in nodes)
            {
                PrintRecursive(n, searchtext);

                if (bfind)
                    return;
            }
        }

        private bool bfind = false;
        private void PrintRecursive(TreeNode treeNode, string searchtext)
        {
            // Print the node.
            System.Diagnostics.Debug.WriteLine(treeNode.Text);

            if (searchtext == treeNode.Text)
            {
                //MessageBox.Show(treeNode.Text);
                mdiShow(treeNode);

                bfind = true;
                return;
            }

            // Print each node recursively.
            foreach (TreeNode tn in treeNode.Nodes)
            {
                PrintRecursive(tn, searchtext);
            }
        }


        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            tableLayoutPanel1.ColumnStyles[0].Width = 0;
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            tableLayoutPanel1.ColumnStyles[0].Width = 230;
        }

        private void uPDATEToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            string updaterfilepath = Environment.CurrentDirectory + string.Format(@"\UPDATER.ini");

            if (!File.Exists(updaterfilepath))
            {
                FileStream stream = File.Create(updaterfilepath);
                stream.Close();

                File.WriteAllText(updaterfilepath, "UPDATER=mes+,create");
            }
            else
            {
                File.WriteAllText(updaterfilepath, "UPDATER=mes+,update");
            }

            File.Delete(Environment.CurrentDirectory + string.Format(@"\valueplus mes+.exe"));
            GetImageFromServer();
            Application.DoEvents();
            Application.Exit();
        }

        private string GetImageFromServer()
        {
            var sql = $"SELECT id, version, program FROM be_updateprogram WHERE memo = 'updater'";
            var dataTable = MySql.Data.MySqlClient.MySqlHelper.ExecuteDataset(_connection, sql).Tables[0];
            foreach (DataRow row in dataTable.Rows)
            {
                var name = row["id"].ToString();
                var ver = row["version"].ToString().Replace(":", "");
                var exeFilePath = Path.Combine("valueplus mes+.exe");
                if (!File.Exists(exeFilePath))
                {
                    var byteArray = (byte[])row["program"];
                    using (var fs = new FileStream(exeFilePath, FileMode.CreateNew, FileAccess.Write))
                    {
                        fs.Write(byteArray, 0, byteArray.Length);
                    }
                }
                return exeFilePath;
            }
            throw new FileNotFoundException($"valueplus mes+.exe could not found on server.");
        }

        private bool NodeFiltering(TreeNode Nodo, string text)
        {
            bool result = false;

            if (Nodo.Nodes.Count == 0)
            {
                string normalized1 = Regex.Replace(Nodo.Text, @"\s", "").ToUpper();
                string normalized2 = Regex.Replace(text, @"\s", "").ToUpper();


                if (normalized1.Contains(normalized2))
                {
                    result = true;
                }
                else
                {
                    Nodo.Remove();
                }
            }
            else
            {
                for (var i = Nodo.Nodes.Count; i > 0; i--)
                {
                    if (NodeFiltering(Nodo.Nodes[i - 1], text)) result = true;
                }

                if (!result) Nodo.Remove();
            }

            return result;
        }

        private void btnSearch_KeyUp(object sender, KeyEventArgs e)
        {
            string abc = btnSearch.Text;
            if (string.IsNullOrEmpty(btnSearch.Text))
            {
                tvMain.BeginUpdate();
                tvMain.Nodes.Clear();
                switch (dbsite)
                {
                    case "SPK-01":
                        treeview_spk_1();
                        treeview_bookmark();
                        Initialize_RestoreTreeview();
                        break;

                    case "SPK-02":
                        treeview_spk_2();
                        AcceptButton = null;
                        //this.CancelButton = null;
                        break;

                    case "SPV":
                    case "SPV_TEST":
                        treeview_spv();
                        treeview_bookmark();
                        break;
                }

                tvMain.SelectedNode = tvMain.Nodes[0];
                tvMain.EndUpdate();
                tvMain.Refresh();
                //tvMain.ExpandAll();
                return;
            }

            if (e.KeyCode != Keys.Enter) return;

            tvMain.BeginUpdate();

            for (var i = tvMain.Nodes.Count; i > 0; i--)
            {
                NodeFiltering(tvMain.Nodes[i - 1], btnSearch.Text);
            }
            tvMain.ExpandAll();

            tvMain.EndUpdate();
        }
    }
}
