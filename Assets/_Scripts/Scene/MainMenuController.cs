// MainMenuController.cs
// MainScene의 Canvas(또는 빈 오브젝트)에 붙이고,
// 버튼 OnClick에 StartGame / QuitGame 연결하면 끝.

using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string inGameSceneName = "InGame";

    [Header("SFX (Optional)")]
    [SerializeField] private bool playClickSfx = true;

    public void StartGame()
    {
        if (playClickSfx)
            SoundManager.PlaySFX(SfxId.Button_Interact);

        SceneManager.LoadScene(inGameSceneName);
    }

    public void QuitGame()
    {
        if (playClickSfx)
            SoundManager.PlaySFX(SfxId.Button_Interact);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
