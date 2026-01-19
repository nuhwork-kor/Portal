// - MainScene(메인 메뉴 씬)의 UI 버튼(OnClick)에서 호출될 함수들을 제공한다.
// - "게임 시작" 버튼은 InGame 씬으로 이동, "나가기" 버튼은 앱 종료(에디터에선 플레이 중지).
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]                            // 같은 GameObject에 MainMenuController를 2개 이상 못 붙이게 방지
public class MainMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string inGameSceneName = "InGame"; // 게임 시작 시 로드할 씬 이름(Build Settings에 등록되어 있어야 함)

    [Header("SFX (Optional)")]
    [SerializeField] private bool playClickSfx = true;          // 버튼 클릭 시 SFX를 재생할지

    /// <summary>
    /// 메인 메뉴의 "게임 시작" 버튼에서 호출한다.
    /// 클릭 SFX를 재생하고, 지정된 InGame 씬을 로드한다.
    /// </summary>
    public void StartGame()
    {
        if (playClickSfx)                                       // 옵션: 클릭 사운드 재생
            SoundManager.PlaySFX(SfxId.Button_Interact);        // 버튼 인터랙트 SFX(2D)

        SceneManager.LoadScene(inGameSceneName);                // InGame 씬으로 전환
    }

    /// <summary>
    /// 메인 메뉴의 "나가기" 버튼에서 호출한다.
    /// 클릭 SFX를 재생하고, 빌드 환경에서는 Application.Quit()로 종료한다.
    /// Unity Editor에서는 플레이 모드를 종료한다.
    /// </summary>
    public void QuitGame()
    {
        if (playClickSfx)                                       // 옵션: 클릭 사운드 재생
            SoundManager.PlaySFX(SfxId.Button_Interact);        // 버튼 인터랙트 SFX(2D)

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;        // 에디터: 플레이 모드 종료
#else
        Application.Quit();                                     // 빌드: 게임 종료
#endif
    }
}
