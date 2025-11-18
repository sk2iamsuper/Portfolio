using System.Globalization;
using System.Resources;

/// <summary>
/// 애플리케이션의 모든 사운드 재생을 관리하는 클래스
/// 스레드 안전한 사운드 재생과 리소스 관리 담당
/// </summary>
public class SoundManager : IDisposable
{
    #region Private Fields
    private readonly SoundPlayer _startPlayer;          // 작업 시작 사운드
    private readonly SoundPlayer _completedPlayer;      // 작업 완료 사운드
    private readonly SoundPlayer _emergencyPlayer;      // 비상 상황 사운드
    private readonly SoundPlayer _readyPlayer;          // 준비 완료 사운드
    private readonly SoundPlayer _doorOpenedPlayer;     // 도어 오픈 사운드
    
    private Thread _soundPlayerThread;                  // 백그라운드 사운드 재생 스레드
    private bool _isPlaySound;                          // 사운드 재생 상태 플래그
    private bool _disposed = false;                     // Dispose 상태 플래그
    #endregion

    #region Constructor
    /// <summary>
    /// SoundManager 생성자 - 문화권에 따라 적절한 사운드 리소스 로드
    /// </summary>
    /// <param name="culture">현재 애플리케이션 문화권</param>
    public SoundManager(CultureInfo culture)
    {
        // 문화권에 따라 사운드 리소스 선택 (한국어/베트남어)
        var resources = culture.Name == "ko-KR" 
            ? Constants.SoundResources.Korean 
            : Constants.SoundResources.Vietnamese;

        // 각 사운드 플레이어 초기화
        _startPlayer = CreateSoundPlayer(resources.START);
        _completedPlayer = CreateSoundPlayer(resources.COMPLETED);
        _emergencyPlayer = CreateSoundPlayer(resources.EMERGENCY);
        _readyPlayer = CreateSoundPlayer(resources.READY);
        _doorOpenedPlayer = CreateSoundPlayer(resources.DOOR_OPENED);
        
        // 사운드 데이터 미리 로드
        LoadAllSounds();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 작업 시작 사운드 재생
    /// </summary>
    public void PlayStart() => PlaySound(_startPlayer);
    
    /// <summary>
    /// 작업 완료 사운드 재생 (루프 모드)
    /// </summary>
    public void PlayCompleted() => PlaySound(_completedPlayer, true);
    
    /// <summary>
    /// 비상 상황 사운드 재생 (루프 모드)
    /// </summary>
    public void PlayEmergency() => PlaySound(_emergencyPlayer, true);
    
    /// <summary>
    /// 준비 완료 사운드 재생
    /// </summary>
    public void PlayReady() => PlaySound(_readyPlayer);
    
    /// <summary>
    /// 도어 오픈 사운드 재생 (루프 모드)
    /// </summary>
    public void PlayDoorOpened() => PlaySound(_doorOpenedPlayer, true);

    /// <summary>
    /// 지정된 사운드 플레이어로 사운드 재생
    /// </summary>
    /// <param name="player">재생할 사운드 플레이어</param>
    /// <param name="isPlayLooping">루프 재생 여부</param>
    public void PlaySound(SoundPlayer player, bool isPlayLooping = false)
    {
        // 기존 재생 중인 사운드 정지
        StopAll();
        
        // 사운드 재생 스레드가 실행 중이면 종료 대기
        _soundPlayerThread?.Join();

        // 새로운 사운드 재생 스레드 생성 및 시작
        _soundPlayerThread = new Thread(() =>
        {
            if (!isPlayLooping)
            {
                // 단일 재생 모드
                player.Play();
            }
            else
            {
                // 루프 재생 모드
                _isPlaySound = true;
                while (_isPlaySound && !_disposed)
                {
                    player.Play();
                    Thread.Sleep(Constants.SOUND_DELAY_MS); // 지정된 간격으로 재생
                }
            }
        })
        { IsBackground = true }; // 백그라운드 스레드로 설정
        
        _soundPlayerThread.Start();
    }

    /// <summary>
    /// 모든 사운드 재생 정지
    /// </summary>
    public void StopAll()
    {
        _isPlaySound = false; // 루프 재생 플래그 해제
        
        // 모든 사운드 플레이어 정지
        _doorOpenedPlayer?.Stop();
        _emergencyPlayer?.Stop();
        _startPlayer?.Stop();
        _completedPlayer?.Stop();
        _readyPlayer?.Stop();
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// 리소스 이름으로 사운드 플레이어 생성
    /// </summary>
    /// <param name="resourceName">리소스 이름</param>
    /// <returns>생성된 SoundPlayer 인스턴스</returns>
    private SoundPlayer CreateSoundPlayer(string resourceName)
    {
        var resourceManager = new ResourceManager(typeof(Resources));
        var soundData = (UnmanagedMemoryStream)resourceManager.GetObject(resourceName);
        return new SoundPlayer(soundData);
    }

    /// <summary>
    /// 모든 사운드 데이터 미리 로드
    /// </summary>
    private void LoadAllSounds()
    {
        _startPlayer.Load();
        _completedPlayer.Load();
        _emergencyPlayer.Load();
        _readyPlayer.Load();
        _doorOpenedPlayer.Load();
    }
    #endregion

    #region IDisposable Implementation
    /// <summary>
    /// 리소스 해제 - 관리되지 않는 리소스 정리
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            StopAll(); // 모든 사운드 정지
            
            // 사운드 재생 스레드 종료 대기 (최대 1초)
            _soundPlayerThread?.Join(1000);
            
            // 모든 사운드 플레이어 리소스 해제
            _startPlayer?.Dispose();
            _completedPlayer?.Dispose();
            _emergencyPlayer?.Dispose();
            _readyPlayer?.Dispose();
            _doorOpenedPlayer?.Dispose();
        }
    }
    #endregion
}
