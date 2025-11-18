/// <summary>
/// UI 애플리케이션을 위한 통합 예외 처리 기능 제공
/// 모든 예외를 일관된 방식으로 처리하고 사용자에게 친숙한 메시지 표시
/// </summary>
public static class ExceptionHandler
{
    #region Private Fields
    private static readonly Logger Logger = LogManager.GetLogger("ExceptionHandler");
    #endregion

    #region Public Methods
    /// <summary>
    /// 동기 작업에 대한 예외 처리 래퍼
    /// </summary>
    /// <param name="action">실행할 작업</param>
    /// <param name="operationName">작업 이름 (로깅용)</param>
    /// <param name="parent">부모 컨트롤 (UI 스레드 동기화용)</param>
    /// <returns>작업 성공 여부</returns>
    public static bool HandleUIException(Action action, string operationName, Control parent = null)
    {
        try
        {
            action(); // 실제 작업 실행
            return true;
        }
        catch (Exception ex)
        {
            // 예외 정보 로깅
            Logger.Error(ex, $"{operationName} failed");
            
            // 사용자에게 오류 메시지 표시
            ShowErrorMessage($"{operationName} 중 오류가 발생했습니다: {ex.Message}", parent);
            return false;
        }
    }

    /// <summary>
    /// 비동기 작업에 대한 예외 처리 래퍼
    /// </summary>
    /// <param name="action">실행할 비동기 작업</param>
    /// <param name="operationName">작업 이름 (로깅용)</param>
    /// <param name="parent">부모 컨트롤 (UI 스레드 동기화용)</param>
    /// <returns>작업 성공 여부를 나타내는 Task</returns>
    public static async Task<bool> HandleUIExceptionAsync(Func<Task> action, string operationName, Control parent = null)
    {
        try
        {
            await action(); // 실제 비동기 작업 실행
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"{operationName} failed");
            ShowErrorMessage($"{operationName} 중 오류가 발생했습니다: {ex.Message}", parent);
            return false;
        }
    }

    /// <summary>
    /// 반환값이 있는 동기 작업에 대한 예외 처리 래퍼
    /// </summary>
    /// <typeparam name="T">반환 타입</typeparam>
    /// <param name="func">실행할 함수</param>
    /// <param name="operationName">작업 이름 (로깅용)</param>
    /// <param name="defaultValue">예외 발생 시 반환할 기본값</param>
    /// <param name="parent">부모 컨트롤 (UI 스레드 동기화용)</param>
    /// <returns>함수 결과 또는 기본값</returns>
    public static T HandleUIException<T>(Func<T> func, string operationName, T defaultValue = default, Control parent = null)
    {
        try
        {
            return func(); // 실제 함수 실행
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"{operationName} failed");
            ShowErrorMessage($"{operationName} 중 오류가 발생했습니다: {ex.Message}", parent);
            return defaultValue; // 예외 발생 시 기본값 반환
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// UI 스레드에서 오류 메시지 박스 표시 (스레드 안전)
    /// </summary>
    private static void ShowErrorMessage(string message, Control parent)
    {
        if (parent?.InvokeRequired == true)
        {
            // UI 스레드에서 실행되도록 Invoke 사용
            parent.Invoke(new Action<string, Control>(ShowErrorMessage), message, parent);
            return;
        }

        // 오류 메시지 박스 표시
        MessageBox.Show(parent, message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    #endregion
}
