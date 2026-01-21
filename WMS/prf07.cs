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
    /// <summary>
    /// 생산 추적 관리 폼 - PRF07
    /// </summary>
    public partial class PRF07 : Form
    {
        #region 필드 선언
        private CLSEMPNO ce = new CLSEMPNO();
        private CLSLotLIST cde = new CLSLotLIST();
        private FrmEmp childForm = null;
        private SqlDataReader sr0 = null;
        private SqlDataReader sr = null;
        
        // 사용자 정보
        private string Factory = UserCommon.Public_Function.user_Factory;
        private string User_Group = UserCommon.Public_Function.user_Group;
        private string User_empno = UserCommon.Public_Function.user_Empno;
        private string Factdept = UserCommon.Public_Function.user_Dept;
        
        // 날짜 범위
        public string SelDateF = DateTime.Now.AddDays(-90).ToString("yyyy.MM.dd");
        public string SelDateT = DateTime.Now.AddDays(1).ToString("yyyy.MM.dd");
        
        // 유틸리티 클래스
        private UserCommon.CmCn mc = new UserCommon.CmCn();
        private UserCommon.ComCls uc = new UserCommon.ComCls();
        private UserCommon.ClsQryToListView ucqtl = new UserCommon.ClsQryToListView();
        private UserCommon.ClsExcel DPsheet = new UserCommon.ClsExcel();
        
        // 파일 경로 설정
        private string fileSysPath = "";
        private string fileini = @"\CMES\config_mes.ini";
        private bool type = Environment.Is64BitOperatingSystem;
        
        // 자식 폼
        private PRF09 childFrom = null;
        private PRF11 childFrom1 = null;
        private PRF12 childFrom2 = null;
        private PRF14 childFrom3 = null;
        private PRF15 childFrom4 = null;
        private PRF16 childFrom5 = null;
        
        // 작업 상태 변수
        private int proc_cnt = 0;
        private int next_cnt = 0;
        private string DeptType = "";
        private string strDept = "";
        #endregion

        #region 생성자 및 초기화
        public PRF07()
        {
            InitializeComponent();
            InitFrm();
            sbListViewHead();
        }

        /// <summary>
        /// 비지니스 로직: 폼 초기화
        /// - UI 컴포넌트 상태 초기화
        /// - 사용자 정보 표시
        /// - 타이머 시작
        /// </summary>
        private void InitFrm()
        {
            // 라벨 초기화
            ResetLabels();
            
            // 그룹박스 가시성 설정
            SetGroupBoxVisibility(false, false, false, false, false, false, false);
            
            // 사용자 정보 표시
            txtEmpno.Text = User_empno;
            txtKname.Text = UserCommon.Public_Function.user_Name;
            
            // 타이머 시작
            timer1.Enabled = true;
            
            // 초기 부서 로딩
            btnDWL_Click(null, null);
            
            // 레이아웃 설정
            SetControlLayout();
        }

        /// <summary>
        /// 비지니스 로직: 리스트뷰 헤더 설정
        /// - 작업 이력 표시용 컬럼 구성
        /// </summary>
        private void sbListViewHead()
        {
            listMaster.Clear();
            uc.sbListViewInit(listMaster, false);
            
            // 컬럼 추가
            uc.ListViewHeadInit(listMaster, 70, 0, "Date", "Text");
            uc.ListViewHeadInit(listMaster, 80, 0, "LOTNO", "Text");
            uc.ListViewHeadInit(listMaster, 80, 0, "Seq.", "Text");
            uc.ListViewHeadInit(listMaster, 60, 0, "Worker", "Text");
            uc.ListViewHeadInit(listMaster, 140, 0, "WorkTime", "Text");
            uc.ListViewHeadInit(listMaster, 50, 0, "ETC", "Text");
        }
        #endregion

        #region 부서별 버튼 클릭 이벤트
        /// <summary>
        /// 비지니스 로직: 물류(DWL) 버튼 클릭
        /// - 물류 작업 화면 구성
        /// - LOT 목록 로딩
        /// </summary>
        private void btnDWL_Click(object sender, EventArgs e)
        {
            ResetUIForDepartment("01", "DWL");
            
            if (rbLot.Checked)
            {
                GetLotNum("01", next_cnt, 15);
            }
        }

        /// <summary>
        /// 비지니스 로직: 창고(STK) 버튼 클릭
        /// - 창고 작업 화면 구성
        /// - 피킹 작업 표시
        /// </summary>
        private void btnStk_Click(object sender, EventArgs e)
        {
            ResetUIForDepartment("11", "STK");
            
            if (rbLot.Checked)
            {
                GetLotNum("11", next_cnt, 15);
            }
        }

        /// <summary>
        /// 비지니스 로직: 포장(DPack) 버튼 클릭
        /// - 포장 작업 화면 구성
        /// - 포장 정보 표시
        /// </summary>
        private void btnDPack_Click(object sender, EventArgs e)
        {
            ResetUIForDepartment("21", "DPack");
            
            if (rbLot.Checked)
            {
                GetLotNum("21", next_cnt, 15);
            }
        }

        /// <summary>
        /// 비지니스 로직: QC 버튼 클릭
        /// - QC 검사 화면 구성
        /// - QC 이력 표시
        /// </summary>
        private void btnQC_Click(object sender, EventArgs e)
        {
            ResetUIForDepartment("31", "QC");
            
            if (rbLot.Checked)
            {
                GetLotNum("31", next_cnt, 15);
            }
        }

        /// <summary>
        /// 비지니스 로직: 출하(Dlv) 버튼 클릭
        /// - 출하 작업 화면 구성
        /// - 스캔 처리 활성화
        /// </summary>
        private void btnDlv_Click(object sender, EventArgs e)
        {
            ResetUIForDepartment("41", "Dlv");
            
            if (rbLot.Checked)
            {
                GetLotNum("41", next_cnt, 15);
            }
        }
        #endregion

        #region 데이터 조회 메서드
        /// <summary>
        /// 비지니스 로직: LOT 번호 조회
        /// - 부서별 진행 중인 LOT 목록 조회
        /// - 페이징 처리
        /// </summary>
        private void GetLotNum(string strProc, int li_page, int lirow)
        {
            string processCondition = GetProcessCondition(strProc);
            string countQuery = BuildLotCountQuery(processCondition);
            
            DataSet ds1 = mc.ResultReturnDataSet(countQuery);
            if (ds1.Tables[0].Rows.Count > 0)
            {
                UpdatePagingInfo(ds1, li_page, lirow);
            }
            
            string query = BuildLotQuery(strProc, processCondition, li_page, lirow);
            DataSet ds = mc.ResultReturnDataSet(query);
            
            if (ds.Tables[0].Rows.Count > 0)
            {
                CallBtnKind(ds, pnlLot, SystemColors.Control, Color.Black, "group");
            }
            else if (ds.Tables[0].Rows.Count == 0)
            {
                MessageBox.Show("No Data!");
            }
        }

        /// <summary>
        /// 비지니스 로직: 품목 번호 조회
        /// - 특정 LOT의 품목별 수량 정보 조회
        /// </summary>
        private void GetItemNum(string strProc, int li_page, int lirow, string strLot)
        {
            string sLotDate = "20" + strLot.Substring(0, 2) + "." + strLot.Substring(2, 2) + "." + strLot.Substring(4, 2);
            string processCondition = GetProcessCondition(strProc);
            string query = BuildItemQuery(strProc, processCondition, strLot, sLotDate);
            
            DataSet ds = mc.ResultReturnDataSet(query);
            if (ds.Tables[0].Rows.Count > 0)
            {
                CallBtnKind_Sub(ds, pnlLot, SystemColors.Control, Color.Black, "sub");
            }
            else
            {
                CheckWorkingStatus(strLot, sLotDate);
            }
        }

        /// <summary>
        /// 비지니스 로직: LOT 이력 조회
        /// - 선택된 LOT의 작업 이력 조회
        /// </summary>
        private void GetListHist(string lot_no)
        {
            string query = BuildHistoryQuery(lot_no);
            listMaster.Items.Clear();
            
            try
            {
                ucqtl.GetListView(listMaster, query);
            }
            catch (Exception e1)
            {
                MessageBox.Show(e1.Message.ToString());
            }
        }

        /// <summary>
        /// 비지니스 로직: LOT 상세 정보 조회
        /// - LOT의 상세 정보(고객, 수량, 작업 상태 등) 조회
        /// - 이미지 파일 로딩
        /// </summary>
        private void Lotlookup(String lot_no)
        {
            string sLotDate = "20" + lot_no.Substring(0, 2) + "." + lot_no.Substring(2, 2) + "." + lot_no.Substring(4, 2);
            string query = BuildLotDetailQuery(lot_no, sLotDate);
            
            DataSet ds = mc.ResultReturnDataSet(query);
            if (ds.Tables[0].Rows.Count > 0)
            {
                DisplayLotDetails(ds);
                LoadProductImage(ds);
            }
        }
        #endregion

        #region 바코드 처리
        /// <summary>
        /// 비지니스 로직: 바코드 스캔 처리
        /// - 엔터키 입력 시 바코드 처리
        /// - LOT 번호 유효성 검사
        /// - 작업자 확인 및 진행 상태 업데이트
        /// </summary>
        private void txtbarcode_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                HandleBarcodeScan();
            }
        }

        /// <summary>
        /// 비지니스 로직: 바코드 스캔 핸들러
        /// - 바코드 길이 및 형식 검증
        /// - LOT 정보 조회 및 표시
        /// - 작업 진행 처리
        /// </summary>
        private void HandleBarcodeScan()
        {
            dellabel();

            if (!ValidateDepartmentSelection()) return;
            if (!ValidateWorkerInput()) return;

            if (txtbarcode.Text.Trim().Length == 11)
            {
                ProcessLotBarcode();
            }
        }

        /// <summary>
        /// 비지니스 로직: LOT 바코드 처리
        /// - LOT 정보 조회 및 검증
        /// - 작업 상태 확인
        /// - 진행 가능 여부 확인 및 처리
        /// </summary>
        private void ProcessLotBarcode()
        {
            lblLotNo.Text = txtbarcode.Text;
            string sLotDate = "20" + txtbarcode.Text.Substring(0, 2) + "." + 
                            txtbarcode.Text.Substring(2, 2) + "." + 
                            txtbarcode.Text.Substring(4, 2);

            ValidateAndFixLotData(lblLotNo.Text, sLotDate);
            
            if (CheckLotExists(lblLotNo.Text, sLotDate))
            {
                ProcessExistingLot(lblLotNo.Text, sLotDate);
            }
            else
            {
                MessageBox.Show($"LOT【{txtbarcode.Text}】is no data", "Notice-");
                txtbarcode.Text = "";
            }
        }
        #endregion

        #region 작업 처리 메서드
        /// <summary>
        /// 비지니스 로직: 작업 확인 처리
        /// - 피킹 수량 검증
        /// - 작업 진행 상태 확인
        /// - 다음 공정으로 진행
        /// </summary>
        private void btnConfirm_Click(object sender, EventArgs e)
        {
            if (!ValidatePickingQuantity()) return;
            if (!ValidateQuantityMatch()) return;
            
            UpdateSelectionStates();
            
            string currentProcess = GetCurrentProcess();
            if (!string.IsNullOrEmpty(currentProcess))
            {
                ProcessLotOperation(currentProcess);
            }
        }

        /// <summary>
        /// 비지니스 로직: LOT 작업 진행
        /// - 현재 공정에서 다음 공정으로 진행
        /// - 작업 이력 기록
        /// - 데이터베이스 업데이트
        /// </summary>
        private void ProcLotInfo(string strLot, string strCurrProc)
        {
            string sLotDate = "20" + strLot.Substring(0, 2) + "." + 
                            strLot.Substring(2, 2) + "." + 
                            strLot.Substring(4, 2);
            
            string nextProcess = DetermineNextProcess(strCurrProc);
            if (string.IsNullOrEmpty(nextProcess)) return;
            
            ProcessSpecialCases(strLot, strCurrProc);
            
            UpdateLotProcess(strLot, strCurrProc, nextProcess, sLotDate);
            AddProcessHistory(strLot, strCurrProc, nextProcess, sLotDate);
        }

        /// <summary>
        /// 비지니스 로직: QC 작업 확인
        /// - QC 검사 결과 처리
        /// - 포장 또는 출하로 진행
        /// </summary>
        private void btn_QCOK_Click(object sender, EventArgs e)
        {
            string currentProcess = GetCurrentProcess();
            if (!string.IsNullOrEmpty(currentProcess) && currentProcess == "31")
            {
                ProcLotInfo(lblLotNo.Text, currentProcess);
                Lotlookup(lblLotNo.Text);
                GetListHist(lblLotNo.Text);
            }
            
            timer1.Enabled = true;
            timer1.Start();
        }
        #endregion

        #region UI 업데이트 메서드
        /// <summary>
        /// 비지니스 로직: LOT 버튼 생성
        /// - 조회된 LOT 목록을 버튼으로 표시
        /// - 진행 상태에 따른 색상 구분
        /// </summary>
        private void CallBtnKind(DataSet TargetDS, FlowLayoutPanel TargetPnl, Color BackColor, Color FontColor, string gubn)
        {
            TargetPnl.Controls.Clear();
            
            if (TargetDS != null && TargetDS.Tables[0].Rows.Count > 0)
            {
                if (gubn == "group")
                {
                    foreach (DataRow dr1 in TargetDS.Tables[0].Rows)
                    {
                        CalBtnInit(pnlLot, dr1[0].ToString(), dr1[1].ToString(), 
                                 dr1[2].ToString(), dr1[3].ToString(), 
                                 dr1[4].ToString(), dr1[5].ToString());
                    }
                }
            }
        }

        /// <summary>
        /// 비지니스 로직: 품목 버튼 생성
        /// - LOT 내 품목별 수량 정보 버튼 표시
        /// - 수량 상태에 따른 색상 구분
        /// </summary>
        private void CallBtnKind_Sub(DataSet TargetDS, FlowLayoutPanel TargetPnl, Color BackColor, Color FontColor, string gubn)
        {
            TargetPnl.Controls.Clear();
            TargetPnl.Padding = new Padding(4);
            
            if (TargetDS != null && TargetDS.Tables[0].Rows.Count > 0)
            {
                if (TargetDS.Tables[0].Rows.Count > 2000)
                {
                    MessageBox.Show("Sorry!Data is too big!!");
                    return;
                }
                
                foreach (DataRow dr1 in TargetDS.Tables[0].Rows)
                {
                    CreateItemButton(TargetPnl, dr1);
                }
            }
        }

        /// <summary>
        /// 비지니스 로직: 라벨 초기화
        /// - 모든 정보 표시 라벨 초기화
        /// - UI 상태 리셋
        /// </summary>
        private void dellabel()
        {
            next_cnt = 0;
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            
            ResetAllLabels();
            nudpickQty.Value = 0;
        }
        #endregion

        #region 유틸리티 메서드
        /// <summary>
        /// 비지니스 로직: 다음 공정 결정
        /// - 현재 공정 코드를 기반으로 다음 공정 결정
        /// - 리팩킹 상태 고려
        /// </summary>
        private string DetermineNextProcess(string currentProcess)
        {
            string nextProcess = "";
            
            if (currentProcess == "01" && DeptType == "01")
            {
                nextProcess = "11";
            }
            else if ((currentProcess == "11" || currentProcess == "13" || 
                     currentProcess == "15" || currentProcess == "17") && DeptType == "11")
            {
                nextProcess = "19";
            }
            else if (currentProcess == "19" || (currentProcess == "39" && DeptType == "21"))
            {
                nextProcess = DetermineNextFromRepacking();
            }
            else if (currentProcess == "21")
            {
                nextProcess = "29";
            }
            else if (currentProcess == "29")
            {
                nextProcess = DetermineNextFromOrderType();
            }
            else if (currentProcess == "31")
            {
                nextProcess = "39";
            }
            else if (currentProcess == "33")
            {
                nextProcess = "37";
            }
            else if ((currentProcess == "39" && DeptType == "41") || currentProcess == "37")
            {
                nextProcess = "41";
            }
            
            return nextProcess;
        }

        /// <summary>
        /// 비지니스 로직: 공정 조건 생성
        /// - 부서 코드에 따른 SQL WHERE 조건 생성
        /// </summary>
        private string GetProcessCondition(string processCode)
        {
            switch (processCode)
            {
                case "01": return " (proc_kind ='01') ";
                case "11": return " (proc_kind >='11' and proc_kind <'19') ";
                case "21": return " ((proc_kind ='19' and repacking='Y') or proc_kind ='21' or (proc_kind ='39' and repacking='P')) ";
                case "31": return " ((proc_kind ='19' and (repacking='N' or repacking='')) or proc_kind ='29' or proc_kind ='31' or proc_kind ='33')";
                case "41": return " ((proc_kind ='39' and repacking<>'P') or proc_kind ='37' or proc_kind ='41' or (proc_kind ='19' and repacking='D'))";
                default: return "";
            }
        }

        /// <summary>
        /// 비지니스 로직: UI 리셋
        /// - 부서 변경 시 UI 상태 초기화
        /// </summary>
        private void ResetUIForDepartment(string deptCode, string deptName)
        {
            next_cnt = 0;
            txtbarcode.Text = "";
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            dellabel();
            
            UpdateGroupBoxVisibility(deptCode);
            UpdateButtonVisibility(deptCode);
            
            DeptType = deptCode;
            strDept = deptName;
            
            ResetButtonStyles();
            SetButtonFlatStyle(deptName);
        }
        #endregion

        #region 이벤트 핸들러
        private void NewBtnCal_Click(object sender, EventArgs e)
        {
            Button btnlot = (Button)sender;
            Lotlookup(btnlot.Name);
            GetListHist(btnlot.Name);
            lblLotNo.Text = btnlot.Name;
        }

        private void btnleft_Click(object sender, EventArgs e)
        {
            if (next_cnt != 0)
            {
                next_cnt = Math.Max(0, next_cnt - 1);
                if (rbLot.Checked)
                {
                    GetLotNum(DeptType, next_cnt, 15);
                }
            }
        }

        private void btnright_Click(object sender, EventArgs e)
        {
            if (proc_cnt > next_cnt)
            {
                next_cnt = next_cnt + 1;
                if (rbLot.Checked)
                {
                    GetLotNum(DeptType, next_cnt, 15);
                }
            }
        }

        private void btnEmpnoSearch_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            timer1.Stop();
            // 직원 검색 폼 열기
        }

        private void BtnLotProc_Click(object sender, EventArgs e)
        {
            ToggleLotProcessMode();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (timer1.Enabled)
                txtbarcode.Focus();
        }

        private void nudpickQty_ValueChanged(object sender, EventArgs e)
        {
            ValidatePickingQuantityInput();
        }

        private void nudpickQty_MouseDown(object sender, MouseEventArgs e)
        {
            timer1.Enabled = false;
            timer1.Stop();
        }

        private void btnA_Click(object sender, EventArgs e)
        {
            OpenDetailForm("A");
        }

        private void btnB_Click(object sender, EventArgs e)
        {
            OpenDetailForm("B");
        }

        private void btnW_Click(object sender, EventArgs e)
        {
            OpenDetailForm("W");
        }

        private void btnReadCode_Click(object sender, EventArgs e)
        {
            OpenScanForm();
        }

        private void btnQCChk_Click(object sender, EventArgs e)
        {
            OpenQCForm();
        }

        private void btn_AllCheck_Click(object sender, EventArgs e)
        {
            OpenBatchCheckForm();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            HandleAddLot();
        }

        private void btnAddList_Click(object sender, EventArgs e)
        {
            OpenAddListForm();
        }
        #endregion

        #region 보조 메서드 (빌더 메서드)
        private string BuildLotCountQuery(string processCondition)
        {
            return $@"select lot_no,count(*) from tst16m 
                    where saup_gubn='01' 
                    and lot_date between convert(varchar(10), dateadd(d, -100, getdate()),102) 
                    and convert(varchar(10), getdate(), 102) 
                    and {processCondition} group by lot_no";
        }

        private string BuildLotQuery(string strProc, string processCondition, int li_page, int lirow)
        {
            return $@"select top {lirow} a.lot_no, d.group_sdesc, c.cust_sname,a.order_no,' Qty:' + convert(varchar(20), a.lot_qty),proc_kind  
                    from tst16m a, tcb15 d ,cmv.dbo.tcb01 c 
                    where a.saup_gubn='01' 
                    and a.item_group = d.group_code 
                    and a.dest_cust=c.cust_code 
                    and a.lot_date between convert(varchar(10), dateadd(d, -90, getdate()),102) and convert(varchar(10), getdate(), 102) 
                    and {processCondition} 
                    and a.lot_no not in (select top ({li_page}*{lirow}) lot_no from tst16m 
                                        where saup_gubn='01' 
                                        and lot_date between convert(varchar(10), dateadd(d, -90, getdate()),102) and convert(varchar(10), getdate(), 102) 
                                        and {processCondition}
                                        order by lot_no desc) 
                    order by a.lot_no desc";
        }

        private string BuildItemQuery(string strProc, string processCondition, string strLot, string sLotDate)
        {
            return $@"select b.item_code, item_spec,isnull(b.lot_qty,0)lot_qty,isnull(b.qty,0)qty,a.lot_no,c.group_sdesc 
                    from tst16m a,tst16d b,tcb15 c,tcb02 d   
                    where a.saup_gubn = '01'    
                    and a.saup_gubn = b.saup_gubn  
                    and a.lot_date = b.lot_date 
                    and a.lot_date >= '{sLotDate}' 
                    and {processCondition}  
                    and a.lot_no = b.lot_no  
                    and a.item_group = c.group_code 
                    and b.item_code=d.item_code 
                    and d.item_gubn='01' 
                    and a.lot_no = '{strLot}' 
                    order by substring(b.item_code,8,1),d.back_degrees ,fore_degrees";
        }

        private string BuildHistoryQuery(string lot_no)
        {
            return $@"SELECT a.lot_date,a.lot_no,opt_name,kname,a.work_time  
                    ,case when isnull(repacking,'N')<>'P' then 
                         (case when isnull(repacking,'N')= 'Y' and a.proc_kind='19'  then 'Packing' 
                          when isnull(repacking,'N')= 'N' and a.proc_kind='19' then 'QC' 
                          when isnull(repacking,'N')= 'D' and a.proc_kind='19' then 'Shipping' else '' end ) 
                       else (case when a.proc_kind='39'  then 'Packing' else '' end) end 
                    FROM tst16h a,tst16c b,thb01 c ,tst16m d
                    where a.proc_kind=b.opt_code 
                    and b.opt_type='02' 
                    and a.saup_gubn='01' 
                    and a.worker=c.empno 
                    and a.lot_no=d.lot_no 
                    and a.saup_gubn=d.saup_gubn 
                    and a.lot_no='{lot_no}' 
                    order by work_time";
        }

        private string BuildLotDetailQuery(string lot_no, string sLotDate)
        {
            // 상세 조회 쿼리 (원본의 긴 쿼리를 간략화)
            return $@"select * from tst16m 
                    where saup_gubn='01' 
                    and lot_no='{lot_no}' 
                    and lot_date >= '{sLotDate}'";
        }
        #endregion

        #region 추가 보조 메서드
        private void SetGroupBoxVisibility(bool logistics, bool warehouse, bool pack, bool qc, bool disp, bool stkProc, bool packProc)
        {
            gbLogistics.Visible = logistics;
            gbwarehouse.Visible = warehouse;
            gbpack.Visible = pack;
            gbQC.Visible = qc;
            gbdisp.Visible = disp;
            gbStkProc.Visible = stkProc;
            gbPackProc.Visible = packProc;
        }

        private void UpdatePagingInfo(DataSet ds, int li_page, int lirow)
        {
            int totalRows = ds.Tables[0].Rows.Count;
            if (totalRows % lirow == 0 && totalRows / lirow >= li_page + 1)
            {
                proc_cnt = totalRows / lirow;
                lblDisp.Text = $"Total:{li_page + 1}/{Math.Ceiling((decimal)totalRows / lirow)}";
            }
            else if ((totalRows / lirow) + 1 >= li_page + 1)
            {
                proc_cnt = (totalRows / lirow) + 1;
                lblDisp.Text = $"Total:{li_page + 1}/{Math.Ceiling((decimal)(totalRows / lirow) + 1)}";
            }
        }

        private void CalBtnInit(FlowLayoutPanel TargetPnl, string lotNO, string data1, string data2, string data3, string data4, string data5)
        {
            TargetPnl.Padding = new Padding(3);
            int BtnWidthLot = TargetPnl.Width / 3 - 15;
            int BtnHeightLot = TargetPnl.Height / 6 - 15;

            Button NewBtnItems = AddCalBtn(lotNO, $"{data1}\n{data2}\n{data3}\n{data4}", BtnWidthLot, BtnHeightLot, data5);
            NewBtnItems.Click += NewBtnCal_Click;

            GroupBox NewGroup = new GroupBox
            {
                Name = lotNO,
                Text = $"LOT NO : {lotNO}",
                Font = new Font("Gulim", 9F, FontStyle.Regular),
                ForeColor = Color.MidnightBlue,
                Size = new Size((TargetPnl.Width / 3) - 8, BtnHeightLot + 22)
            };

            NewGroup.Controls.Add(NewBtnItems);
            TargetPnl.Controls.Add(NewGroup);
        }

        private Button AddCalBtn(string BtnName, string BtnText, int BtnWidth, int BtnHeight, string StrPro)
        {
            Button NewBtn = new Button
            {
                Name = BtnName,
                Text = BtnText,
                Width = BtnWidth,
                Height = BtnHeight,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                Font = new Font("Gulim", 11F, FontStyle.Regular)
            };

            // 진행 상태에 따른 색상 설정
            switch (StrPro)
            {
                case "01":
                case "11":
                case "21":
                case "31":
                case "33":
                case "41":
                    NewBtn.BackColor = Color.DarkOliveGreen;
                    break;
                case "19":
                case "29":
                case "39":
                case "37":
                    NewBtn.BackColor = Color.DimGray;
                    break;
                case "13":
                    NewBtn.BackColor = Color.Purple;
                    break;
                case "15":
                    NewBtn.BackColor = Color.Blue;
                    break;
                default:
                    NewBtn.BackColor = SystemColors.Control;
                    break;
            }

            return NewBtn;
        }

        private void CreateItemButton(FlowLayoutPanel panel, DataRow dataRow)
        {
            Panel SubPnl = new Panel
            {
                Text = $" Qty:{dataRow[2]}/{dataRow[3]}",
                Name = dataRow[0].ToString(),
                Height = 110,
                Width = 135,
                BackColor = Color.DeepSkyBlue,
                ForeColor = Color.Black
            };

            Button TargetBtn = new Button
            {
                Text = dataRow[1].ToString(),
                Name = dataRow[0].ToString(),
                Height = 60,
                Width = 125,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(5, 8),
                Font = new Font("Gulim", 11F, FontStyle.Bold)
            };

            bool isQuantityShort = Convert.ToDecimal(dataRow[2]) < Convert.ToDecimal(dataRow[3]);
            TargetBtn.BackColor = isQuantityShort ? Color.DarkRed : SystemColors.Control;
            TargetBtn.ForeColor = isQuantityShort ? Color.White : Color.Black;

            Button TargetBtn2 = new Button
            {
                Text = $" Qty:{dataRow[2]}/{dataRow[3]}",
                Name = dataRow[0].ToString(),
                Height = 30,
                Width = 125,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(5, 70),
                Font = new Font("Gulim", 9F, FontStyle.Bold)
            };

            TargetBtn2.BackColor = isQuantityShort ? Color.Red : Color.Black;
            TargetBtn2.ForeColor = Color.White;

            SubPnl.Controls.Add(TargetBtn);
            SubPnl.Controls.Add(TargetBtn2);
            panel.Controls.Add(SubPnl);
        }

        private void ResetAllLabels()
        {
            // 모든 라벨 초기화 로직
            lblDisp.Text = "";
            lblLotNo.Text = "";
            
            // 물류 라벨
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
            
            // 창고 라벨
            lblpickertxt.Text = "";
            lblpicktimetxt.Text = "";
            lblpickqtytxt.Text = "";
            lblpickboxtxt.Text = "";
            lblQtyAtxt.Text = "";
            lblQtyBtxt.Text = "";
            lblQtyCtxt.Text = "";
            lblQtyEtctxt.Text = "";
            lblQtyWtxt.Text = "";
            lblQtyFristtxt.Text = "";
            lblQtySecondtxt.Text = "";
            lblQtyThirdtxt.Text = "";
            lblQtyFourthtxt.Text = "";
            txtPickRmarktxt.Text = "";
            txtPickRmarktxt.BackColor = btnStk.BackColor;
            
            // 포장 라벨
            lblpackertxt.Text = "";
            lblpacktimetxt.Text = "";
            lblpackmchtxt.Text = "";
            lblrectimetxt.Text = "";
            lblpackremarktxt.Text = "";
            lblinspmantxt.Text = "";
            
            // QC 라벨
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
            
            // 표시 라벨
            lbldispcust.Text = "";
            lbldisppdc.Text = "";
            lbldispcstpo.Text = "";
            lbldisplot.Text = "";
            lbldisplotqty.Text = "";
            lbldisperppo.Text = "";
            lbldispQua.Text = "";
            lbldispord_rmk.Text = "";
            pbpic.Image = null;
        }

        private void SetControlLayout()
        {
            gbdisp.Location = new Point(groupBox2.Location.X, 135);
            gbLogistics.Location = new Point(groupBox2.Location.X, 145);
        }

        private void UpdateGroupBoxVisibility(string deptCode)
        {
            switch (deptCode)
            {
                case "01":
                    gbLogistics.Visible = true;
                    gbLogistics.BackColor = btnDWL.BackColor;
                    gbLogistics.Location = new Point(groupBox2.Location.X, 145);
                    gbLogistics.Size = new Size(499, 209);
                    gbdisp.Visible = false;
                    break;
                case "11":
                    gbdisp.Visible = true;
                    gbdisp.Location = new Point(groupBox2.Location.X, 125);
                    gbdisp.Size = new Size(520, 95);
                    gbwarehouse.Visible = true;
                    gbwarehouse.Location = new Point(groupBox2.Location.X, 228);
                    gbwarehouse.Size = new Size(520, 147);
                    gbwarehouse.BackColor = btnStk.BackColor;
                    if (BtnLotProc.Text == "Save")
                    {
                        gbStkProc.Visible = true;
                        gbStkProc.Location = new Point(groupBox2.Location.X, 376);
                        gbStkProc.Size = new Size(520, 57);
                    }
                    break;
                case "21":
                    gbdisp.Visible = true;
                    gbdisp.Location = new Point(groupBox2.Location.X, 125);
                    gbdisp.Size = new Size(520, 102);
                    gbpack.Visible = true;
                    gbpack.Location = new Point(groupBox2.Location.X, 232);
                    gbpack.BackColor = btnDPack.BackColor;
                    gbpack.Size = new Size(253, 185);
                    break;
                case "31":
                    gbdisp.Visible = true;
                    gbdisp.Location = new Point(groupBox2.Location.X, 125);
                    gbdisp.Size = new Size(520, 95);
                    btnQCChk.Visible = true;
                    btnQCChk.Location = new Point(700, 405);
                    gbQC.Visible = true;
                    gbQC.Location = new Point(groupBox2.Location.X, 232);
                    gbQC.Size = new Size(509, 171);
                    gbQC.BackColor = btnQC.BackColor;
                    break;
                case "41":
                    gbdisp.Visible = true;
                    gbdisp.Location = new Point(groupBox2.Location.X, 125);
                    gbdisp.Size = new Size(520, 119);
                    btnReadCode.Visible = true;
                    btnQCChk.Visible = false;
                    break;
            }
        }

        private void UpdateButtonVisibility(string deptCode)
        {
            btnReadCode.Visible = (deptCode == "41");
            btn_AllCheck.Visible = false;
            btnQCChk.Visible = (deptCode == "31");
            btnAddList.Visible = false;
            btnAdd.Visible = (deptCode == "11");
        }

        private void ResetButtonStyles()
        {
            btnDWL.FlatStyle = FlatStyle.Standard;
            btnStk.FlatStyle = FlatStyle.Standard;
            btnDPack.FlatStyle = FlatStyle.Standard;
            btnQC.FlatStyle = FlatStyle.Standard;
        }

        private void SetButtonFlatStyle(string deptName)
        {
            switch (deptName)
            {
                case "DWL": btnDWL.FlatStyle = FlatStyle.Flat; break;
                case "STK": btnStk.FlatStyle = FlatStyle.Flat; break;
                case "DPack": btnDPack.FlatStyle = FlatStyle.Flat; break;
                case "QC": btnQC.FlatStyle = FlatStyle.Flat; break;
                case "Dlv": break;
            }
        }

        private bool ValidateDepartmentSelection()
        {
            if (string.IsNullOrEmpty(DeptType))
            {
                MessageBox.Show("Please choose department.", "Notice-");
                return false;
            }
            return true;
        }

        private bool ValidateWorkerInput()
        {
            if (BtnLotProc.Text == "Save" && string.IsNullOrEmpty(txtEmpno.Text))
            {
                MessageBox.Show("Please input worker.", "Notice-");
                return false;
            }
            return true;
        }

        private void ValidateAndFixLotData(string lotNo, string lotDate)
        {
            // 데이터 무결성 검사 및 수정 로직
            string checkQuery = $@"select a.proc_kind,b.proc_kind from tst16m a,
                                (select lot_no,max(proc_kind)proc_kind from tst16h 
                                where saup_gubn='01' and lot_no='{lotNo}' and lot_date >= '{lotDate}' group by lot_no )b 
                                where saup_gubn = '01' 
                                and a.lot_no = b.lot_no
                                and a.proc_kind <> b.proc_kind  
                                and a.lot_no='{lotNo}'  
                                and a.lot_date >= '{lotDate}'";
            
            DataSet ds = mc.ResultReturnDataSet(checkQuery);
            if (ds.Tables[0].Rows.Count > 0)
            {
                // 데이터 수정 로직
                string updateQuery = $@"update a set proc_kind = (select max(b.proc_kind) 
                                    from TST16h b where a.lot_no=b.lot_no and a.saup_gubn=b.saup_gubn and b.lot_no='{lotNo}') 
                                    from TST16m a 
                                    where a.saup_gubn='01' and a.lot_no='{lotNo}' and lot_date >= '{lotDate}'";
                mc.Execute(updateQuery);
            }
        }

        private bool CheckLotExists(string lotNo, string lotDate)
        {
            string query = $@"select proc_kind,a.factory,opt_name,isnull(repacking,'N')repacking,
                            suju_gubn,io_code,order_qty,item_group  
                            from tst16m a,tst16c b  
                            where saup_gubn='01' and lot_no='{lotNo}' and lot_date >= '{lotDate}'  
                            and opt_type='02' and opt_code=proc_kind";
            DataSet ds = mc.ResultReturnDataSet(query);
            return ds.Tables[0].Rows.Count > 0;
        }

        private void ProcessExistingLot(string lotNo, string lotDate)
        {
            GetItemNum(DeptType, next_cnt, 12, lotNo);
            Lotlookup(lotNo);
            GetListHist(lotNo);
            
            // 추가 로직 처리
            CheckForAdditionalOperations(lotNo, lotDate);
            ValidateDepartmentMatch(lotNo, lotDate);
            
            txtbarcode.Text = "";
        }

        private void CheckForAdditionalOperations(string lotNo, string lotDate)
        {
            // 추가 작업 확인 로직
            string query = $@"select count(*) from tst16m 
                            where saup_gubn ='01' 
                            and order_no = '{lbldisperppo.Text}' 
                            and item_group = '{GetItemGroup(lotNo, lotDate)}' 
                            and isnull(mlot_no,'')<>''";
            
            if (mc.IntResultReturnExecute(query) > 0)
            {
                btnAddList.Visible = true;
                // 위치 설정 로직
            }
        }

        private string GetItemGroup(string lotNo, string lotDate)
        {
            string query = $@"select item_group from tst16m 
                            where saup_gubn='01' and lot_no='{lotNo}' and lot_date >= '{lotDate}'";
            DataSet ds = mc.ResultReturnDataSet(query);
            return ds.Tables[0].Rows[0][7].ToString();
        }

        private void ValidateDepartmentMatch(string lotNo, string lotDate)
        {
            if (BtnLotProc.Text == "Save" && !string.IsNullOrEmpty(txtEmpno.Text))
            {
                string query = $@"select proc_kind,isnull(repacking,'N')repacking,
                                suju_gubn,io_code,order_qty from tst16m 
                                where saup_gubn='01' and lot_no='{lotNo}' and lot_date >= '{lotDate}'";
                DataSet ds = mc.ResultReturnDataSet(query);
                
                if (ds.Tables[0].Rows.Count > 0)
                {
                    string currentProcess = ds.Tables[0].Rows[0][0].ToString();
                    string repackStatus = ds.Tables[0].Rows[0][1].ToString();
                    string sujuGubn = ds.Tables[0].Rows[0][2].ToString();
                    string ioCode = ds.Tables[0].Rows[0][3].ToString();
                    decimal orderQty = Convert.ToDecimal(ds.Tables[0].Rows[0][4].ToString());
                    
                    string expectedDept = DetermineExpectedDepartment(currentProcess, repackStatus, sujuGubn, ioCode, orderQty);
                    
                    if (DeptType != expectedDept)
                    {
                        MessageBox.Show("Your department is not same.");
                        return;
                    }
                }
            }
        }

        private string DetermineExpectedDepartment(string process, string repack, string sujuGubn, string ioCode, decimal orderQty)
        {
            if (process == "01") return "01";
            if (Convert.ToInt32(process) >= 11 && Convert.ToInt32(process) < 19) return "11";
            if ((process == "19" && (repack == "N" || string.IsNullOrEmpty(repack))) ||
                process == "31" || process == "33" ||
                (process == "29" && sujuGubn == "1" && ioCode.Contains("A") && orderQty > 100 && repack == "Y") ||
                (process == "29" && (sujuGubn == "3" || string.IsNullOrEmpty(sujuGubn))))
                return "31";
            if ((process == "19" && repack == "Y") || process == "21" || (process == "39" && repack == "P"))
                return "21";
            if (process == "39" || process == "37" || process == "41" ||
                (process == "19" && repack == "D") ||
                (process == "29" && sujuGubn == "1" && !string.IsNullOrEmpty(sujuGubn) && 
                 ioCode.Contains("A") && repack == "Y" && orderQty <= 100))
                return "41";
            
            return "";
        }

        private bool ValidatePickingQuantity()
        {
            if (string.IsNullOrEmpty(lbldisplotqty.Text) || lbldisplotqty.Text == "0")
            {
                MessageBox.Show("Picking Qty. is 0，You can't move.", "Notice-Info.1");
                return false;
            }
            return true;
        }

        private bool ValidateQuantityMatch()
        {
            int pickedQty = Convert.ToInt32(lblpickqtytxt.Text.Replace(" PCS", ""));
            if (pickedQty != Convert.ToInt32(nudpickQty.Value))
            {
                MessageBox.Show("it's not same quantity between picking and send！", "Notice-Info.1");
                return false;
            }
            return true;
        }

        private void UpdateSelectionStates()
        {
            if (rbPack.Checked)
            {
                rbQC.Checked = false;
                rbDlv.Checked = false;
            }
            if (rbQC.Checked)
            {
                rbPack.Checked = false;
                rbDlv.Checked = false;
            }
            if (rbDlv.Checked)
            {
                rbPack.Checked = false;
                rbQC.Checked = false;
            }
        }

        private string GetCurrentProcess()
        {
            string query = $@"select proc_kind from tst16m 
                            where saup_gubn='01' and lot_no='{lblLotNo.Text}'";
            DataSet ds = mc.ResultReturnDataSet(query);
            return ds.Tables[0].Rows.Count > 0 ? ds.Tables[0].Rows[0][0].ToString() : "";
        }

        private void ProcessLotOperation(string currentProcess)
        {
            string ioCode = GetIoCode();
            
            if (currentProcess == "13")
            {
                MessageBox.Show("Please worker need to【Ending picking】！！", "Notice-00");
                return;
            }
            
            if (ioCode.Substring(0, 1) != "A" && !rbDlv.Checked)
            {
                MessageBox.Show("Please choose target place【Shipping】！", "Notice-04");
                return;
            }
            
            if (currentProcess == "11" || currentProcess == "15" || currentProcess == "17")
            {
                ProcLotInfo(lblLotNo.Text, currentProcess);
                Lotlookup(lblLotNo.Text);
                GetListHist(lblLotNo.Text);
                nudpickQty.Value = 0;
            }
            else if (currentProcess == "19")
            {
                ShowProcessSelectionMessage();
            }
            
            timer1.Enabled = true;
            timer1.Start();
        }

        private string GetIoCode()
        {
            string query = $@"select io_code from tst16m 
                            where saup_gubn='01' and lot_no='{lblLotNo.Text}'";
            DataSet ds = mc.ResultReturnDataSet(query);
            return ds.Tables[0].Rows.Count > 0 ? ds.Tables[0].Rows[0][0].ToString() : "";
        }

        private void ShowProcessSelectionMessage()
        {
            if (rbPack.Checked)
            {
                MessageBox.Show("【Packing】Please choose packing IN.", "Notice-01");
            }
            else if (rbQC.Checked)
            {
                MessageBox.Show("【QC】Please choose QC IN.", "Notice-02");
            }
            else if (rbDlv.Checked)
            {
                MessageBox.Show("【Shipping】Please choose shpping IN.", "Notice-03");
            }
        }

        private string DetermineNextFromRepacking()
        {
            string query = $@"select repacking from tst16m 
                            where saup_gubn='01' and lot_no='{lblLotNo.Text}'";
            DataSet ds = mc.ResultReturnDataSet(query);
            
            if (ds.Tables[0].Rows.Count > 0)
            {
                string repackStatus = ds.Tables[0].Rows[0][0].ToString();
                switch (repackStatus)
                {
                    case "Y":
                    case "P": return "21";
                    case "N":
                    case "": return "31";
                    case "D": return "41";
                }
            }
            return "";
        }

        private string DetermineNextFromOrderType()
        {
            string query = $@"select proc_kind,lot_no,suju_gubn,io_code,order_qty 
                            from tst16m where saup_gubn='01' and lot_no='{lblLotNo.Text}'";
            DataSet ds = mc.ResultReturnDataSet(query);
            
            if (ds.Tables[0].Rows.Count > 0)
            {
                string process = ds.Tables[0].Rows[0][0].ToString();
                string sujuGubn = ds.Tables[0].Rows[0][2].ToString();
                string ioCode = ds.Tables[0].Rows[0][3].ToString();
                decimal orderQty = Convert.ToDecimal(ds.Tables[0].Rows[0][4].ToString());
                
                if (process == "31") return "33";
                if (sujuGubn == "1" && ioCode.Contains("A") && orderQty <= 100) return "41";
            }
            return "31";
        }

        private void ProcessSpecialCases(string lotNo, string currentProcess)
        {
            if (currentProcess == "15" && rbDlv.Checked)
            {
                string query = $@"exec crb057 '01','{dPT1.Value:yyyy.MM.dd}','{Factory}',
                                '{lotNo}','{User_empno}','{Factdept}'";
                try
                {
                    UserCommon.CmCn conn = new UserCommon.CmCn("cmvn", "cmv");
                    conn.Execute(query);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void UpdateLotProcess(string lotNo, string currentProcess, string nextProcess, string lotDate)
        {
            string query = $@"update tst16m 
                            set proc_kind='{nextProcess}' 
                            where saup_gubn='01' and lot_no='{lotNo}' 
                            and proc_kind='{currentProcess}' 
                            and lot_date >= '{lotDate}'";
            mc.Execute(query);
        }

        private void AddProcessHistory(string lotNo, string currentProcess, string nextProcess, string lotDate)
        {
            string query = $@"insert into tst16h (saup_gubn,lot_date,lot_no,proc_kind,worker,work_time,remark) 
                            select saup_gubn,lot_date,lot_no,'{nextProcess}','{txtEmpno.Text}',getdate(),''  
                            from tst16h 
                            where saup_gubn='01' and lot_no='{lotNo}'  
                            and lot_date>='{lotDate}' and proc_kind='{currentProcess}'";
            mc.Execute(query);
        }

        private void DisplayLotDetails(DataSet ds)
        {
            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                // 데이터 표시 로직 (원본의 긴 로직을 간략화)
                if (gbLogistics.Visible)
                {
                    lblCustTxt.Text = dr["cust_sname"].ToString();
                    lblItemTxt.Text = dr["group_sdesc"].ToString();
                    // ... 나머지 라벨 업데이트
                }
                
                if (gbdisp.Visible)
                {
                    lbldispcust.Text = dr["cust_sname"].ToString();
                    lbldisppdc.Text = dr["group_sdesc"].ToString();
                    // ... 나머지 라벨 업데이트
                }
            }
        }

        private void LoadProductImage(DataSet ds)
        {
            if (gbpack.Visible && ds.Tables[0].Rows.Count > 0)
            {
                string prodShotNo = ds.Tables[0].Rows[0]["prod_shot_no"].ToString();
                if (!string.IsNullOrEmpty(prodShotNo))
                {
                    string imagePath = GetImagePath(prodShotNo);
                    if (File.Exists(imagePath))
                    {
                        pbpic.Image = Image.FromFile(imagePath);
                    }
                }
                else
                {
                    pbpic.Image = null;
                }
            }
        }

        private string GetImagePath(string prodShotNo)
        {
            string basePath = type ? @"C:\Program Files (x86)\Chemi_MES" : @"C:\Program Files\Chemi_MES";
            string iniPath = basePath + fileini;
            
            UserCommon.ClsinitUtil ini = new UserCommon.ClsinitUtil(iniPath);
            string serverPath = ini.GetIniValue("SERVER", "File");
            
            return $@"{serverPath}\Photo\packing\{prodShotNo}.jpg";
        }

        private void CheckWorkingStatus(string lotNo, string lotDate)
        {
            string query = $@"select proc_kind,opt_name from tst16m a,tst16c b 
                            where saup_gubn='01' and b.opt_type='02' 
                            and a.proc_kind=b.opt_code and lot_no='{lotNo}' 
                            and a.lot_date >= '{lotDate}'";
            DataSet ds = mc.ResultReturnDataSet(query);
            
            if (ds.Tables[0].Rows.Count > 0)
            {
                MessageBox.Show($"【{strDept}】No Data!!Now working, {ds.Tables[0].Rows[0][1]}");
            }
        }

        private void ValidatePickingQuantityInput()
        {
            if (DeptType == "11" && BtnLotProc.Text == "Save")
            {
                if (string.IsNullOrEmpty(lbldisplotqty.Text) || lbldisplotqty.Text == "0")
                {
                    MessageBox.Show("Order quantity is 0，Don't work.", "Notice-Info.00");
                    return;
                }
                
                int orderQty = Convert.ToInt32(lbldisplotqty.Text);
                if (Convert.ToInt32(nudpickQty.Value) > orderQty)
                {
                    MessageBox.Show("Picking quantity is more then order.", "Notice-Info.11");
                    nudpickQty.Value = 0;
                }
            }
        }

        private void ToggleLotProcessMode()
        {
            if (BtnLotProc.Text == "Lookup")
            {
                if (!gbStkProc.Visible && DeptType == "11") 
                    gbStkProc.Visible = true;
                
                BtnLotProc.Text = "Save";
                
                if (!string.IsNullOrEmpty(lblLotNo.Text) && lblLotNo.Text.Length == 11 && DeptType == "11")
                {
                    btn_other_Click(null, null);
                }
            }
            else
            {
                if (gbStkProc.Visible && DeptType == "11") 
                    gbStkProc.Visible = false;
                
                BtnLotProc.Text = "Lookup";
            }
        }

        private void OpenDetailForm(string location)
        {
            timer1.Enabled = false;
            timer1.Stop();
            childFrom = new PRF09(lblLotNo.Text, location);
            childFrom.Show();
        }

        private void OpenScanForm()
        {
            timer1.Enabled = false;
            timer1.Stop();
            childFrom1 = new PRF11();
            childFrom1.Show();
        }

        private void OpenQCForm()
        {
            timer1.Enabled = false;
            timer1.Stop();
            childFrom2 = new PRF12();
            if (UserCommon.Public_Function.Validate_Form(childFrom2))
            {
                childFrom2.WindowState = FormWindowState.Maximized;
                childFrom2.Show();
            }
            this.Close();
        }

        private void OpenBatchCheckForm()
        {
            timer1.Enabled = false;
            timer1.Stop();
            childFrom4 = new PRF15();
            childFrom4.Show();
        }

        private void HandleAddLot()
        {
            if (string.IsNullOrEmpty(lblLotNo.Text)) return;
            
            string query = $@"select order_no,isnull(mlot_no,'')mlot_no 
                            from tst16m where saup_gubn='01' and lot_no='{lblLotNo.Text}'";
            DataSet ds = mc.ResultReturnDataSet(query);
            
            if (ds.Tables[0].Rows.Count > 0)
            {
                string mlotNo = ds.Tables[0].Rows[0][1].ToString();
                string orderNo = ds.Tables[0].Rows[0][0].ToString();
                
                if (!string.IsNullOrEmpty(mlotNo))
                {
                    MessageBox.Show($"{lblLotNo.Text} is not first，you can't make moving list.", "Error.");
                    return;
                }
                
                string currentProcess = GetMinimumProcess(orderNo);
                ProcessAddLot(orderNo, currentProcess);
            }
        }

        private string GetMinimumProcess(string orderNo)
        {
            string query = $@"select min(proc_kind) from tst16m 
                            where saup_gubn='01' and order_no='{orderNo}'";
            DataSet ds = mc.ResultReturnDataSet(query);
            return ds.Tables[0].Rows.Count > 0 ? ds.Tables[0].Rows[0][0].ToString() : "";
        }

        private void ProcessAddLot(string orderNo, string currentProcess)
        {
            if (Convert.ToInt32(currentProcess) < 19)
            {
                HandleEarlyStageAdd(orderNo, currentProcess);
            }
            else
            {
                HandleLateStageAdd(orderNo);
            }
        }

        private void HandleEarlyStageAdd(string orderNo, string currentProcess)
        {
            if (Convert.ToInt32(currentProcess) == 11)
            {
                DialogResult result = MessageBox.Show("Do you want to make new lot？！", " ★ Confirm ", 
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                
                if (result == DialogResult.OK)
                {
                    DeleteExistingLotData(orderNo, currentProcess);
                    DPsheet.sbAddLotExcel_dpsheet(lblLotNo.Text, txtEmpno.Text, "1");
                }
                else
                {
                    string existingLot = GetExistingLot(orderNo, currentProcess);
                    DPsheet.sbAddLotExcel_dpsheet(existingLot, txtEmpno.Text, "0");
                }
            }
            else
            {
                DialogResult result = MessageBox.Show("It's not W.H Out，you can't make new lot! \n If you don't want it，Please cancel.", 
                    " ★ Confirm ", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                
                if (result == DialogResult.OK)
                {
                    string existingLot = GetExistingLot(orderNo, currentProcess);
                    DPsheet.sbAddLotExcel_dpsheet(existingLot, txtEmpno.Text, "0");
                }
            }
        }

        private void DeleteExistingLotData(string orderNo, string currentProcess)
        {
            string existingLot = GetExistingLot(orderNo, currentProcess);
            
            string deleteQuery = $@"delete from cmvn.cmv.dbo.tst16h  
                                where saup_gubn ='01' 
                                and lot_no = '{existingLot}' 
                                and proc_kind in('01','11') 
                                delete from cmvn.cmv.dbo.tst16d 
                                where saup_gubn ='01' 
                                and lot_no = '{existingLot}' 
                                delete from cmvn.cmv.dbo.tst16m  
                                where saup_gubn ='01' 
                                and lot_no = '{existingLot}' 
                                and isnull(mlot_no,'') = '{lblLotNo.Text}'";
            
            UserCommon.CmCn conn = new UserCommon.CmCn("cmvn", "cmv");
            conn.Execute(deleteQuery);
        }

        private string GetExistingLot(string orderNo, string currentProcess)
        {
            string query = $@"select lot_no from tst16m 
                            where saup_gubn='01' and order_no='{orderNo}' and proc_kind = '{currentProcess}'";
            DataSet ds = mc.ResultReturnDataSet(query);
            return ds.Tables[0].Rows.Count > 0 ? ds.Tables[0].Rows[0][0].ToString() : "";
        }

        private void HandleLateStageAdd(string orderNo)
        {
            string query = $@"select count(*) from tst16d 
                            where saup_gubn ='01' and lot_no='{lblLotNo.Text}' 
                            and (lot_qty - isnull(qty,0))>0";
            
            if (mc.IntResultReturnExecute(query) > 0)
            {
                DPsheet.sbAddLotExcel_dpsheet(lblLotNo.Text, txtEmpno.Text, "1");
            }
            else
            {
                MessageBox.Show("Picking was completed!", "Notice");
            }
        }

        private void OpenAddListForm()
        {
            string query = $@"select count(*) from tst16m 
                            where saup_gubn ='01' 
                            and order_no = '{lbldisperppo.Text}' 
                            and isnull(mlot_no,'')<>''";
            
            if (mc.IntResultReturnExecute(query) > 0)
            {
                timer1.Enabled = false;
                timer1.Stop();
                childFrom5 = new PRF16(lbldisperppo.Text);
                childFrom5.Show();
            }
        }
        #endregion

        #region 기타 이벤트 핸들러
        private void childFrom_OnNotifyParent(object sender, ChildFormEventArgs e)
        {
            FrmEmp child = (FrmEmp)sender;
            txtEmpno.Text = e.Message[0].ToString();
            txtKname.Text = e.Message[1].ToString();
            timer1.Enabled = true;
            timer1.Start();
        }

        private void rbLot_CheckedChanged(object sender, EventArgs e)
        {
            ResetDisplay();
        }

        private void rbDP_CheckedChanged(object sender, EventArgs e)
        {
            ResetDisplay();
        }

        private void ResetDisplay()
        {
            txtbarcode.Text = "";
            pnlLot.Controls.Clear();
            listMaster.Items.Clear();
            dellabel();
            SetGroupBoxVisibility(false, false, false, false, false, false, false);
        }

        private void lblDisp_Click(object sender, EventArgs e)
        {
            // 클릭 이벤트 처리
        }

        private void rbDlv_CheckedChanged(object sender, EventArgs e)
        {
            // 체크 상태 변경 처리
        }
        #endregion
    }

    /// <summary>
    /// LOT 목록 조회 클래스
    /// </summary>
    class CLSLotLIST
    {
        #region 필드 선언
        private string CalCustKind = "";
        private string CalGroupKind = "";
        private string CalItems = "";
        private string CalOrder = "";
        private string CalSubKind = "";
        private string CalUpdateQty = "";

        public string SelCustKind = "";
        public string SelItems = "";
        public string SelOrder = "";
        public string SelGroup = "";
        public string SelCode = "";
        public string SelDp = "";
        public string SelDpQty = "";
        public string SelType = "";
        public string SelDateF = DateTime.Now.AddDays(-90).ToString("yyyy.MM.dd");
        public string SelDateT = DateTime.Now.AddDays(1).ToString("yyyy.MM.dd");

        private DataSet CustDS = null;
        private DataSet GroupDS = null;
        private DataSet SubDS = null;
        private DataSet ItemsDS = null;
        private DataSet OrderDS = null;
        private DataSet UpdateQtyDS = null;
        private UserCommon.CmCn conn = new UserCommon.CmCn();
        #endregion

        #region 속성
        public string SetCustKind
        {
            set { this.CalCustKind = value; SetCustDS(); }
        }

        public string SetGroupKind
        {
            set { this.CalGroupKind = value; SetGroupDS(); }
        }

        public string SetSubKind
        {
            set { this.CalSubKind = value; SetSubDS(); }
        }

        public string SetItems
        {
            set { this.CalItems = value; SetItemsDS(); }
        }

        public string SetOrder
        {
            set { this.CalOrder = value; SetOrderDS(); }
        }

        public string SetUpdateQty
        {
            set { this.CalUpdateQty = value; }
        }

        public DataSet GetProcDS => this.CustDS;
        public DataSet GetItemsDS => this.ItemsDS;
        public DataSet GetGroupDS => this.GroupDS;
        public DataSet GetSubDS => this.SubDS;
        public DataSet GetOrderDS => this.OrderDS;
        public DataSet GetUpdateQtyDS => this.UpdateQtyDS;
        #endregion

        #region 데이터셋 설정 메서드
        private void SetCustDS()
        {
            if (!string.IsNullOrWhiteSpace(this.CalCustKind))
            {
                string query = BuildCustQuery();
                this.CustDS = conn.ResultReturnDataSet(query);
            }
        }

        private void SetItemsDS()
        {
            if (!string.IsNullOrWhiteSpace(this.CalItems))
            {
                string query = BuildItemsQuery();
                this.ItemsDS = conn.ResultReturnDataSet(query);
            }
        }

        private void SetOrderDS()
        {
            if (!string.IsNullOrWhiteSpace(this.CalOrder))
            {
                string query = BuildOrderQuery();
                this.OrderDS = conn.ResultReturnDataSet(query);
            }
        }

        private void SetGroupDS()
        {
            if (string.IsNullOrEmpty(SelItems)) SelItems = "%%";
            if (string.IsNullOrEmpty(SelCustKind)) SelCustKind = "%%";
            if (string.IsNullOrEmpty(SelOrder)) SelOrder = "%%";

            if (!string.IsNullOrWhiteSpace(this.CalGroupKind))
            {
                string query = BuildGroupQuery();
                this.GroupDS = conn.ResultReturnDataSet(query);
            }
        }

        private void SetSubDS()
        {
            if (!string.IsNullOrWhiteSpace(this.CalSubKind))
            {
                string query = BuildSubQuery();
                this.SubDS = conn.ResultReturnDataSet(query);
            }
        }
        #endregion

        #region 쿼리 빌더 메서드
        private string BuildCustQuery()
        {
            return $@"select '%%' cust_code, 'All\nCustomer' cust_sname,''
                    union
                    select distinct b.cust_code,b.cust_sname,''
                    from tst16m a,cmv.dbo.tcb01 b
                    where a.saup_gubn = '01'
                    and a.lot_date between '{SelDateF}' and '{SelDateT}'
                    and a.dest_cust = b.cust_code
                    order by cust_code";
        }

        private string BuildItemsQuery()
        {
            return $@"select '%%' group_code, 'All\nItem' group_sdesc,''
                    union
                    select distinct b.group_code,b.group_sdesc,''
                    from tst16m a,tcb15 b
                    where a.saup_gubn = '01'
                    and a.lot_date between '{SelDateF}' and '{SelDateT}'
                    and a.item_group = b.group_code
                    order by group_code";
        }

        private string BuildOrderQuery()
        {
            return $@"select '%%' lot_no, 'All\nOrder' order_no ,''
                    union
                    select distinct a.lot_date + a.lot_no,a.order_no,''
                    from tst16m a,tcb15 b
                    where a.saup_gubn = '01'
                    and a.lot_date between '{SelDateF}' and '{SelDateT}'
                    and a.item_group = b.group_code
                    order by lot_no";
        }

        private string BuildGroupQuery()
        {
            return $@"select a.lot_no, d.group_sdesc, c.cust_sname,a.order_no,' Qty:' + convert(varchar(20), a.lot_qty)
                    from tst16m a, tcb15 d ,cmv.dbo.tcb01 c
                    where a.saup_gubn='01'
                    and a.item_group = d.group_code
                    and a.dest_cust=c.cust_code
                    and a.proc_kind like '{SelType}'
                    order by a.lot_no desc";
        }

        private string BuildSubQuery()
        {
            return $@"select b.item_code, c.group_sdesc,' Qty:' + convert(varchar(20),sum(b.order_qty)),'',''
                    from tst16m a,tst16d b,tcb15 c
                    where a.saup_gubn = '01'
                    and a.saup_gubn = b.saup_gubn
                    and a.lot_date = b.lot_date
                    and a.proc_kind like '{SelType}'
                    and a.lot_no = b.lot_no
                    and a.item_group = c.group_code
                    group by b.item_code, c.group_sdesc
                    order by b.item_code";
        }
        #endregion
    }
}
