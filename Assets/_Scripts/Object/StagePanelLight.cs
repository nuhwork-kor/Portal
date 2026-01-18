using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class StagePanelLight : MonoBehaviour
{
    [Header("Trigger Filter")]
    [Tooltip("이 레이어만 TriggerEnter 시 연출 시작")]
    [SerializeField] private LayerMask playerLayerMask; // Inspector에서 Player만 체크

    [Header("Target")]
    [SerializeField] private Renderer targetRenderer;   // 비우면 자식까지 찾아줌
    [SerializeField] private int materialIndex = 0;     // 여러 머티리얼이면 인덱스 지정

    [Header("Emission Colors (HDR)")]
    [ColorUsage(true, true)][SerializeField] private Color offColor = Color.black;
    [ColorUsage(true, true)][SerializeField] private Color onColor = Color.white;
    [SerializeField, Min(0f)] private float onIntensity = 2f;

    [Header("Blink")]
    [SerializeField, Min(0)] private int blinkCount = 5;
    [SerializeField, Min(0.01f)] private float blinkInterval = 0.08f;

    [Header("Options")]
    [SerializeField] private bool playOnce = true;
    [SerializeField] private bool buzzLoop = false;
    [SerializeField] private bool stopBuzzOnExit = false;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private Material _mat;
    private Coroutine _co;
    private bool _isOn;

    private void Reset()
    {
        // 트리거 필수
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;

        // 기본값: Player 레이어만(프로젝트에 "Player" 레이어가 있어야 함)
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            playerLayerMask = 1 << playerLayer;
    }

    private void Awake()
    {
        // 렌더러 자동 탐색: 자기 + 자식까지
        if (!targetRenderer)
            targetRenderer = GetComponentInChildren<Renderer>(true);

        CacheMaterial();
        ApplyEmission(offColor, 0f); // 시작은 꺼진 상태
    }

    private void CacheMaterial()
    {
        if (!targetRenderer) return;

        var mats = targetRenderer.materials;
        if (mats == null || mats.Length == 0) return;

        materialIndex = Mathf.Clamp(materialIndex, 0, mats.Length - 1);
        _mat = mats[materialIndex];

        if (_mat != null)
            _mat.EnableKeyword("_EMISSION");
    }

    private void OnTriggerEnter(Collider other)
    {
        // 트리거끼리 겹침 같은 잡음 제거(원하면 제거 가능)
        if (other.isTrigger) return;

        // Player 레이어만 통과
        if (((1 << other.gameObject.layer) & playerLayerMask.value) == 0)
            return;

        if (playOnce && _isOn) return;

        if (_mat == null) CacheMaterial();
        if (_mat == null) return;

        // SFX 동시 재생 (원샷)
        SoundManager.PlaySFX(SfxId.StagePanel_LightOn, worldPos: transform.position);

        if (buzzLoop)
            SoundManager.PlaySFX(SfxId.StagePanel_Buzz, owner: this, follow: transform, loop: true);
        else
            SoundManager.PlaySFX(SfxId.StagePanel_Buzz, worldPos: transform.position);

        // 연출 시작(중복 실행 방지)
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(BlinkThenOn());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!stopBuzzOnExit) return;

        // Player 레이어만 처리
        if (((1 << other.gameObject.layer) & playerLayerMask.value) == 0)
            return;

        if (buzzLoop)
            SoundManager.PlaySFX(SfxId.StagePanel_Buzz, owner: this, stop: true);
    }

    private IEnumerator BlinkThenOn()
    {
        _isOn = false;

        for (int i = 0; i < blinkCount; i++)
        {
            ApplyEmission(onColor, onIntensity);
            yield return new WaitForSeconds(blinkInterval);

            ApplyEmission(offColor, 0f);
            yield return new WaitForSeconds(blinkInterval);
        }

        ApplyEmission(onColor, onIntensity);
        _isOn = true;
        _co = null;
    }

    private void ApplyEmission(Color baseColor, float intensity)
    {
        if (_mat == null) return;

        Color c = baseColor * Mathf.Max(0f, intensity);
        _mat.SetColor(EmissionColorID, c);
    }
}
