/// <summary>
/// 애플리케이션 설정을 중앙에서 관리하는 클래스
/// 프로그램 전반에 걸친 설정 값을 캡슐화
/// </summary>
public class AppConfiguration
{
    #region Public Properties
    /// <summary>
    /// 장비 이름 - MES/MOS 시스템에서 식별하는 장비 ID
    /// </summary>
    public string EquipmentName { get; private set; }
    
    /// <summary>
    /// 작업 단계 이름 - 현재 공정 단계 식별자
    /// </summary>
    public string OperationStepName { get; private set; }
    
    /// <summary>
    /// 작업 단계 ID - 데이터베이스에서 사용하는 공정 단계 ID
    /// </summary>
    public string OperationStepId { get; private set; }
    
    /// <summary>
    /// 전체 모드 여부 - 전후면 모두 사용하는 모드인지 여부
    /// </summary>
    public bool IsFullMode { get; private set; }
    
    /// <summary>
    /// 진단 모드 활성화 여부 - 디버깅 및 진단 기능 사용 여부
    /// </summary>
    public bool IsDiagnosticsEnabled { get; private set; }
    
    /// <summary>
    /// 후면 라벨 생략 여부 - 후면 라벨링을 건너뛸지 여부
    /// </summary>
    public bool SkipBackLabel { get; private set; }
    #endregion

    #region Constructor
    /// <summary>
    /// private 생성자 - 팩토리 메서드 패턴 사용
    /// </summary>
    private AppConfiguration() { }
    #endregion

    #region Public Methods
    /// <summary>
    /// 애플리케이션 설정을 로드하는 팩토리 메서드
    /// 프로그램 설정에서 값을 읽어와 AppConfiguration 인스턴스 생성
    /// </summary>
    /// <returns>로드된 설정 인스턴스</returns>
    public static AppConfiguration Load()
    {
        return new AppConfiguration
        {
            EquipmentName = Program.GeneralConfig["EQUIPMENT_NAME"].StringValue,
            OperationStepName = Program.GeneralConfig["OPERATION_STEP_NAME"].StringValue,
            OperationStepId = Program.GeneralConfig["OPERATION_STEP_ID"].StringValue,
            IsFullMode = Program.GeneralConfig["FULL_MODE"].BoolValue,
            IsDiagnosticsEnabled = Program.GeneralConfig["Diagnostics"].BoolValue,
            SkipBackLabel = Program.GeneralConfig["SKIP_BACKLABEL"].BoolValue
        };
    }
    #endregion
}
