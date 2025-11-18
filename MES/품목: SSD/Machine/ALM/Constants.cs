/// <summary>
/// 애플리케이션에서 사용하는 모든 상수 값을 정의하는 클래스
/// 매직 넘버와 문자열을 제거하여 유지보수성을 높임
/// </summary>
public static class Constants
{
    // LOT 관련 상수
    public const int LOT_ID_LENGTH = 10;                    // LOT ID의 고정 길이
    public const int LABEL_CREATE_COUNT = 50;               // MOS 모드에서 한번에 생성할 라벨 수
    public const int SAMPLE_READ_COUNT = 10;                // 로드셀 측정 시 샘플링 횟수
    public const int SOUND_DELAY_MS = 3000;                 // 사운드 루프 재생 시 대기 시간(ms)
    
    // 로드셀 측정 관련 상수
    public const double LOAD_CELL_MIN = 0.9;                // 로드셀 최소 허용 값(kg)
    public const double LOAD_CELL_MAX = 1.1;                // 로드셀 최대 허용 값(kg)
    public const double LOAD_CELL_THRESHOLD = 0.1;          // 유효한 로드셀 값 판단 임계값
    
    /// <summary>
    /// 사용자 역할 정의 - 권한 체크에 사용
    /// </summary>
    public static class UserRoles
    {
        public const string SW_DEVELOPMENT = "SW개발";      // 소프트웨어 개발자 역할
        public const string MANUFACTURING_TECH = "제조기술"; // 제조 기술자 역할
    }
    
    /// <summary>
    /// 사용자 메시지 상수 - UI에 표시되는 메시지 통합 관리
    /// </summary>
    public static class Messages
    {
        public const string LOT_NOT_FOUND = "Could not find LOT Information.";
        public const string LOT_STEP_MISMATCH = "LOT step does not match.";
        public const string SYSTEM_NOT_READY = "Status is not ready.";
        public const string DOOR_OPENED_WARNING = "The facility door is open. Close the door and run it again.";
        public const string INVALID_LOT_ID = "Invalid LOT id.";
    }
    
    /// <summary>
    /// 사운드 리소스 이름 상수 - 리소스 관리자에서 사운드 파일을 로드할 때 사용
    /// </summary>
    public static class SoundResources
    {
        /// <summary>
        /// 한국어 사운드 리소스 이름
        /// </summary>
        public static class Korean
        {
            public const string START = "Start";
            public const string COMPLETED = "Completed";
            public const string EMERGENCY = "Emergency";
            public const string READY = "Ready";
            public const string DOOR_OPENED = "DoorOpened";
        }
        
        /// <summary>
        /// 베트남어 사운드 리소스 이름
        /// </summary>
        public static class Vietnamese
        {
            public const string START = "Start_vn";
            public const string COMPLETED = "Completed_vn";
            public const string EMERGENCY = "Emergency_vn";
            public const string READY = "Ready_vn";
            public const string DOOR_OPENED = "DoorOpened_vn";
        }
    }
}
