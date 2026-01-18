using UnityEngine;

public class BGMController : MonoBehaviour
{
    [SerializeField] private BgmId startBgm = BgmId.Main;

    private void Start()
    {
        SoundManager.PlayBGM(startBgm, restart: false);
    }

    // 필요하면 나중에:
    public void SwitchTo(BgmId id, bool restart = false)
    {
        SoundManager.PlayBGM(id, restart);
    }
}
