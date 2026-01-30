using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MES_WMS.UserCommon
{
    /// <summary>
    /// Windows INI 파일을 읽고 쓰기 위한 유틸리티 클래스
    /// </summary>
    public class IniFileManager : IDisposable
    {
        #region Win32 API 상수 및 메서드 선언

        private const int MaximumStringLength = 255;
        private const string Kernel32Library = "kernel32.dll";

        [DllImport(Kernel32Library, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetPrivateProfileString(
            string section,
            string key,
            string defaultValue,
            StringBuilder returnValue,
            uint size,
            string filePath);

        [DllImport(Kernel32Library, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool WritePrivateProfileString(
            string section,
            string key,
            string value,
            string filePath);

        [DllImport(Kernel32Library, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetPrivateProfileSection(
            string section,
            byte[] returnBuffer,
            uint size,
            string filePath);

        [DllImport(Kernel32Library, CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool WritePrivateProfileSection(
            string section,
            string data,
            string filePath);

        #endregion

        #region 필드 선언

        private readonly string _iniFilePath;
        private bool _disposed = false;

        #endregion

        #region 생성자

        /// <summary>
        /// 지정된 경로의 INI 파일을 관리하는 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="filePath">INI 파일 경로</param>
        /// <exception cref="ArgumentException">파일 경로가 null이거나 비어있는 경우</exception>
        public IniFileManager(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("INI 파일 경로는 필수입니다.", nameof(filePath));

            _iniFilePath = filePath;
            
            // 파일 디렉토리 확인 및 생성
            EnsureDirectoryExists();
        }

        #endregion

        #region 속성

        /// <summary>
        /// INI 파일의 전체 경로를 가져옵니다.
        /// </summary>
        public string FilePath => _iniFilePath;

        /// <summary>
        /// INI 파일이 존재하는지 여부를 확인합니다.
        /// </summary>
        public bool FileExists => File.Exists(_iniFilePath);

        /// <summary>
        /// INI 파일의 디렉토리 경로를 가져옵니다.
        /// </summary>
        public string DirectoryPath => Path.GetDirectoryName(_iniFilePath);

        #endregion

        #region 값 읽기 메서드

        /// <summary>
        /// INI 파일에서 특정 섹션과 키에 해당하는 값을 읽어옵니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="defaultValue">값이 없을 경우 반환할 기본값</param>
        /// <returns>읽어온 값 또는 기본값</returns>
        /// <exception cref="ArgumentException">섹션 또는 키가 null이거나 비어있는 경우</exception>
        public string GetValue(string section, string key, string defaultValue = "")
        {
            ValidateSectionAndKey(section, key);

            StringBuilder stringBuilder = new StringBuilder(MaximumStringLength);
            
            uint result = GetPrivateProfileString(
                section,
                key,
                defaultValue,
                stringBuilder,
                (uint)stringBuilder.Capacity,
                _iniFilePath
            );

            // Win32 API 오류 확인
            if (Marshal.GetLastWin32Error() != 0)
            {
                LogError($"INI 파일 읽기 오류 - 섹션: {section}, 키: {key}");
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// INI 파일에서 특정 섹션과 키에 해당하는 값을 정수형으로 읽어옵니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="defaultValue">값이 없거나 변환 실패 시 반환할 기본값</param>
        /// <returns>정수형 값</returns>
        public int GetIntegerValue(string section, string key, int defaultValue = 0)
        {
            string stringValue = GetValue(section, key, defaultValue.ToString());
            
            if (int.TryParse(stringValue, out int result))
            {
                return result;
            }

            return defaultValue;
        }

        /// <summary>
        /// INI 파일에서 특정 섹션과 키에 해당하는 값을 불리언형으로 읽어옵니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="defaultValue">값이 없거나 변환 실패 시 반환할 기본값</param>
        /// <returns>불리언 값</returns>
        public bool GetBooleanValue(string section, string key, bool defaultValue = false)
        {
            string stringValue = GetValue(section, key, defaultValue.ToString()).ToLower();
            
            if (stringValue == "true" || stringValue == "1" || stringValue == "yes")
                return true;
            else if (stringValue == "false" || stringValue == "0" || stringValue == "no")
                return false;
            else
                return defaultValue;
        }

        /// <summary>
        /// 특정 섹션의 모든 키-값 쌍을 읽어옵니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <returns>키-값 쌍의 딕셔너리</returns>
        public Dictionary<string, string> GetAllValuesInSection(string section)
        {
            var result = new Dictionary<string, string>();
            
            if (string.IsNullOrWhiteSpace(section))
                return result;

            byte[] buffer = new byte[MaximumStringLength * 100]; // 충분한 크기의 버퍼
            
            uint bytesRead = GetPrivateProfileSection(
                section,
                buffer,
                (uint)buffer.Length,
                _iniFilePath
            );

            if (bytesRead > 0)
            {
                string sectionData = Encoding.Unicode.GetString(buffer, 0, (int)bytesRead);
                string[] keyValuePairs = sectionData.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                
                foreach (string pair in keyValuePairs)
                {
                    int equalsIndex = pair.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = pair.Substring(0, equalsIndex);
                        string value = pair.Substring(equalsIndex + 1);
                        result[key] = value;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// INI 파일의 모든 섹션 이름을 읽어옵니다.
        /// </summary>
        /// <returns>섹션 이름 배열</returns>
        public string[] GetAllSections()
        {
            // 섹션 목록을 읽기 위해 빈 키와 섹션으로 호출
            byte[] buffer = new byte[MaximumStringLength * 100];
            
            uint bytesRead = GetPrivateProfileString(
                null,
                null,
                null,
                buffer,
                (uint)buffer.Length,
                _iniFilePath
            );

            if (bytesRead > 0)
            {
                string sectionsData = Encoding.Unicode.GetString(buffer, 0, (int)bytesRead);
                return sectionsData.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            }

            return Array.Empty<string>();
        }

        #endregion

        #region 값 쓰기 메서드

        /// <summary>
        /// INI 파일에 특정 섹션과 키에 값을 설정합니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="value">설정할 값</param>
        /// <returns>작업 성공 여부</returns>
        /// <exception cref="ArgumentException">섹션 또는 키가 null이거나 비어있는 경우</exception>
        public bool SetValue(string section, string key, string value)
        {
            ValidateSectionAndKey(section, key);

            // 값이 null인 경우 빈 문자열로 처리
            value ??= string.Empty;

            bool success = WritePrivateProfileString(
                section,
                key,
                value,
                _iniFilePath
            );

            if (!success)
            {
                LogError($"INI 파일 쓰기 오류 - 섹션: {section}, 키: {key}, 값: {value}");
            }
            else
            {
                LogInfo($"INI 값 설정 - 섹션: {section}, 키: {key}");
            }

            return success;
        }

        /// <summary>
        /// INI 파일에 특정 섹션과 키에 정수값을 설정합니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="value">설정할 정수값</param>
        /// <returns>작업 성공 여부</returns>
        public bool SetValue(string section, string key, int value)
        {
            return SetValue(section, key, value.ToString());
        }

        /// <summary>
        /// INI 파일에 특정 섹션과 키에 불리언값을 설정합니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">키 이름</param>
        /// <param name="value">설정할 불리언값</param>
        /// <returns>작업 성공 여부</returns>
        public bool SetValue(string section, string key, bool value)
        {
            return SetValue(section, key, value.ToString());
        }

        /// <summary>
        /// INI 파일에서 특정 섹션과 키를 삭제합니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="key">삭제할 키 이름 (null이면 전체 섹션 삭제)</param>
        /// <returns>작업 성공 여부</returns>
        public bool DeleteKey(string section, string key = null)
        {
            if (string.IsNullOrWhiteSpace(section))
                return false;

            bool success = WritePrivateProfileString(
                section,
                key,
                null,
                _iniFilePath
            );

            if (success)
            {
                LogInfo($"INI 키 삭제 - 섹션: {section}, 키: {key ?? "(전체 섹션)"}");
            }

            return success;
        }

        /// <summary>
        /// 특정 섹션의 모든 내용을 설정합니다.
        /// </summary>
        /// <param name="section">섹션 이름</param>
        /// <param name="keyValuePairs">설정할 키-값 쌍</param>
        /// <returns>작업 성공 여부</returns>
        public bool SetSection(string section, Dictionary<string, string> keyValuePairs)
        {
            if (string.IsNullOrWhiteSpace(section) || keyValuePairs == null)
                return false;

            StringBuilder sectionData = new StringBuilder();
            
            foreach (var pair in keyValuePairs)
            {
                sectionData.AppendFormat("{0}={1}\0", pair.Key, pair.Value ?? string.Empty);
            }

            sectionData.Append('\0'); // 섹션 종료 표시

            bool success = WritePrivateProfileSection(
                section,
                sectionData.ToString(),
                _iniFilePath
            );

            return success;
        }

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// INI 파일의 디렉토리가 존재하는지 확인하고 없으면 생성합니다.
        /// </summary>
        private void EnsureDirectoryExists()
        {
            try
            {
                string directory = DirectoryPath;
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    LogInfo($"INI 파일 디렉토리 생성: {directory}");
                }
            }
            catch (Exception ex)
            {
                LogError($"디렉토리 생성 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 섹션과 키의 유효성을 검사합니다.
        /// </summary>
        private void ValidateSectionAndKey(string section, string key)
        {
            if (string.IsNullOrWhiteSpace(section))
                throw new ArgumentException("섹션은 필수입니다.", nameof(section));

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("키는 필수입니다.", nameof(key));
        }

        /// <summary>
        /// INI 파일을 백업합니다.
        /// </summary>
        /// <param name="backupPath">백업 파일 경로 (null이면 동일 디렉토리에 생성)</param>
        /// <returns>백업 파일 경로</returns>
        public string BackupFile(string backupPath = null)
        {
            if (!FileExists)
                return null;

            try
            {
                string backupFilePath = backupPath ?? 
                    Path.Combine(
                        DirectoryPath,
                        $"{Path.GetFileNameWithoutExtension(_iniFilePath)}_backup_" +
                        $"{DateTime.Now:yyyyMMdd_HHmmss}" +
                        $"{Path.GetExtension(_iniFilePath)}"
                    );

                File.Copy(_iniFilePath, backupFilePath, true);
                LogInfo($"INI 파일 백업: {backupFilePath}");
                
                return backupFilePath;
            }
            catch (Exception ex)
            {
                LogError($"INI 파일 백업 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// INI 파일을 백업에서 복원합니다.
        /// </summary>
        /// <param name="backupPath">백업 파일 경로</param>
        /// <returns>복원 성공 여부</returns>
        public bool RestoreFromBackup(string backupPath)
        {
            if (!File.Exists(backupPath))
            {
                LogError($"백업 파일을 찾을 수 없음: {backupPath}");
                return false;
            }

            try
            {
                // 현재 파일 백업 (복원 실패 시 복구용)
                string tempBackup = BackupFile();
                
                File.Copy(backupPath, _iniFilePath, true);
                LogInfo($"INI 파일 복원: {backupPath}");
                
                return true;
            }
            catch (Exception ex)
            {
                LogError($"INI 파일 복원 실패: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 로깅 메서드

        /// <summary>
        /// 오류 메시지를 로그합니다.
        /// </summary>
        private void LogError(string message)
        {
            // 실제 구현에서는 로그 파일 또는 이벤트 로그에 기록
            Console.WriteLine($"[INI ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        }

        /// <summary>
        /// 정보 메시지를 로그합니다.
        /// </summary>
        private void LogInfo(string message)
        {
            // 실제 구현에서는 로그 파일 또는 이벤트 로그에 기록
            Console.WriteLine($"[INI INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
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
                // 여기서는 특별히 정리할 리소스가 없음
                _disposed = true;
            }
        }

        /// <summary>
        /// 소멸자
        /// </summary>
        ~IniFileManager()
        {
            Dispose(false);
        }

        #endregion
    }
}
