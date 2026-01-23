using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.IO;

namespace MES_WMS
{
    public partial class PRF08 : Form
    {
        // 클래스 멤버 변수 선언
        CLSEMPNO ce = new CLSEMPNO();
        CLSINNERBOXLIST cde = new CLSINNERBOXLIST();
        FrmEmp childForm = null;
        SqlDataReader sr = null;
        
        // 사용자 정보 및 설정 변수
        private string Factory = UserCommon.Public_Function.user_Factory;
        private string User_Group = MES_WMS.UserCommon.Public_Function.user_Group;
        private string User_empno = UserCommon.Public_Function.user_Empno;
        public string SelDateF = DateTime.Now.AddDays(-10).ToString("yyyy.MM.dd");
        public string SelDateT = DateTime.Now.AddDays(1).ToString("yyyy.MM.dd");
        UserCommon.CmCn mc = new MES_WMS.UserCommon.CmCn();
        UserCommon.ComCls uc = new MES_WMS.UserCommon.ComCls();
        UserCommon.ClsQryToListView ucqtl = new MES_WMS.UserCommon.ClsQryToListView();
        private string Factdept = MES_WMS.UserCommon.Public_Function.user_Dept;

        private string fileSysPath = "";
        private string fileini = @"\CMES\config_mes.ini";
        private bool type = Environment.Is64BitOperatingSystem;

        private int proc_cnt = 0;
        private int next_cnt = 0;
        private string DeptType = "";
        private string strDept = "";

        public PRF08()
        {
            InitializeComponent();
            InitFrm();
            sbListViewHead();
        }

        /// <summary>
        /// 폼 초기화 함수
        /// 모든 컨트롤을 기본 상태로 설정
        /// </summary>
        private void InitFrm()
        {
            // 라벨 및 텍스트 박스 초기화
            lblDisp.Text = "";
            lblLotNo.Text = "";
            txtbarcode.Text = "";
            
            // 물류 정보 라벨 초기화
            lblCustTxt.Text = "";
            lblItemTxt.Text = "";
            lblShipDateTxt.Text = "";
            lblShipTypeTxt.Text = "";
            lblPackTypeTxt.Text = "";
            lblMarkTxt.Text = "";
            lblRemarkTxt.Text = "";
            lblOrderTxt.Text = "";
            lblOrdQtyTxt.Text = "";
            lblERPOrderTxt.Text = "";
            lblQualityTxt.Text = "";
            lblEnvelopKindTxt.Text = "";
            lblPackMarkTxt.Text = "";
            lblOrdAgentTxt.Text = "";
            
            // 창고 정보 라벨 초기화
            lblpickertxt.Text = "";
            lblpicktimetxt.Text = "";
            lblpickqtytxt.Text = "";
            lblpickboxtxt.Text = "";
            lblQtyAtxt.Text = "";
            lblQtyBtxt.Text = "";
            lblQtyCtxt.Text = "";
            lblQtyEtctxt.Text = "";
            lblQtyFristtxt.Text = "";
            lblQtySecondtxt.Text = "";
            lblQtyThirdtxt.Text = "";
            lblQtyFourthtxt.Text = "";
            txtPickRmarktxt.Text = "";
            txtPickRmarktxt.BackColor = btnStk.BackColor;
            
            // 포장 정보 라벨 초기화
            lblpackertxt.Text = "";
            lblpacktimetxt.Text = "";
            lblpackmchtxt.Text = "";
            lblrectimetxt.Text = "";
            lblpackremarktxt.Text = "";
            lblinspmantxt.Text = "";
            
            // QC 정보 라벨 초기화
            lblinspectortxt.Text = "";
            lblinsp_timetxt.Text = "";
            lblinsp_resulttxt.Text = "";
            txtinsp_error.Text = "";
            lblinsp_typetxt.Text = "";
            lblinsp_qtytxt.Text = "";
            lblinsp_packtxt.Text = "";
            lblinsp_marktxt.Text = "";
            lblinsp_remarktxt.Text = "";
            lblinsp_agenttxt.Text = "";
            txtinsp_error.BackColor = btnQC.BackColor;

            // 디스플레이 라벨 초기화
            lbldispcust.Text = "";
            lbldisppdc.Text = "";
            lbldispcstpo.Text = "";
            lbldisplot.Text = "";
            lbldisplotqty.Text = "";
            lbldisperppo.Text = "";
            pbpic.Image = null;

            // 그룹 박스 가시성 설정
            gbLogistics.Visible = false;
            gbwarehouse.Visible = false;
            gbpack.Visible = false;
            gbQC.Visible = false;
            gbdisp.Visible = false;
            gbStkProc.Visible = false;
            
            // 사용자 정보 설정
            txtEmpno.Text = User_empno;
            txtKname.Text = MES_WMS.UserCommon.Public_Function.user_Name;

            timer1.Enabled = true;
            if (!gbStkProc.Visible && DeptType == "11") gbStkProc.Visible = true;
            BtnLotProc.Text = "Lookup";
        }

        /// <summary>
        /// 리스트뷰 헤더 설정 함수
        /// ListView 컨트롤의 컬럼 헤더를 초기화
        /// </summary>
        private void sbListViewHead()
        {
            listMaster.Clear();
            uc.sbListViewInit(listMaster, false);
            
            // 컬럼 헤더 추가
            uc.ListViewHeadInit(listMaster, 80, 0, "PickDate", "Text");
            uc.ListViewHeadInit(listMaster, 90, 0, "BoxNo.", "Text");
            uc.ListViewHeadInit(listMaster, 80, 0, "WorkDate", "Text");         
            uc.ListViewHeadInit(listMaster, 90, 0, "Inner No", "Text");
            uc.ListViewHeadInit(listMaster, 80, 0, "Inbox", "Text");
            uc.ListViewHeadInit(listMaster, 80, 0, "Worker", "Text");
            uc.ListViewHeadInit(listMaster, 150, 0, "WorkTime", "Text");
            uc.ListViewHeadInit(listMaster, 0, 0, "ETC", "Text");
        }

        /// <summary>
        /// 물류(DWL) 버튼 클릭 이벤트 핸들러
        /// 물류 관련 화면 구성 및 데이터 로드
        /// </summary>
        private void btnDWL_Click(object sender, EventArgs e)
        {
            next_cnt = 0;
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            dellabel();
            gbdisp.Visible = false;

            // 물류 그룹박스 설정
            gbLogistics.Visible = true;
            gbLogistics.BackColor = btnDWL.BackColor;
            gbLogistics.Location = new Point(491, 145);
            gbLogistics.Size = new Size(520, 189);
            
            // 다른 그룹박스 숨김
            gbwarehouse.Visible = false;
            gbpack.Visible = false;
            gbQC.Visible = false;
            gbStkProc.Visible = false;
            
            btnDWL.FlatStyle = FlatStyle.Flat;
            DeptType = "01";
            
            // LOT 번호 조회
            if (rbLot.Checked)
            {
                GetLotNum(DeptType, next_cnt, 15);
            }
            
            strDept = btnDWL.Text;
        }

        /// <summary>
        /// 창고(Stk) 버튼 클릭 이벤트 핸들러
        /// 창고 관련 화면 구성 및 데이터 로드
        /// </summary>
        private void btnStk_Click(object sender, EventArgs e)
        {
            next_cnt = 0;
            txtbarcode.Text = "";
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            dellabel();
            
            // 디스플레이 그룹박스 설정
            gbdisp.Visible = true;
            gbdisp.Location = new Point(491, 135);
            gbdisp.Size = new Size(520, 91);
            
            // 창고 그룹박스 설정
            gbwarehouse.Visible = true;
            gbwarehouse.Location = new Point(491, 228);
            gbwarehouse.Size = new Size(520, 138);
            gbwarehouse.BackColor = btnStk.BackColor;

            // LOT 처리 프로세스 설정
            if (BtnLotProc.Text == "OK")
            {
                gbStkProc.Visible = true;
                gbStkProc.Location = new Point(491, 368);
                gbStkProc.Size = new Size(520, 43);
            }

            // 다른 그룹박스 숨김
            gbLogistics.Visible = false;
            gbpack.Visible = false;
            gbQC.Visible = false;
            btnStk.FlatStyle = FlatStyle.Flat;

            DeptType = "11";
            
            // LOT 번호 조회
            if (rbLot.Checked)
            {
                GetLotNum(DeptType, next_cnt, 15);
            }
            
            strDept = btnStk.Text;
        }

        /// <summary>
        /// 포장(DPack) 버튼 클릭 이벤트 핸들러
        /// 포장 관련 화면 구성 및 데이터 로드
        /// </summary>
        private void btnDPack_Click(object sender, EventArgs e)
        {
            next_cnt = 0;
            txtbarcode.Text = "";
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            dellabel();
            
            // 디스플레이 그룹박스 설정
            gbdisp.Visible = true;
            gbdisp.Location = new Point(491, 135);
            gbdisp.Size = new Size(520, 91);
            
            // 포장 그룹박스 설정
            gbpack.Visible = true;
            gbpack.Location = new Point(491, 228);
            gbpack.BackColor = btnDPack.BackColor;
            gbpack.Size = new Size(517, 183);
            
            // 다른 그룹박스 숨김
            gbLogistics.Visible = false;
            gbwarehouse.Visible = false;
            gbQC.Visible = false;
            gbStkProc.Visible = false;
            btnDPack.FlatStyle = FlatStyle.Flat;

            DeptType = "21";
            
            // LOT 번호 조회
            if (rbLot.Checked)
            {
                GetLotNum(DeptType, next_cnt, 15);
            }
            
            strDept = btnDPack.Text;
        }

        /// <summary>
        /// QC 버튼 클릭 이벤트 핸들러
        /// QC 관련 화면 구성 및 데이터 로드
        /// </summary>
        private void btnQC_Click(object sender, EventArgs e)
        {
            next_cnt = 0;
            txtbarcode.Text = "";
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            dellabel();
            
            // 디스플레이 그룹박스 설정
            gbdisp.Visible = true;
            gbdisp.Location = new Point(491, 135);
            gbdisp.Size = new Size(520, 91);
            
            // QC 그룹박스 설정
            gbQC.Visible = true;
            gbQC.Location = new Point(491, 228);
            gbQC.Size = new Size(520, 176);
            gbQC.BackColor = btnQC.BackColor;
            
            // 다른 그룹박스 숨김
            gbLogistics.Visible = false;
            gbwarehouse.Visible = false;
            gbpack.Visible = false;
            gbStkProc.Visible = false;

            btnQC.FlatStyle = FlatStyle.Flat;
            DeptType = "31";
            
            // LOT 번호 조회
            if (rbLot.Checked)
            {
                GetLotNum(DeptType, next_cnt, 15);
            }
            
            strDept = btnQC.Text;
        }

        /// <summary>
        /// 출하(Dlv) 버튼 클릭 이벤트 핸들러
        /// 출하 관련 화면 구성 및 데이터 로드
        /// </summary>
        private void btnDlv_Click(object sender, EventArgs e)
        {
            next_cnt = 0;
            txtbarcode.Text = "";
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            dellabel();
            
            // 디스플레이 그룹박스 설정
            gbdisp.Visible = true;
            gbdisp.Location = new Point(491, 135);
            gbdisp.Size = new Size(520, 91);
            
            // 다른 그룹박스 숨김
            gbLogistics.Visible = false;
            gbwarehouse.Visible = false;
            gbpack.Visible = false;
            gbQC.Visible = false;
            gbStkProc.Visible = false;
            
            DeptType = "41";
            
            // LOT 번호 조회
            if (rbLot.Checked)
            {
                GetLotNum(DeptType, next_cnt, 15);
            }
            
            strDept = btnDlv.Text;
        }

        /// <summary>
        /// LOT 번호 조회 함수
        /// 프로세스 유형에 따라 LOT 데이터를 조회하고 버튼 생성
        /// </summary>
        /// <param name="strProc">프로세스 유형 코드</param>
        /// <param name="li_page">페이지 번호</param>
        /// <param name="lirow">페이지당 행 수</param>
        private void GetLotNum(string strProc, int li_page, int lirow)
        {
            string qry = "", qry1 = "", strprocess0 = "", strprocess1 = "";
            
            // 프로세스 유형에 따른 조건 설정
            if (strProc == "01")
            {
                strprocess0 = " (proc_kind ='01') ";
                strprocess1 = " (a.proc_kind ='01') ";
            }
            else if (strProc == "11")
            {
                strprocess0 = " (proc_kind >='11' and proc_kind <'19') ";
                strprocess1 = " (a.proc_kind >='11' and a.proc_kind <'19') ";
            }
            else if (strProc == "21")
            {
                strprocess0 = " ((proc_kind ='19' and repacking='Y') or proc_kind ='21')  ";
                strprocess1 = " ((a.proc_kind ='19' and a.repacking='Y') or a.proc_kind ='21')  ";
            }
            else if (strProc == "31")
            {
                strprocess0 = " ((proc_kind ='19' and repacking='N') or proc_kind ='29'or proc_kind ='31' )";
                strprocess1 = " ((a.proc_kind ='19' and a.repacking='N') or a.proc_kind ='29' or a.proc_kind ='31' )";
            }
            else if (strProc == "41")
            {
                strprocess0 = " (proc_kind ='39' or proc_kind ='41' )";
                strprocess1 = " (a.proc_kind ='39' or a.proc_kind ='41' )";
            }
            
            // 총 데이터 수 조회
            qry1 = "select lot_no,count(*) from tst16m  where saup_gubn='01' and " + strprocess0 + "  group by lot_no ";
            UserCommon.CmCn mc = new UserCommon.CmCn();

            DataSet ds1 = mc.ResultReturnDataSet(qry1);
            if (ds1.Tables[0].Rows.Count > 0)
            {
                // 페이지 정보 계산
                if (ds1.Tables[0].Rows.Count % lirow == 0 && ds1.Tables[0].Rows.Count / lirow >= li_page + 1)
                {
                    proc_cnt = ds1.Tables[0].Rows.Count / lirow;
                    lblDisp.Text = "Total:" + Math.Ceiling(Convert.ToDecimal(ds1.Tables[0].Rows.Count / lirow)).ToString() + "/" + Convert.ToInt16(li_page + 1) + "页";
                }
                else if (Convert.ToDecimal((ds1.Tables[0].Rows.Count / lirow) + 1) >= li_page + 1)
                {
                    proc_cnt = (ds1.Tables[0].Rows.Count / lirow) + 1;
                    lblDisp.Text = "Total:" + Math.Ceiling(Convert.ToDecimal((ds1.Tables[0].Rows.Count / lirow) + 1)).ToString() + "/" + Convert.ToInt16(li_page + 1) + "页";
                }
            }

            // LOT 데이터 조회 쿼리
            qry = "select top " + lirow + "  a.lot_no, d.group_sdesc, c.cust_sname,a.order_no,' Qty:' + convert(varchar(20), a.lot_qty),proc_kind  \n";
            qry += " from tst16m a, tcb15 d ,cmv.dbo.tcb01 c \n";
            qry += "where a.saup_gubn='01' \n";
            qry += "  and a.item_group = d.group_code \n";
            qry += "  and a.dest_cust=c.cust_code \n";
            qry += "  and " + strprocess1 + " \n";
            qry += "  and a.lot_no not in (select top (" + li_page + "*" + lirow + ") lot_no from tst16m \n";
            qry += "                        where saup_gubn='01' \n";
            qry += "                          and " + strprocess0 + "\n";
            qry += "                          order by lot_no desc) \n";
            qry += "order by a.lot_no desc  ";

            DataSet ds = mc.ResultReturnDataSet(qry);
            if (ds.Tables[0].Rows.Count > 0)
            {
                // LOT 버튼 생성
                CallBtnKind(ds, pnlLot, SystemColors.Control, Color.Black, "group");
            }
            else if (ds.Tables[0].Rows.Count == 0)
            {
                MessageBox.Show("No Data!");
                return;
            }
        }

        /// <summary>
        /// 아이템 번호 조회 함수
        /// 특정 LOT의 아이템 데이터를 조회
        /// </summary>
        /// <param name="strProc">프로세스 유형 코드</param>
        /// <param name="li_page">페이지 번호</param>
        /// <param name="lirow">페이지당 행 수</param>
        /// <param name="strLot">LOT 번호</param>
        private void GetItemNum(string strProc, int li_page, int lirow, string strLot)
        {
            string qry = "", strprocess1 = "";
            
            // 프로세스 유형에 따른 조건 설정
            if (strProc == "01")
            {
                strprocess1 = " (a.proc_kind ='01') ";
            }
            else if (strProc == "11")
            {
                strprocess1 = " (a.proc_kind >='11' and a.proc_kind <'19') ";
            }
            else if (strProc == "21")
            {
                strprocess1 = " ((a.proc_kind ='19' and a.repacking='Y') or a.proc_kind ='21')  ";
            }
            else if (strProc == "31")
            {
                strprocess1 = " ((a.proc_kind ='19' and a.repacking='N') or a.proc_kind ='29' or a.proc_kind ='31' )";
            }
            else if (strProc == "41")
            {
                strprocess1 = " (a.proc_kind ='39' or a.proc_kind ='41' )";
            }
            
            // 아이템 데이터 조회 쿼리
            qry = " select b.item_code  ,item_spec,' Qty:' + convert(varchar(20), b.order_qty),a.lot_no,c.group_sdesc \n";
            qry += "  from tst16m a,tst16d b,tcb15 c,tcb02 d   \n";
            qry += "  where a.saup_gubn = '01'    \n";
            qry += "    and a.saup_gubn = b.saup_gubn  \n";
            qry += "    and a.lot_date = b.lot_date \n";
            qry += "    and " + strprocess1 + "  \n";
            qry += "    and a.lot_no = b.lot_no  \n";
            qry += "    and a.item_group = c.group_code \n";
            qry += "    and b.item_code=d.item_code \n";
            qry += "    and d.item_gubn='01' \n";
            qry += "    and a.lot_no='" + strLot + "' \n";
            qry += "order by  substring(b.item_code,8,1),d.back_degrees ,fore_degrees";
            
            UserCommon.CmCn mc = new UserCommon.CmCn();
            DataSet ds = mc.ResultReturnDataSet(qry);
            if (ds.Tables[0].Rows.Count > 0)
            {
                // 서브 버튼 생성
                CallBtnKind_Sub(ds, pnlLot, SystemColors.Control, Color.Black, "sub");
            }
            else if (ds.Tables[0].Rows.Count == 0)
            {
                // 프로세스 정보 확인
                string Qry0 = "select proc_kind,opt_name from tst16m a,tst16c b where saup_gubn='01' and b.opt_type='02' and a.proc_kind=b.opt_code and lot_no='" + strLot + "'";
                DataSet ds0 = mc.ResultReturnDataSet(Qry0);
                if (ds0.Tables[0].Rows.Count > 0)
                {
                    MessageBox.Show(strDept + "No Data!!" + ds0.Tables[0].Rows[0][1].ToString());
                    return;
                }
            }
        }

        /// <summary>
        /// 내부 박스 번호 조회 함수
        /// 내부 박스 번호로 아이템 데이터를 조회
        /// </summary>
        /// <param name="lotno">내부 박스 번호</param>
        private void GetInnerNum(string lotno)
        {
            string qry = "";

            qry = " select b.item_code  ,item_spec,' Qty:' + convert(varchar(20), b.qty),a.order_no,c.group_sdesc \n";
            qry += "  from tst13m a,tst13e b,tcb15 c,tcb02 d   \n";
            qry += "  where a.saup_gubn = '01'    \n";
            qry += "    and a.chul_date >= convert(varchar(10),dateadd(d,-45,getdate()),102)    \n";
            qry += "    and a.saup_gubn = b.saup_gubn  \n";
            qry += "    and a.chul_date = b.chul_date \n";
            qry += "    and a.chul_no = b.chul_no  \n";
            qry += "    and a.item_group = c.group_code \n";
            qry += "    and b.item_code = d.item_code \n";
            qry += "    and d.item_gubn = '01' \n";
            qry += "    and b.inner_no='" + lblLotNo.Text  + "' \n";
            qry += "order by  substring(b.item_code,8,1),d.back_degrees ,fore_degrees";
            
            UserCommon.CmCn mc = new UserCommon.CmCn();
            DataSet ds = mc.ResultReturnDataSet(qry);
            if (ds.Tables[0].Rows.Count > 0)
            {
                // 서브 버튼 생성
                CallBtnKind_Sub(ds, pnlLot, SystemColors.Control, Color.Black, "sub");
            }
            else if (ds.Tables[0].Rows.Count == 0)
            {
                MessageBox.Show(strDept + "No data");
                return;
            }
        }

        /// <summary>
        /// 바코드 입력 텍스트 변경 이벤트 핸들러
        /// </summary>
        private void txtbarcode_TextChanged(object sender, EventArgs e)
        {
            // 텍스트 변경 시 추가 처리 없음
        }

        /// <summary>
        /// 바코드 입력 키다운 이벤트 핸들러
        /// 엔터 키 입력 시 내부 박스 조회 및 처리
        /// </summary>
        private void txtbarcode_KeyDown(object sender, KeyEventArgs e)
        {
            string Qry = "", strCurProc = "", strPrcName = "", strRepack = "";
            string strchul_date = "", strchul_no = "";

            // 엔터 키 확인
            if (e.KeyCode == Keys.Enter)
            {
                // 12자리 바코드 처리
                if (txtbarcode.Text.Trim().Length == 12)
                {
                    lblLotNo.Text = txtbarcode.Text;
                    
                    // 내부 박스 정보 조회
                    Qry = "select inner_no,inner_no2,unit,sum(qty),chul_date,chul_no from tst13e \n";
                    Qry += " where saup_gubn='01' and inner_no = '" + lblLotNo.Text + "' group by inner_no,inner_no2,unit,chul_date,chul_no ";
                    DataSet ds = mc.ResultReturnDataSet(Qry);

                    // 작업자 확인
                    if (txtEmpno.Text == "")
                    {
                        MessageBox.Show("Worker is not correct.");
                        txtbarcode.Text = "";
                        return;
                    }

                    if (ds.Tables[0].Rows.Count > 0)
                    {
                        strCurProc = ds.Tables[0].Rows[0][0].ToString();
                        strPrcName = ds.Tables[0].Rows[0][1].ToString();
                        strRepack = ds.Tables[0].Rows[0][3].ToString();
                        strchul_date = ds.Tables[0].Rows[0]["chul_date"].ToString();
                        strchul_no = ds.Tables[0].Rows[0]["chul_no"].ToString();

                        // 내부 박스 번호로 아이템 조회 및 히스토리 로드
                        GetInnerNum(lblLotNo.Text);
                        GetListHist(lblLotNo.Text);
                       
                        // 내부 박스 체크 처리
                        if (BtnLotProc.Text == "OK" || BtnLotProc.Text != "OK")
                        {
                            UserCommon.CmCn mc1 = new UserCommon.CmCn();
                            string Qry1 = "select inner_no from check_inner_box where inner_no='" + lblLotNo.Text + "'";
                            DataSet ds1 = mc1.ResultReturnDataSet(Qry1);
                            if (ds1.Tables[0].Rows.Count > 0)
                            {
                                MessageBox.Show("It's already in.");
                            }
                            else
                            {
                                // 내부 박스 체크인 기록 추가
                                string Qry3 = "insert into check_inner_box(chul_date,chul_no,work_date,inner_no,qty,worker,work_time) \n";
                                Qry3 += "   select '" + strchul_date + "','" + strchul_no + "',convert(varchar(10),getdate(),102),'" + lblLotNo.Text + "','" + strRepack + "','" + txtEmpno.Text + "',getdate() \n";

                                UserCommon.CmCn mcupd = new UserCommon.CmCn();
                                try
                                {
                                    mcupd.Execute(Qry3);
                                }
                                catch (Exception e3)
                                {
                                    MessageBox.Show(e3.Message.ToString());
                                }
                            }
                        }

                        txtbarcode.Text = "";
                    }
                    else
                    {
                        MessageBox.Show("【" + txtbarcode.Text + "】no innerbox", "Notice-");
                        txtbarcode.Text = "";
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 라벨 및 컨트롤 데이터 초기화 함수
        /// 모든 표시 데이터를 초기화
        /// </summary>
        private void dellabel()
        {
            next_cnt = 0;
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            lblDisp.Text = "";
            lblLotNo.Text = "";

            // 물류 정보 초기화
            lblCustTxt.Text = "";
            lblItemTxt.Text = "";
            lblShipDateTxt.Text = "";
            lblShipTypeTxt.Text = "";
            lblPackTypeTxt.Text = "";
            lblMarkTxt.Text = "";
            lblRemarkTxt.Text = "";
            lblOrderTxt.Text = "";
            lblOrdQtyTxt.Text = "";
            lblERPOrderTxt.Text = "";
            lblQualityTxt.Text = "";
            lblEnvelopKindTxt.Text = "";
            lblPackMarkTxt.Text = "";
            lblOrdAgentTxt.Text = "";
            
            // 창고 정보 초기화
            lblpickertxt.Text = "";
            lblpicktimetxt.Text = "";
            lblpickqtytxt.Text = "";
            lblpickboxtxt.Text = "";
            lblQtyAtxt.Text = "";
            lblQtyBtxt.Text = "";
            lblQtyCtxt.Text = "";
            lblQtyEtctxt.Text = "";
            lblQtyFristtxt.Text = "";
            lblQtySecondtxt.Text = "";
            lblQtyThirdtxt.Text = "";
            lblQtyFourthtxt.Text = "";
            txtPickRmarktxt.Text = "";
            
            // 포장 정보 초기화
            lblpackertxt.Text = "";
            lblpacktimetxt.Text = "";
            lblpackmchtxt.Text = "";
            lblrectimetxt.Text = "";
            lblpackremarktxt.Text = "";
            lblinspmantxt.Text = "";
            
            // QC 정보 초기화
            lblinspectortxt.Text = "";
            lblinsp_timetxt.Text = "";
            lblinsp_resulttxt.Text = "";
            txtinsp_error.Text = "";
            lblinsp_typetxt.Text = "";
            lblinsp_qtytxt.Text = "";
            lblinsp_packtxt.Text = "";
            lblinsp_marktxt.Text = "";
            lblinsp_remarktxt.Text = "";
            lblinsp_agenttxt.Text = "";

            // 디스플레이 정보 초기화
            lbldispcust.Text = "";
            lbldisppdc.Text = "";
            lbldispcstpo.Text = "";
            lbldisplot.Text = "";
            lbldisplotqty.Text = "";
            lbldisperppo.Text = "";
            pbpic.Image = null;
            nudpickQty.Value = 0;
        }

        /// <summary>
        /// LOT 버튼 생성 함수
        /// 데이터셋을 기반으로 LOT 버튼을 생성
        /// </summary>
        /// <param name="TargetDS">LOT 데이터셋</param>
        /// <param name="TargetPnl">버튼이 추가될 패널</param>
        /// <param name="BackColor">배경색</param>
        /// <param name="FontColor">글자색</param>
        /// <param name="gubn">구분자</param>
        private void CallBtnKind(DataSet TargetDS, FlowLayoutPanel TargetPnl, Color BackColor, Color FontColor, string gubn)
        {
            TargetPnl.Controls.Clear();
            Button TargetBtn = new Button();

            if (TargetDS != null)
            {
                if (TargetDS.Tables[0].Rows.Count > 0)
                {
                    if (gubn == "group")
                    {
                        if (TargetDS.Tables[0].Rows.Count > 0)
                        {
                            // 각 행에 대해 LOT 버튼 생성
                            foreach (DataRow dr1 in TargetDS.Tables[0].Rows)
                            {
                                CalBtnInit(pnlLot, dr1[0].ToString(), dr1[1].ToString(), dr1[2].ToString(), dr1[3].ToString(), dr1[4].ToString(), dr1[5].ToString());
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// LOT 버튼 초기화 함수
        /// 개별 LOT 버튼과 그룹박스를 생성
        /// </summary>
        /// <param name="TargetPnl">대상 패널</param>
        /// <param name="lotNO">LOT 번호</param>
        /// <param name="data1">데이터1</param>
        /// <param name="data2">데이터2</param>
        /// <param name="data3">데이터3</param>
        /// <param name="data4">데이터4</param>
        /// <param name="data5">데이터5(프로세스 코드)</param>
        private void CalBtnInit(FlowLayoutPanel TargetPnl, string lotNO, string data1, string data2, string data3, string data4, string data5)
        {
            TargetPnl.Padding = new System.Windows.Forms.Padding(3);
            int BtnWidthLot = Convert.ToInt32(TargetPnl.Width / 3) - 15;
            int BtnHeightLot = Convert.ToInt32(TargetPnl.Height / 6) - 15;

            Button NewBtnItems = new Button();
            GroupBox NewGroup = new GroupBox();

            // LOT 버튼 생성
            NewBtnItems = AddCalBtn(lotNO, data1 + " \n " + data2 + " \n " + data3 + " \n " + data4, BtnWidthLot, BtnHeightLot, data5);
            
            NewBtnItems.Location = new Point(3, 14);
            NewBtnItems.Padding = new Padding(1, 0, 1, 0);
            NewBtnItems.Click += new EventHandler(NewBtnCal_Click);
            NewBtnItems.Font = new System.Drawing.Font("Gulim", 8F, (System.Drawing.FontStyle.Regular));

            // 그룹박스 생성 및 설정
            NewGroup.Name = lotNO;
            NewGroup.Text = "LOT NO : " + lotNO;
            NewGroup.Font = new System.Drawing.Font("Gulim", 9F, FontStyle.Regular);
            NewGroup.ForeColor = Color.MidnightBlue;
            NewGroup.Size = new System.Drawing.Size((TargetPnl.Width / 3) - 8, BtnHeightLot + 22);
            NewGroup.Controls.Add(NewBtnItems);
            TargetPnl.Controls.Add(NewGroup);
            txtbarcode.Text = "";
        }

        /// <summary>
        /// LOT 버튼 생성 헬퍼 함수
        /// 버튼 속성 설정 및 프로세스 상태에 따른 색상 적용
        /// </summary>
        private Button AddCalBtn(String BtnName, String BtnText, int BtnWidth, int BtnHeight, string StrPro)
        {
            Button NewBtn = new Button();
            NewBtn.Name = BtnName;
            NewBtn.Text = BtnText;
            NewBtn.Width = BtnWidth;
            NewBtn.Height = BtnHeight;
            NewBtn.FlatStyle = FlatStyle.Flat;
            
            // 프로세스 상태에 따른 색상 설정
            if (StrPro == "01" || StrPro == "11" || StrPro == "21" || StrPro == "31" || StrPro == "41")
            {
                NewBtn.BackColor = System.Drawing.Color.DarkOliveGreen;
            }
            else if (StrPro == "19" || StrPro == "29" || StrPro == "39")
            {
                NewBtn.BackColor = System.Drawing.Color.DimGray;
            }
            else if (StrPro == "13")
            {
                NewBtn.BackColor = System.Drawing.Color.Purple;
            }
            else if (StrPro == "15")
            {
                NewBtn.BackColor = System.Drawing.Color.Blue;
            }
            
            NewBtn.ForeColor = Color.White;
            NewBtn.Font = new System.Drawing.Font("Gulim", 11F, (System.Drawing.FontStyle.Regular));
            return NewBtn;
        }

        /// <summary>
        /// LOT 버튼 클릭 이벤트 핸들러
        /// LOT 상세 정보 조회 및 히스토리 로드
        /// </summary>
        public void NewBtnCal_Click(object sender, EventArgs e)
        {
            Button btnlot = (Button)sender;
            Lotlookup(btnlot.Name);
            GetListHist(btnlot.Name);
        }

        /// <summary>
        /// 왼쪽 페이지 이동 버튼 클릭 이벤트 핸들러
        /// 이전 페이지의 LOT 데이터 로드
        /// </summary>
        private void btnleft_Click(object sender, EventArgs e)
        {
            if (next_cnt != 0)
            {
                if (proc_cnt >= next_cnt) next_cnt = next_cnt - 1;
                if (rbLot.Checked)
                {
                    GetLotNum(DeptType, next_cnt, 15);
                }
            }
            else
            {
                MessageBox.Show("It's first!");
            }
        }

        /// <summary>
        /// 오른쪽 페이지 이동 버튼 클릭 이벤트 핸들러
        /// 다음 페이지의 LOT 데이터 로드
        /// </summary>
        private void btnright_Click(object sender, EventArgs e)
        {
            if (proc_cnt > next_cnt) next_cnt = next_cnt + 1;
            if (rbLot.Checked)
            {
                GetLotNum(DeptType, next_cnt, 15);
            }
        }

        /// <summary>
        /// 히스토리 목록 조회 함수
        /// 작업자의 내부 박스 체크인 히스토리를 조회
        /// </summary>
        /// <param name="lot_no">LOT 번호</param>
        private void GetListHist(string lot_no)
        {
            string qry = "";
            listMaster.Items.Clear();

            qry = " SELECT top 20 chul_date,chul_no,work_date,inner_no,qty,worker,work_time  \n";
            qry += "  FROM check_inner_box \n";
            qry += "  where work_date = convert(varchar(10),getdate(),102) \n";
            qry += "    and worker like '" + txtEmpno.Text + "' order by work_time desc \n";

            try
            {
                ucqtl.GetListView(listMaster, qry);
            }
            catch (Exception e1)
            {
                MessageBox.Show(e1.Message.ToString());
            }
        }

        /// <summary>
        /// LOT 상세 정보 조회 함수
        /// LOT의 상세 정보를 조회하고 화면에 표시
        /// </summary>
        /// <param name="lot_no">LOT 번호</param>
        private void Lotlookup(String lot_no)
        {
            string qry = "";
            string path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // INI 파일 경로 설정
            if (type == true) fileSysPath = @"C:\Program Files (x86)\Chemi_MES";
            else fileSysPath = @"C:\Program Files\Chemi_MES";
            string filePath = fileSysPath + fileini;

            MES_WMS.UserCommon.ClsinitUtil ini = new MES_WMS.UserCommon.ClsinitUtil(filePath);
            string Exepath = ini.GetIniValue("SERVER", "File") + @"\Photo\packing\";

            // LOT 상세 정보 조회 쿼리
            qry = " select c.cust_sname,a.lot_no,lot_date, d.group_sdesc, lot_qty,shipping_date,order_no,shipping_type, \n";
            qry += "       quality,packing_type,envelop_kind,laser_mark,packing_mark,order_remark,b.kname,  \n";
            qry += "       picker,isnull(picking_time,getdate())picking_time,isnull(picking_qty,0)picking_qty,isnull(picking_box,0)picking_box, \n";
            qry += "       isnull(qty_a,0)qty_a,isnull(qty_b,0)qty_b,isnull(qty_c,0)qty_c,isnull(qty_etc,0)qty_etc, \n";
            qry += "       isnull(qty_first,0)qty_first,isnull(qty_second,0)qty_second,isnull(qty_third,0)qty_third,isnull(qty_fourth,0)qty_fourth,picking_remark, \n";
            qry += "       packer,isnull(packing_time,getdate())packing_time,isnull(receipt_time,getdate())receipt_time,packing_machine,packing_inspector,packing_remark, \n";
            qry += "       inspector,isnull(insp_time,getdate())insp_time,insp_result,insp_error, \n";
            qry += "       insp_type,isnull(insp_qty,0)insp_qty,insp_packing,insp_marking,insp_remark,insp_agent,prod_shot_no,cust_order_no,(order_qty-isnull(picking_qty,0))NbakQty \n";
            qry += "  from tst16m a, tcb15 d ,cmv.dbo.tcb01 c ,thb01 b \n";
            qry += " where a.saup_gubn='01'  \n";
            qry += "   and a.item_group = d.group_code\n";
            qry += "   and a.dest_cust=c.cust_code  \n";
            qry += "   and b.empno=order_agent \n";
            qry += "   and a.lot_no like '" + lot_no + "' \n";
            
            DataSet ds = mc.ResultReturnDataSet(qry);
            if (ds.Tables[0].Rows.Count > 0)
            {
                foreach (DataRow dr1 in ds.Tables[0].Rows)
                {
                    // 물류 정보 표시
                    if (gbLogistics.Visible)
                    {
                        lblCustTxt.Text = dr1["cust_sname"].ToString();
                        lblItemTxt.Text = dr1["group_sdesc"].ToString();
                        lblShipDateTxt.Text = dr1["shipping_date"].ToString();
                        lblShipTypeTxt.Text = dr1["shipping_type"].ToString();
                        lblPackTypeTxt.Text = dr1["packing_type"].ToString();
                        lblMarkTxt.Text = dr1["laser_mark"].ToString();
                        lblRemarkTxt.Text = dr1["order_remark"].ToString();
                        lblOrderTxt.Text = dr1["lot_no"].ToString();
                        lblOrdQtyTxt.Text = dr1["lot_qty"].ToString();
                        lblERPOrderTxt.Text = dr1["order_no"].ToString();
                        lblQualityTxt.Text = dr1["quality"].ToString();
                        lblEnvelopKindTxt.Text = dr1["envelop_kind"].ToString();
                        lblPackMarkTxt.Text = dr1["packing_mark"].ToString();
                        lblOrdAgentTxt.Text = dr1["kname"].ToString();
                    }
                    
                    // 디스플레이 정보 표시
                    if (gbdisp.Visible)
                    {
                        lbldispcust.Text = dr1["cust_sname"].ToString();
                        lbldisppdc.Text = dr1["group_sdesc"].ToString();
                        lbldispcstpo.Text = dr1["cust_order_no"].ToString();
                        lbldisplot.Text = dr1["lot_no"].ToString();
                        if (DeptType == "11")
                        {
                            lbldisplotqty.Text = dr1["lot_qty"].ToString();
                        }
                        else
                        {
                            lbldisplotqty.Text = dr1["picking_qty"].ToString();
                        }
                        lbldisperppo.Text = dr1["order_no"].ToString();
                    }
                    
                    // 창고 정보 표시
                    if (gbwarehouse.Visible)
                    {
                        lblpickertxt.Text = dr1["picker"].ToString();
                        lblpicktimetxt.Text = Convert.ToDateTime(dr1["picking_time"]).ToString("yyyy.MM.dd");
                        lblpickqtytxt.Text = dr1["picking_qty"].ToString() + " PCS";
                        lblpickboxtxt.Text = dr1["picking_box"].ToString();
                        lblQtyAtxt.Text = dr1["qty_a"].ToString();
                        lblQtyBtxt.Text = dr1["qty_b"].ToString();
                        lblQtyCtxt.Text = dr1["qty_c"].ToString();
                        lblQtyEtctxt.Text = dr1["qty_etc"].ToString();
                        lblQtyFristtxt.Text = dr1["qty_first"].ToString() + " PCS";
                        lblQtySecondtxt.Text = dr1["qty_second"].ToString() + " PCS";
                        lblQtyThirdtxt.Text = dr1["qty_third"].ToString() + " PCS";
                        lblQtyFourthtxt.Text = dr1["qty_fourth"].ToString() + " PCS";
                        txtPickRmarktxt.Text = dr1["picking_remark"].ToString();

                        nudpickQty.Value = Convert.ToInt64(dr1["picking_qty"].ToString());
                    }
                    
                    // 포장 정보 표시
                    if (gbpack.Visible)
                    {
                        lblpackertxt.Text = dr1["packer"].ToString();
                        lblpacktimetxt.Text = Convert.ToDateTime(dr1["packing_time"]).ToString("yyyy.MM.dd");
                        lblpackmchtxt.Text = dr1["packing_machine"].ToString();
                        lblrectimetxt.Text = Convert.ToDateTime(dr1["receipt_time"]).ToString("yyyy.MM.dd");
                        lblpackremarktxt.Text = dr1["packing_remark"].ToString();
                        lblinspmantxt.Text = dr1["packing_inspector"].ToString();
                        
                        // 제품 사진 표시
                        if (dr1["prod_shot_no"].ToString() != "")
                        {
                            FileInfo file1 = new FileInfo(Exepath + dr1["prod_shot_no"].ToString() + ".jpg");
                            if (file1.Exists)
                            {
                                pbpic.Image = Image.FromFile(Exepath + dr1["prod_shot_no"].ToString() + ".jpg");
                            }
                        }
                        else
                        {
                            pbpic.Image = null;
                        }
                    }

                    // QC 정보 표시
                    if (gbQC.Visible)
                    {
                        lblinspectortxt.Text = dr1["inspector"].ToString();
                        lblinsp_timetxt.Text = Convert.ToDateTime(dr1["insp_time"]).ToString("yyyy.MM.dd");
                        lblinsp_resulttxt.Text = dr1["insp_result"].ToString();
                        txtinsp_error.Text = dr1["insp_error"].ToString();
                        lblinsp_typetxt.Text = dr1["insp_type"].ToString();
                        lblinsp_qtytxt.Text = dr1["insp_qty"].ToString();
                        lblinsp_packtxt.Text = dr1["insp_packing"].ToString();
                        lblinsp_marktxt.Text = dr1["insp_marking"].ToString();
                        lblinsp_remarktxt.Text = dr1["insp_remark"].ToString();
                        lblinsp_agenttxt.Text = dr1["insp_agent"].ToString();
                    }
                }
            }
        }

        /// <summary>
        /// 확인 버튼 클릭 이벤트 핸들러
        /// LOT 처리 확인 및 프로세스 진행
        /// </summary>
        private void btnConfirm_Click(object sender, EventArgs e)
        {
            string strCurProc = "";
            int ibakQty = 0;

            // 남은 수량 확인
            if (lbldisplotqty.Text == "" || lbldisplotqty.Text == "0")
            {
                MessageBox.Show("Remain quantity is 0", "Notice");
                ibakQty = 0;
                return;
            }
            else
            {
                ibakQty = Convert.ToInt32(lbldisplotqty.Text);
            }

            // 라디오 버튼 상태 확인
            if (rbPack.Checked)
            {
                rbQC.Checked = false;
            }
            if (rbQC.Checked)
            {
                rbPack.Checked = false;
            }
            
            // 피킹 수량 확인
            if (Convert.ToInt32(nudpickQty.Value) == 0)
            {
                MessageBox.Show("Please input picking quantity.", "Notice");
                return;
            }
            else if (Convert.ToInt32(nudpickQty.Value) > ibakQty)
            {
                MessageBox.Show("Picking quantity is more than order.", "Notice");
                nudpickQty.Value = 0;
                return;
            }
            
            // 현재 프로세스 확인
            UserCommon.CmCn mc = new UserCommon.CmCn();
            string Qry0 = "select proc_kind,picking_qty from tst16m where saup_gubn='01' and lot_no='" + lblLotNo.Text + "'";
            DataSet ds0 = mc.ResultReturnDataSet(Qry0);
            if (ds0.Tables[0].Rows.Count > 0)
            {
                strCurProc = ds0.Tables[0].Rows[0][0].ToString();
                
                // 프로세스 진행 가능 여부 확인
                if (strCurProc == "11" || strCurProc == "13" || strCurProc == "15" || strCurProc == "17")
                {
                    ProcLotInfo(lblLotNo.Text, strCurProc);
                    Lotlookup(lblLotNo.Text);
                    GetListHist(lblLotNo.Text);
                    nudpickQty.Value = 0;
                }
                else if (strCurProc == "19")
                {
                    if (rbPack.Checked)
                    {
                        MessageBox.Show("Please Packing IN.", "Notice-");
                        return;
                    }
                    if (rbQC.Checked)
                    {
                        MessageBox.Show("Please QC IN.", "Notice-");
                        return;
                    }
                }
            }
            
            timer1.Enabled = true;
            timer1.Start();
        }

        /// <summary>
        /// LOT 정보 처리 함수
        /// LOT의 프로세스 상태를 업데이트하고 히스토리 기록
        /// </summary>
        /// <param name="strLot">LOT 번호</param>
        /// <param name="strCurrProc">현재 프로세스 코드</param>
        private void ProcLotInfo(string strLot, string strCurrProc)
        {
            string qry = "", sQry = "", strNextProc = "", strRepack = "";

            // 다음 프로세스 코드 결정
            if (strCurrProc != "" && strCurrProc == "01" && DeptType == "01")
            {
                strNextProc = "11";
            }
            else if (strCurrProc != "" && (strCurrProc == "11" || strCurrProc == "13" || strCurrProc == "15" || strCurrProc == "17") && DeptType == "11")
            {
                // 피킹 수량 확인
                if (Convert.ToInt32(nudpickQty.Value) == 0)
                {
                    MessageBox.Show("请录入配货数量", "Notice");
                    return;
                }
                
                // 리패킹 여부 결정
                if (rbPack.Checked)
                {
                    strRepack = "Y";
                }
                else if (rbQC.Checked)
                {
                    strRepack = "N";
                }
                strNextProc = "19";
            }
            else if (strCurrProc != "" && strCurrProc == "19")
            {
                UserCommon.CmCn mc = new UserCommon.CmCn();
                sr = mc.ResultReturnExecute("select repacking from tst16m where saup_gubn='01' and lot_no='" + strLot + "'");
                while (sr.Read())
                {
                    if (sr.GetValue(0).ToString() == "Y")
                    {
                        strNextProc = "21";
                    }
                    else
                        strNextProc = "31";
                }
            }
            else if (strCurrProc != "" && strCurrProc == "21")
            {
                strNextProc = "29";
            }
            else if (strCurrProc != "" && strCurrProc == "29")
            {
                strNextProc = "31";
            }
            else if (strCurrProc != "" && strCurrProc == "31")
            {
                strNextProc = "39";
            }
            else if (strCurrProc != "" && strCurrProc == "39")
            {
                strNextProc = "41";
            }

            // LOT 정보 업데이트 쿼리
            qry = "update tst16m \n";
            qry += "  set proc_kind='" + strNextProc + "' \n";

            // 프로세스별 추가 업데이트 항목
            if (strNextProc == "19" && (strCurrProc == "11" || strCurrProc == "13" || strCurrProc == "15" || strCurrProc == "17"))
            {
                qry += "      ,picking_qty= case '" + strNextProc + "' when '19' then " + Convert.ToInt32(nudpickQty.Value) + " else picking_qty end \n";
                qry += "      ,picker='" + txtEmpno.Text + "' \n";
                qry += "      ,picking_time=getdate() \n";
                qry += "      ,repacking='" + strRepack + "' \n";
            }
            else if (strNextProc == "21")
            {
                qry += "      ,receipt_time=getdate() \n";
            }
            else if (strNextProc == "29")
            {
                qry += "      ,packing_time=getdate() \n";
                qry += "      ,packer='" + txtEmpno.Text + "' \n";
            }
            else if (strNextProc == "39")
            {
                qry += "      ,insp_time=getdate() \n";
                qry += "      ,inspector='" + txtEmpno.Text + "' \n";
            }
            
            qry += "where saup_gubn='01' and lot_no='" + strLot + "' \n";
            qry += "  and proc_kind='" + strCurrProc + "' ";

            // LOT 정보 업데이트 실행
            UserCommon.CmCn mcc = new UserCommon.CmCn();
            try
            {
                mcc.Execute(qry);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message.ToString());
            }

            // 히스토리 기록 추가
            UserCommon.CmCn conn = new UserCommon.CmCn();
            sQry = "insert into tst16h ( saup_gubn,lot_date,lot_no,proc_kind,worker,work_time,remark) \n";
            sQry += "select saup_gubn,lot_date,lot_no,'" + strNextProc + "' ,'" + txtEmpno.Text + "', getdate() ,''  \n";
            sQry += "from tst16h \n";
            sQry += "where saup_gubn='01' and lot_no='" + strLot + "' and proc_kind='" + strCurrProc + "' \n";

            try
            {
                conn.Execute(sQry);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message.ToString());
            }
        }

        /// <summary>
        /// 서브 버튼 생성 함수
        /// 아이템 데이터를 기반으로 서브 버튼 생성
        /// </summary>
        private void CallBtnKind_Sub(DataSet TargetDS, FlowLayoutPanel TargetPnl, Color BackColor, Color FontColor, string gubn)
        {
            TargetPnl.Controls.Clear();
            TargetPnl.Padding = new System.Windows.Forms.Padding(6);
            Panel SubPnl = new Panel();
            Button TargetBtn = new Button();
            Button TargetBtn2 = new Button();
            int BtnWidth = 0;
            int BtnHeight = 0;
            int PnlWidth = 0;
            int PnlHeight = 0;

            BtnHeight = 60;
            BtnWidth = 125;
            
            if (TargetDS != null)
            {
                // 데이터 크기 제한 확인
                if (TargetDS.Tables[0].Rows.Count > 2000)
                {
                    MessageBox.Show("sorry! Data is too big!!");
                    goto EXIT_LOOP;
                }
                
                if (TargetDS.Tables[0].Rows.Count > 0)
                {
                    // 각 아이템에 대해 패널과 버튼 생성
                    foreach (DataRow dr1 in TargetDS.Tables[0].Rows)
                    {
                        SubPnl = new Panel();
                        SubPnl.Text = dr1[2].ToString();
                        SubPnl.Name = dr1[0].ToString();
                        SubPnl.Height = BtnHeight * 2 - 10;
                        SubPnl.Width = BtnWidth + 10;
                        SubPnl.BackColor = Color.DeepSkyBlue;
                        SubPnl.ForeColor = FontColor;
                        SubPnl.Location = new Point(10, 15);
                        
                        TargetBtn = new Button();
                        TargetBtn.Text = dr1[1].ToString();
                        TargetBtn.Name = dr1[0].ToString();
                        TargetBtn.Height = BtnHeight;
                        TargetBtn.Width = BtnWidth;
                        TargetBtn.FlatStyle = FlatStyle.Flat;
                        TargetBtn.BackColor = BackColor;
                        TargetBtn.ForeColor = FontColor;
                        TargetBtn.Location = new Point(5, 8);
                        TargetBtn.Font = new System.Drawing.Font("Gulim", 12F, (System.Drawing.FontStyle.Bold));

                        SubPnl.Controls.Add(TargetBtn);
                        
                        TargetBtn2 = new Button();
                        TargetBtn2.Text = dr1[2].ToString();
                        TargetBtn2.Name = dr1[0].ToString();
                        TargetBtn2.Height = BtnHeight - 30;
                        TargetBtn2.Width = BtnWidth;
                        TargetBtn2.FlatStyle = FlatStyle.Flat;
                        TargetBtn2.BackColor = Color.Black;
                        TargetBtn2.ForeColor = Color.White;
                        TargetBtn2.Location = new Point(5, BtnHeight + 10);
                        TargetBtn2.Font = new System.Drawing.Font("Gulim", 12F, (System.Drawing.FontStyle.Bold));

                        SubPnl.Controls.Add(TargetBtn2);
                        PnlWidth += BtnWidth;
                        TargetPnl.Controls.Add(SubPnl);
                        
                        // 패널 크기 조정
                        if (TargetPnl.Width < PnlWidth)
                        {
                            PnlWidth = 0;
                            PnlHeight += BtnHeight;
                        }
                    }
                }
            EXIT_LOOP: ;
            }
        }

        /// <summary>
        /// 사원번호 검색 버튼 클릭 이벤트 핸들러
        /// 사원 선택 폼을 열고 결과를 받아 처리
        /// </summary>
        private void btnEmpnoSearch_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            timer1.Stop();
            
            // 부서 유형에 따른 사원 선택 폼 생성
            if (DeptType == "01" || DeptType == "11" || DeptType == "41" || DeptType == "")
            {
                childForm = new FrmEmp("1152");
            }
            if (DeptType == "21")
            {
                childForm = new FrmEmp("1155");
            }
            if (DeptType == "31")
            {
                childForm = new FrmEmp("1271");
            }

            childForm.OnNotifyParent += new ChildFromEventHandler(childFrom_OnNotifyParent);
            childForm.Show();
        }

        /// <summary>
        /// 자식 폼에서의 알림 이벤트 핸들러
        /// 사원 선택 결과를 받아 화면에 표시
        /// </summary>
        void childFrom_OnNotifyParent(object sender, ChildFormEventArgs e)
        {
            FrmEmp child = (FrmEmp)sender;
            txtEmpno.Text = e.Message[0].ToString();
            txtKname.Text = e.Message[1].ToString();
            timer1.Enabled = true;
            timer1.Start();
        }

        /// <summary>
        /// LOT 처리 버튼 클릭 이벤트 핸들러
        /// LOT 처리 모드 전환
        /// </summary>
        private void BtnLotProc_Click(object sender, EventArgs e)
        {
            if (BtnLotProc.Text == "Lookup")
            {
                if (!gbStkProc.Visible && DeptType == "11") gbStkProc.Visible = true;
                BtnLotProc.Text = "OK";
            }
            else
            {
                if (gbStkProc.Visible && DeptType == "11") gbStkProc.Visible = false;
                BtnLotProc.Text = "Lookup";
            }
        }

        /// <summary>
        /// LOT 라디오 버튼 체크 변경 이벤트 핸들러
        /// </summary>
        private void rbLot_CheckedChanged(object sender, EventArgs e)
        {
            txtbarcode.Text = "";
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            dellabel();
            gbLogistics.Visible = false;
            gbwarehouse.Visible = false;
            gbpack.Visible = false;
            gbQC.Visible = false;
            gbStkProc.Visible = false;
        }

        /// <summary>
        /// DP 라디오 버튼 체크 변경 이벤트 핸들러
        /// </summary>
        private void rbDP_CheckedChanged(object sender, EventArgs e)
        {
            txtbarcode.Text = "";
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            dellabel();
            gbLogistics.Visible = false;
            gbwarehouse.Visible = false;
            gbpack.Visible = false;
            gbQC.Visible = false;
            gbStkProc.Visible = false;
        }

        /// <summary>
        /// 타이머 틱 이벤트 핸들러
        /// 바코드 입력 필드에 포커스 설정
        /// </summary>
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (timer1.Enabled != false)
                txtbarcode.Focus();
        }

        /// <summary>
        /// 피킹 수량 값 변경 이벤트 핸들러
        /// 수량 유효성 검사
        /// </summary>
        private void nudpickQty_ValueChanged(object sender, EventArgs e)
        {
            int ibakQty = 0;

            if (lbldisplotqty.Text == "" || lbldisplotqty.Text == "0")
            {
                MessageBox.Show("Remain quantity is 0!!", "Notice");
                ibakQty = 0;
                return;
            }
            else
            {
                ibakQty = Convert.ToInt32(lbldisplotqty.Text);

                // 피킹 수량이 남은 수량보다 많은지 확인
                if (Convert.ToInt32(nudpickQty.Value) > ibakQty)
                {
                    MessageBox.Show("Picking quantity is more than order.", "Notice");
                    nudpickQty.Value = 0;
                    return;
                }
            }
        }

        /// <summary>
        /// 피킹 수량 마우스 다운 이벤트 핸들러
        /// 타이머 정지
        /// </summary>
        private void nudpickQty_MouseDown(object sender, MouseEventArgs e)
        {
            timer1.Enabled = false;
            timer1.Stop();
        }
    }

    /// <summary>
    /// 내부 박스 리스트 관리 클래스
    /// 다양한 조건에 따른 데이터셋을 관리
    /// </summary>
    class CLSINNERBOXLIST
    {
        private string CalCustKind = "";
        private string CalGroupKind = "";
        private string CalItems = "";
        private string CalOrder = "";
        private string CalSubKind = "";
        private string CalUpdateQty = "";
        private string CalInnerbox = "";

        // 선택 조건 변수
        public string SelCustKind = "";
        public string SelItems = "";
        public string SelOrder = "";
        public string SelGroup = "";
        public string SelCode = "";
        public string SelDp = "";
        public string SelDpQty = "";
        public string SelType = "";
        public string SelInnerbox = "";
        public string SelDateF = DateTime.Now.AddDays(-10).ToString("yyyy.MM.dd");
        public string SelDateT = DateTime.Now.AddDays(1).ToString("yyyy.MM.dd");

        // 데이터셋 변수
        private DataSet CustDS = null;
        private DataSet GroupDS = null;
        private DataSet SubDS = null;
        private DataSet LotDS = null;
        private DataSet EmpDS = null;
        private DataSet ItemsDS = null;
        private DataSet OrderDS = null;
        private DataSet UpdateQtyDS = null;
        private DataSet InnerboxDS = null;
        UserCommon.CmCn conn = new UserCommon.CmCn();

        public CLSINNERBOXLIST()
        {
        }

        // 속성 설정 및 데이터셋 로드
        public string SetCustKind
        {
            set
            {
                this.CalCustKind = value;
                SetCustDS();
            }
        }

        public string SetGroupKind
        {
            set
            {
                this.CalGroupKind = value;
                SetGroupDS();
            }
        }

        public string SetSubKind
        {
            set
            {
                this.CalSubKind = value;
                SetSubDS();
            }
        }

        public string SetItems
        {
            set
            {
                this.CalItems = value;
                SetItemsDS();
            }
        }

        public string SetInnerbox
        {
            set
            {
                this.CalInnerbox = value;
                SetInnerboxDS();
            }
        }
        
        public string SetOrder
        {
            set
            {
                this.CalOrder = value;
                SetOrderDS();
            }
        }
        
        public string SetUpdateQty
        {
            set
            {
                this.CalUpdateQty = value;
            }
        }

        /// <summary>
        /// 고객 데이터셋 설정 함수
        /// 고객 목록을 데이터베이스에서 조회
        /// </summary>
        private void SetCustDS()
        {
            string qry = "";

            if (!string.IsNullOrWhiteSpace(this.CalCustKind))
            {
                qry = " select '%%' cust_code, 'All\nCustomer' cust_sname,'' \n";
                qry += " union \n";
                qry += " select distinct b.cust_code,b.cust_sname,''\n";
                qry += "from tst16m a,cmv.dbo.tcb01 b \n";
                qry += "where a.saup_gubn = '01'\n";
                qry += "  and a.lot_date between '" + SelDateF + "' and '" + SelDateT + "' \n";
                qry += "  and a.dest_cust = b.cust_code \n";
                qry += "order by cust_code";
                this.CustDS = conn.ResultReturnDataSet(qry);
            }
        }

        /// <summary>
        /// 아이템 데이터셋 설정 함수
        /// 아이템 그룹 목록을 데이터베이스에서 조회
        /// </summary>
        private void SetItemsDS()
        {
            string qry = "";
            if (!string.IsNullOrWhiteSpace(this.CalItems))
            {
                qry = " select '%%' group_code, 'All\nItem' group_sdesc,'' \n";
                qry += " union \n";
                qry += "select distinct b.group_code,b.group_sdesc,''\n";
                qry += "from tst16m a,tcb15 b \n";
                qry += "where a.saup_gubn = '01'\n";
                qry += "  and a.lot_date between '" + SelDateF + "' and '" + SelDateT + "' \n";
                qry += "  and a.item_group = b.group_code \n";
                qry += "order by group_code";

                this.ItemsDS = conn.ResultReturnDataSet(qry);
            }
        }

        /// <summary>
        /// 주문 데이터셋 설정 함수
        /// 주문 목록을 데이터베이스에서 조회
        /// </summary>
        private void SetOrderDS()
        {
            string qry = "";
            if (!string.IsNullOrWhiteSpace(this.CalOrder))
            {
                qry = " select '%%' lot_no, 'All\nOrder' order_no ,''\n";
                qry += " union \n";
                qry += " select distinct a.lot_date + a.lot_no,a.order_no,'' \n";
                qry += "from tst16m a,tcb15 b \n";
                qry += "where a.saup_gubn = '01'\n";
                qry += "  and a.lot_date between '" + SelDateF + "' and '" + SelDateT + "' \n";
                qry += "  and a.item_group = b.group_code \n";
                qry += "order by lot_no";

                this.OrderDS = conn.ResultReturnDataSet(qry);
            }
        }

        /// <summary>
        /// 그룹 데이터셋 설정 함수
        /// 그룹별 LOT 목록을 데이터베이스에서 조회
        /// </summary>
        private void SetGroupDS()
        {
            string qry = "";
            if (SelItems == "") SelItems = "%%";
            if (SelCustKind == "") SelCustKind = "%%";
            if (SelOrder == "") SelOrder = "%%";

            if (!string.IsNullOrWhiteSpace(this.CalGroupKind))
            {
                qry = "select a.lot_no, d.group_sdesc, c.cust_sname,a.order_no,' Qty:' + convert(varchar(20), a.lot_qty)  \n";
                qry += " from tst16m a, tcb15 d ,cmv.dbo.tcb01 c \n";
                qry += "where a.saup_gubn='01' \n";
                qry += "  and a.item_group = d.group_code \n";
                qry += "  and a.dest_cust=c.cust_code \n";
                qry += "  and a.proc_kind like '" + SelType + "' \n";
                qry += "order by a.lot_no desc  ";

                this.GroupDS = conn.ResultReturnDataSet(qry);
            }
        }

        /// <summary>
        /// 서브 데이터셋 설정 함수
        /// 서브 아이템 목록을 데이터베이스에서 조회
        /// </summary>
        private void SetSubDS()
        {
            string qry = "";

            if (!string.IsNullOrWhiteSpace(this.CalSubKind))
            {
                qry = "select b.item_code  ,c.group_sdesc,' Qty:' + convert(varchar(20),sum( b.order_qty)) ,'',''  \n";
                qry += " from tst16m a,tst16d b,tcb15 c \n";
                qry += "where a.saup_gubn = '01'  \n";
                qry += "  and a.saup_gubn = b.saup_gubn \n";
                qry += "  and a.lot_date = b.lot_date  \n";
                qry += "  and a.proc_kind like '" + SelType + "' \n";
                qry += "  and a.lot_no = b.lot_no \n";
                qry += "  and a.item_group = c.group_code \n";
                qry += "group by  b.item_code  ,c.group_sdesc \n";
                qry += "order by b.item_code  ";
                this.SubDS = conn.ResultReturnDataSet(qry);
            }
        }

        /// <summary>
        /// 사원 데이터셋 설정 함수
        /// 사원 목록을 데이터베이스에서 조회
        /// </summary>
        private void SetEmpDS()
        {
            string qry = "";

            if (!string.IsNullOrWhiteSpace(this.CalCustKind))
            {
                qry = " select empno, kname from thb01 where saup_gubn = '01' \n";
                qry += "   and goju_gubn like '" + this.CalGroupKind + "%' \n";
                qry += "   and goout_gubn = '1' \n";
                qry += " order by empno \n";

                this.EmpDS = conn.ResultReturnDataSet(qry);
            }
        }

        /// <summary>
        /// 내부 박스 데이터셋 설정 함수
        /// 내부 박스 목록을 데이터베이스에서 조회
        /// </summary>
        private void SetInnerboxDS()
        {
            string qry = "";

            if (!string.IsNullOrWhiteSpace(this.CalInnerbox))
            {
                qry = " select inner_no, inner_no2 from tst13e  where saup_gubn = '01' \n";
                qry += "   and inner_no = '" + this.CalInnerbox + "%' \n";
                qry += " order by inner_no \n";

                this.InnerboxDS = conn.ResultReturnDataSet(qry);
            }
        }

        // 데이터셋 접근 속성
        public DataSet GetProcDS
        {
            get { return this.CustDS; }
        }
        
        public DataSet GetItemsDS
        {
            get { return this.ItemsDS; }
        }
        
        public DataSet GetGroupDS
        {
            get { return this.GroupDS; }
        }
        
        public DataSet GetSubDS
        {
            get { return this.SubDS; }
        }
        
        public DataSet GetLotDS
        {
            get { return this.LotDS; }
        }
        
        public DataSet GetEmpDS
        {
            get { return this.EmpDS; }
        }

        public DataSet GetOrderDS
        {
            get { return this.OrderDS; }
        }
        
        public DataSet GetUpdateQtyDS
        {
            get { return this.UpdateQtyDS; }
        }
    }
}
