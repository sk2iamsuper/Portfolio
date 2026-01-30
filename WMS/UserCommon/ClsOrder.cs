using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;

namespace MES_WMS.UserCommon
{
    /// <summary>
    /// 주문 정보를 관리하고 주문-매출 변환을 처리하는 클래스
    /// </summary>
    public class OrderManager : IDisposable
    {
        #region 상수 정의

        private const string BusinessCode = "01";
        private const string MasterOrderTable = "tst01mrx";
        private const string DetailOrderTable = "tst01drx";
        private const string MasterSalesTable = "tst02mrx";
        private const string DetailSalesTable = "tst02drx";
        private const string CompletedStatus = "3";
        private const string CanceledStatus = "9";

        #endregion

        #region 필드 선언

        private readonly DatabaseManager _databaseManager;
        private bool _disposed = false;

        #endregion

        #region 속성

        /// <summary>
        /// 주문 날짜
        /// </summary>
        public string OrderDate { get; private set; }

        /// <summary>
        /// 주문 번호
        /// </summary>
        public string OrderNumber { get; private set; }

        /// <summary>
        /// 고객 코드
        /// </summary>
        public string CustomerCode { get; private set; }

        /// <summary>
        /// 배송지 코드
        /// </summary>
        public string DestinationCode { get; private set; }

        /// <summary>
        /// 우측(R) 아이템 그룹
        /// </summary>
        public string RightItemGroup { get; private set; }

        /// <summary>
        /// 좌측(L) 아이템 그룹
        /// </summary>
        public string LeftItemGroup { get; private set; }

        /// <summary>
        /// 우측(R) 조립 코드
        /// </summary>
        public string RightAssemblyCode { get; private set; }

        /// <summary>
        /// 좌측(L) 조립 코드
        /// </summary>
        public string LeftAssemblyCode { get; private set; }

        /// <summary>
        /// 우측(R) 주문 수량
        /// </summary>
        public int RightQuantity { get; private set; }

        /// <summary>
        /// 좌측(L) 주문 수량
        /// </summary>
        public int LeftQuantity { get; private set; }

        #endregion

        #region 생성자

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public OrderManager()
        {
            _databaseManager = new DatabaseManager();
            InitializeProperties();
        }

        /// <summary>
        /// 주문 날짜와 번호로 초기화하는 생성자
        /// </summary>
        /// <param name="orderDate">주문 날짜</param>
        /// <param name="orderNumber">주문 번호</param>
        public OrderManager(string orderDate, string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderDate))
                throw new ArgumentException("주문 날짜는 필수입니다.", nameof(orderDate));
                
            if (string.IsNullOrWhiteSpace(orderNumber))
                throw new ArgumentException("주문 번호는 필수입니다.", nameof(orderNumber));
            
            _databaseManager = new DatabaseManager();
            InitializeProperties();
            
            OrderDate = orderDate;
            OrderNumber = orderNumber;
            
            LoadOrderData(orderDate, orderNumber);
        }

        #endregion

        #region 초기화

        /// <summary>
        /// 속성들을 기본값으로 초기화합니다.
        /// </summary>
        private void InitializeProperties()
        {
            OrderDate = string.Empty;
            OrderNumber = string.Empty;
            CustomerCode = string.Empty;
            DestinationCode = string.Empty;
            RightItemGroup = string.Empty;
            LeftItemGroup = string.Empty;
            RightAssemblyCode = string.Empty;
            LeftAssemblyCode = string.Empty;
            RightQuantity = 0;
            LeftQuantity = 0;
        }

        #endregion

        #region 주문 정보 조회

        /// <summary>
        /// 주문 번호의 존재 여부를 확인합니다.
        /// </summary>
        /// <returns>주문 번호가 존재하면 true, 아니면 false</returns>
        public bool OrderExists()
        {
            try
            {
                string query = BuildOrderExistenceQuery();
                int count = _databaseManager.ExecuteScalarInt(query);
                return count > 0;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"주문 확인 중 오류 발생: {ex.Message}", "조회 오류");
                return false;
            }
        }

        /// <summary>
        /// 주문 존재 여부 확인 쿼리를 생성합니다.
        /// </summary>
        private string BuildOrderExistenceQuery()
        {
            return $@"
                SELECT COUNT(*) 
                FROM {MasterOrderTable} 
                WHERE SAUP_GUBN = '{BusinessCode}' 
                  AND SUJU_DATE = '{OrderDate}' 
                  AND SUJU_NO = '{OrderNumber}'";
        }

        /// <summary>
        /// 주문 데이터를 로드합니다.
        /// </summary>
        /// <param name="orderDate">주문 날짜</param>
        /// <param name="orderNumber">주문 번호</param>
        /// <returns>로드 성공 여부</returns>
        public bool LoadOrderData(string orderDate, string orderNumber)
        {
            try
            {
                OrderDate = orderDate;
                OrderNumber = orderNumber;

                string query = BuildOrderDataQuery();
                DataSet dataSet = _databaseManager.ExecuteDataSet(query);

                if (dataSet != null && dataSet.Tables.Count > 0 && dataSet.Tables[0].Rows.Count > 0)
                {
                    ParseOrderData(dataSet.Tables[0].Rows[0]);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"주문 데이터 로드 실패: {ex.Message}", "데이터 오류");
                return false;
            }
        }

        /// <summary>
        /// 주문 데이터 조회 쿼리를 생성합니다.
        /// </summary>
        private string BuildOrderDataQuery()
        {
            return $@"
                SELECT 
                    suju_date, 
                    suju_no, 
                    cust_code, 
                    dest_cust,
                    MAX(CASE lr_gubn WHEN 'R' THEN item_group END) as item_group_r,
                    MAX(CASE lr_gubn WHEN 'L' THEN item_group END) as item_group_l,
                    MAX(CASE lr_gubn WHEN 'R' THEN assy_code END) as assy_code_r,
                    MAX(CASE lr_gubn WHEN 'L' THEN assy_code END) as assy_code_l,
                    MAX(CASE lr_gubn WHEN 'R' THEN suju_qty END) as suju_qty_r,
                    MAX(CASE lr_gubn WHEN 'L' THEN suju_qty END) as suju_qty_l
                FROM {MasterOrderTable} 
                WHERE saup_gubn = '{BusinessCode}' 
                  AND suju_date = '{OrderDate}' 
                  AND suju_no = '{OrderNumber}' 
                GROUP BY suju_date, suju_no, cust_code, dest_cust";
        }

        /// <summary>
        /// DataRow에서 주문 데이터를 파싱합니다.
        /// </summary>
        /// <param name="row">데이터 행</param>
        private void ParseOrderData(DataRow row)
        {
            CustomerCode = GetSafeString(row, 2);
            DestinationCode = GetSafeString(row, 3);
            RightItemGroup = GetSafeString(row, 4);
            LeftItemGroup = GetSafeString(row, 5);
            RightAssemblyCode = GetSafeString(row, 6);
            LeftAssemblyCode = GetSafeString(row, 7);

            RightQuantity = GetSafeInt(row, 8);
            LeftQuantity = GetSafeInt(row, 9);

            // 아이템 그룹이 존재하면 수량을 1로 설정
            if (!string.IsNullOrWhiteSpace(RightItemGroup))
                RightQuantity = 1;

            if (!string.IsNullOrWhiteSpace(LeftItemGroup))
                LeftQuantity = 1;
        }

        #endregion

        #region 주문-매출 변환

        /// <summary>
        /// 주문을 매출로 변환 저장합니다.
        /// </summary>
        /// <returns>저장 성공 여부</returns>
        public bool ConvertOrderToSales()
        {
            try
            {
                // 주문 존재 확인
                if (!OrderExists())
                {
                    ShowErrorMessage("주문이 존재하지 않습니다.", "변환 오류");
                    return false;
                }

                // 출고 번호 생성
                string shippingDate = GetCurrentDate();
                string shippingNumber = GenerateShippingNumber(shippingDate);

                // 매출 상세 데이터 저장
                if (!SaveSalesDetail(shippingDate, shippingNumber))
                {
                    ShowErrorMessage("매출 상세 데이터 저장 실패", "저장 오류");
                    return false;
                }

                // 매출 마스터 데이터 저장
                if (!SaveSalesMaster(shippingDate, shippingNumber))
                {
                    // 마스터 저장 실패 시 상세 데이터 삭제
                    DeleteSales(shippingDate, shippingNumber);
                    ShowErrorMessage("매출 마스터 데이터 저장 실패", "저장 오류");
                    return false;
                }

                // 주문 완료 상태로 업데이트
                if (!UpdateOrderStatus(CompletedStatus))
                {
                    ShowErrorMessage("주문 상태 업데이트 실패", "상태 오류");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"주문-매출 변환 중 오류 발생: {ex.Message}", "시스템 오류");
                return false;
            }
        }

        /// <summary>
        /// 매출 상세 데이터를 저장합니다.
        /// </summary>
        private bool SaveSalesDetail(string shippingDate, string shippingNumber)
        {
            string query = $@"
                INSERT INTO {MasterSalesTable} 
                    (saup_gubn, chul_date, chul_no, lr_gubn, service_code, chul_amt, order_no, end_gubn) 
                SELECT 
                    saup_gubn, '{shippingDate}', '{shippingNumber}', lr_gubn, service_code, suju_amt, order_no, '1'
                FROM {MasterOrderTable} 
                WHERE saup_gubn = '{BusinessCode}' 
                  AND suju_date = '{OrderDate}' 
                  AND suju_no = '{OrderNumber}'";

            ExecuteQueryAndValidate(query, "매출 상세 데이터 저장");
            return true;
        }

        /// <summary>
        /// 매출 마스터 데이터를 저장합니다.
        /// </summary>
        private bool SaveSalesMaster(string shippingDate, string shippingNumber)
        {
            string query = $@"
                INSERT INTO {DetailSalesTable} 
                    (saup_gubn, chul_date, chul_no, chul_serl, lr_gubn, cust_code, item_group, item_code, 
                     dept_code, empno, chul_gubn, suju_kind, plan_no, order_no, curr_code, curr_rate, 
                     chul_chasu, sph, cyl, axis, adds, dia, io_base, io_prism, ud_base, ud_prism, curv, 
                     service_code, color, chul_qty, cost_k, amt_k, cost_f, amt_f, rmk, cust_rmk, suju_date, 
                     suju_no, suju_serl, io_gbn_code, io_gbn, lens_long, lens_short, lens_diag, lens_height, 
                     lens_size, lens_bridge, lens_axis, lens_centthick, lens_adgethick, lr_kind, assy_code, 
                     dest_cust, service_rmk, p_mm, vdia, davich_chk, davich_no, match_gbn, color_group, 
                     color_no, color_gubn, color_depth, color_rmk, color_depth2, accept_chasu, wh_code)
                SELECT 
                    saup_gubn, '{shippingDate}', '{shippingNumber}', suju_serl, lr_gubn, cust_code, 
                    item_group, item_code, dept_code, empno, suju_gubn, suju_kind, plan_no, order_no, 
                    curr_code, curr_rate, '1', sph, cyl, axis, adds, dia, io_base, io_prism, ud_base, 
                    ud_prism, curv, service_code, color, suju_qty, cost_k, amt_k, cost_f, amt_f, rmk, 
                    cust_rmk, suju_date, suju_no, suju_serl, io_gbn_code, io_gbn, lens_long, lens_short, 
                    lens_diag, lens_height, lens_size, lens_bridge, lens_axis, lens_centthick, 
                    lens_adgethick, lr_kind, assy_code, dest_cust, service_rmk, p_mm, vdia, davich_chk, 
                    davich_no, match_gbn, color_group, color_no, color_gubn, color_depth, color_rmk, 
                    color_depth2, accept_chasu, 'R0'
                FROM {DetailOrderTable} 
                WHERE saup_gubn = '{BusinessCode}' 
                  AND suju_date = '{OrderDate}' 
                  AND suju_no = '{OrderNumber}'";

            ExecuteQueryAndValidate(query, "매출 마스터 데이터 저장");
            return true;
        }

        #endregion

        #region 출고 번호 생성

        /// <summary>
        /// 출고 번호를 생성합니다.
        /// </summary>
        /// <param name="shippingDate">출고 날짜</param>
        /// <returns>생성된 출고 번호</returns>
        private string GenerateShippingNumber(string shippingDate)
        {
            try
            {
                string query = $@"
                    SELECT '295' + RIGHT('0000' + CAST(ISNULL(MAX(CAST(RIGHT(chul_no, 4) AS INT)), 0) + 1 AS VARCHAR), 4)
                    FROM {MasterSalesTable} 
                    WHERE saup_gubn = '{BusinessCode}' 
                      AND chul_date = '{shippingDate}'";

                string result = _databaseManager.ExecuteScalarString(query);
                return result ?? "2950001";
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"출고 번호 생성 실패: {ex.Message}", "번호 생성 오류");
                return "2950001";
            }
        }

        #endregion

        #region 주문 상태 관리

        /// <summary>
        /// 주문을 완료 상태로 업데이트합니다.
        /// </summary>
        /// <returns>업데이트 성공 여부</returns>
        public bool CompleteOrder()
        {
            return UpdateOrderStatus(CompletedStatus);
        }

        /// <summary>
        /// 주문을 취소 상태로 업데이트합니다.
        /// </summary>
        /// <returns>업데이트 성공 여부</returns>
        public bool CancelOrder()
        {
            try
            {
                // 주문 상태 취소로 업데이트
                if (!UpdateOrderStatus(CanceledStatus))
                    return false;

                // 관련 매출 데이터 삭제
                DeleteRelatedSalesData();
                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"주문 취소 중 오류 발생: {ex.Message}", "취소 오류");
                return false;
            }
        }

        /// <summary>
        /// 주문 상태를 업데이트합니다.
        /// </summary>
        /// <param name="status">업데이트할 상태 코드</param>
        /// <returns>업데이트 성공 여부</returns>
        private bool UpdateOrderStatus(string status)
        {
            string query = $@"
                UPDATE {MasterOrderTable} 
                SET end_gubn = '{status}' 
                WHERE saup_gubn = '{BusinessCode}' 
                  AND suju_date = '{OrderDate}' 
                  AND suju_no = '{OrderNumber}'";

            return ExecuteQueryAndValidate(query, $"주문 상태 업데이트 ({status})");
        }

        #endregion

        #region 매출 데이터 관리

        /// <summary>
        /// 주문과 관련된 매출 데이터를 삭제합니다.
        /// </summary>
        private void DeleteRelatedSalesData()
        {
            try
            {
                string query = $@"
                    SELECT chul_date, chul_no 
                    FROM {DetailSalesTable} 
                    WHERE saup_gubn = '{BusinessCode}' 
                      AND suju_date = '{OrderDate}' 
                      AND suju_no = '{OrderNumber}'";

                DataSet dataSet = _databaseManager.ExecuteDataSet(query);

                if (dataSet != null && dataSet.Tables.Count > 0)
                {
                    foreach (DataRow row in dataSet.Tables[0].Rows)
                    {
                        string shippingDate = GetSafeString(row, 0);
                        string shippingNumber = GetSafeString(row, 1);
                        
                        if (!string.IsNullOrWhiteSpace(shippingDate) && !string.IsNullOrWhiteSpace(shippingNumber))
                        {
                            DeleteSales(shippingDate, shippingNumber);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"관련 매출 데이터 삭제 실패: {ex.Message}", "삭제 오류");
            }
        }

        /// <summary>
        /// 매출 데이터를 삭제합니다.
        /// </summary>
        /// <param name="shippingDate">출고 날짜</param>
        /// <param name="shippingNumber">출고 번호</param>
        private void DeleteSales(string shippingDate, string shippingNumber)
        {
            try
            {
                // 상세 데이터 삭제
                string detailQuery = $@"
                    DELETE FROM {DetailSalesTable} 
                    WHERE saup_gubn = '{BusinessCode}' 
                      AND chul_date = '{shippingDate}' 
                      AND chul_no = '{shippingNumber}'";

                ExecuteQueryAndValidate(detailQuery, "매출 상세 데이터 삭제");

                // 마스터 데이터 삭제
                string masterQuery = $@"
                    DELETE FROM {MasterSalesTable} 
                    WHERE saup_gubn = '{BusinessCode}' 
                      AND chul_date = '{shippingDate}' 
                      AND chul_no = '{shippingNumber}'";

                ExecuteQueryAndValidate(masterQuery, "매출 마스터 데이터 삭제");
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"매출 데이터 삭제 실패: {ex.Message}", "삭제 오류");
            }
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 현재 날짜를 문자열 형식으로 가져옵니다.
        /// </summary>
        /// <returns>현재 날짜 문자열</returns>
        private string GetCurrentDate()
        {
            try
            {
                string query = "SELECT CONVERT(VARCHAR(10), GETDATE(), 102)";
                return _databaseManager.ExecuteScalarString(query) ?? DateTime.Now.ToString("yyyy.MM.dd");
            }
            catch
            {
                return DateTime.Now.ToString("yyyy.MM.dd");
            }
        }

        /// <summary>
        /// 안전하게 문자열 값을 가져옵니다.
        /// </summary>
        private string GetSafeString(DataRow row, int index)
        {
            return row != null && index < row.ItemArray.Length && row[index] != DBNull.Value
                ? row[index].ToString()
                : string.Empty;
        }

        /// <summary>
        /// 안전하게 정수 값을 가져옵니다.
        /// </summary>
        private int GetSafeInt(DataRow row, int index)
        {
            if (row == null || index >= row.ItemArray.Length || row[index] == DBNull.Value)
                return 0;

            if (int.TryParse(row[index].ToString(), out int result))
                return result;

            return 0;
        }

        /// <summary>
        /// 쿼리를 실행하고 결과를 검증합니다.
        /// </summary>
        private bool ExecuteQueryAndValidate(string query, string operationName)
        {
            try
            {
                _databaseManager.ExecuteNonQuery(query);
                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"{operationName} 실패: {ex.Message}", "실행 오류");
                return false;
            }
        }

        /// <summary>
        /// 에러 메시지를 표시합니다.
        /// </summary>
        private void ShowErrorMessage(string message, string title)
        {
            var messageForm = new ChildFrm.MsgFrm(message, 20, Color.Red, 200, 100);
            messageForm.ShowDialog();
        }

        #endregion

        #region IDisposable 구현

        /// <summary>
        /// 리소스를 정리합니다.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리 리소스 정리 여부</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _databaseManager?.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// 소멸자
        /// </summary>
        ~OrderManager()
        {
            Dispose(false);
        }

        #endregion
    }
}
