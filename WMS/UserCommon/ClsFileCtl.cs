using System;
using System.IO;
using System.Windows.Forms;

namespace MES_WMS.UserCommon
{
    /// <summary>
    INI 설정 파일을 관리하는 클래스
    </summary>
    public class ConfigFileManager
    {
        #region 상수 정의

        private const string DefaultConfigFileName = "config_mes.ini";
        private const string DefaultConfigDirectory = @"C:\Windows\CMES\";
        private const string LoginSection = "LOGIN";
        private const string EmpNoKey = "empno";
        private const string PasswordKey = "password";

        #endregion

        #region 필드 선언

        private readonly IniFileUtil _iniFileUtil;
        private readonly string _configFilePath;

        #endregion

        #region 생성자

        /// <summary>
        /// 기본 생성자 - 기본 경로에서 설정 파일을 로드합니다.
        /// </summary>
        public ConfigFileManager()
        {
            _configFilePath = GetDefaultConfigFilePath();
            _iniFileUtil = new IniFileUtil(_configFilePath);
            
            EnsureConfigFileExists();
        }

        /// <summary>
        /// 사용자 지정 경로에서 설정 파일을 로드합니다.
        /// </summary>
        /// <param name="configFilePath">설정 파일 경로</param>
        public ConfigFileManager(string configFilePath)
        {
            if (string.IsNullOrWhiteSpace(configFilePath))
                throw new ArgumentException("설정 파일 경로는 필수입니다.", nameof(configFilePath));
            
            _configFilePath = configFilePath;
            _iniFileUtil = new IniFileUtil(_configFilePath);
            
            EnsureConfigFileExists();
        }

        #endregion

        #region 속성

        /// <summary>
        /// 설정 파일의 전체 경로를 가져옵니다.
        /// </summary>
        public string ConfigFilePath => _configFilePath;

        /// <summary>
        /// 설정 파일이 존재하는지 여부를 확인합니다.
        /// </summary>
        public bool ConfigFileExists => File.Exists(_configFilePath);

        #endregion

        #region 기본 설정 파일 관리

        /// <summary>
        /// 기본 설정 파일 경로를 가져옵니다.
        /// </summary>
        /// <returns>설정 파일의 전체 경로</returns>
        private string GetDefaultConfigFilePath()
        {
            return Path.Combine(DefaultConfigDirectory, DefaultConfigFileName);
        }

        /// <summary>
        /// 설정 파일이 존재하는지 확인하고 없으면 경고 메시지를 표시합니다.
        /// </summary>
        private void EnsureConfigFileExists()
        {
            if (!ConfigFileExists)
            {
                ShowWarningMessage(
                    $"설정 파일을 찾을 수 없습니다: {_configFilePath}\n" +
                    "기본값을 사용하거나 관리자에게 문의하세요.",
                    "설정 파일 오류"
                );
            }
        }

        #endregion

        #region 로그인 정보 관리

        /// <summary>
        /// 로그인 정보를 가져옵니다.
        /// </summary>
        /// <returns>사번과 비밀번호를 포함하는 문자열 배열</returns>
        public string[] GetLoginCredentials()
        {
            try
            {
                string[] credentials = new string[2];
                
                credentials[0] = GetConfigValue(LoginSection, EmpNoKey);
                credentials[1] = GetConfigValue(LoginSection, PasswordKey);
                
                return credentials;
            }
            catch (Exception ex)
            {
                LogError($"로그인 정보 조회 실패: {ex.Message}");
                throw new ApplicationException("로그인 정보를 가져오는 중 오류가 발생했습니다.", ex);
            }
        }

        /// <summary>
        /// 사번(직원번호)을 가져옵니다.
        /// </summary>
        /// <returns>사번 문자열</returns>
        public string GetEmployeeNumber()
        {
            return GetConfigValue(LoginSection, EmpNoKey);
        }

        /// <summary>
        /// 비밀번호를 가져옵니다.
        /// </summary>
        /// <returns>비밀번호 문자열</returns>
        public string GetPassword()
        {
            return GetConfigValue(LoginSection, PasswordKey);
        }

        /// <summary>
        /// 로그인 정보를 설정합니다.
        /// </summary>
        /// <param name="employeeNumber">사번</param>
        /// <param name="password">비밀번호</param>
        public void SetLoginCredentials(string employeeNumber, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(employeeNumber))
                    throw new ArgumentException("사번은 필수입니다.", nameof(employeeNumber));
                
                SetConfigValue(LoginSection, EmpNoKey, employeeNumber);
                SetConfigValue(LoginSection, PasswordKey, password ?? string.Empty);
            }
            catch (Exception ex)
            {
                LogError($"로그인 정보 저장 실패: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 일반 설정 값 관리

        /// <summary>
        /// 특정 섹션과 키의 설정 값을 가져옵니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <returns>설정 값</returns>
        public string GetConfigValue(string section, string key)
        {
            if (string.IsNullOrWhiteSpace(section))
                throw new ArgumentException("섹션은 필수입니다.", nameof(section));
            
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("키는 필수입니다.", nameof(key));
            
            try
            {
                return _iniFileUtil.GetIniValue(section, key) ?? string.Empty;
            }
            catch (Exception ex)
            {
                LogError($"설정 값 조회 실패 - 섹션: {section}, 키: {key}, 오류: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 특정 섹션과 키의 설정 값을 정수형으로 가져옵니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="defaultValue">기본값</param>
        /// <returns>정수형 설정 값</returns>
        public int GetConfigValueAsInt(string section, string key, int defaultValue = 0)
        {
            string stringValue = GetConfigValue(section, key);
            
            if (int.TryParse(stringValue, out int intValue))
            {
                return intValue;
            }
            
            return defaultValue;
        }

        /// <summary>
        /// 특정 섹션과 키의 설정 값을 불리언형으로 가져옵니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="defaultValue">기본값</param>
        /// <returns>불리언형 설정 값</returns>
        public bool GetConfigValueAsBool(string section, string key, bool defaultValue = false)
        {
            string stringValue = GetConfigValue(section, key).ToLower();
            
            if (stringValue == "true" || stringValue == "1" || stringValue == "yes")
                return true;
            else if (stringValue == "false" || stringValue == "0" || stringValue == "no")
                return false;
            else
                return defaultValue;
        }

        /// <summary>
        /// 특정 섹션과 키의 설정 값을 설정합니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="value">설정할 값</param>
        public void SetConfigValue(string section, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(section))
                throw new ArgumentException("섹션은 필수입니다.", nameof(section));
            
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("키는 필수입니다.", nameof(key));
            
            try
            {
                _iniFileUtil.SetIniValue(section, key, value ?? string.Empty);
                
                // 설정 변경 로깅 (민감 정보 제외)
                if (!IsSensitiveInformation(key))
                {
                    LogInfo($"설정 값 저장 - 섹션: {section}, 키: {key}");
                }
            }
            catch (Exception ex)
            {
                LogError($"설정 값 저장 실패 - 섹션: {section}, 키: {key}, 오류: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 특정 섹션과 키의 설정 값을 정수형으로 설정합니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="value">설정할 정수값</param>
        public void SetConfigValue(string section, string key, int value)
        {
            SetConfigValue(section, key, value.ToString());
        }

        /// <summary>
        /// 특정 섹션과 키의 설정 값을 불리언형으로 설정합니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="value">설정할 불리언값</param>
        public void SetConfigValue(string section, string key, bool value)
        {
            SetConfigValue(section, key, value.ToString());
        }

        /// <summary>
        /// 특정 섹션의 모든 설정 값을 가져옵니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <returns>섹션의 모든 키-값 쌍</returns>
        public Dictionary<string, string> GetAllValuesInSection(string section)
        {
            var values = new Dictionary<string, string>();
            
            try
            {
                // IniFileUtil에 GetAllValuesInSection 메서드가 있다고 가정
                // 실제 구현에 따라 수정 필요
                // values = _iniFileUtil.GetAllValuesInSection(section);
            }
            catch (Exception ex)
            {
                LogError($"섹션 값 조회 실패 - 섹션: {section}, 오류: {ex.Message}");
            }
            
            return values;
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 키가 민감한 정보를 포함하는지 확인합니다.
        /// </summary>
        /// <param name="key">확인할 키</param>
        /// <returns>민감 정보 여부</returns>
        private bool IsSensitiveInformation(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            
            string lowerKey = key.ToLower();
            
            return lowerKey.Contains("password") ||
                   lowerKey.Contains("pwd") ||
                   lowerKey.Contains("secret") ||
                   lowerKey.Contains("token") ||
                   lowerKey.Contains("key");
        }

        /// <summary>
        /// 설정 파일의 백업을 생성합니다.
        /// </summary>
        /// <param name="backupDirectory">백업 디렉토리 (기본값: 원본 파일과 동일)</param>
        public void CreateBackup(string backupDirectory = null)
        {
            try
            {
                if (!ConfigFileExists)
                    return;
                
                string backupPath = backupDirectory ?? Path.GetDirectoryName(_configFilePath);
                string backupFileName = $"{Path.GetFileNameWithoutExtension(_configFilePath)}_" +
                                       $"{DateTime.Now:yyyyMMdd_HHmmss}" +
                                       $"{Path.GetExtension(_configFilePath)}";
                
                string fullBackupPath = Path.Combine(backupPath, backupFileName);
                File.Copy(_configFilePath, fullBackupPath, true);
                
                LogInfo($"설정 파일 백업 생성: {fullBackupPath}");
            }
            catch (Exception ex)
            {
                LogError($"백업 생성 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 설정 파일을 기본값으로 초기화합니다.
        /// </summary>
        public void ResetToDefaults()
        {
            try
            {
                CreateBackup(); // 변경 전 백업
                
                // 기본값 설정 로직 구현
                // 예: SetConfigValue("LOGIN", "empno", "");
                // 예: SetConfigValue("LOGIN", "password", "");
                
                LogInfo("설정 파일을 기본값으로 초기화했습니다.");
            }
            catch (Exception ex)
            {
                LogError($"설정 초기화 실패: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region 로깅 및 메시지 표시

        /// <summary>
        /// 오류 메시지를 로그합니다.
        /// </summary>
        /// <param name="message">로그 메시지</param>
        private void LogError(string message)
        {
            // 실제 구현: 로그 파일 또는 이벤트 로그에 기록
            Console.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        /// <summary>
        /// 정보 메시지를 로그합니다.
        /// </summary>
        /// <param name="message">로그 메시지</param>
        private void LogInfo(string message)
        {
            // 실제 구현: 로그 파일 또는 이벤트 로그에 기록
            Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        /// <summary>
        /// 경고 메시지를 표시합니다.
        /// </summary>
        /// <param name="message">메시지 내용</param>
        /// <param name="title">창 제목</param>
        private void ShowWarningMessage(string message, string title = "경고")
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        #endregion
    }
}
