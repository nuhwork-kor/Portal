using System.Collections;
using UnityEngine;

// 이 스크립트는 "스테이지 패널의 Emission(발광)을 트리거로 켜고, 깜빡인 다음 On 상태로 유지"한다.
// - Player 레이어만 트리거로 인정(playerLayerMask)
// - targetRenderer/materialIndex로 특정 머티리얼의 _EmissionColor를 제어
// - blinkCount/interval만큼 깜빡인 뒤 최종 OnColor*Intensity로 고정
// - 옵션: playOnce(한 번만), buzzLoop(버즈음을 루프로), stopBuzzOnExit(나가면 루프 종료)
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class StagePanelLight : MonoBehaviour
{
    [Header("Trigger Filter")]
    [Tooltip("이 레이어만 TriggerEnter 시 반응하도록")]
    [SerializeField] private LayerMask playerLayerMask;                                 // 플레이어 레이어 마스크(인스펙터에서 Player 체크)

    [Header("Target")]
    [SerializeField] private Renderer targetRenderer;                                   // 발광을 적용할 렌더러(자식에서 자동 탐색 가능)
    [SerializeField] private int materialIndex = 0;                                     // 렌더러 materials 배열에서 사용할 인덱스

    [Header("Emission Colors (HDR)")]
    [ColorUsage(true, true)][SerializeField] private Color offColor = Color.black;      // Off 상태 발광 컬러(HDR)
    [ColorUsage(true, true)][SerializeField] private Color onColor = Color.white;       // On 상태 발광 컬러(HDR)
    [SerializeField, Min(0f)] private float onIntensity = 2f;                            // On 상태 발광 강도 배수

    [Header("Blink")]
    [SerializeField, Min(0)] private int blinkCount = 5;                                 // 깜빡이는 횟수
    [SerializeField, Min(0.01f)] private float blinkInterval = 0.08f;                    // 깜빡 간격(초)

    [Header("Options")]
    [SerializeField] private bool playOnce = true;                                       // true면 한 번 켜지면 다시 트리거해도 무시
    [SerializeField] private bool buzzLoop = false;                                      // true면 버즈음을 루프로 재생
    [SerializeField] private bool stopBuzzOnExit = false;                                // true면 트리거 Exit에서 버즈음을 정지(루프일 때 의미 있음)

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor"); // _EmissionColor 프로퍼티 ID 캐시

    private Material _mat;                                                               // 제어할 머티리얼 인스턴스
    private Coroutine _co;                                                               // 깜빡 코루틴 핸들
    private bool _isOn;                                                                  // 최종 On 상태 여부

    /// <summary>
    /// 유니티 에디터 편의: 콜라이더를 트리거로 설정하고 Player 레이어 마스크 기본값을 세팅한다.
    /// </summary>
    private void Reset()
    {
        var c = GetComponent<Collider>();                                                // 콜라이더 참조
        if (c) c.isTrigger = true;                                                       // 트리거로 설정

        int playerLayer = LayerMask.NameToLayer("Player");                              // "Player" 레이어 번호 탐색
        if (playerLayer >= 0)
            playerLayerMask = 1 << playerLayer;                                         // Player 레이어만 통과하도록 기본값 세팅
    }

    /// <summary>
    /// 유니티 생명주기: 렌더러/머티리얼 캐시 후 Off 상태로 초기화한다.
    /// </summary>
    private void Awake()
    {
        if (!targetRenderer)
            targetRenderer = GetComponentInChildren<Renderer>(true);                    // 자식 포함 렌더러 자동 탐색

        CacheMaterial();                                                                // 머티리얼 캐시
        ApplyEmission(offColor, 0f);                                                    // 시작은 Off 상태
    }

    /// <summary>
    /// targetRenderer/materialIndex 기반으로 제어할 머티리얼을 캐시하고 Emission 키워드를 켠다.
    /// </summary>
    private void CacheMaterial()
    {
        if (!targetRenderer) return;                                                    // 렌더러 없으면 종료

        var mats = targetRenderer.materials;                                            // materials 배열(인스턴스 생성될 수 있음)
        if (mats == null || mats.Length == 0) return;                                   // 비어있으면 종료

        materialIndex = Mathf.Clamp(materialIndex, 0, mats.Length - 1);                 // 인덱스 범위 보정
        _mat = mats[materialIndex];                                                     // 사용할 머티리얼 저장

        if (_mat != null)
            _mat.EnableKeyword("_EMISSION");                                            // Emission 키워드 활성화
    }

    /// <summary>
    /// 트리거 진입: Player만 인정하며, 깜빡임과 사운드를 재생한다.
    /// </summary>
    /// <param name="other">진입한 콜라이더</param>
    private void OnTriggerEnter(Collider other)
    {
        if (other.isTrigger) return;                                                    // 트리거끼리는 무시(원치 않으면 제거 가능)

        if (((1 << other.gameObject.layer) & playerLayerMask.value) == 0)               // Player 레이어가 아니면
            return;                                                                     // 무시

        if (playOnce && _isOn) return;                                                  // 한 번만 옵션 + 이미 켜졌으면 무시

        if (_mat == null) CacheMaterial();                                              // 머티리얼이 없으면 재캐시
        if (_mat == null) return;                                                       // 그래도 없으면 종료

        SoundManager.PlaySFX(SfxId.StagePanel_LightOn, worldPos: transform.position);   // 라이트 온 SFX(3D)

        if (buzzLoop)
            SoundManager.PlaySFX(SfxId.StagePanel_Buzz, owner: this, follow: transform, loop: true); // 버즈음 루프(팔로우)
        else
            SoundManager.PlaySFX(SfxId.StagePanel_Buzz, worldPos: transform.position);  // 버즈음 원샷(3D)

        if (_co != null) StopCoroutine(_co);                                            // 기존 코루틴 중지
        _co = StartCoroutine(BlinkThenOn());                                            // 깜빡인 뒤 On으로 고정
    }

    /// <summary>
    /// 트리거 이탈: 옵션에 따라 루프 버즈음을 정지한다.
    /// </summary>
    /// <param name="other">이탈한 콜라이더</param>
    private void OnTriggerExit(Collider other)
    {
        if (!stopBuzzOnExit) return;                                                    // 옵션이 꺼져있으면 종료

        if (((1 << other.gameObject.layer) & playerLayerMask.value) == 0)               // Player 레이어가 아니면
            return;                                                                     // 무시

        if (buzzLoop)
            SoundManager.PlaySFX(SfxId.StagePanel_Buzz, owner: this, stop: true);       // 루프 SFX 정지(Owner 기준)
    }

    /// <summary>
    /// blinkCount만큼 On/Off를 반복한 뒤 최종적으로 On 상태로 만든다.
    /// </summary>
    /// <returns>IEnumerator(코루틴)</returns>
    private IEnumerator BlinkThenOn()
    {
        _isOn = false;                                                                  // 깜빡 중에는 최종 On 아님

        for (int i = 0; i < blinkCount; i++)                                            // 지정 횟수만큼 반복
        {
            ApplyEmission(onColor, onIntensity);                                        // On 발광 적용
            yield return new WaitForSeconds(blinkInterval);                             // 대기

            ApplyEmission(offColor, 0f);                                                // Off 발광 적용
            yield return new WaitForSeconds(blinkInterval);                             // 대기
        }

        ApplyEmission(onColor, onIntensity);                                            // 최종 On 발광 적용
        _isOn = true;                                                                   // On 상태로 확정
        _co = null;                                                                     // 코루틴 핸들 해제
    }

    /// <summary>
    /// _EmissionColor에 baseColor * intensity 값을 적용한다.
    /// </summary>
    /// <param name="baseColor">기본 컬러(HDR 가능)</param>
    /// <param name="intensity">강도 배수</param>
    private void ApplyEmission(Color baseColor, float intensity)
    {
        if (_mat == null) return;                                                       // 머티리얼 없으면 종료

        Color c = baseColor * Mathf.Max(0f, intensity);                                 // 강도 적용(음수 방지)
        _mat.SetColor(EmissionColorID, c);                                              // EmissionColor 세팅
    }
}
