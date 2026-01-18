// EndingTrigger.cs
// 케이크 테이블 쪽 "Trigger Collider(isTrigger)" 오브젝트에 붙여서 사용.
// 트리거되면 Ending 씬으로 넘어감.

using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class EndingTrigger : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string endingSceneName = "Ending";
    [SerializeField] private float loadDelay = 0f;

    [Header("Player Detect")]
    [Tooltip("비워두면 'Player' 레이어를 자동으로 사용")]
    [SerializeField] private LayerMask playerMask;

    [Header("Optional SFX")]
    [SerializeField] private bool playSfx = true;
    [SerializeField] private SfxId sfxId = SfxId.StageTeleport;

    private bool _fired;

    private void Reset()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void Awake()
    {
        // 기본: Player 레이어 자동
        if (playerMask.value == 0)
        {
            int layer = LayerMask.NameToLayer("Player");
            if (layer >= 0) playerMask = 1 << layer;
        }

        // 실수 방지: 트리거 강제
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!other) return;

        if ((playerMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        _fired = true;

        if (playSfx)
            SoundManager.PlaySFX(sfxId);

        if (loadDelay <= 0f)
            SceneManager.LoadScene(endingSceneName);
        else
            Invoke(nameof(LoadEnding), loadDelay);
    }

    private void LoadEnding()
    {
        SceneManager.LoadScene(endingSceneName);
    }
}
