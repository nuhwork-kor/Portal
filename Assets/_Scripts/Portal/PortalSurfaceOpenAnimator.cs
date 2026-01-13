using System.Collections;
using UnityEngine;

public class PortalSurfaceOpenAnimator : MonoBehaviour
{
    [Header("Target Renderers")]
    [SerializeField] private Renderer portalSurfaceRenderer; // PortalSurface (Quad)
    [SerializeField] private Renderer portalOutlineRenderer; // PortalOutline

    [Header("Duration")]
    [SerializeField, Range(0.01f, 1.0f)] private float duration = 0.2f;

    [Header("PortalSurface (PortalMask ShaderGraph)")]
    [SerializeField] private string surfaceOpenProp = "_Open";     // 너 ShaderGraph 그대로
    [SerializeField] private string surfaceEllipseProp = "_Ellipse"; // 너 ShaderGraph 그대로
    [SerializeField] private float surfaceOpenFrom = 0.0f;         // 시작: 점(거의 안보임)
    [SerializeField] private float surfaceOpenTo = 0.5f;           // 끝: 너가 말한 최종값
    [SerializeField] private Vector2 surfaceEllipse = new Vector2(1f, 1f); // 최종: (1,1)

    [Header("PortalOutline (Outline ShaderGraph)")]
    [SerializeField] private string outlineEllipseScaleProp = "_EllipseScale"; // 스샷 1번 프로퍼티명
    [SerializeField] private Vector2 outlineScaleFrom = new Vector2(0.05f, 0.05f); // 시작: 거의 점
    [SerializeField] private Vector2 outlineScaleTo = new Vector2(0.97f, 0.96f); // 끝: 너 스샷 값

    // 내부
    private Material _surfaceMatInstance;
    private Material _outlineMatInstance;
    private Coroutine _co;

    private int _surfaceOpenID;
    private int _surfaceEllipseID;
    private int _outlineEllipseScaleID;

    private void Awake()
    {
        // 자동 탐색(안 꽂아도 되게)
        if (!portalSurfaceRenderer)
        {
            var t = transform.Find("PortalSurface");
            if (t) portalSurfaceRenderer = t.GetComponent<Renderer>();
        }
        if (!portalOutlineRenderer)
        {
            var t = transform.Find("PortalOutline");
            if (t) portalOutlineRenderer = t.GetComponent<Renderer>();
        }

        // 프로퍼티 ID
        _surfaceOpenID = Shader.PropertyToID(surfaceOpenProp);
        _surfaceEllipseID = Shader.PropertyToID(surfaceEllipseProp);
        _outlineEllipseScaleID = Shader.PropertyToID(outlineEllipseScaleProp);

        // material 인스턴스화(공유 머티리얼 건드리지 않게)
        if (portalSurfaceRenderer) _surfaceMatInstance = portalSurfaceRenderer.material;
        if (portalOutlineRenderer) _outlineMatInstance = portalOutlineRenderer.material;
    }

    /// <summary>
    /// 포탈 생성될 때 호출: 점 -> 타원 확장
    /// </summary>
    public void Play()
    {
        if (_co != null) StopCoroutine(_co);

        // 시작 상태로 즉시 리셋 후 재생
        ApplySurface(surfaceOpenFrom, surfaceEllipse);
        ApplyOutline(outlineScaleFrom);

        _co = StartCoroutine(CoPlay());
    }

    private IEnumerator CoPlay()
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);

            // 부드럽게(원하면 그냥 a 써도 됨)
            float s = a * a * (3f - 2f * a); // SmoothStep

            float open = Mathf.Lerp(surfaceOpenFrom, surfaceOpenTo, s);
            Vector2 outlineScale = Vector2.Lerp(outlineScaleFrom, outlineScaleTo, s);

            ApplySurface(open, surfaceEllipse);
            ApplyOutline(outlineScale);

            yield return null;
        }

        // 끝값 고정
        ApplySurface(surfaceOpenTo, surfaceEllipse);
        ApplyOutline(outlineScaleTo);

        _co = null;
    }

    private void ApplySurface(float open, Vector2 ellipse)
    {
        if (_surfaceMatInstance == null) return;

        // 애매하면 애매할수도있는데: 네 ShaderGraph에서 Reference 이름이 다르면 HasProperty가 false가 될 수 있음.
        if (_surfaceMatInstance.HasProperty(_surfaceOpenID))
            _surfaceMatInstance.SetFloat(_surfaceOpenID, open);

        if (_surfaceMatInstance.HasProperty(_surfaceEllipseID))
            _surfaceMatInstance.SetVector(_surfaceEllipseID, ellipse);
    }

    private void ApplyOutline(Vector2 ellipseScale)
    {
        if (_outlineMatInstance == null) return;

        if (_outlineMatInstance.HasProperty(_outlineEllipseScaleID))
            _outlineMatInstance.SetVector(_outlineEllipseScaleID, ellipseScale);
    }

#if UNITY_EDITOR
    [ContextMenu("TEST Play")]
    private void TestPlay() => Play();
#endif
}
