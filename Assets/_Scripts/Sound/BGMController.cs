using UnityEngine;

// 이 스크립트는 "씬 시작 시 지정된 BGM을 재생"하고, 필요하면 다른 BGM으로 전환하는 컨트롤러다.
public class BGMController : MonoBehaviour
{
    [SerializeField] private BgmId startBgm = BgmId.Main;  // Start()에서 최초로 재생할 BGM ID

    /// <summary>
    /// 유니티 생명주기: 오브젝트가 활성화된 후 1회 호출되며, 시작 BGM을 재생한다.
    /// </summary>
    private void Start()
    {
        SoundManager.PlayBGM(startBgm, restart: false);     // 이미 같은 곡이 재생 중이면 유지(재시작 X)
    }

    /// <summary>
    /// BGM을 지정한 ID로 전환한다.
    /// </summary>
    /// <param name="id">재생할 BGM ID</param>
    /// <param name="restart">true면 같은 곡이라도 처음부터 재생, false면 같은 곡이면 유지</param>
    public void SwitchTo(BgmId id, bool restart = false)
    {
        SoundManager.PlayBGM(id, restart);                  // SoundManager에 BGM 재생 요청
    }
}
