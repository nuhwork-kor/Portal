// - "Trigger Collider(isTrigger)"가 붙어있는 오브젝트에 달아두고,
//   플레이어가 들어오면 Ending 씬으로 전환한다.
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]                // 같은 GameObject에 EndingTrigger를 2개 이상 못 붙이게 방지
[RequireComponent(typeof(Collider))]       // Collider가 반드시 필요(트리거 감지용)
public class EndingTrigger : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string endingSceneName = "Ending"; // 이동할 엔딩 씬 이름(프로젝트 Build Settings에 등록되어 있어야 함)
    [SerializeField] private float loadDelay = 0f;              // 씬 로드까지 지연 시간(연출/사운드 재생용)

    [Header("Player Detect")]
    [Tooltip("기본값은 'Player' 레이어를 자동으로 사용")]
    [SerializeField] private LayerMask playerMask;              // 어떤 레이어를 '플레이어'로 취급할지

    [Header("Optional SFX")]
    [SerializeField] private bool playSfx = true;               // 트리거 시 SFX를 재생할지
    [SerializeField] private SfxId sfxId = SfxId.StageTeleport;  // 재생할 SFX ID(기본: 스테이지 텔레포트)

    private bool _fired;                                        // 중복 발동 방지 플래그(한 번만 실행)

    private void Reset()
    {
        // 인스펙터에서 스크립트 붙였을 때 자동 세팅: Collider를 트리거로 맞춰준다.
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void Awake()
    {
        // playerMask가 비어있으면 "Player" 레이어를 자동 설정
        // - 프로젝트에 "Player" 레이어가 반드시 존재해야 함(없으면 layer=-1)
        if (playerMask.value == 0)
        {
            int layer = LayerMask.NameToLayer("Player");
            if (layer >= 0) playerMask = 1 << layer;
        }

        // 안전장치: Collider가 트리거가 아니면 OnTriggerEnter가 안 올 수 있어서 강제 트리거로 설정
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;                                     // 이미 한 번 발동했으면 무시
        if (!other) return;

        // 들어온 오브젝트의 레이어가 playerMask에 포함되는지 검사
        // (playerMask에 Player만 넣었다면 Player 레이어만 통과)
        if ((playerMask.value & (1 << other.gameObject.layer)) == 0)
            return;

        _fired = true;                                          // 여기서부터는 한 번만 실행

        // 옵션: SFX 재생
        // - 지금 SoundManager.PlaySFX는 worldPos를 안 주면 2D로 재생됨(현재 구현 기준)
        if (playSfx)
            SoundManager.PlaySFX(sfxId);

        // loadDelay가 0이면 즉시 로드, 아니면 Invoke로 지연 로드
        if (loadDelay <= 0f)
            SceneManager.LoadScene(endingSceneName);
        else
            Invoke(nameof(LoadEnding), loadDelay);
    }

    private void LoadEnding()
    {
        // 지연 호출되는 씬 로드 함수
        SceneManager.LoadScene(endingSceneName);
    }
}
