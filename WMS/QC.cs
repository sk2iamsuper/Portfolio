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
using System.Collections;
using System.Data.SqlClient;

namespace MES_WMS
{
    public partial class QC : Form
    {
        // 폼 변수 선언
        CLSEMPNO ce = new CLSEMPNO();
        CLSORDERLIST cde = new CLSORDERLIST();
        public event ChildFromEventHandler OnNotifyParent;
        UserCommon.ClsExcel DPsheet = new UserCommon.ClsExcel();
        private string Factory = UserCommon.Public_Function.user_Factory;
        public string GroupWork;      // 작업 그룹
        public string GroupDept;      // 부서
        public string GroupProc;      // 공정
        public string GroupKind;      // 종류
        public Control[] G1_controls; // 상단 패널 컨트롤 배열
        public Control[] G2_controls; // 메인 패널 컨트롤 배열
        public Control[] G3_controls; // 서브 패널 컨트롤 배열
        public Control[] G4_controls; // 패널 컨트롤 배열
        public Control[] G5_controls; // 버튼2 컨트롤 배열
        private TouchPad FrmTouch;    // 터치패드 폼
        private string DPQTY;         // 수량 값
        private Int16 scaleX = 2;     // X축 스케일
        private Int16 scaleY = 2;     // Y축 스케일
        private string User_factory = UserCommon.Public_Function.user_Factory;
        private string User_dept = UserCommon.Public_Function.user_Dept;
        private string User_empno = UserCommon.Public_Function.user_Empno;
        private string User_name = UserCommon.Public_Function.user_Name;
        private string User_IP = UserCommon.Public_Function.user_IP;
        private string User_WH = UserCommon.Public_Function.user_WH;
        private static string SerName = UserCommon.Public_Function.user_Server;

        double boxuse = 0;            // 박스 사용량
        Boolean Select_Mode = false;  // 입력모드, 조회모드
        UserCommon.CmCn conn = new UserCommon.CmCn(SerName, "cmv");
        UserCommon.ComCls uc = new UserCommon.ComCls();
        private ArrayList SelectSS = new ArrayList(); // 데이터그리드뷰 컬럼 정보

        /// <summary>
        /// 생성자
        /// </summary>
        public QC()
        {
            InitializeComponent();
            InitFrm();
        }

        /// <summary>
        /// 부서코드를 받는 생성자
        /// </summary>
        public QC(string Dept_code)
        {
            InitializeComponent();
            GroupDept = Dept_code.Substring(3, 1);
            InitFrm();
        }

        /// <summary>
        /// 폼 초기화
        /// </summary>
        private void InitFrm()
        {
            GroupWork = UserCommon.Public_Function.user_Group;
            setDG(); // 데이터그리드뷰 설정

            cde.SelWH = User_WH;
            cde.SelEmpno = User_empno;
            cde.SelDept = User_dept;
            cde.SelProcKind = btnproc_kind.Tag.ToString();

            btnWH.Text = User_WH;
            btnWH.Tag = User_WH;
        }

        /// <summary>
        /// 부모 폼에 이벤트 통지
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
        /// 터치패드에서 수량 받아오기
        /// </summary>
        private void ChildFrom_OnNotifyParent4(object sender, AddBadKindEventArgs e)
        {
            DPQTY = e.Message[2].ToString();
        }

        #region 버튼 생성 메서드

        /// <summary>
        /// 상단 패널에 버튼 생성 (그룹 버튼 등)
        /// </summary>
        private void CallBtnKind(DataSet TargetDS, FlowLayoutPanel TargetPnl, Color BackColor, Color FontColor, string gubn)
        {
            pnlMain.Controls.Clear();
            pnlSub.Controls.Clear();
            TargetPnl.Controls.Clear();
            TargetPnl.Padding = new Padding(5);
            Button TargetBtn = new Button();
            int BtnWidth = 0;
            int BtnHeight = 0;
            int PnlWidth = 0;
            int PnlHeight = 0;

            if (TargetDS != null)
            {
                // 데이터 행 수에 따라 버튼 크기 조정
                if (TargetDS.Tables[0].Rows.Count < 4)
                {
                    BtnWidth = Convert.ToInt32(TargetPnl.Width / 3) - 5;
                    BtnHeight = Convert.ToInt32(TargetPnl.Height / 3) - 18;
                }
                else
                {
                    BtnWidth = Convert.ToInt32(TargetPnl.Width / 4) - 25;
                    BtnHeight = Convert.ToInt32(TargetPnl.Height / 4) - 18;
                }
            }
            else
            {
                BtnWidth = Convert.ToInt32(TargetPnl.Width / 4) - 8;
                BtnHeight = Convert.ToInt32(TargetPnl.Height / 4) - 18;
            }

            BtnHeight = 68;
            if (TargetDS != null && TargetDS.Tables[0].Rows.Count > 0)
            {
                if (gubn == "group")
                {
                    G1_controls = new Control[TargetDS.Tables[0].Rows.Count];
                }

                // 데이터 행마다 버튼 생성
                foreach (DataRow dr1 in TargetDS.Tables[0].Rows)
                {
                    TargetBtn = new Button();
                    TargetBtn.Text = dr1[1].ToString(); // 표시 텍스트
                    TargetBtn.Name = dr1[0].ToString(); // 코드 값
                    TargetBtn.Tag = dr1[2].ToString();  // 추가 정보
                    TargetBtn.Height = BtnHeight;
                    TargetBtn.Width = BtnWidth;
                    TargetBtn.FlatStyle = FlatStyle.Flat;
                    TargetBtn.BackColor = BackColor;
                    TargetBtn.ForeColor = FontColor;
                    TargetBtn.Location = new Point(10, 15);
                    TargetBtn.Font = new Font("Gulim", 14F, FontStyle.Bold);

                    // 버튼 종류에 따라 이벤트 핸들러 연결
                    if (gubn == "group")
                        TargetBtn.Click += new EventHandler(GroupBtn_Click);
                    else if (gubn == "work")
                        TargetBtn.Click += new EventHandler(GroupBtn_Click);
                    else
                        TargetBtn.Click += new EventHandler(EmpBtn_Click);

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
                if (PnlHeight < BtnHeight)
                {
                    TargetPnl.Height = BtnHeight + 20;
                }
                else
                {
                    TargetPnl.Height = PnlHeight + 100;
                }
            }
        }

        /// <summary>
        /// 메인 패널에 버튼 생성 (카트론, 내부상자 등)
        /// </summary>
        private void CallBtnKind_Sub(DataSet TargetDS, FlowLayoutPanel TargetPnl, Color BackColor, Color FontColor, string gubn)
        {
            TargetPnl.Controls.Clear();
            pnlSub.Controls.Clear();
            TargetPnl.Padding = new Padding(5);
            Panel SubPnl = new Panel();
            Button TargetBtn = new Button();
            Button TargetBtn2 = new Button();
            int BtnWidth = 120;
            int BtnHeight = 68;

            if (TargetDS != null && TargetDS.Tables[0].Rows.Count > 0)
            {
                // 데이터가 너무 많으면 경고
                if (TargetDS.Tables[0].Rows.Count > 2000)
                {
                    MessageBox.Show("데이터가 너무 많습니다!");
                    return;
                }

                G2_controls = new Control[TargetDS.Tables[0].Rows.Count];
                G4_controls = new Control[TargetDS.Tables[0].Rows.Count];
                G5_controls = new Control[TargetDS.Tables[0].Rows.Count];

                foreach (DataRow dr1 in TargetDS.Tables[0].Rows)
                {
                    if (dr1[0].ToString() != "0NEW") // 특수 코드 제외
                    {
                        // 패널 생성
                        SubPnl = new Panel();
                        SubPnl.Text = dr1[2].ToString();
                        SubPnl.Name = dr1[0].ToString();
                        SubPnl.Height = BtnHeight * 2;
                        SubPnl.Width = BtnWidth + 10;
                        SubPnl.BackColor = Color.DeepSkyBlue;
                        SubPnl.ForeColor = FontColor;
                        SubPnl.Location = new Point(10, 15);

                        // 메인 버튼 생성
                        TargetBtn = new Button();
                        TargetBtn.Text = dr1[1].ToString();
                        TargetBtn.Name = dr1[0].ToString();
                        TargetBtn.Tag = gubn;
                        TargetBtn.Height = BtnHeight;
                        TargetBtn.Width = BtnWidth;
                        TargetBtn.FlatStyle = FlatStyle.Flat;
                        TargetBtn.BackColor = BackColor;
                        TargetBtn.ForeColor = FontColor;
                        TargetBtn.Location = new Point(5, 8);

                        // QC DP 버튼 처리
                        if (rbCtn.Checked && gubn == "dp")
                        {
                            TargetBtn.Text = dr1[1].ToString(); // DP 값
                            TargetBtn.Name = dr1[2].ToString(); // 수량
                            TargetBtn.Tag = dr1[0].ToString();  // 아이템 코드
                            TargetBtn.Click += new EventHandler(QCdp_Click);
                        }
                        else
                        {
                            TargetBtn.Click += new EventHandler(SubBtn_Click);
                        }

                        // 폰트 크기 조정
                        if (gubn == "work")
                            TargetBtn.Font = new Font("Gulim", 19F, FontStyle.Bold);
                        else if (gubn == "inner" || gubn == "carton")
                            TargetBtn.Font = new Font("Gulim", 24F, FontStyle.Bold);
                        else
                            TargetBtn.Font = new Font("Gulim", 14F, FontStyle.Bold);

                        SubPnl.Controls.Add(TargetBtn);

                        // 서브 버튼 생성 (수량 표시)
                        TargetBtn2 = new Button();
                        TargetBtn2.Text = dr1[2].ToString();
                        TargetBtn2.Name = dr1[0].ToString();
                        TargetBtn2.Tag = gubn;
                        TargetBtn2.Height = BtnHeight - 20;
                        TargetBtn2.Width = BtnWidth;
                        TargetBtn2.FlatStyle = FlatStyle.Flat;
                        TargetBtn2.BackColor = Color.Black;
                        TargetBtn2.ForeColor = Color.White;
                        TargetBtn2.Location = new Point(5, BtnHeight + 8);
                        TargetBtn2.Click += new EventHandler(SubBtn_Click);
                        TargetBtn2.Font = new Font("Gulim", 14F, FontStyle.Bold);

                        SubPnl.Controls.Add(TargetBtn2);
                        TargetPnl.Controls.Add(SubPnl);

                        // 컨트롤 배열에 저장
                        G2_controls[TargetPnl.Controls.Count - 1] = TargetBtn;
                        G4_controls[TargetPnl.Controls.Count - 1] = SubPnl;
                        G5_controls[TargetPnl.Controls.Count - 1] = TargetBtn2;
                    }
                }
            }

            txtbarcode.Focus(); // 바코드 입력창에 포커스
        }

        /// <summary>
        /// DP(Degree of Power) 버튼 생성
        /// </summary>
        private void CallBtnKind_DP(DataSet TargetDS, FlowLayoutPanel TargetPnl, Color BackColor, Color FontColor, string gubn)
        {
            TargetPnl.Controls.Clear();
            TargetPnl.Padding = new Padding(5);
            Panel SubPnl = new Panel();
            int BtnHeight = 55;
            int BtnWidth = 100;

            if (TargetDS != null && TargetDS.Tables[0].Rows.Count > 0)
            {
                // 데이터가 너무 많으면 경고
                if (TargetDS.Tables[0].Rows.Count > 1000)
                {
                    MessageBox.Show("데이터가 너무 많습니다!");
                    return;
                }

                // "검사중" 표시 패널 생성
                if (cde.Selinnerupdate == "4")
                {
                    SubPnl = new Panel();
                    SubPnl.Text = "검사";
                    SubPnl.Name = "CHK";
                    SubPnl.Height = BtnHeight + 18;
                    SubPnl.Width = 280;
                    SubPnl.BackColor = SystemColors.ActiveBorder;
                    SubPnl.ForeColor = FontColor;
                    SubPnl.Location = new Point(10, 10);

                    Button TargetBtn = new Button();
                    TargetBtn.Text = "검사중";
                    TargetBtn.Height = BtnHeight;
                    TargetBtn.Width = 170;
                    TargetBtn.FlatStyle = FlatStyle.Flat;
                    TargetBtn.BackColor = Color.Yellow;
                    TargetBtn.ForeColor = FontColor;
                    TargetBtn.Location = new Point(8, 5);
                    TargetBtn.Font = new Font("Gulim", 16F, FontStyle.Bold);

                    SubPnl.Controls.Add(TargetBtn);
                }

                // 각 DP 데이터에 대한 버튼 생성
                foreach (DataRow dr1 in TargetDS.Tables[0].Rows)
                {
                    SubPnl = new Panel();
                    SubPnl.Text = dr1[2].ToString();
                    SubPnl.Name = dr1[0].ToString();
                    SubPnl.Height = BtnHeight + 10;
                    SubPnl.Width = 270;
                    SubPnl.BackColor = SystemColors.ActiveBorder;
                    SubPnl.ForeColor = FontColor;
                    SubPnl.Location = new Point(10, 15);

                    // DP 값 표시 버튼
                    Button TargetBtn = new Button();
                    TargetBtn.Text = dr1[1].ToString(); // DP 값
                    TargetBtn.Name = dr1[2].ToString(); // 수량
                    TargetBtn.Tag = dr1[0].ToString();  // 아이템 코드
                    TargetBtn.Height = BtnHeight;
                    TargetBtn.Width = 170;
                    TargetBtn.FlatStyle = FlatStyle.Flat;
                    TargetBtn.BackColor = BackColor;
                    TargetBtn.ForeColor = FontColor;
                    TargetBtn.Location = new Point(8, 5);

                    if (!rbCtn.Checked)
                    {
                        TargetBtn.Click += new EventHandler(QCdp_Click); // QC DP 클릭 이벤트
                    }

                    TargetBtn.Font = new Font("Gulim", 16F, FontStyle.Bold);
                    SubPnl.Controls.Add(TargetBtn);

                    // 수량 표시 버튼
                    Button TargetBtn2 = new Button();
                    TargetBtn2.Text = dr1[2].ToString(); // 수량
                    TargetBtn2.Name = dr1[0].ToString();
                    TargetBtn2.Tag = dr1[0].ToString();
                    TargetBtn2.Height = BtnHeight;
                    TargetBtn2.Width = 90;
                    TargetBtn2.FlatStyle = FlatStyle.Flat;
                    TargetBtn2.BackColor = Color.Black;
                    TargetBtn2.ForeColor = Color.White;
                    TargetBtn2.Location = new Point(177, 5);
                    TargetBtn2.Font = new Font("Gulim", 16F, FontStyle.Bold);

                    SubPnl.Controls.Add(TargetBtn2);
                    TargetPnl.Controls.Add(SubPnl);
                }
            }
        }

        #endregion

        #region 버튼 액션 메서드

        /// <summary>
        /// 그룹 버튼 클릭 이벤트
        /// </summary>
        public void GroupBtn_Click(object sender, EventArgs e)
        {
            int Cnt = pnlTop.Controls.Count;

            // 모든 버튼 색상 초기화
            for (int index = 0; index < Cnt; index++)
            {
                G1_controls[index].BackColor = SystemColors.Control;
            }

            Button btn = (Button)sender;
            btn.BackColor = SystemColors.ButtonHighlight; // 선택된 버튼 하이라이트
            GroupKind = btn.Tag.ToString();

            // 현재 LOT의 공정 상태 확인
            UserCommon.CmCn mc1 = new UserCommon.CmCn();
            string Qry1 = "select proc_kind from tst16m where saup_gubn='01' and lot_no='" + txtSubCode.Text + "'";
            DataSet ds1 = mc1.ResultReturnDataSet(Qry1);
            
            if (ds1.Tables[0].Rows.Count > 0)
            {
                int icur = Convert.ToInt16(ds1.Tables[0].Rows[0][0].ToString());
                // 공정 상태에 따라 그룹 설정
                if (icur >= 15)
                {
                    if (btn.Name == "3") cde.SelGroup = "6";
                    if (btn.Name == "4") cde.SelGroup = "5";
                }
                else
                    cde.SelGroup = btn.Name;
            }
            else
                cde.SelGroup = btn.Name;
        }

        /// <summary>
        /// 서브 버튼 클릭 이벤트 (고객, 아이템, 주문, 작업 등)
        /// </summary>
        public void SubBtn_Click(object sender, EventArgs e)
        {
            int Cnt = pnlMain.Controls.Count;
            Button btn = (Button)sender;

            // 버튼 선택 상태 표시
            for (int index = 0; index < Cnt; index++)
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

            // 태그가 없으면 기본값 설정
            if (btn.Tag == null)
            {
                btn.Tag = "dp";
                GroupKind = "dp";
                cde.SelGroup = "4";
            }

            // 버튼 종류에 따른 처리
            if (btn.Tag.ToString() == "work")
            {
                WorkBtn_Click(btn, e); // 작업 버튼 처리
            }
            else if (btn.Tag.ToString() == "carton")
            {
                btnCTNO.Text = btn.Text;
                btnCTNO.Tag = btn.Name;
                cde.SelDate = btnCTNO.Tag.ToString().Substring(0, 10);
                cde.SelWorkno = btnCTNO.Tag.ToString().Substring(10, 7);
                cde.SelCarton = btn.Text;

                // 카트론 선택 시 DP 목록 표시
                if (rbCtn.Checked && cde.SelWorkno != "")
                {
                    cde.SetCtnDPlist = GroupProc;
                    CallBtnKind_Sub(cde.GetCtnDPlistDS, pnlMain, SystemColors.Control, Color.Black, "dp");
                }
                else
                {
                    if (cde.SelWorkno != "")
                    {
                        cde.SetLotKind = "01";
                        cde.SelWH = btnWH.Text;
                        cde.SetCartonEnd = "1"; // 카트론 마감 처리
                    }
                    btnInner_Click(null, null); // 내부상자 조회
                }
            }
            else if (btn.Tag.ToString() == "inner")
            {
                // 내부상자 선택
                btnINNO.Text = btn.Text;
                btnINNO.Tag = btn.Name;
                cde.Selinnerno = btn.Text;
                btnINNO_Click(null, null);
            }

            txtbarcode.Focus();
        }

        /// <summary>
        /// QC DP 버튼 클릭 이벤트
        /// </summary>
        public void QCdp_Click(object sender, EventArgs e)
        {
            int Cnt = pnlMain.Controls.Count;
            Button btn = (Button)sender;

            if (btn.Tag.ToString() != "")
            {
                // 선택된 DP 정보 설정
                btnboxuse.Text = btn.Text; // DP 값
                nudChkQty.Value = Convert.ToInt32(btn.Name); // 수량
                btnQty.Text = btn.Name; // 수량 표시
                btnQty2.Tag = btn.Tag; // 아이템 코드
                cde.SelDp = btn.Tag.ToString(); // 선택된 DP

                // 카트론 모드일 때 선택 상태 표시
                if (rbCtn.Checked)
                {
                    for (int index = 0; index < Cnt; index++)
                    {
                        if (btn.Tag.ToString() != "")
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

                    // 내부상자 번호 목록 표시
                    cde.SetInnerNolist = GroupProc;
                    CallBtnKind_DP(cde.GetInnerNolistDS, pnlSub, SystemColors.Control, Color.Black, "inner");
                }
            }
        }

        /// <summary>
        작업 버튼 클릭 이벤트 (LOT 스캔)
        /// </summary>
        public void WorkBtn_Click(object sender, EventArgs e)
        {
            sbChangeGroup("new");
            Button btn = (Button)sender;
            txtbarcode.Text = btn.Text.ToString();

            // LOT 번호 길이 확인 (11자리)
            if (txtbarcode.Text.Length == 11)
            {
                txtSubCode.Text = txtbarcode.Text;

                // LOT 정보 조회 쿼리
                string Qry1 = "select lot_no,a.item_group,b.group_sdesc,c.cust_code,c.cust_name,a.order_no,a.proc_kind";
                Qry1 += " from tst16m a,cmv.dbo.tcb15 b,cmv.dbo.tcb01 c";
                Qry1 += " where a.saup_gubn = '01'";
                Qry1 += " and a.item_group = b.group_code";
                Qry1 += " and a.dest_cust = c.cust_code";
                Qry1 += " and a.lot_date between convert(varchar(10),dateadd(d,-90,convert(datetime,getdate())),102)";
                Qry1 += " and convert(varchar(10),dateadd(d,1,convert(datetime,getdate())),102)";
                Qry1 += " and a.lot_no = '" + txtSubCode.Text + "'";

                DataSet dr = conn.ResultReturnDataSet(Qry1);

                if (dr.Tables[0].Rows.Count == 0)
                {
                    MessageBox.Show("데이터가 없습니다! 주문을 확인하세요. " + txtbarcode.Text + " 알림!!!");
                    return;
                }

                txtbarcode.Text = "";

                // 조회된 데이터로 컨트롤 설정
                foreach (DataRow dr1 in dr.Tables[0].Rows)
                {
                    btnCustomer.Text = dr1[4].ToString(); // 고객명
                    btnCustomer.Name = dr1[3].ToString(); // 고객코드
                    cde.SelCustcode = dr1[3].ToString();

                    btnItems.Text = dr1[2].ToString(); // 아이템명
                    btnItems.Name = dr1[1].ToString(); // 아이템코드
                    cde.SelItems = dr1[1].ToString();

                    btnOrder.Text = dr1[5].ToString(); // 주문번호
                    btnOrder.Name = dr1[0].ToString(); // LOT번호
                    cde.SelOrder = dr1[5].ToString();
                    cde.SelLotno = dr1[0].ToString();

                    cde.SelProcKind = dr1[6].ToString(); // 공정종류
                    // 공정 종류에 따른 텍스트 설정
                    if (cde.SelProcKind == "13")
                        btnproc_kind.Text = "피킹 중";
                    else if (cde.SelProcKind == "15")
                        btnproc_kind.Text = "피킹 완료";
                    else if (cde.SelProcKind == "11")
                        btnproc_kind.Text = "창고 입고";
                }

                // 공정 설정
                if (cde.SelProcKind != "50")
                {
                    cde.SelUpdateproc = cde.SelProcKind;
                    cde.SetUpdateproc = cde.SelProcKind;
                    cde.SelGroup = "5";
                    cde.SetSubKind = cde.SelProcKind;
                }

                btnCarton_Click(null, null); // 카트론 조회
                btnboxuse_Click(null, null); // 박스 사용량 조회
            }
            else if (txtbarcode.Text.Length > 11)
            {
                txtbarcode.Text = "";
            }
            txtbarcode.Text = "";
        }

        /// <summary>
        /// 그룹 변경 메서드
        /// </summary>
        public void sbChangeGroup(string Group)
        {
            if (Group == "ALL")
            {
                // 전체 모드
                btnFactory.BackColor = SystemColors.Control;
                btnCustomer.BackColor = SystemColors.Control;
                btnItems.BackColor = SystemColors.Control;
                btnLookup.Enabled = true;
                GroupWork = "%";
            }
            else if (Group == "clear")
            {
                // 데이터 초기화
                DialogResult pcDR = MessageBox.Show("이전 데이터가 저장되지 않았습니다. 계속하시겠습니까?", "경고 - 확인", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                if (pcDR == DialogResult.OK)
                {
                    cde.Selinnerupdate = "4"; // 임시 수량 초기화
                    cde.SetInnerupdate = "4";

                    cde.SelDate = DateTime.Now.AddDays(0).ToString("yyyy.MM.dd");
                    cde.SelWorkno = "";
                    cde.SelDp = "";
                    cde.SelLotno = "";
                    cde.SelCarton = "";
                    cde.Selinnerno = "";

                    // 컨트롤 초기화
                    btnboxuse.Text = "DP";
                    nudChkQty.Value = 0;
                    btnQty.Text = "0";
                    btnCustomer.Text = "고객사";
                    btnItems.Text = "품목";
                    btnOrder.Text = "주문번호";
                    txtSubCode.Text = "";
                    btnCTNO.Text = "번호";
                    btnCTNO.Tag = null;
                    btnINNO.Text = "번호";
                    btnINNO.Tag = null;
                    btnQty1.Text = "0";
                    btnQty2.Text = "0";
                    btnCnt.Text = "0";
                    btnmod.Text = "0";
                    pnlMain.Controls.Clear();
                    pnlSub.Controls.Clear();
                    sbBoxsize(); // 박스 사이즈 재계산
                }
            }

            txtbarcode.Focus();
        }

        #endregion

        #region 데이터그리드뷰 처리

        /// <summary>
        /// 데이터그리드뷰 컬럼 설정
        /// </summary>
        private void setDG()
        {
            DelBadDG(); // 기존 데이터 삭제
            int gFieldNum = 0;
            dgv1.EnableHeadersVisualStyles = false;
            dgv1.ColumnHeadersDefaultCellStyle.Font = new Font("Gulim", 13, FontStyle.Bold);
            dgv1.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // 컬럼 추가
            uc.sbMakeDG(dgv1, "LOT NO", "text", 0, gFieldNum, "", SelectSS, "lot_no"); gFieldNum++;
            uc.sbMakeDG(dgv1, "품목", "text", 0, gFieldNum, "", SelectSS, "item_group"); gFieldNum++;
            uc.sbMakeDG(dgv1, "SPH", "text", 0, gFieldNum, "", SelectSS, "sph"); gFieldNum++;
            uc.sbMakeDG(dgv1, "CYL", "text", 0, gFieldNum, "", SelectSS, "cyl"); gFieldNum++;
            uc.sbMakeDG(dgv1, "DP", "text", 160, gFieldNum, "", SelectSS, "dp"); gFieldNum++;
            uc.sbMakeDG(dgv1, "수량", "text", 90, gFieldNum, "", SelectSS, "qty"); gFieldNum++;
        }

        /// <summary>
        /// 데이터그리드뷰 데이터 삭제
        /// </summary>
        private void DelBadDG()
        {
            dgv1.Rows.Clear();
        }

        /// <summary>
        /// 행 번호 표시
        /// </summary>
        private void dgv1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            Rectangle rectangle = new Rectangle(e.RowBounds.Location.X, e.RowBounds.Location.Y,
                dgv1.RowHeadersWidth, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(),
                dgv1.RowHeadersDefaultCellStyle.Font, rectangle,
                dgv1.RowHeadersDefaultCellStyle.ForeColor, TextFormatFlags.Right);
        }

        /// <summary>
        /// QC 데이터 조회
        /// </summary>
        private void GetQCData_Qty(string StrLot, string StrChulDate)
        {
            String qry = " select isnull(pick_lot,'')pick_lot,a.item_group,b.fore_degrees,b.back_degrees,b.fore_degrees +'/'+b.back_degrees as DP,b.lot_seq \n";
            qry += " from cmvn.cmv.dbo.tqt161m a,cmvn.cmv.dbo.tqt161d b \n";
            qry += " where a.Saup_Gubn = b.Saup_Gubn \n";
            qry += "   and a.insp_date = b.insp_date \n";
            qry += "   and a.insp_no = b.insp_no  \n";
            qry += "   and a.saup_gubn='01' \n";
            qry += "   and isnull(pick_lot,'') = '" + StrLot + "' \n";
            qry += "   and a.insp_date >= '" + StrChulDate + "' \n";
            qry += "  order by case when left(b.fore_degrees,1)='-' and left(b.back_degrees,1)='-' then '1' \n";
            qry += "       when left(b.fore_degrees,1)='+' and left(b.back_degrees,1)='+' then '2' \n";
            qry += "       when left(b.fore_degrees,1)='+' and left(b.back_degrees,1)='-' then '3' end ,b.back_degrees ,b.fore_degrees \n";
            GetWorkDateListView_view(qry);
        }

        /// <summary>
        /// 데이터그리드뷰에 데이터 표시
        /// </summary>
        private void GetWorkDateListView_view(String Qry)
        {
            if (String.IsNullOrEmpty(Qry)) return;

            SqlDataReader UserRec = conn.ResultReturnExecute(Qry);
            if (UserRec.HasRows)
            {
                while (UserRec.Read())
                {
                    String[] InsertData = new String[SelectSS.Count];
                    for (int j = 0; j < UserRec.FieldCount; j++)
                    {
                        InsertData[j] = UserRec.GetValue(j).ToString();
                    }
                    dgv1.Rows.Add(InsertData);
                }

                // 짝수행 배경색 변경
                if (dgv1.Rows.Count != 0)
                {
                    for (int ii = 0; ii < dgv1.Rows.Count - 1; ii++)
                    {
                        dgv1.Rows[ii].DefaultCellStyle.BackColor = Color.Gainsboro;
                        ii += 1;
                    }
                }
            }
            UserRec.Close();

            // 총 수량 계산
            decimal sumQty = 0;
            for (int li_r = 0; li_r < dgv1.Rows.Count; li_r++)
            {
                sumQty += Convert.ToInt32(dgv1.Rows[li_r].Cells[5].Value);
            }
            btnQty2.Text = sumQty.ToString();
        }

        #endregion

        #region 버튼 액션

        /// <summary>
        /// 카트론 버튼 클릭
        /// </summary>
        private void btnCarton_Click(object sender, EventArgs e)
        {
            GroupProc = "01";
            cde.SelGroup = "5";
            cde.SetCartonlist = GroupProc;
            btnboxuse.Text = "DP";
            nudChkQty.Value = 0;
            btnQty.Text = "0";
            CallBtnKind_Sub(cde.GetCartonlistDS, pnlMain, SystemColors.ActiveCaption, Color.Black, "carton");
            sbChangeGroup("ALL");
        }

        /// <summary>
        /// 내부상자 버튼 클릭
        /// </summary>
        private void btnInner_Click(object sender, EventArgs e)
        {
            btnboxuse.Text = "DP";
            nudChkQty.Value = 0;
            btnQty.Text = "0";

            // 카트론이 선택되지 않았으면 카트론 조회
            if (btnCTNO.Tag == null)
            {
                btnCarton_Click(null, null);
                cde.SetInnerlist = GroupProc;
                CallBtnKind_Sub(cde.GetInnerlistDS, pnlMain, Color.Teal, Color.White, "inner");
            }
            else
            {
                GroupProc = "01";
                cde.SelGroup = "6";
                cde.SelCarton = btnCTNO.Text;
                cde.SelDate = btnCTNO.Tag.ToString().Substring(0, 10);
                cde.SelWorkno = btnCTNO.Tag.ToString().Substring(10, 7);
                cde.SetInnerlist = GroupProc;
                CallBtnKind_Sub(cde.GetInnerlistDS, pnlMain, Color.Teal, Color.White, "inner");
                sbChangeGroup("ALL");
            }
        }

        /// <summary>
        /// 내부상자 번호 클릭
        /// </summary>
        private void btnINNO_Click(object sender, EventArgs e)
        {
            if (btnINNO.Tag != null)
            {
                GroupProc = "01";
                cde.SelGroup = "6";
                cde.SelCarton = btnCTNO.Text;
                cde.SelDate = btnCTNO.Tag.ToString().Substring(0, 10);
                cde.SelWorkno = btnCTNO.Tag.ToString().Substring(10, 7);
                cde.Selinnerno = btnINNO.Tag.ToString();

                cde.SetInnerdplist = GroupProc;
                CallBtnKind_DP(cde.GetInnerdplistDS, pnlSub, SystemColors.Control, Color.Black, "inner");
                sbChangeGroup("ALL");
            }
        }

        /// <summary>
        /// 박스 사용량 버튼 클릭
        /// </summary>
        private void btnboxuse_Click(object sender, EventArgs e)
        {
            GroupProc = "01";
            cde.SelGroup = "6";
            cde.Selinnerupdate = "4";
            cde.SetInnerdptemp = GroupProc;
            CallBtnKind_DP(cde.GetInnerdptempDS, pnlSub, SystemColors.Control, Color.Black, "inner");
            sbChangeGroup("ALL");
        }

        /// <summary>
        /// 그룹 버튼 클릭
        /// </summary>
        private void btnGroup_Click(object sender, EventArgs e)
        {
            GroupProc = "01";
            cde.SetGroupKind = GroupProc;
            CallBtnKind(cde.GetGroupDS, pnlTop, SystemColors.Control, Color.Black, "group");
            sbChangeGroup("ALL");
        }

        /// <summary>
        /// 공정 완료 버튼 클릭 (ERP 저장)
        /// </summary>
        private void btnproc_ok_Click(object sender, EventArgs e)
        {
            string Qry0 = "", insertLot = "", strIP = "", strQCDept = "", strDept = "", strInsp = "", strChul_date = "", strSuju = "";
            string strCust_Order = "", strItemGroup = "", strInspDate = "";
            UserCommon.CmCn mc1 = new UserCommon.CmCn();

            // LOT 날짜 포맷 변환
            string LotDate = "20" + cde.SelLotno.Substring(0, 2) + "." + cde.SelLotno.Substring(2, 2) + "." + cde.SelLotno.Substring(4, 2);

            // 공장별 부서 코드 설정
            if (Factory == "F11") strDept = "1";
            else if (Factory == "F12") strDept = "4";
            else if (Factory == "F21") strDept = "2";
            else if (Factory == "F22") strDept = "5";

            // LOT 기본 정보 조회
            string Qry = "select item_group,cust_order_no,shipping_date,suju_gubn from tst16m where saup_gubn='01' and lot_date='" + LotDate + "' and lot_no='" + cde.SelLotno + "'";
            DataSet ds = mc1.ResultReturnDataSet(Qry);
            if (ds.Tables[0].Rows.Count > 0)
            {
                strChul_date = ds.Tables[0].Rows[0][2].ToString(); // 출하일자
                strSuju = ds.Tables[0].Rows[0][3].ToString();      // 수주구분
                strCust_Order = ds.Tables[0].Rows[0][1].ToString(); // 고객주문번호
                strItemGroup = ds.Tables[0].Rows[0][0].ToString();  // 품목그룹
            }

            // 사용자 정보 조회
            if (User_empno.Trim() != "")
            {
                Qry0 = "select case isnull(g_ip,'00') when '' then '00' when '00' then '00' else g_ip end,dept_code,convert(varchar(10),getdate(),102) \n";
                Qry0 += " from thb01 where saup_gubn='01' and goout_gubn='1' and empno= '" + User_empno + "' \n";

                DataSet ds0 = mc1.ResultReturnDataSet(Qry0);
                if (ds0.Tables[0].Rows.Count > 0)
                {
                    strIP = ds0.Tables[0].Rows[0][0].ToString();     // IP
                    strQCDept = ds0.Tables[0].Rows[0][1].ToString(); // QC 부서
                    strInspDate = ds0.Tables[0].Rows[0][2].ToString(); // 검사일자
                }
            }

            // 데이터가 있을 경우 저장 처리
            if (dgv1.Rows.Count > 0)
            {
                DialogResult dr = MessageBox.Show("박스에 담으시겠습니까?", "★ 확인 ★", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (dr == DialogResult.OK)
                {
                    // 기존 검사 데이터 삭제
                    Qry = "select distinct a.insp_date,a.insp_no \n";
                    Qry += "From cmvn.cmv.dbo.tqt161m a,cmvn.cmv.dbo.tqt161d b \n";
                    Qry += "where a.insp_date = b.insp_date \n";
                    Qry += "  and a.insp_no = b.insp_no \n";
                    Qry += "  and a.saup_gubn = b.saup_gubn \n";
                    Qry += "  and a.saup_gubn = '01' \n";
                    Qry += "  and a.insp_date >= '" + LotDate + "' \n";
                    Qry += "  and a.chul_date = '" + strChul_date + "' \n";
                    Qry += "  and a.lot_no = '" + strCust_Order + "' \n";
                    Qry += "  and a.item_group = '" + strItemGroup + "' \n";
                    Qry += "  and a.pick_lot = '" + txtSubCode.Text + "' \n";

                    UserCommon.CmCn cnu = new UserCommon.CmCn();
                    DataSet dsu = cnu.ResultReturnDataSet(Qry);
                    if (dsu.Tables[0].Rows.Count > 0)
                    {
                        string sQry = "";
                        foreach (DataRow dr2 in dsu.Tables[0].Rows)
                        {
                            // 기존 검사 데이터 삭제
                            UserCommon.CmCn cnum = new UserCommon.CmCn();
                            sQry = "  delete from cmvn.cmv.dbo.tqt161m \n";
                            sQry += "  where saup_gubn ='01' \n";
                            sQry += "  and insp_date='" + dr2["insp_date"].ToString() + "' \n";
                            sQry += "  and insp_no = '" + dr2["insp_no"].ToString() + "' \n";
                            sQry += "  and chul_date = '" + strChul_date + "' \n";
                            sQry += "  and pick_lot = '" + txtSubCode.Text + "' \n";

                            sQry += "  delete from cmvn.cmv.dbo.tqt161d \n";
                            sQry += "  where saup_gubn ='01' \n";
                            sQry += "  and insp_date='" + dr2["insp_date"].ToString() + "' \n";
                            sQry += "  and insp_no = '" + dr2["insp_no"].ToString() + "' \n";

                            cnum.Execute(sQry);
                        }
                    }

                    // 새 검사번호 생성
                    UserCommon.CmCn cn = new UserCommon.CmCn();
                    strInsp = strDept + strIP + cn.StrResultReturnExecute(
                        "select substring(convert(char, convert(int, isnull(max(insp_no), '0')) + 10000001), 5, 4) \n" +
                        "from cmvn.cmv.dbo.tqt161m where saup_gubn='01' and insp_date='" + strInspDate + "' and left(insp_no,3)='" + strDept + strIP + "' ");

                    if (strInsp.Length < 7) strInsp = strDept + strIP + "0001";

                    // 검사 마스터 데이터 저장
                    UserCommon.CmCn cnm = new UserCommon.CmCn();
                    insertLot = "insert into cmvn.cmv.dbo.tqt161m(saup_gubn, insp_date, insp_no, chul_date, mcust_code, cust_code, item_group, qc_gubn, worker, pick_lot \n";
                    insertLot += " ,Aql_Gubn,lot_seq,lot_no,lot_code,ToInsp_Date,Group_Desc_QC)\n";
                    insertLot += " select saup_gubn,convert(varchar(10),getdate(),102),'" + strInsp + "',shipping_date,dest_cust,'" + strQCDept + "' \n";
                    insertLot += " ,item_group,'1','" + User_empno + "',lot_no ,case '" + strSuju + "' when '3' then '0' else '1' end," + btnQty2.Text + "  \n";
                    insertLot += " ,cust_order_no,picking_qty,convert(varchar(10),getdate(),102),convert(varchar(25),getdate(),21) \n";
                    insertLot += " from tst16m \n";
                    insertLot += " where saup_gubn='01' \n";
                    insertLot += "   and lot_date='" + LotDate + "' \n";
                    insertLot += "   and lot_no='" + cde.SelLotno + "' \n";

                    try
                    {
                        cnm.Execute(insertLot);
                    }
                    catch (Exception e0)
                    {
                        MessageBox.Show(e0.Message.ToString());
                    }

                    // 검사 상세 데이터 저장
                    for (int i = 0; i < dgv1.Rows.Count; i++)
                    {
                        UserCommon.CmCn cnd = new UserCommon.CmCn();
                        insertLot = "insert into cmvn.cmv.dbo.tqt161d(saup_gubn, insp_date, insp_no, insp_page, insp_serl  \n";
                        insertLot += "    ,item_code,  fore_degrees, back_degrees,  lot_seq,qty_a) \n";
                        insertLot += "values('01',convert(varchar(10),getdate(),102),'" + strInsp + "' \n";
                        insertLot += "  , '0001',substring(convert(char, cast(" + i + " + 10001 as int)), 2, 4) \n";
                        insertLot += "  ,'" + dgv1.Rows[i].Cells[1].Value.ToString() + "' \n";
                        insertLot += "  ,'" + dgv1.Rows[i].Cells[2].Value.ToString() + "' \n";
                        insertLot += "  ,'" + dgv1.Rows[i].Cells[3].Value.ToString() + "' \n";
                        insertLot += "  ,'" + dgv1.Rows[i].Cells[5].Value.ToString() + "' \n";
                        insertLot += "  ,'" + dgv1.Rows[i].Cells[5].Value.ToString() + "') \n";

                        try
                        {
                            cnd.Execute(insertLot);
                        }
                        catch (Exception e1)
                        {
                            MessageBox.Show(e1.Message.ToString());
                        }
                    }

                    MessageBox.Show("ERP에 저장되었습니다.", "알림");
                    btnboxuse.Text = "DP";
                    nudChkQty.Value = 0;
                    btnQty.Text = "0";
                }
                else
                {
                    MessageBox.Show("취소되었습니다!");
                }
            }
            else
            {
                MessageBox.Show("데이터가 없습니다. 오류", "오류");
                return;
            }
        }

        /// <summary>
        /// 바코드 입력 처리
        /// </summary>
        private void txtbarcode_KeyDown(object sender, KeyEventArgs e)
        {
            Select_Mode = true;
            if (Select_Mode == true) // 등록 모드
            {
                if (e.KeyCode == Keys.Enter)
                {
                    // 기존 데이터가 있을 경우 확인
                    if (txtSubCode.Text != "" && Convert.ToInt32(btnQty1.Text) > 0)
                    {
                        DialogResult pcDR = MessageBox.Show("이전 LOT가 저장되지 않았습니다. 계속하시겠습니까?", "경고 - 확인", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                        if (pcDR == DialogResult.OK)
                        {
                            txtbarcode.Text = "";
                            sbChangeGroup("clear"); // 데이터 초기화
                        }
                        else
                        {
                            txtSubCode.Text = "";
                            txtbarcode.Text = "";
                            sbChangeGroup("clear");
                        }
                    }
                    else // 새 작업
                    {
                        sbChangeGroup("clear");
                        if (txtbarcode.Text.Length == 11) // LOT 번호 확인
                        {
                            txtSubCode.Text = txtbarcode.Text;

                            // LOT 정보 조회
                            string Qry1 = "select lot_no,a.item_group,b.group_sdesc,c.cust_code,c.cust_name \n";
                            Qry1 += " ,a.order_no,a.proc_kind \n";
                            Qry1 += " ,(select opt_name from tst16c where opt_type='02' and opt_code=a.proc_kind)proc_desc  \n";
                            Qry1 += " ,io_code,a.order_no,a.lot_date \n";
                            Qry1 += " from tst16m a,cmv.dbo.tcb15 b,cmv.dbo.tcb01 c \n";
                            Qry1 += " where a.saup_gubn = '01' \n";
                            Qry1 += " and a.item_group = b.group_code \n";
                            Qry1 += " and a.dest_cust = c.cust_code \n";
                            Qry1 += " and a.lot_date between convert(varchar(10),dateadd(d,-90,convert(datetime,getdate())),102) and convert(varchar(10),dateadd(d,1,convert(datetime,getdate())),102) \n";
                            Qry1 += " and a.lot_no = '" + txtSubCode.Text + "'";

                            DataSet dr = conn.ResultReturnDataSet(Qry1);

                            if (dr.Tables[0].Rows.Count == 0)
                            {
                                MessageBox.Show("****데이터 없음*******!!! " + txtbarcode.Text + "알림!!!");
                                return;
                            }

                            txtbarcode.Text = "";

                            // 조회된 정보 설정
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
                                cde.SelProcKind = dr1["proc_kind"].ToString();
                                cde.SelLotDate = dr1["lot_date"].ToString();
                                btnproc_kind.Text = dr1["proc_desc"].ToString();
                            }

                            if (cde.SelProcKind != "50")
                            {
                                cde.SelUpdateproc = cde.SelProcKind;
                            }

                            // QC 데이터 조회
                            string Qry3 = "select min(chul_date)chul_date \n";
                            Qry3 += " from tst13m b \n";
                            Qry3 += " where saup_gubn = '01' \n";
                            Qry3 += " and order_no = '" + cde.SelOrder + "' \n";
                            Qry3 += " and chul_date >='" + cde.SelLotDate + "' \n";
                            Qry3 += " and lot_no = '" + txtSubCode.Text + "'";

                            DataSet dr3 = conn.ResultReturnDataSet(Qry3);

                            if (dr3.Tables[0].Rows.Count > 0)
                            {
                                string strChul_date = dr3.Tables[0].Rows[0]["chul_date"].ToString();
                                DelBadDG();
                                GetQCData_Qty(txtSubCode.Text, strChul_date);
                            }

                            if (cde.SelProcKind != "50")
                            {
                                cde.SelGroup = "5";
                            }
                        }
                        else if (txtbarcode.Text.Length > 11)
                        {
                            txtbarcode.Text = "";
                        }
                        txtbarcode.Text = "";
                    }
                }
            }
        }

        #endregion

        #region QC 처리

        /// <summary>
        /// 검사 데이터 추가 (박스 비우기)
        /// </summary>
        private void btnboxempty_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            String Qry = "", strItemCode = "";
            int ll_row = 0;

            // 중복 DP 확인
            if (btnboxuse.Text != "" && dgv1.Rows.Count > 0)
            {
                for (int li = 0; li < dgv1.Rows.Count; li++)
                {
                    strItemCode = dgv1.Rows[li].Cells[4].Value.ToString();
                    if (strItemCode.Trim().ToUpper() == btnboxuse.Text.Trim().ToString().ToUpper())
                    {
                        ll_row++;
                    }
                }
            }

            // DP와 수량이 있을 경우 데이터 추가
            if (btnboxuse.Text.Length > 10 && Convert.ToInt32(btnQty.Text) > 0)
            {
                Qry = " select '" + cde.SelLotno + "' lot_no ,'" + cde.SelItems + "' item_group ,fore_degrees, back_degrees,'" + btnboxuse.Text + "' dp,cast('" + btnQty.Text + "' as int) qty \n";
                Qry += " from tcb02   \n";
                Qry += "where item_gubn='01'  \n";
                Qry += "  and item_code='" + btnQty2.Tag.ToString() + "'  \n";

                GetWorkDateListView_view(Qry);
            }
        }

        /// <summary>
        /// 엑셀 출력
        /// </summary>
        private void btnPrintSheet_Click(object sender, EventArgs e)
        {
            if (dgv1.RowCount == 0)
            {
                MessageBox.Show("DP 수량을 선택하세요.");
                return;
            }
            else if (cde.SelLotno == "")
            {
                MessageBox.Show("LOT를 스캔한 후 EXCEL을 클릭하세요.");
                return;
            }
            else if (dgv1.RowCount > 0 && cde.SelItems != "")
            {
                // 품목 표준 수량 확인
                string qry = "select isnull(e_group8,'')e_group8,group_sdesc from tcb15 where group_code='" + cde.SelItems + "' ";
                UserCommon.CmCn mc = new UserCommon.CmCn();
                DataSet ds = mc.ResultReturnDataSet(qry);
                if (ds.Tables[0].Rows.Count > 0)
                {
                    if (ds.Tables[0].Rows[0][0].ToString() == "")
                    {
                        MessageBox.Show("이 품목은 표준 수량이 없습니다!!  \r\n\n" + ds.Tables[0].Rows[0][1].ToString(), "알림");
                        return;
                    }
                    else
                    {
                        DPsheet.sbCheckdpsheet(cde.SelLotno, cde.SelOrder, cde.SelItems); // 엑셀 생성
                    }
                }
                else
                {
                    MessageBox.Show("이 품목은 정보가 없습니다！" + ds.Tables[0].Rows[0][1].ToString(), "알림-");
                    return;
                }
            }
        }

        /// <summary>
        /// 행 삭제
        /// </summary>
        private void btnDelete_Click(object sender, EventArgs e)
        {
            int RowNumber = 0;
            if (dgv1.CurrentCell == null)
            {
                return;
            }
            RowNumber = dgv1.CurrentCell.RowIndex;
            if (MessageBox.Show("삭제하시겠습니까？", "삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                dgv1.Rows.RemoveAt(RowNumber);
            }
        }

        #endregion

        #region 기타 메서드

        /// <summary>
        /// 박스 사이즈 계산
        /// </summary>
        public void sbBoxsize()
        {
            if (btnBase.Text != "")
            {
                double boxsize = Convert.ToDouble(pnlboxview.Width);
                double lensize = boxsize / Convert.ToDouble(btnBase.Text);

                if (Convert.ToDouble(btnmod.Text) == 0)
                {
                    btnboxuse.Width = 0;
                }
                boxuse = Convert.ToDouble(btnboxuse.Width);
            }
        }

        /// <summary>
        /// 수량 버튼 클릭 (터치패드 호출)
        /// </summary>
        private void btnQty_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            FrmTouch = new TouchPad("qty");
            FrmTouch.OnNotifyParent += new AddBadKindEventHandler(ChildFrom_OnNotifyParent4);
            FrmTouch.ShowDialog();

            if (DPQTY != "")
            {
                btnQty.Text = DPQTY;
            }
        }

        /// <summary>
        /// 수량 변경 시 검증
        /// </summary>
        private void btnQty_TextChanged(object sender, EventArgs e)
        {
            if (Convert.ToInt32(btnQty.Text) > nudChkQty.Value)
            {
                MessageBox.Show("박스에 담는 수량이 피킹 수량보다 많습니다.", "알림-");
                btnQty.Text = "0";
                return;
            }
        }

        /// <summary>
        /// 카트론 모드 변경
        /// </summary>
        private void rbCtn_CheckedChanged(object sender, EventArgs e)
        {
            InitFrm();
            sbChangeGroup("clear");
        }

        /// <summary>
        /// 박스 모드 변경
        /// </summary>
        private void rbBox_CheckedChanged(object sender, EventArgs e)
        {
            InitFrm();
            sbChangeGroup("clear");
        }

        #endregion
    }

    /// <summary>
    /// 카트론 DP 리스트 클래스 (현재 미사용)
    /// </summary>
    class CLSPRO
    {
        private string CalCtnDPlist = "";
        public string SelOrder = "";
        public string SelLotno = "";
        public string SelCarton = "";
        public string SelDate = "";
        public string SelWorkno = "";
        private DataSet CtnDPlistDS = null;
        UserCommon.CmCn conn = new UserCommon.CmCn();
    }

} // namespace
