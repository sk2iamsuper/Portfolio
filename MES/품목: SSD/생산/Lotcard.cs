using ITS.lib.Database;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ITS
{
    public partial class frmLOTCARD_PRINT : Form
    {
        private MySqlConnection _connection; // MySQL 데이터베이스 연결 객체
        
        /// <summary>
        /// Lot 카드 프린트 폼 생성자
        /// </summary>
        /// <param name="connection">MySQL 데이터베이스 연결 객체</param>
        public frmLOTCARD_PRINT(MySqlConnection connection)
        {
            InitializeComponent();
            _connection = connection; // 데이터베이스 연결 저장
        }

        /// <summary>
        /// 폼 로드 이벤트 핸들러 - COM 포트 검색 및 초기화 수행
        /// </summary>
        private void frmLOTCARD_PRINT_Load(object sender, EventArgs e)
        {
            // 설정 파일에서 바코드 프린터 COM 포트 검색
            // "MH_LOTCARD_ZM410"는 설정 파일에서 사용하는 포트 식별 이름
            if (!SearchPort("MH_LOTCARD_ZM410"))
            {
                // COM 포트 검색 실패 시 추가 처리 로직
                // 현재는 특별한 처리가 구현되어 있지 않음
            }
        }

        /// <summary>
        /// 설정 파일(SETTING.ini)에서 지정된 이름의 COM 포트를 검색하고 초기화합니다.
        /// </summary>
        /// <param name="name">설정 파일에서 찾을 포트의 식별 이름</param>
        /// <returns>COM 포트 검색 및 초기화 성공 여부 (true: 성공, false: 실패)</returns>
        private bool SearchPort(string name)
        {
            try
            {
                // 설정 파일 존재 여부 확인 및 열기 시도
                using (var fs = File.Open("SETTING.ini", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    fs.Close(); // 파일 스트림 닫기 (존재 확인만을 위한 작업)

                    // 설정 파일 전체 내용을 기본 인코딩으로 읽기
                    var readData = File.ReadAllText("SETTING.ini", Encoding.Default);

                    // 줄 바꿈 문자를 기준으로 내용을 분할
                    string[] stringSeparators = new string[] { "\r\n" };
                    string[] lines = readData.Split(stringSeparators, StringSplitOptions.None);
                    
                    // 각 줄을 순회하며 지정된 이름의 포트 검색
                    foreach (string s in lines)
                    {
                        // '=' 문자를 기준으로 키와 값 분리
                        if (s.Split('=')[0] == name)
                        {
                            txtComport.Text = s.Split('=')[1]; // COM 포트 값을 텍스트 박스에 설정
                            break; // 찾은 후 루프 종료
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 설정 파일을 찾을 수 없거나 접근 불가능한 경우
                MessageBox.Show("SETTING.ini File not found.");
                return false; // 검색 실패
            }

            // COM 포트 값이 비어있는 경우 처리
            if (txtComport.Text == string.Empty)
            {
                MessageBox.Show($"NO COMPORT {name}");
                return false; // 검색 실패
            }
            else
            {
                try
                {
                    // 시리얼 포트 설정 및 연결 테스트
                    spBarcode.PortName = txtComport.Text; // COM 포트 이름 설정
                    spBarcode.Open(); // 포트 열기 시도

                    // 연결 테스트 성공 후 즉시 닫기 (실제 사용을 위한 연결은 아님)
                    if (spBarcode.IsOpen)
                        spBarcode.Close();
                }
                catch (Exception)
                {
                    // COM 포트 연결 실패 시 오류 메시지 표시
                    MessageBox.Show("바코드 프린터 연결을 확인하세요. ", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false; // 초기화 실패
                }
            }

            return true; // COM 포트 검색 및 초기화 성공
        }

        /// <summary>
        /// 스캔 데이터 입력 처리 (Enter 키 이벤트 핸들러)
        /// </summary>
        private void txtScandata_KeyUp(object sender, KeyEventArgs e)
        {
            // Enter 키가 눌리고 스캔 데이터가 비어있지 않은 경우 처리
            if (e.KeyData == Keys.Enter && txtScandata.Text != string.Empty)
            {
                // 입력 데이터를 대문자로 변환 (표준화)
                txtScandata.Text = txtScandata.Text.ToUpper();

                // 메시지 표시 초기화
                txtMessage.Text = string.Empty;
                txtMessage.BackColor = SystemColors.Control;

                // Lot ID 길이가 10자인 경우에만 처리 (유효성 검사)
                if (txtScandata.Text.Length == 10)
                {
                    // Lot 카드 바코드 프린트 데이터 생성
                    var labelData = LotCardBarcodePrint(_connection, txtScandata.Text);

                    // FAB LINE 검증 실패 시 처리
                    if (labelData == "FAB LINE NG")
                    {
                        txtMessage.Text = "FAB LINE NG"; // 오류 메시지 표시
                        txtMessage.BackColor = Color.Red; // 배경색을 빨간색으로 변경
                        return; // 처리 중단
                    }

                    try
                    {
                        // 바코드 프린터로 데이터 전송
                        if (!spBarcode.IsOpen)
                            spBarcode.Open(); // 포트가 열려있지 않으면 열기

                        spBarcode.Write(labelData); // 생성된 바코드 데이터 전송
                        spBarcode.Close(); // 전송 후 포트 닫기
                    }
                    catch (Exception ex)
                    {
                        // 프린트 오류 발생 시 메시지 표시
                        MessageBox.Show(ex.Message); // 예외 메시지
                        MessageBox.Show(labelData); // 생성된 라벨 데이터도 표시 (디버깅용)
                    }
                }

                // 스캔 데이터 입력 필드 초기화 (다음 입력을 위해)
                txtScandata.Text = string.Empty;
            }
        }

        /// <summary>
        /// Lot 카드 바코드 프린트 데이터를 생성합니다.
        /// 데이터베이스에서 Lot 정보를 조회하고 라벨 템플릿에 데이터를 적용합니다.
        /// </summary>
        /// <param name="_connection">MySQL 데이터베이스 연결 객체</param>
        /// <param name="lotid">Lot ID</param>
        /// <returns>생성된 라벨 데이터 문자열 (FAB LINE 검증 실패 시 "FAB LINE NG" 반환)</returns>
        private string LotCardBarcodePrint(MySqlConnection _connection, string lotid)
        {
            // 라벨 데이터에 사용될 변수들 초기화
            var prod_code = string.Empty;      // 제품 코드
            var sale_code = string.Empty;      // 판매 코드
            var lot_type = string.Empty;       // Lot 유형
            var reflow_type = string.Empty;    // 리플로우 타입
            var k9prod_code = string.Empty;    // K9 제품 코드
            var k9fab_line = string.Empty;     // K9 FAB 라인
            var k9option_code = string.Empty;  // K9 옵션 코드
            var comp_k9_opt = string.Empty;    // 구성품 K9 옵션
            var k4prod_code = string.Empty;    // K4 제품 코드
            var issueWeek = string.Empty;      // 발행 주차
            var k9CompLotid = string.Empty;    // K9 구성품 Lot ID
            var lot_qty = string.Empty;        // Lot 수량
            var date_format = string.Empty;    // 날짜 형식
            var label_pdf = string.Empty;      // 라벨 PDF 정보
            var series = string.Empty;         // 시리즈 정보
            var k9Week = string.Empty;         // K9 주차

            // 기본 Lot 정보 조회 SQL
            string sql =
                $@"SELECT PROD_ID, LOT_ID, LOT_TYPE, PROC_ID, CHIP_QTY, COMPLOT, TIER, OPTCODE, WEEKCODE, OPTION_CODE, FABSITE, OTHER, ASSYINTIME, 
                (SELECT SALESCODE FROM MODULE.MC_CONSM WHERE CONSM_ID = COMPLOT) AS COMP_SALESCODE, 
                (SELECT SUBSTR(SALESCODE, 20, 1) FROM MODULE.MC_CONSM WHERE CONSM_ID = COMPLOT) AS FABLINE,
                (SELECT DESIGN_SPEC_ID||'-'||REV_ID AS ID FROM LEGACY_MDM.BE_MES_MOD_SSD_PRODCT WHERE PROD_ID LIKE L.PROD_ID) AS LABEL_SPEC,
                (SELECT MODEL_NAME FROM SSD_ESPEC WHERE PROD_ID = SUBSTR((L.PROD_ID), 1, 18) )
                FROM MODULE.MC_LOT L
                WHERE LOT_ID = '{lotid}' ";
            
            // Oracle 데이터베이스에서 Lot 정보 조회
            var dr = OracleHelper.GetDataList(sql);
            while (dr.Read())
            {
                // 조회된 데이터를 변수에 할당
                prod_code = dr[0].ToString();                      // 제품 코드
                sale_code = dr[0].ToString().Substring(0, 18);     // 판매 코드 (첫 18자리)
                lotid = dr[1].ToString();                          // Lot ID
                lot_type = dr[2].ToString();                       // Lot 유형
                reflow_type = string.Empty;                        // 리플로우 타입 (초기화)
                k9prod_code = dr[13].ToString();                   // 구성품 판매 코드
                k9fab_line = dr[14].ToString();                    // FAB 라인 정보
                k9option_code = string.Empty;                      // K9 옵션 코드 (초기화)
                comp_k9_opt = dr[6].ToString() + dr[7].ToString(); // Tier + Option 코드 조합
                k4prod_code = string.Empty;                        // K4 제품 코드 (초기화)
                issueWeek = dr[8].ToString();                      // 발행 주차
                k9CompLotid = dr[5].ToString();                    // 구성품 Lot ID
                lot_qty = dr[4].ToString();                        // Lot 수량
                date_format = dr[12].ToString();                   // 날짜 형식
                label_pdf = dr[15].ToString();                     // 라벨 PDF 정보
                k9Week = string.Empty;                             // K9 주차 (초기화)
                series = dr[16].ToString();                        // 시리즈 정보
            }

            // 구성품(Consumable) 정보 조회 SQL
            sql =
                $@"SELECT C.CONSM_PROD_ID, C.CONSM_ID , C.FABLINE, C.OPTIONCODE, SUBSTR(C.OPTIONCODE, 1, 2), C.WEEKCODE, (SELECT DISTINCT OTHER FROM MODULE.MC_LOT_HIST WHERE EVENT_NAME = 'ConsumeMaterial' AND LOT_ID = H.LOT_ID AND OTHER  IS NOT NULL ) 
                FROM MODULE.MC_LOT_HIST H, MODULE.MC_CONSM C
                WHERE H.CONSMED_CONSM_ID = C.CONSM_ID 
                AND H.LOT_ID = '{lotid}'
                AND H.EVENT_NAME  = 'CompConsume' 
                ORDER BY C.CONSM_PROD_ID ";
            
            dr = OracleHelper.GetDataList(sql);
            var pcbavl = ""; // PCB 공급업체 정보
            while (dr.Read())
            {
                // K9/KL 제품 정보 처리
                if (dr[0].ToString().Substring(0, 2) == "K9" || dr[0].ToString().Substring(0, 2) == "KL")
                {
                    k9prod_code = dr[0].ToString();        // K9 제품 코드
                    k9CompLotid = dr[1].ToString();        // K9 구성품 Lot ID
                    k9fab_line = dr[2].ToString();         // K9 FAB 라인
                    k9option_code = dr[3].ToString();      // K9 옵션 코드
                    comp_k9_opt = dr[4].ToString();        // 구성품 K9 옵션
                    k9Week = dr[5].ToString();             // K9 주차
                    pcbavl = dr[6].ToString();             // PCB 공급업체 정보 설정
                }
                // K4 제품 정보 처리
                else if (dr[0].ToString().Substring(0, 2) == "K4")
                {
                    k4prod_code = dr[0].ToString();        // K4 제품 코드
                }
            }

            // FAB 라인 검증 로직
            // MZ7L3500HBLU-1BW00-MQ2 형식의 제품 코드 기준 검증
            // "* FAB Info.  M (M/L), S (P/B)"
            if (prod_code.Substring(19, 1) == "M")
            {
                // M 제품은 M 또는 L FAB 라인만 허용
                if (k9fab_line != "M" && k9fab_line != "L")
                {
                    return "FAB LINE NG"; // FAB 라인 불일치
                }
            }
            else if (prod_code.Substring(19, 1) == "S")
            {
                // S 제품은 P 또는 B FAB 라인만 허용
                if (k9fab_line != "P" && k9fab_line != "B")
                {
                    return "FAB LINE NG"; // FAB 라인 불일치
                }
            }
            else
            {
                // 그 외의 경우 FAB 라인 불일치
                return "FAB LINE NG";
            }

            // UI에 정보 표시
            txtProductCode.Text = prod_code;   // 제품 코드 표시
            txtLotid.Text = lotid;             // Lot ID 표시
            txtLotQty.Text = lot_qty;          // Lot 수량 표시
            txtOption.Text = comp_k9_opt;      // 옵션 정보 표시
            txtWeek.Text = issueWeek;          // 발행 주차 표시

            // 데이터베이스 규칙 확인 메시지 표시 (사용자에게 알림)
            MessageBox.Show("tb_mos_rules", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Stop);

            // MySQL에서 라벨 템플릿 데이터 조회
            var datname = "Lot_Card_R0"; // 라벨 템플릿 이름
            var labelString = MySqlHelper.ExecuteDataset(_connection, $"SELECT dat FROM tb_mes_std_dat WHERE dat_name = '{datname}' ").Tables[0].Rows[0][0].ToString();

            // 라벨 템플릿에 실제 데이터 치환
            var labelData = labelString.Replace("_PRODCODE", prod_code);           // 제품 코드
            labelData = labelData.Replace("_SALECODE", sale_code);                 // 판매 코드
            labelData = labelData.Replace("_LOTIDBARCODE", lotid);                 // Lot ID 바코드
            labelData = labelData.Replace("_LOTID", lotid);                        // Lot ID
            //labelData = labelData.Replace("_YYYY/MM/DD_HH:MM:SS", $@"{date_format.Substring(0, 4)}/{date_format.Substring(4, 2)}/{date_format.Substring(6, 2)} {date_format.Substring(8, 2)}:{date_format.Substring(10, 2)}:{date_format.Substring(12, 2)}"); // 날짜/시간
            labelData = labelData.Replace("_PCBAVL", pcbavl);                      // PCB 공급업체
            labelData = labelData.Replace("_LOTTYPE", lot_type);                   // Lot 유형
            labelData = labelData.Replace("_SERIES", series);                      // 시리즈 정보

            // 리플로우 타입 처리 (상단/하단 리플로우)
            if (reflow_type.Split(',').Length == 2)
            {
                labelData = labelData.Replace("_T/REFLOWTYPE", reflow_type.Split(',')[0]); // 상단 리플로우
                labelData = labelData.Replace("_B/REFLOWTYPE", reflow_type.Split(',')[1]); // 하단 리플로우
            }
            else
            {
                labelData = labelData.Replace("_T/REFLOWTYPE", reflow_type); // 단일 리플로우 타입
            }

            // 나머지 플레이스홀더 치환
            labelData = labelData.Replace("_LOTQTY", lot_qty);                    // Lot 수량
            labelData = labelData.Replace("_LOTNO", "");                          // Lot 번호 (빈 값)
            labelData = labelData.Replace("_K9WEEK", k9Week);                     // K9 주차
            labelData = labelData.Replace("_WEEK", issueWeek);                    // ISSUE WEEK

            labelData = labelData.Replace("_OPTIONCODE", comp_k9_opt);            // 옵션 코드

            labelData = labelData.Replace("_FAB", k9fab_line);                    // FAB 라인
            labelData = labelData.Replace("_K9OPTIONCODE", k9option_code);        // K9 옵션 코드
            labelData = labelData.Replace("_K4PRODCODE", k4prod_code);            // K4 제품 코드
            labelData = labelData.Replace("_K9PRODCODE", k9prod_code);            // K9 제품 코드
            labelData = labelData.Replace("_K9LOTID", k9CompLotid.Replace("QSI-SMT_", "")); // 구성품 Lot ID (접두어 제거)
            labelData = labelData.Replace("_LABELPDF", label_pdf);                // 라벨 PDF 정보

            return labelData; // 완성된 라벨 데이터 반환
        }
    }
}
