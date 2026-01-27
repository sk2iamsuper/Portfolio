using System;
using System.Data;
using System.Windows.Forms;
using UserCommon;

namespace MES_WMS
{
    public partial class PRF10 : Form
    {
        // 사용자 공통 정보
        private readonly string _userFactory = UserCommon.Public_Function.user_Factory;
        private readonly CmCn _cnj;  // 데이터베이스 연결 객체 (읽기 전용)
        private readonly CmCn _cn;   // 데이터베이스 연결 객체 (기본)

        // 상수 정의 - 시스템 코드값
        private const string OPTION_TYPE = "03";     // 옵션 타입: 직원-창고 매핑 관리
        private const string FACTORY_CODE = "F21";   // 공장 코드
        private const string WAREHOUSE_F2101 = "F2101";  // 1.60 창고 코드
        private const string WAREHOUSE_F2102 = "F2102";  // 1.56 창고 코드

        public PRF10()
        {
            InitializeComponent();
            
            // 서버 연결 정보 설정
            string serverName = UserCommon.Public_Function.user_Server;
            _cnj = new CmCn(serverName, "cmv");  // 특정 데이터베이스 연결
            _cn = new CmCn();                     // 기본 데이터베이스 연결
            
            // 화면 초기화
            InitForm();
        }

        private void InitForm()
        {
            // 라벨 초기화
            lblStkEmp1.Text = string.Empty;
            
            // 콤보박스 데이터 로드
            LoadStkEmployees();   // 담당자 목록 로드
            LoadWarehouses();     // 창고 목록 로드
        }

        /// <summary>
        /// 담당자 목록을 콤보박스에 로드 (비즈니스 로직 1)
        /// </summary>
        private void LoadStkEmployees()
        {
            Cmb_StkEmp1.Items.Clear();  // 기존 항목 제거
            
            // 데이터베이스에서 담당자 정보 조회
            DataSet ds = GetStkEmployees(_userFactory);

            if (ds.Tables[0].Rows.Count > 0)
            {
                // 각 담당자 정보를 콤보박스에 추가
                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    // 코드와 이름을 조합하여 표시 형식 생성
                    string itemText = FormatDisplayText(row[0].ToString(), row[1].ToString());
                    Cmb_StkEmp1.Items.Add(itemText);
                }
                // 첫 번째 항목 선택
                Cmb_StkEmp1.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 창고 목록을 콤보박스에 로드 (비즈니스 로직 2)
        /// </summary>
        private void LoadWarehouses()
        {
            Cmb_SetStk.Items.Clear();  // 기존 항목 제거
            
            // 데이터베이스에서 창고 정보 조회
            DataSet ds = GetWarehouses();

            if (ds.Tables[0].Rows.Count > 0)
            {
                // 각 창고 정보를 콤보박스에 추가
                foreach (DataRow row in ds.Tables[0].Rows)
                {
                    // 코드와 이름을 조합하여 표시 형식 생성
                    string itemText = FormatDisplayText(row[0].ToString(), row[1].ToString());
                    Cmb_SetStk.Items.Add(itemText);
                }
                // 첫 번째 항목(빈 항목) 선택
                Cmb_SetStk.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 담당자 정보 조회 (비즈니스 로직 3)
        /// </summary>
        /// <param name="factory">공장 코드</param>
        /// <returns>담당자 데이터셋</returns>
        private DataSet GetStkEmployees(string factory)
        {
            // SQL 쿼리: 특정 공장의 사용 가능한 담당자 조회
            string query = @"
                SELECT remark, opt_name 
                FROM tst16c 
                WHERE opt_type = @OptType 
                    AND use_yn = 'Y'           -- 사용 여부
                    AND factory = @Factory     -- 공장 조건
                    AND ISNULL(sub_code, '') <> ''";  -- 서브코드(창고코드)가 있는 경우만

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@OptType", OPTION_TYPE),
                new SqlParameter("@Factory", factory)
            };

            // 데이터베이스 조회 실행
            return _cnj.ResultReturnDataSet(query, parameters);
        }

        /// <summary>
        /// 창고 정보 조회 (비즈니스 로직 4)
        /// </summary>
        /// <returns>창고 데이터셋</returns>
        private DataSet GetWarehouses()
        {
            // SQL 쿼리: 시스템에 정의된 창고 목록 조회
            // 빈 항목 + F2101(1.60창고) + F2102(1.56창고)
            string query = @"
                SELECT '', ''                      -- 빈 항목
                UNION 
                SELECT 'F2101', '1.60仓库'         -- 1.60 창고
                UNION 
                SELECT 'F2102', '1.56仓库'";       -- 1.56 창고

            return _cnj.ResultReturnDataSet(query);
        }

        /// <summary>
        /// 표시 텍스트 포맷팅 (유틸리티)
        /// </summary>
        private string FormatDisplayText(string code, string name)
        {
            return $"{code}  {name}";
        }

        /// <summary>
        /// 콤보박스 항목에서 코드 추출 (유틸리티)
        /// </summary>
        private string ExtractCodeFromComboBoxItem(object item)
        {
            if (item == null) return string.Empty;
            string itemText = item.ToString();
            // 첫 5자리가 코드 (예: "F2101")
            return itemText.Length >= 5 ? itemText.Substring(0, 5) : itemText;
        }

        /// <summary>
        /// 콤보박스 항목에서 remark 추출 (비즈니스 로직 5)
        /// </summary>
        private string ExtractRemarkFromComboBoxItem(object item)
        {
            if (item == null) return string.Empty;
            string itemText = item.ToString();
            // 첫 6자리가 remark (사원코드 등의 식별자)
            return itemText.Length >= 6 ? itemText.Substring(0, 6) : itemText;
        }

        /// <summary>
        /// 담당자 선택 변경 이벤트 핸들러 (비즈니스 로직 6)
        /// </summary>
        private void Cmb_StkEmp1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Cmb_StkEmp1.SelectedItem == null) return;

            // 선택된 담당자의 remark(식별자) 추출
            string remark = ExtractRemarkFromComboBoxItem(Cmb_StkEmp1.SelectedItem);
            
            // 해당 담당자의 현재 할당된 창고 정보 조회
            string warehouseInfo = GetEmployeeWarehouseInfo(remark);

            if (!string.IsNullOrEmpty(warehouseInfo))
            {
                // 라벨에 창고 정보 표시
                lblStkEmp1.Text = warehouseInfo;
            }
            else
            {
                // 창고가 할당되지 않은 경우 경고
                MessageBox.Show("未设置仓库别，禁止变更", "Notice", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 담당자의 창고 정보 조회 (비즈니스 로직 7)
        /// </summary>
        /// <param name="remark">담당자 식별자</param>
        /// <returns>창고 정보 문자열</returns>
        private string GetEmployeeWarehouseInfo(string remark)
        {
            // SQL 쿼리: 특정 담당자의 현재 할당된 창고 정보 조회
            string query = @"
                SELECT 
                    sub_code + '-' +     -- 창고코드 + 설명
                    CASE sub_code 
                        WHEN 'F2102' THEN '1.56仓库' 
                        WHEN 'F2101' THEN '1.60仓库' 
                    END as warehouse_info
                FROM tst16c 
                WHERE opt_type = @OptType 
                    AND factory = @Factory 
                    AND remark = @Remark 
                    AND ISNULL(sub_code, '') <> ''";  // 창고가 할당된 경우만

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@OptType", OPTION_TYPE),
                new SqlParameter("@Factory", FACTORY_CODE),
                new SqlParameter("@Remark", remark)
            };

            DataSet ds = _cn.ResultReturnDataSet(query, parameters);
            
            if (ds.Tables[0].Rows.Count > 0)
            {
                // 창고 정보 반환 (예: "F2101-1.60仓库")
                return ds.Tables[0].Rows[0]["warehouse_info"].ToString();
            }

            return string.Empty;  // 할당된 창고 없음
        }

        /// <summary>
        /// 변경할 창고 선택 변경 이벤트 핸들러 (비즈니스 로직 8)
        /// </summary>
        private void Cmb_SetStk_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 빈 항목 선택 시 무시
            if (Cmb_SetStk.SelectedItem == null || 
                string.IsNullOrWhiteSpace(Cmb_SetStk.SelectedItem.ToString()))
            {
                return;
            }

            // 선택된 창고 코드 추출
            string selectedWarehouseCode = ExtractCodeFromComboBoxItem(Cmb_SetStk.SelectedItem);
            // 현재 선택된 담당자 remark 추출
            string employeeRemark = ExtractRemarkFromComboBoxItem(Cmb_StkEmp1.SelectedItem);

            // 이미 해당 창고가 할당되어 있는지 확인
            if (IsWarehouseAlreadyAssigned(employeeRemark, selectedWarehouseCode))
            {
                MessageBox.Show("仓库别相同，禁止变更", "Notice", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                // 선택을 빈 항목으로 리셋
                Cmb_SetStk.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 창고 중복 할당 여부 확인 (비즈니스 로직 9)
        /// </summary>
        private bool IsWarehouseAlreadyAssigned(string remark, string warehouseCode)
        {
            // SQL 쿼리: 해당 담당자에게 이미 해당 창고가 할당되었는지 확인
            string query = @"
                SELECT COUNT(*) as count
                FROM tst16c 
                WHERE opt_type = @OptType 
                    AND factory = @Factory 
                    AND remark = @Remark 
                    AND ISNULL(sub_code, '') = @WarehouseCode";

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@OptType", OPTION_TYPE),
                new SqlParameter("@Factory", FACTORY_CODE),
                new SqlParameter("@Remark", remark),
                new SqlParameter("@WarehouseCode", warehouseCode)
            };

            DataSet ds = _cn.ResultReturnDataSet(query, parameters);
            int count = Convert.ToInt32(ds.Tables[0].Rows[0]["count"]);
            
            // count > 0 이면 이미 할당된 상태
            return count > 0;
        }

        /// <summary>
        /// 설정 버튼 클릭 이벤트 핸들러 (비즈니스 로직 10 - 메인)
        /// </summary>
        private void btnSet_Click(object sender, EventArgs e)
        {
            // 입력값 유효성 검사
            if (!ValidateInput()) return;

            // 선택된 값 추출
            string employeeRemark = ExtractRemarkFromComboBoxItem(Cmb_StkEmp1.SelectedItem);
            string newWarehouseCode = ExtractCodeFromComboBoxItem(Cmb_SetStk.SelectedItem);
            
            // 현재 창고와 변경할 창고가 같은지 확인
            if (IsCurrentWarehouseSameAsSelected(newWarehouseCode))
            {
                MessageBox.Show("仓库相同，禁止变更", "Notice", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Cmb_SetStk.SelectedIndex = 0;
                return;
            }

            // 담당자의 창고 정보 업데이트
            UpdateEmployeeWarehouse(employeeRemark, newWarehouseCode);
        }

        /// <summary>
        /// 입력값 유효성 검사 (비즈니스 로직 11)
        /// </summary>
        private bool ValidateInput()
        {
            // 필수 항목 선택 여부 확인
            if (Cmb_StkEmp1.SelectedItem == null || 
                Cmb_SetStk.SelectedItem == null ||
                string.IsNullOrWhiteSpace(Cmb_SetStk.SelectedItem.ToString()))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 현재 창고와 선택된 창고 동일 여부 확인 (비즈니스 로직 12)
        /// </summary>
        private bool IsCurrentWarehouseSameAsSelected(string newWarehouseCode)
        {
            // 현재 라벨에 표시된 창고 코드 추출
            string currentWarehouseCode = lblStkEmp1.Text.Length >= 5 ? 
                lblStkEmp1.Text.Substring(0, 5) : lblStkEmp1.Text;
            
            // 현재 창고와 새 창고 비교
            return currentWarehouseCode == newWarehouseCode;
        }

        /// <summary>
        /// 담당자 창고 정보 업데이트 (비즈니스 로직 13 - 핵심)
        /// </summary>
        private void UpdateEmployeeWarehouse(string remark, string warehouseCode)
        {
            // SQL 쿼리: 담당자의 sub_code(창고코드) 업데이트
            string query = @"
                UPDATE tst16c 
                SET sub_code = @WarehouseCode
                WHERE opt_type = @OptType 
                    AND factory = @Factory 
                    AND remark = @Remark 
                    AND ISNULL(sub_code, '') <> ''";  // 기존에 창고가 할당된 경우만

            var parameters = new SqlParameter[]
            {
                new SqlParameter("@WarehouseCode", warehouseCode),
                new SqlParameter("@OptType", OPTION_TYPE),
                new SqlParameter("@Factory", FACTORY_CODE),
                new SqlParameter("@Remark", remark)
            };

            try
            {
                // 데이터베이스 업데이트 실행
                _cn.Execute(query, parameters);
                
                // 화면 초기화로 변경사항 반영
                InitForm();
                
                // 성공 메시지 표시
                MessageBox.Show("变更成功!!", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                // 업데이트 실패 시 에러 메시지
                MessageBox.Show($"更新失败: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
