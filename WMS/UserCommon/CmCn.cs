using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows.Forms;

namespace MES_WMS.UserCommon
{
    /// <summary>
    /// 데이터베이스 연결 및 쿼리 실행을 관리하는 클래스
    /// </summary>
    public class DatabaseManager
    {
        #region 필드 선언

        private readonly string _serverName;
        private readonly string _databaseName;
        private SqlConnection _connection;
        private string _connectionState;
        private string _queryExecutionState;
        
        // 상수 정의
        private const string DefaultUserId = "erp";
        private const string DefaultPassword = "121535";
        private const int DefaultCommandTimeout = 100000;
        private const string ConnectionOpenState = "OPEN";
        private const string QuerySuccessState = "S";
        private const string QueryFailureState = "F";

        #endregion

        #region 생성자

        /// <summary>
        /// 기본 생성자 - 설정 파일에서 서버 및 데이터베이스 정보를 읽어옵니다.
        /// </summary>
        public DatabaseManager()
        {
            string filePath = GetConfigurationFilePath();
            ClsinitUtil configReader = new ClsinitUtil(filePath);
            
            _serverName = "cmvn"; // configReader.GetIniValue("SERVER", "SERVER_NAME");
            _databaseName = "cmv"; // configReader.GetIniValue("SERVER", "DATABASE");
            
            InitializeConnection();
        }

        /// <summary>
        /// 사용자 지정 서버 및 데이터베이스로 초기화하는 생성자
        /// </summary>
        /// <param name="serverName">서버 이름</param>
        /// <param name="databaseName">데이터베이스 이름</param>
        public DatabaseManager(string serverName, string databaseName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("서버 이름은 필수입니다.", nameof(serverName));
                
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("데이터베이스 이름은 필수입니다.", nameof(databaseName));
            
            _serverName = serverName;
            _databaseName = databaseName;
            
            InitializeConnection();
        }

        #endregion

        #region 속성

        /// <summary>
        /// 현재 연결 상태를 가져옵니다.
        /// </summary>
        public string ConnectionState => _connectionState;

        /// <summary>
        /// 마지막 쿼리 실행 상태를 가져옵니다.
        /// </summary>
        public string QueryExecutionState => _queryExecutionState;

        #endregion

        #region 연결 관리

        /// <summary>
        /// 설정 파일 경로를 반환합니다.
        /// </summary>
        /// <returns>설정 파일 전체 경로</returns>
        private string GetConfigurationFilePath()
        {
            string programFilesPath = Environment.Is64BitOperatingSystem
                ? @"C:\Program Files (x86)\Chemi_MES"
                : @"C:\Program Files\Chemi_MES";
            
            string fileName = @"\CMES\config_mes.ini";
            return programFilesPath + fileName;
        }

        /// <summary>
        /// 데이터베이스 연결을 초기화합니다.
        /// </summary>
        private void InitializeConnection()
        {
            string connectionString = BuildConnectionString();
            
            try
            {
                _connection = new SqlConnection(connectionString);
                _connection.Open();
                _connectionState = ConnectionOpenState;
            }
            catch (Exception ex)
            {
                _connectionState = "CLOSED";
                ShowErrorMessage($"데이터베이스 연결 실패: {ex.Message}", "연결 오류");
                throw new ApplicationException("데이터베이스 연결에 실패했습니다.", ex);
            }
        }

        /// <summary>
        /// 연결 문자열을 생성합니다.
        /// </summary>
        /// <returns>완성된 연결 문자열</returns>
        private string BuildConnectionString()
        {
            return $"server={_serverName}; Initial Catalog={_databaseName}; " +
                   $"User ID={DefaultUserId}; Password={DefaultPassword}";
        }

        /// <summary>
        /// 연결을 안전하게 종료합니다.
        /// </summary>
        private void SafeCloseConnection()
        {
            try
            {
                if (_connection != null && _connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
            }
            catch (Exception ex)
            {
                DebugLog($"연결 종료 중 오류 발생: {ex.Message}");
            }
            finally
            {
                _connection = null;
                _connectionState = "CLOSED";
            }
        }

        #endregion

        #region 쿼리 실행 메서드

        /// <summary>
        /// SELECT 쿼리를 실행하고 SqlDataReader를 반환합니다.
        /// </summary>
        /// <param name="query">실행할 SELECT 쿼리</param>
        /// <returns>SqlDataReader 객체</returns>
        public SqlDataReader ExecuteQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("쿼리는 필수입니다.", nameof(query));
            
            ValidateConnection();
            
            try
            {
                using (SqlConnection tempConnection = CreateNewConnection())
                {
                    using (SqlCommand command = new SqlCommand(query, tempConnection))
                    {
                        command.CommandTimeout = DefaultCommandTimeout;
                        return command.ExecuteReader(CommandBehavior.CloseConnection);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"쿼리 실행 실패: {ex.Message}\n쿼리: {query}", "쿼리 실행 오류");
                throw;
            }
        }

        /// <summary>
        /// INSERT, UPDATE, DELETE 등 데이터 조작 쿼리를 실행합니다.
        /// </summary>
        /// <param name="query">실행할 쿼리</param>
        public void ExecuteNonQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("쿼리는 필수입니다.", nameof(query));
            
            ValidateConnection();
            
            using (SqlCommand command = new SqlCommand(query, _connection))
            {
                try
                {
                    command.ExecuteNonQuery();
                    _queryExecutionState = QuerySuccessState;
                }
                catch (Exception ex)
                {
                    _queryExecutionState = QueryFailureState;
                    ShowErrorMessage($"쿼리 실행 실패: {ex.Message}\n쿼리: {query}", "실행 오류");
                    throw;
                }
                finally
                {
                    SafeCloseConnection();
                }
            }
        }

        /// <summary>
        /// 단일 값을 반환하는 스칼라 쿼리를 실행합니다.
        /// </summary>
        /// <param name="query">실행할 쿼리</param>
        /// <returns>쿼리 결과 문자열</returns>
        public string ExecuteScalarString(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("쿼리는 필수입니다.", nameof(query));
            
            ValidateConnection();
            
            using (SqlCommand command = new SqlCommand(query, _connection))
            {
                try
                {
                    object result = command.ExecuteScalar();
                    return result?.ToString() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    ShowErrorMessage($"스칼라 쿼리 실행 실패: {ex.Message}", "쿼리 오류");
                    throw;
                }
            }
        }

        /// <summary>
        /// 단일 정수 값을 반환하는 스칼라 쿼리를 실행합니다.
        /// </summary>
        /// <param name="query">실행할 쿼리</param>
        /// <returns>쿼리 결과 정수값</returns>
        public int ExecuteScalarInt(string query)
        {
            string result = ExecuteScalarString(query);
            
            if (int.TryParse(result, out int intValue))
            {
                return intValue;
            }
            
            return 0;
        }

        /// <summary>
        /// 쿼리 실행 결과를 DataSet으로 반환합니다.
        /// </summary>
        /// <param name="query">실행할 쿼리</param>
        /// <returns>DataSet 객체</returns>
        public DataSet ExecuteDataSet(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentException("쿼리는 필수입니다.", nameof(query));
            
            string connectionString = BuildConnectionString();
            
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = DefaultCommandTimeout;
                    
                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        DataSet dataSet = new DataSet();
                        
                        try
                        {
                            connection.Open();
                            adapter.Fill(dataSet);
                            return dataSet;
                        }
                        catch (Exception ex)
                        {
                            ShowErrorMessage($"DataSet 생성 실패: {ex.Message}", "데이터 오류");
                            throw;
                        }
                    }
                }
            }
        }

        #endregion

        #region 디버그 및 유틸리티 메서드

        /// <summary>
        /// 디버그 모드에서만 쿼리를 실행합니다 (권한이 있는 사용자만).
        /// </summary>
        /// <param name="query">실행할 쿼리</param>
        /// <param name="workerId">작업자 ID</param>
        public void DebugExecute(string query, string workerId)
        {
            if (string.IsNullOrWhiteSpace(query))
                return;
            
            // 디버그 권한 확인
            string permissionQuery = $"SELECT COUNT(*) FROM vhb01 WHERE empno = '{workerId}'";
            int hasPermission = ExecuteScalarInt(permissionQuery);
            
            if (hasPermission > 0)
            {
                MessageBox.Show($"디버그 실행 중...\n쿼리: {query}", "디버그", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                ExecuteNonQuery(query);
            }
        }

        /// <summary>
        /// 연결 상태를 검증합니다.
        /// </summary>
        private void ValidateConnection()
        {
            if (_connectionState != ConnectionOpenState || _connection == null)
            {
                throw new InvalidOperationException("데이터베이스에 연결되어 있지 않습니다.");
            }
        }

        /// <summary>
        /// 새로운 연결을 생성합니다.
        /// </summary>
        /// <returns>새로운 SqlConnection 객체</returns>
        private SqlConnection CreateNewConnection()
        {
            string connectionString = BuildConnectionString();
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        #endregion

        #region 에러 처리 및 로깅

        /// <summary>
        /// 에러 메시지를 표시합니다.
        /// </summary>
        /// <param name="message">에러 메시지</param>
        /// <param name="title">창 제목</param>
        private void ShowErrorMessage(string message, string title = "오류")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// 디버그 로그를 출력합니다 (개발 환경에서만).
        /// </summary>
        /// <param name="message">로그 메시지</param>
        private void DebugLog(string message)
        {
            #if DEBUG
            Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss} - {message}");
            #endif
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
        /// 관리/비관리 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리 리소스 정리 여부</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                SafeCloseConnection();
            }
        }

        /// <summary>
        /// 소멸자
        /// </summary>
        ~DatabaseManager()
        {
            Dispose(false);
        }

        #endregion
    }
}
