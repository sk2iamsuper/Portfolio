using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Data.SqlClient;
using MES_WMS.ChildFrm;

namespace MES_WMS
{
    public partial class PRF05 : Form
    {
        #region Form Declarations and Initialization
        // 클래스 변수 및 객체 선언
        CLSEMPNO ce = new CLSEMPNO();
        CLSORDERLIST cde = new CLSORDERLIST();
        UserCommon.ClsExcel DPsheet = new UserCommon.ClsExcel();
        public event ChildFromEventHandler OnNotifyParent;
        
        // 사용자 및 시스템 설정 변수
        private string Factory = UserCommon.Public_Function.user_Factory;
        private Encoding enc = Encoding.GetEncoding(949);
        public string GroupWork, GroupDept, GroupDeptF, GroupProc, GroupKind;
        
        // 컨트롤 배열
        public Control[] G1_controls, G2_controls, G3_controls, G4_controls, G5_controls, G6_controls;
        
        // 서브 폼 및 기타 변수
        private TouchPad FrmTouch;
        private UserForm.PRF18 NewBox;
        private string DPQTY;
        private Int16 scaleX = 2, scaleY = 2;
        private string User_factory = UserCommon.Public_Function.user_Factory;
        private string User_dept = UserCommon.Public_Function.user_Dept;
        private string User_empno = UserCommon.Public_Function.user_Empno;
        private string User_name = UserCommon.Public_Function.user_Name;
        private string User_IP = UserCommon.Public_Function.user_IP;
        private string User_WH = UserCommon.Public_Function.user_WH;
        private static string SerName = UserCommon.Public_Function.user_Server;
        double boxuse = 0;
        
        // 프린터 관련
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern SafeFileHandle CreateFile(string lpFileName, FileAccess dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, FileMode dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);
        private string Print_path = "";
        private string strClick = "";
        
        Boolean Select_Mode = false; // 입력모드, 조회모드 구분
        UserCommon.CmCn conn = new UserCommon.CmCn(SerName, "cmv");

        /// <summary>
        /// 기본 생성자 - 폼 초기화
        /// </summary>
        public PRF05()
        {
            InitializeComponent();
            InitFrm();
        }

        /// <summary>
        /// 부서 코드를 받는 생성자
        /// </summary>
        /// <param name="Dept_code">부서 코드</param>
        public PRF05(string Dept_code)
        {
            InitializeComponent();
            GroupDept = Dept_code.Substring(3, 1);
            InitFrm();
        }

        /// <summary>
        /// 폼 초기화 함수 - 사용자 설정 및 UI 준비
        /// </summary>
        private void InitFrm()
        {
            GroupWork = UserCommon.Public_Function.user_Group;
            
            // 공장 설정
            if (User_factory == "F11")
            {
                User_dept = "0104";
                btnWH.Text = "A1";
                btnWH.Name = "A1";
                cde.SelWH = "A1";
            }
            
            // 기본값 설정
            cde.SelFactory = User_factory;
            btnFactory.Text = User_factory;
            btnFactory.Name = User_factory;
            cde.SelWH = User_WH;
            cde.SelEmpno = User_empno;
            cde.SelDept = User_dept;
            cde.SelProcKind = btnproc_kind.Tag.ToString();
            btnWH.Text = User_WH;
            btnWH.Tag = User_WH;
            lblWorker_ip.Text = User_IP;
            lblWorker.Text = User_name;
            
            // 프린터 경로 설정
            UserCommon.ClsFileCtl cl = new UserCommon.ClsFileCtl();
            Print_path = cl.GetConfigValues("BARCODE", "PRINT");
            
            btnGroup_Click(null, null);
        }

        /// <summary>
        /// 부모 폼에 이벤트 알림
        /// </summary>
        protected virtual void NotifyParent(ChildFormEventArgs e)
        {
            ChildFromEventHandler handler = OnNotifyParent;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        
        /// <summary>
        /// TouchPad에서 수량 정보를 받는 이벤트 핸들러
        /// </summary>
        private void ChildFrom_OnNotifyParent4(object sender, AddBadKindEventArgs e)
        {
            DPQTY = e.Message[2].ToString();
        }
        #endregion

        #region Dynamic Button Creation Methods
        /// <summary>
        /// 기본 버튼 생성 메서드 - FlowLayoutPanel에 버튼 동적 생성
        /// </summary>
        /// <param name="TargetDS">버튼 데이터 소스</param>
        /// <param name="TargetPnl">타겟 패널</param>
        /// <param name="BackColor">배경색</param>
        /// <param name="FontColor">글자색</param>
        /// <param name="gubn">버튼 종류 구분자</param>
        private void CallBtnKind(DataSet TargetDS, FlowLayoutPanel TargetPnl, Color BackColor, Color FontColor, string gubn)
        {
            pnlMain.Controls.Clear();
            pnlSub.Controls.Clear();
            TargetPnl.Controls.Clear();
            TargetPnl.Padding = new Padding(5);
            
            Button TargetBtn = new Button();
            int BtnWidth = 0, BtnHeight = 0, PnlWidth = 0, PnlHeight = 0;

            // 버튼 크기 계산
            if (TargetDS != null && TargetDS.Tables[0].Rows.Count < 4)
            {
                BtnWidth = Convert.ToInt32(TargetPnl.Width / 4) - 5;
                BtnHeight = Convert.ToInt32(TargetPnl.Height / 4) - 18;
            }
            else
            {
                BtnWidth = Convert.ToInt32(TargetPnl.Width / 4) - 25;
                BtnHeight = Convert.ToInt32(TargetPnl.Height / 4) - 18;
            }
            
            BtnHeight = 68; // 고정 높이

            if (TargetDS != null && TargetDS.Tables[0].Rows.Count > 0)
            {
                if (gubn == "group")
                {
                    G1_controls = new Control[TargetDS.Tables[0].Rows.Count];
                }

                foreach (DataRow dr1 in TargetDS.Tables[0].Rows)
                {
                    TargetBtn = new Button();
                    TargetBtn.Text = dr1[1].ToString();
                    TargetBtn.Name = dr1[0].ToString();
                    TargetBtn.Tag = dr1[2].ToString();
                    TargetBtn.Height = BtnHeight;
                    TargetBtn.Width = BtnWidth;
                    TargetBtn.FlatStyle = FlatStyle.Flat;
                    TargetBtn.BackColor = BackColor;
                    TargetBtn.ForeColor = FontColor;
                    TargetBtn.Location = new Point(10, 15);

                    // 폰트 설정
                    if (gubn == "items")
                        TargetBtn.Font = new Font("Gulim", 10F, FontStyle.Bold);
                    else if (gubn == "order")
                        TargetBtn.Font = new Font("Gulim", 17F, FontStyle.Bold);
                    else
                        TargetBtn.Font = new Font("Gulim", 14F, FontStyle.Bold);

                    // 이벤트 핸들러 연결
                    AttachButtonEventHandler(TargetBtn, gubn);

                    TargetPnl.Controls.Add(TargetBtn);
                    PnlWidth += BtnWidth;

                    if (TargetPnl.Width < PnlWidth + 100)
                    {
                        PnlWidth = 0;
                        PnlHeight += BtnHeight;
                    }

                    if (gubn == "group")
                    {
                        G1_controls[TargetPnl.Controls.Count - 1] = TargetBtn;
                    }
                }

                // 패널 높이 조정
                TargetPnl.Height = (PnlHeight < BtnHeight) ? BtnHeight + 20 : PnlHeight + 100;
            }
        }

        /// <summary>
        /// 버튼 이벤트 핸들러 연결
        /// </summary>
        private void AttachButtonEventHandler(Button btn, string gubn)
        {
            switch (gubn)
            {
                case "dept": btn.Click += NewBtnCall_Click; break;
                case "factory": btn.Click += FactBtn_Click; break;
                case "proc": btn.Click += ProcessBtn_Click; break;
                case "items": btn.Click += ItemsBtn_Click; break;
                case "order": btn.Click += OrderBtn_Click; break;
                case "group": 
                case "work": btn.Click += GroupBtn_Click; break;
                default: btn.Click += EmpBtn_Click; break;
            }
        }

        /// <summary>
        /// 서브 패널 버튼 생성 메서드 - 2개의 버튼을 가진 패널 생성
        /// </summary>
        private void CallBtnKind_Sub(DataSet TargetDS, FlowLayoutPanel TargetPnl, Color BackColor, Color FontColor, string gubn)
        {
            TargetPnl.Controls.Clear();
            pnlSub.Controls.Clear();
            TargetPnl.Padding = new Padding(5);
            
            int BtnWidth = 120, BtnHeight = 68;
            int PnlWidth = 0, PnlHeight = 0;

            if (TargetDS != null && TargetDS.Tables[0].Rows.Count > 0)
            {
                if (TargetDS.Tables[0].Rows.Count > 2000)
                {
                    MessageBox.Show("sorry! Data is too big!!");
                    return;
                }

                G2_controls = new Control[TargetDS.Tables[0].Rows.Count];
                G4_controls = new Control[TargetDS.Tables[0].Rows.Count];
                G5_controls = new Control[TargetDS.Tables[0].Rows.Count];

                foreach (DataRow dr1 in TargetDS.Tables[0].Rows)
                {
                    Panel SubPnl = CreateSubPanel(dr1, BtnWidth, BtnHeight, FontColor);
                    Button TargetBtn = CreateSubButton(dr1, BtnWidth, BtnHeight, BackColor, FontColor, gubn);
                    Button TargetBtn2 = CreateSubButton2(dr1, BtnWidth, BtnHeight, gubn);

                    SubPnl.Controls.Add(TargetBtn);
                    SubPnl.Controls.Add(TargetBtn2);
                    TargetPnl.Controls.Add(SubPnl);

                    // 컨트롤 배열에 저장
                    int index = TargetPnl.Controls.Count - 1;
                    G2_controls[index] = TargetBtn;
                    G4_controls[index] = SubPnl;
                    G5_controls[index] = TargetBtn2;

                    PnlWidth += BtnWidth;
                    if (TargetPnl.Width < PnlWidth)
                    {
                        PnlWidth = 0;
                        PnlHeight += BtnHeight;
                    }
                }
            }
        }

        /// <summary>
        /// 서브 패널 생성
        /// </summary>
        private Panel CreateSubPanel(DataRow dr, int width, int height, Color fontColor)
        {
            Panel panel = new Panel();
            panel.Text = dr[2].ToString();
            panel.Name = dr[0].ToString();
            panel.Height = height * 2;
            panel.Width = width + 10;
            panel.BackColor = Color.DeepSkyBlue;
            panel.ForeColor = fontColor;
            panel.Location = new Point(10, 15);
            return panel;
        }

        /// <summary>
        /// 서브 버튼1 생성
        /// </summary>
        private Button CreateSubButton(DataRow dr, int width, int height, Color backColor, Color fontColor, string gubn)
        {
            Button btn = new Button();
            btn.Text = dr[1].ToString();
            btn.Name = dr[0].ToString();
            btn.Tag = gubn;
            btn.Height = height;
            btn.Width = width;
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = backColor;
            btn.ForeColor = fontColor;
            btn.Location = new Point(5, 8);
            btn.Click += SubBtn_Click;

            // 폰트 설정
            if (gubn == "work") btn.Font = new Font("Gulim", 19F, FontStyle.Bold);
            else if (gubn == "inner" || gubn == "carton") btn.Font = new Font("Gulim", 24F, FontStyle.Bold);
            else btn.Font = new Font("Gulim", 14F, FontStyle.Bold);

            return btn;
        }

        /// <summary>
        /// 서브 버튼2 생성 (수량 표시)
        /// </summary>
        private Button CreateSubButton2(DataRow dr, int width, int height, string gubn)
        {
            Button btn = new Button();
            btn.Text = dr[2].ToString();
            btn.Name = dr[0].ToString();
            btn.Tag = gubn;
            btn.Height = height - 20;
            btn.Width = width;
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = Color.Black;
            btn.ForeColor = Color.White;
            btn.Location = new Point(5, height + 8);
            btn.Click += SubBtn_Click;
            btn.Font = new Font("Gulim", 14F, FontStyle.Bold);
            return btn;
        }
        #endregion

        #region Button Click Event Handlers
        /// <summary>
        /// 공장 선택 버튼 클릭 이벤트
        /// </summary>
        public void FactBtn_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            btnFactory.Text = btn.Text;
            btnFactory.Name = btn.Name;
            cde.SelFactory = btn.Name;
            pnlTop.Controls.Clear();
            btnGroup_Click(null, null);
        }

        /// <summary>
        /// 공정 선택 버튼 클릭 이벤트
        /// </summary>
        public void ProcessBtn_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            btnCustomer.Text = btn.Text;
            btnCustomer.Name = btn.Name;
            cde.SelCustcode = btn.Name;
            pnlTop.Controls.Clear();
            btnGroup_Click(null, null);
        }

        /// <summary>
        /// 주문 선택 버튼 클릭 이벤트
        /// </summary>
        public void OrderBtn_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            btnOrder.Text = btn.Text;
            btnOrder.Name = btn.Name;
            cde.SelCustcode = btn.Name;
            pnlTop.Controls.Clear();
            btnGroup_Click(null, null);
        }

        /// <summary>
        /// 작업 버튼 클릭 이벤트 - 바코드 스캔 처리
        /// </summary>
        public void WorkBtn_Click(object sender, EventArgs e)
        {
            sbChangeGroup("new");
            Button btn = (Button)sender;
            txtbarcode.Text = btn.Text.ToString();

            if (txtbarcode.Text.Length == 11)
            {
                txtSubCode.Text = txtbarcode.Text;
                ProcessBarcodeScan();
            }
            else if (txtbarcode.Text.Length > 11)
            {
                txtbarcode.Text = "";
            }
            
            txtbarcode.Text = "";
            btnInner_ok.Text = "OK";
        }

        /// <summary>
        /// 바코드 스캔 처리 로직
        /// </summary>
        private void ProcessBarcodeScan()
        {
            string sLotDate = "20" + txtSubCode.Text.Substring(0, 2) + "." + 
                            txtSubCode.Text.Substring(2, 2) + "." + 
                            txtSubCode.Text.Substring(4, 2);

            string Qry1 = "select lot_no,a.item_group,b.group_sdesc,c.cust_code,c.cust_name,a.order_no,a.proc_kind,a.lot_date";
            Qry1 += " from tst16m a,cmv.dbo.tcb15 b,cmv.dbo.tcb01 c";
            Qry1 += " where a.saup_gubn = '01'";
            Qry1 += " and a.item_group = b.group_code";
            Qry1 += " and a.dest_cust = c.cust_code";
            Qry1 += " and a.lot_date between '" + sLotDate + "' and convert(varchar(10),dateadd(d,1,convert(datetime,getdate())),102)";
            Qry1 += " and a.lot_no = '" + txtSubCode.Text + "'";

            DataSet dr = conn.ResultReturnDataSet(Qry1);

            if (dr.Tables[0].Rows.Count == 0)
            {
                MessageBox.Show("There is no data!!! Please check order. " + txtbarcode.Text + "Notice!!!");
                return;
            }

            foreach (DataRow dr1 in dr.Tables[0].Rows)
            {
                btnCustomer.Text = dr1[4].ToString();
                btnCustomer.Name = dr1[3].ToString();
                cde.SelCustcode = dr1[3].ToString();

                btnItems.Text = dr1[2].ToString();
                btnItems.Name = dr1[1].ToString();
                cde.SelItems = dr1[1].ToString();

                btnOrder.Text = dr1[5].ToString();
                btnOrder.Name = dr1[0].ToString();
                cde.SelOrder = dr1[5].ToString();
                cde.SelLotno = dr1[0].ToString();
                cde.SelLotDate = dr1["lot_date"].ToString();

                cde.SelProcKind = dr1[6].ToString();
                UpdateProcKindDisplay();
            }

            UpdateProcessSettings();
            btnCarton_Click(null, null);
            btnboxuse_Click(null, null);
        }

        /// <summary>
        /// 공정 종류에 따른 표시 업데이트
        /// </summary>
        private void UpdateProcKindDisplay()
        {
            switch (cde.SelProcKind)
            {
                case "13": btnproc_kind.Text = "Picking"; break;
                case "15": btnproc_kind.Text = "End Picking"; break;
                case "11": btnproc_kind.Text = "In WH"; break;
            }
        }

        /// <summary>
        /// 공정 설정 업데이트
        /// </summary>
        private void UpdateProcessSettings()
        {
            if (cde.SelProcKind == "11" || cde.SelProcKind == "13")
            {
                cde.SelUpdateproc = "13";
                cde.SetUpdateproc = "13";
                cde.SelGroup = "4";
                cde.SetSubKind = "13";
            }
            else if (cde.SelProcKind != "50")
            {
                cde.SelUpdateproc = cde.SelProcKind;
                cde.SetUpdateproc = cde.SelProcKind;
                cde.SelGroup = "5";
                cde.SetSubKind = cde.SelProcKind;
            }
        }

        /// <summary>
        /// 그룹 버튼 클릭 이벤트
        /// </summary>
        public void GroupBtn_Click(object sender, EventArgs e)
        {
            int Cnt = pnlTop.Controls.Count;

            // 모든 버튼 색상 초기화
            for (Int32 index = 0; index < Cnt; index++)
            {
                G1_controls[index].BackColor = SystemColors.Control;
            }

            Button btn = (Button)sender;
            btn.BackColor = SystemColors.ButtonHighlight;
            GroupKind = btn.Tag.ToString();

            // 공정 상태에 따른 그룹 설정
            UserCommon.CmCn mc1 = new UserCommon.CmCn();
            string Qry1 = "select proc_kind from tst16m where saup_gubn='01' and lot_no like '" + txtSubCode.Text + "%'";
            DataSet ds1 = mc1.ResultReturnDataSet(Qry1);
            
            if (ds1.Tables[0].Rows.Count > 0)
            {
                int icur = Convert.ToInt16(ds1.Tables[0].Rows[0][0].ToString());
                if (icur >= 15)
                {
                    cde.SelGroup = (btn.Name == "3") ? "6" : "5";
                }
                else
                    cde.SelGroup = btn.Name;
            }
            else
                cde.SelGroup = btn.Name;

            cde.SetSubKind = GroupProc;
            CallBtnKind_Sub(cde.GetSubDS, pnlMain, SystemColors.Control, Color.Black, GroupKind);
        }

        /// <summary>
        /// 서브 버튼 클릭 이벤트 - 다양한 기능 처리
        /// </summary>
        public void SubBtn_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            int Cnt = pnlMain.Controls.Count;

            // 버튼 색상 업데이트
            for (Int32 index = 0; index < Cnt; index++)
            {
                if (btn.Tag.ToString() == "inner")
                {
                    if (btn.BackColor != Color.Black)
                        G2_controls[index].BackColor = btn.BackColor;
                }
                else
                {
                    G2_controls[index].BackColor = SystemColors.ButtonHighlight;
                }
            }

            btn.BackColor = Color.Brown;
            btn.ForeColor = Color.Black;

            if (btn.Tag == null)
            {
                btn.Tag = "dp";
                GroupKind = "dp";
                cde.SelGroup = "4";
            }

            // 버튼 종류에 따른 처리
            ProcessSubButtonClick(btn);
            txtbarcode.Focus();
        }

        /// <summary>
        /// 서브 버튼 클릭 처리
        /// </summary>
        private void ProcessSubButtonClick(Button btn)
        {
            switch (btn.Tag.ToString())
            {
                case "cust":
                    ProcessCustomerButton(btn);
                    break;
                case "items":
                    ProcessItemsButton(btn);
                    break;
                case "order":
                    ProcessOrderButton(btn);
                    break;
                case "wh":
                    ProcessWHButton(btn);
                    break;
                case "proc":
                    ProcessProcButton(btn);
                    break;
                case "work":
                    WorkBtn_Click(btn, null);
                    break;
                case "carton":
                    ProcessCartonButton(btn);
                    break;
                case "inner":
                    ProcessInnerButton(btn);
                    break;
                case "dp":
                    ProcessDPButton(btn);
                    break;
            }
        }
        #endregion

        #region UI Control Methods
        /// <summary>
        /// 그룹 변경 메서드 - UI 상태 업데이트
        /// </summary>
        public void sbChangeGroup(string Group)
        {
            switch (Group)
            {
                case "ALL":
                    ResetGroupColors();
                    btnLookup.Enabled = true;
                    GroupWork = "%";
                    break;
                case "Factory":
                    SetFactoryGroup();
                    break;
                case "new":
                    InitializeNewGroup();
                    break;
                case "clear":
                    ClearCurrentGroup();
                    break;
            }
            
            cde.SetGroupKind = GroupWork;
            txtbarcode.Focus();
        }

        /// <summary>
        /// 그룹 색상 초기화
        /// </summary>
        private void ResetGroupColors()
        {
            btnFactory.BackColor = SystemColors.Control;
            btnCustomer.BackColor = SystemColors.Control;
            btnItems.BackColor = SystemColors.Control;
        }

        /// <summary>
        /// 공장 그룹 설정
        /// </summary>
        private void SetFactoryGroup()
        {
            btnFactory.BackColor = SystemColors.ActiveCaption;
            btnCustomer.BackColor = SystemColors.Control;
            btnItems.BackColor = SystemColors.Control;
            btnLookup.Enabled = false;
            GroupWork = "0";
        }

        /// <summary>
        /// 새 그룹 초기화
        /// </summary>
        private void InitializeNewGroup()
        {
            cde.Selinnerupdate = "4";
            cde.SetInnerupdate = "4";
            cde.SelDate = DateTime.Now.ToString("yyyy.MM.dd");
            cde.SelWorkno = "";
            cde.SelDp = "";
            cde.SelLotno = "";
            
            ResetUIElements();
            pnlMain.Controls.Clear();
            pnlSub.Controls.Clear();
            sbBoxsize();
        }

        /// <summary>
        /// UI 요소 초기화
        /// </summary>
        private void ResetUIElements()
        {
            btnCustomer.Text = "Customer";
            btnItems.Text = "ITEM";
            btnOrder.Text = "Oreder No.";
            txtSubCode.Text = "";
            btnCTNO.Text = "NO";
            btnCTNO.Tag = null;
            btnINNO.Text = "NO";
            btnINNO.Tag = null;
            btnQty1.Text = "0";
            btnQty2.Text = "0";
            btnCnt.Text = "0";
            btnmod.Text = "0";
            btnInner_ok.Text = "OK";
        }

        /// <summary>
        /// 박스 사이즈 계산 및 표시 업데이트
        /// </summary>
        public void sbBoxsize()
        {
            if (!string.IsNullOrEmpty(btnBase.Text))
            {
                double boxsize = Convert.ToDouble(pnlboxview.Width);
                double lensize = boxsize / Convert.ToDouble(btnBase.Text);
                boxuse = Convert.ToDouble(btnboxuse.Width);

                btnboxuse.Width = Convert.ToInt32(Convert.ToDouble(pnlboxview.Width) * (boxuse / boxsize));
                btnboxuse.Text = "InBox (" + btnQty2.Text + ")";
                btnboxempty.Width = Convert.ToInt32(Convert.ToDouble(pnlboxview.Width) - Convert.ToDouble(btnboxuse.Width)) - 15;

                double remainingCapacity = (boxsize - boxuse) / lensize;
                btnboxempty.Text = (remainingCapacity > Convert.ToDouble(btnBase.Text)) ? 
                    btnBase.Text : Convert.ToInt16(remainingCapacity).ToString();

                UpdateBoxEmpty2();
            }
        }

        /// <summary>
        /// 박스 빈 공간 표시 업데이트
        /// </summary>
        private void UpdateBoxEmpty2()
        {
            if (btnboxempty2.Width >= btnboxempty.Width)
            {
                btnboxempty2.Text = btnboxempty.Text;
                btnboxempty2.Visible = true;
            }
            else
            {
                btnboxempty2.Visible = false;
            }
        }
        #endregion

        #region Print Methods
        /// <summary>
        /// 바코드 프린트 메서드
        /// </summary>
        private void sbdp_print_0(string inner_no, string cust_code)
        {
            string DATA = "", INNO = "", strBarcode = "", strOperator = "", strDP = "", strQuantity = "", strPrintDate = "";
            string strNewItem = "", strCustInner = "", strbz = "", strDest_cust = "", strYW = "";
            int Tqty = 0;
            int nCx = 250, nLineHeight = 50, nCy1 = 38, nCy2 = 650, nFont = 40, nCnt = 1;
            
            cde.Selinnerno = inner_no;
            cde.SetInnerdplist = GroupProc;

            if (cde.GetInnerdplistDS != null && cde.GetInnerdplistDS.Tables[0].Rows.Count > 0)
            {
                InitializePrintData(ref strBarcode, ref strOperator, ref strPrintDate, ref strNewItem, 
                                  ref strCustInner, ref strDest_cust, ref strYW, ref strbz);
                
                DATA = InitializePrintFormat();
                
                if (strDest_cust.Trim() == "12089" || strDest_cust.Trim() == "12132")
                {
                    PrintSpecialCustomerFormat(ref DATA, ref strNewItem, ref INNO, ref Tqty, 
                                             ref nCx, ref nLineHeight, ref nCy1, ref nCy2, 
                                             ref nFont, ref nCnt);
                }
                else
                {
                    PrintNormalFormat(ref DATA, ref strNewItem, ref strDP, ref strQuantity, 
                                    ref INNO, ref Tqty, ref nCx, ref nLineHeight, ref nCy1, 
                                    ref nCy2, ref nFont, ref nCnt);
                }

                AddPrintFooter(ref DATA, strBarcode, Tqty, strPrintDate, strbz, INNO);
                SendToPrinter(DATA);
            }
        }

        /// <summary>
        /// 프린트 데이터 초기화
        /// </summary>
        private void InitializePrintData(ref string strBarcode, ref string strOperator, ref string strPrintDate,
                                       ref string strNewItem, ref string strCustInner, ref string strDest_cust,
                                       ref string strYW, ref string strbz)
        {
            strBarcode = cde.Selinnerno;
            strOperator = User_name;
            strPrintDate = cde.SelDate;
            strNewItem = cde.GetInnerdplistDS.Tables[0].Rows[0]["item_code"].ToString().Substring(0, 6);
            strCustInner = cde.GetInnerdplistDS.Tables[0].Rows[0]["cust_inner"].ToString();
            strDest_cust = cde.GetInnerdplistDS.Tables[0].Rows[0]["dest_cust"].ToString();
            
            // 주차/년도 정보
            UserCommon.CmCn mcyw = new UserCommon.CmCn();
            string QryYW = "select case when len(datepart(week,'" + strPrintDate + "'))=1 then '0'+cast(datepart(week,'" + strPrintDate + "') as varchar(2)) else cast(datepart(week,'" + strPrintDate + "') as varchar(2)) end +'/' +substring(convert(varchar(10),'" + strPrintDate + "',102),1,4) ";
            DataSet dsyw = mcyw.ResultReturnDataSet(QryYW);
            if (dsyw.Tables[0].Rows.Count > 0)
            {
                strYW = dsyw.Tables[0].Rows[0][0].ToString();
            }

            if (strPrintDate.Length == 10) 
                strPrintDate = strPrintDate.Replace(".", "/").Substring(2, 8);

            // 특수 마크 정보
            UserCommon.CmCn mcbz = new UserCommon.CmCn();
            string Qry0 = "select sub_code from cmv.dbo.tst16c where opt_type='10' and remark='" + strDest_cust + "' and opt_code=left('" + cde.GetInnerdplistDS.Tables[0].Rows[0]["item_code"] + "',6) ";
            DataSet dsbz = mcbz.ResultReturnDataSet(Qry0);
            if (dsbz.Tables[0].Rows.Count > 0)
            {
                strbz = dsbz.Tables[0].Rows[0][0].ToString();
            }
        }

        /// <summary>
        /// 프린트 포맷 초기화
        /// </summary>
        private string InitializePrintFormat()
        {
            string DATA = "^XA";
            DATA += "^SEE:UHANGUL.DAT^FS";
            DATA += "^CW1,E:corefont.TTF^CI26^FS";
            return DATA;
        }

        /// <summary>
        /// 프린터로 데이터 전송
        /// </summary>
        private void SendToPrinter(string DATA)
        {
            Byte[] buffer = new byte[DATA.Length];
            buffer = enc.GetBytes(DATA);
            SafeFileHandle printer = CreateFile(Print_path + @"\13", FileAccess.ReadWrite, 0, IntPtr.Zero, FileMode.OpenOrCreate, 0, IntPtr.Zero);

            if (!printer.IsInvalid)
            {
                FileStream lpt1 = new FileStream(printer, FileAccess.ReadWrite);
                lpt1.Write(buffer, 0, buffer.Length);
                lpt1.Close();
            }
        }
        #endregion

        #region Data Access Class
        /// <summary>
        /// 주문 리스트 데이터 관리 클래스
        /// </summary>
        class CLSORDERLIST
        {
            #region 변수 선언
            private DataSet FactDS = null, WHDS = null, ProcDS = null, GroupDS = null, SubDS = null;
            private DataSet LotDS = null, EmpDS = null, ItemsDS = null, OrderDS = null;
            private DataSet UpdateQtyDS = null, UpdateprocDS = null, WorklistDS = null;
            private DataSet CartonDS = null, CartonEndDS = null, InnerDS1 = null, InnerDS2 = null;
            private DataSet InnerdplistDS = null, InnerNo2DS = null, InnerdptempDS = null;
            private DataSet InnerlistDS = null, InnerupdateDS = null, CartonlistDS = null;
            private DataSet CtnDPlistDS = null, InnerNolistDS = null;

            // 선택 값 저장 변수
            public string SelFactory = "", SelWH = "", SelDept = "", SelEmpno = "", SelCustcode = "";
            public string SelProcKind = "", SelUpdateproc = "", SelItems = "", SelOrder = "";
            public string SelLotno = "", SelGroup = "", SelCode = "", SelDp = "", SelDpQty = "";
            public string SelWorkno = "", SelCarton = "", SelCartonEnd = "", Selinnerno = "";
            public string Selinnerno2 = "", Selinnerupdate = "";
            public string SelDateF = DateTime.Now.AddDays(-90).ToString("yyyy.MM.dd");
            public string SelDateT = DateTime.Now.AddDays(1).ToString("yyyy.MM.dd");
            public string SelDate = DateTime.Now.ToString("yyyy.MM.dd");
            public string SelLotDate = "", selMLot = "", selMLot_yn = "", selsuju_date = "", selsuju_no = "";

            UserCommon.CmCn conn = new UserCommon.CmCn();
            #endregion

            /// <summary>
            /// 공정 종류 설정 및 데이터셋 업데이트
            /// </summary>
            public string SetProcKind
            {
                set
                {
                    SetProcDS(value);
                }
            }

            /// <summary>
            /// 공정 데이터셋 설정
            /// </summary>
            private void SetProcDS(string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    string qry = "select '00' opt_code, 'PROC_KIND' opt_name,'' opt_code \n";
                    qry += "union \n";
                    qry += "select opt_code,opt_name,opt_code from cmv.dbo.tst16c \n";
                    qry += "where opt_type = '02' \n";
                    qry += "order by opt_code";
                    
                    this.ProcDS = conn.ResultReturnDataSet(qry);
                }
            }

            /// <summary>
            /// 서브 데이터셋 설정
            /// </summary>
            public string SetSubKind
            {
                set
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string qry = "exec crf051 '01','" + SelFactory + "','" + SelCustcode + "','" + SelItems + "','" + SelLotno + "','" + SelGroup + "','" + SelProcKind + "'";
                        this.SubDS = conn.ResultReturnDataSet(qry);
                    }
                }
            }

            /// <summary>
            /// 서브 데이터셋 반환
            /// </summary>
            public DataSet GetSubDS
            {
                get { return this.SubDS; }
            }

            // 다른 데이터셋 Getter들...
            public DataSet GetFactDS { get { return this.FactDS; } }
            public DataSet GetWHDS { get { return this.WHDS; } }
            public DataSet GetProcDS { get { return this.ProcDS; } }
            public DataSet GetItemsDS { get { return this.ItemsDS; } }
            public DataSet GetGroupDS { get { return this.GroupDS; } }
            public DataSet GetLotDS { get { return this.LotDS; } }
            public DataSet GetEmpDS { get { return this.EmpDS; } }
            public DataSet GetOrderDS { get { return this.OrderDS; } }
            public DataSet GetUpdateQtyDS { get { return this.UpdateQtyDS; } }
            public DataSet GetCartonDS { get { return this.CartonDS; } }
            public DataSet GetCartonEndDS { get { return this.CartonEndDS; } }
            public DataSet GetInnerDS1 { get { return this.InnerDS1; } }
            public DataSet GetInnerDS2 { get { return this.InnerDS2; } }
            public DataSet GetCartonlistDS { get { return this.CartonlistDS; } }
            public DataSet GetWorklistDS { get { return this.WorklistDS; } }
            public DataSet GetInnerlistDS { get { return this.InnerlistDS; } }
            public DataSet GetInnerNo2DS { get { return this.InnerNo2DS; } }
            public DataSet GetInnerdplistDS { get { return this.InnerdplistDS; } }
            public DataSet GetInnerdptempDS { get { return this.InnerdptempDS; } }
            public DataSet GetInnerupdateDS { get { return this.InnerupdateDS; } }
        }
        #endregion

        #region
        public string SetCtnDPlist
        {
            set
            {
                this.CalCtnDPlist = value;
                SetCtnDPlistDS();
            } // set
        } // public 
        public string SetInnerNolist
        {
            set
            {
                this.CalInnerNolist = value;
                SetInnerNolistDS();
            } // set
        } // public

        public DataSet GetCtnDPlistDS
        {
            get
            {
                return this.CtnDPlistDS;
            }
        }
        public DataSet GetInnerNolistDS
        {
            get
            {
                return this.InnerNolistDS;
            }
        }
        private void SetCtnDPlistDS()
        {
            string qry = "";
            //string LotDate = "20" + SelLotno.Substring(0, 2) + "." + SelLotno.Substring(2, 2) + "." + SelLotno.Substring(4, 2);

            if (!string.IsNullOrWhiteSpace(this.CalCtnDPlist))
            {
                qry = " select b.item_code,b.sph+ ' / ' +b.cyl,convert(varchar,sum(qty)) \n";
                qry += " from cmv.dbo.tst13m a,cmv.dbo.tst13e b,cmv.dbo.tst16m d \n";
                qry += " where a.saup_gubn = '01' \n";
                qry += "   and a.chul_date = '" + SelDate + "' \n";
                qry += "   and a.chul_no= '" + SelWorkno + "'  \n";
                qry += "   and a.order_no = '" + SelOrder + "' \n";
                qry += "   and cast(cast(a.ct_no as int) as varchar(5)) = '" + SelCarton + "' \n";
                qry += "   and a.saup_gubn = b.saup_gubn \n";
                qry += "   and a.chul_date = b.chul_date \n";
                qry += "   and a.chul_no = b.chul_no \n";
                qry += "   and a.lot_no = b.lot_no \n";
                qry += "   and isnull(inner_no2,'')<>'' \n";
                qry += "   and a.saup_gubn = d.saup_gubn  \n";
                qry += "   and a.lot_no = d.lot_no  \n";
                qry += "   and d.lot_date = '" + SelLotDate + "'\n";
                qry += "   and a.lot_no='" + SelLotno + "' \n";
                qry += " group by b.item_code,a.order_no,b.sph,b.cyl,substring(b.item_code,8,1) \n";
                qry += " order by substring(b.item_code,8,1) ,b.cyl ,b.sph  \n";

                this.CtnDPlistDS = conn.ResultReturnDataSet(qry);

            } // if close
        }

        private void SetInnerNolistDS()
        {
            string qry = "";
            if (!string.IsNullOrWhiteSpace(this.CalInnerNolist))
            {

                qry = " select item_code,'Box: '+inner_no2,sum(qty) \n";
                qry += "  from cmv.dbo.tst13e  \n";
                qry += " where saup_gubn = '01'  \n";
                qry += "   and chul_date = '" + SelDate + "' \n";
                qry += "   and chul_no = '" + SelWorkno + "' \n";
                qry += "   and lot_no = '" + SelLotno + "' \n";
                qry += "   and item_code = '" + SelDp + "' \n";
                qry += " group by item_code,inner_no2 \n";
                qry += " order by inner_no2 \n";

                this.InnerNolistDS = conn.ResultReturnDataSet(qry);

            } // if close
        }

        #endregion

    } // PRF05 클래스 끝
} // 네임스페이스 끝
