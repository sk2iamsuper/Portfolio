using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MES_WMS.UserCommon
{
    /// <summary>
    /// 공용 유틸리티 함수들을 제공하는 클래스
    /// </summary>
    public class Public_Function
    {
        #region 전역 변수

        // 사용자 정보 관련 변수들
        public static int PreviousMenuValue;              // 이전 메뉴 호출을 위한 변수
        
        public static string UserCompany = "";           // 사용자 사업장
        public static string UserFactory = "";           // 사용자 공장
        public static string UserDepartmentCode = "";    // 사용자 부서 코드
        public static string UserDepartmentName = "";    // 사용자 부서 이름
        public static string UserWorkplace = "";         // 사용자 작업장
        public static string UserMachine = "";           // 사용자 장비
        public static string UserEmployeeNumber = "";    // 사용자 사번
        public static string UserName = "";              // 사용자 이름
        public static string UserProcess = "";           // 사용자 공정
        public static string UserPort = "";              // 사용자 바코드 포트
        public static string UserPortUsage = "";         // 사용자 바코드 사용유무
        public static string UserGrade = "";             // 사용자 등급
        public static string UserStatus = "";            // 사용자 구분 
        public static string UserServer = "";            // 사용자 서버
        public static string UserDatabase = "";          // 사용자 데이터베이스 
        public static string UserDatabaseType = "";      // 사용자 데이터베이스 종류
        public static string UserGroup = "";             // 사용자 조(그룹)
        public static string UserStartDate = "";         // 계획 시작일자
        public static string UserEndDate = "";           // 계획 종료일자 
        public static string UserWarehouse = "";         // 창고
        public static string UserIP = "";                // 고유번호(IP 주소)

        private static DataTable _dataTable = new DataTable();  // 임시 데이터 테이블

        #endregion

        #region 버튼 상태 관리

        /// <summary>
        /// 버튼 배열의 상태를 조작 모드로 설정합니다.
        /// 조회/입력/수정/삭제는 비활성화, 저장/취소는 활성화합니다.
        /// </summary>
        /// <param name="buttonArray">상태를 변경할 버튼 배열</param>
        public static void SetButtonsToEditMode(Button[] buttonArray)
        {
            if (buttonArray == null || buttonArray.Length < 9)
            {
                throw new ArgumentException("버튼 배열은 최소 9개의 요소를 가져야 합니다.");
            }

            buttonArray[0].Enabled = false;   // 조회     
            buttonArray[1].Enabled = false;   // 입력
            buttonArray[2].Enabled = false;   // 수정
            buttonArray[3].Enabled = false;   // 삭제
            buttonArray[4].Enabled = true;    // 저장
            buttonArray[5].Enabled = true;    // 취소
            buttonArray[6].Enabled = false;   // 엑셀 내보내기
            buttonArray[7].Enabled = false;   // 출력
            buttonArray[8].Enabled = false;   // 종료
        }

        /// <summary>
        /// 버튼 배열의 상태를 보기 모드로 설정합니다.
        /// 조회/입력/종료는 활성화, 저장/취소는 비활성화합니다.
        /// </summary>
        /// <param name="buttonArray">상태를 변경할 버튼 배열</param>
        public static void SetButtonsToViewMode(Button[] buttonArray)
        {
            if (buttonArray == null || buttonArray.Length < 9)
            {
                throw new ArgumentException("버튼 배열은 최소 9개의 요소를 가져야 합니다.");
            }

            buttonArray[0].Enabled = true;    // 조회     
            buttonArray[1].Enabled = true;    // 입력
            buttonArray[2].Enabled = false;   // 수정
            buttonArray[3].Enabled = false;   // 삭제
            buttonArray[4].Enabled = false;   // 저장
            buttonArray[5].Enabled = false;   // 취소
            buttonArray[6].Enabled = true;    // 엑셀 내보내기
            buttonArray[7].Enabled = true;    // 출력
            buttonArray[8].Enabled = true;    // 종료
        }

        #endregion

        #region 폼 관리

        /// <summary>
        /// 폼의 중복 생성을 방지하고 이미 열려있는 폼이 있으면 포커스를 줍니다.
        /// </summary>
        /// <param name="formToCheck">확인할 폼 인스턴스</param>
        /// <returns>폼을 새로 생성해야 하면 true, 이미 존재하면 false</returns>
        public static bool PreventDuplicateFormCreation(Form formToCheck)
        {
            if (formToCheck == null)
            {
                throw new ArgumentNullException(nameof(formToCheck));
            }

            // 현재 열려있는 모든 폼을 확인
            foreach (Form openForm in Application.OpenForms)
            {
                // 동일한 타입의 폼이 이미 열려있는지 확인
                if (openForm.GetType() == formToCheck.GetType())
                {
                    // 숨겨진 폼이면 보이게 설정
                    if (!openForm.Visible)
                    {
                        openForm.Visible = true;
                    }

                    // 폼을 최상위로 가져오고 포커스 설정
                    openForm.BringToFront();
                    openForm.Focus();

                    return false; // 새로 생성할 필요 없음
                }
            }

            return true; // 새로 생성해야 함
        }

        /// <summary>
        /// 특정 타입의 폼이 이미 열려있는지 확인합니다.
        /// </summary>
        /// <typeparam name="T">확인할 폼 타입</typeparam>
        /// <returns>이미 열려있으면 해당 폼 인스턴스, 아니면 null</returns>
        public static T GetExistingForm<T>() where T : Form
        {
            foreach (Form openForm in Application.OpenForms)
            {
                if (openForm is T formOfType)
                {
                    return formOfType;
                }
            }

            return null;
        }

        #endregion

        #region 사용자 정보 관리

        /// <summary>
        /// 현재 사용자의 기본 정보를 초기화합니다.
        /// </summary>
        public static void InitializeUserInfo()
        {
            UserCompany = "";
            UserFactory = "";
            UserDepartmentCode = "";
            UserDepartmentName = "";
            UserWorkplace = "";
            UserMachine = "";
            UserEmployeeNumber = "";
            UserName = "";
            UserProcess = "";
            UserPort = "";
            UserPortUsage = "";
            UserGrade = "";
            UserStatus = "";
            UserServer = "";
            UserDatabase = "";
            UserDatabaseType = "";
            UserGroup = "";
            UserStartDate = "";
            UserEndDate = "";
            UserWarehouse = "";
            UserIP = "";
        }

        /// <summary>
        /// 사용자 정보를 업데이트합니다.
        /// </summary>
        /// <param name="propertyName">속성 이름</param>
        /// <param name="value">설정할 값</param>
        public static void UpdateUserInfo(string propertyName, string value)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("속성 이름은 필수입니다.", nameof(propertyName));
            }

            switch (propertyName.ToLower())
            {
                case "company":
                    UserCompany = value;
                    break;
                case "factory":
                    UserFactory = value;
                    break;
                case "departmentcode":
                    UserDepartmentCode = value;
                    break;
                case "departmentname":
                    UserDepartmentName = value;
                    break;
                case "workplace":
                    UserWorkplace = value;
                    break;
                case "machine":
                    UserMachine = value;
                    break;
                case "employeenumber":
                    UserEmployeeNumber = value;
                    break;
                case "name":
                    UserName = value;
                    break;
                case "process":
                    UserProcess = value;
                    break;
                case "port":
                    UserPort = value;
                    break;
                case "portusage":
                    UserPortUsage = value;
                    break;
                case "grade":
                    UserGrade = value;
                    break;
                case "status":
                    UserStatus = value;
                    break;
                case "server":
                    UserServer = value;
                    break;
                case "database":
                    UserDatabase = value;
                    break;
                case "databasetype":
                    UserDatabaseType = value;
                    break;
                case "group":
                    UserGroup = value;
                    break;
                case "startdate":
                    UserStartDate = value;
                    break;
                case "enddate":
                    UserEndDate = value;
                    break;
                case "warehouse":
                    UserWarehouse = value;
                    break;
                case "ip":
                    UserIP = value;
                    break;
                default:
                    Debug.WriteLine($"알 수 없는 속성 이름: {propertyName}");
                    break;
            }
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 안전하게 문자열을 정수로 변환합니다.
        /// </summary>
        /// <param name="value">변환할 문자열</param>
        /// <param name="defaultValue">변환 실패 시 기본값</param>
        /// <returns>변환된 정수값</returns>
        public static int SafeParseInt(string value, int defaultValue = 0)
        {
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return defaultValue;
        }

        /// <summary>
        /// 안전하게 문자열을 날짜로 변환합니다.
        /// </summary>
        /// <param name="value">변환할 문자열</param>
        /// <param name="defaultValue">변환 실패 시 기본값</param>
        /// <returns>변환된 날짜값</returns>
        public static DateTime SafeParseDate(string value, DateTime defaultValue)
        {
            if (DateTime.TryParse(value, out DateTime result))
            {
                return result;
            }
            return defaultValue;
        }

        #endregion
    }
}
