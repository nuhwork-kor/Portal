using System.Collections;
using UnityEngine;

// PortalSurfaceOpenAnimator: 포탈 생성 순간 ShaderGraph 프로퍼티를 애니메이션하여 "점 -> 타원 확장" 연출을 수행하는 클래스
public class PortalSurfaceOpenAnimator : MonoBehaviour
{
    [Header("Target Renderers")]
    [SerializeField] private Renderer portalSurfaceRenderer;                      // PortalSurface 렌더러(마스크 셰이더 적용)
    [SerializeField] private Renderer portalOutlineRenderer;                      // PortalOutline 렌더러(아웃라인 셰이더 적용)

    [Header("Duration")]
    [SerializeField, Range(0.01f, 1.0f)] private float duration = 0.2f;           // 애니메이션 지속 시간(초)

    [Header("PortalSurface (PortalMask ShaderGraph)")]
    [SerializeField] private string surfaceOpenProp = "_Open";                    // 표면 오픈 프로퍼티 이름
    [SerializeField] private string surfaceEllipseProp = "_Ellipse";              // 표면 타원 프로퍼티 이름
    [SerializeField] private float surfaceOpenFrom = 0.0f;                        // 시작 open 값(거의 점)
    [SerializeField] private float surfaceOpenTo = 0.5f;                          // 끝 open 값(최종)
    [SerializeField] private Vector2 surfaceEllipse = new Vector2(1f, 1f);        // 최종 타원 값

    [Header("PortalOutline (Outline ShaderGraph)")]
    [SerializeField] private string outlineEllipseScaleProp = "_EllipseScale";    // 아웃라인 타원 스케일 프로퍼티 이름
    [SerializeField] private Vector2 outlineScaleFrom = new Vector2(0.05f, 0.05f);// 시작 스케일(점)
    [SerializeField] private Vector2 outlineScaleTo = new Vector2(0.97f, 0.96f);  // 끝 스케일(최종)

    private Material _surfaceMatInstance;                                         // 표면 머티리얼 인스턴스(공유 머티리얼 보호)
    private Material _outlineMatInstance;                                         // 아웃라인 머티리얼 인스턴스(공유 머티리얼 보호)
    private Coroutine _co;                                                        // 실행 중인 코루틴 핸들

    private int _surfaceOpenID;                                                   // surfaceOpenProp의 Shader Property ID
    private int _surfaceEllipseID;                                                // surfaceEllipseProp의 Shader Property ID
    private int _outlineEllipseScaleID;                                           // outlineEllipseScaleProp의 Shader Property ID

    /// <summary>
    /// Unity Awake: 렌더러 자동 탐색, 프로퍼티 ID 캐싱, 머티리얼 인스턴스 생성.
    /// </summary>
    private void Awake()
    {
        if (!portalSurfaceRenderer)                                               // 표면 렌더러가 비어있으면
        {
            var t = transform.Find("PortalSurface");                              // 자식 PortalSurface 탐색
            if (t) portalSurfaceRenderer = t.GetComponent<Renderer>();            // Renderer 할당
        }
        if (!portalOutlineRenderer)                                               // 아웃라인 렌더러가 비어있으면
        {
            var t = transform.Find("PortalOutline");                              // 자식 PortalOutline 탐색
            if (t) portalOutlineRenderer = t.GetComponent<Renderer>();            // Renderer 할당
        }

        _surfaceOpenID = Shader.PropertyToID(surfaceOpenProp);                    // Open 프로퍼티 ID 변환
        _surfaceEllipseID = Shader.PropertyToID(surfaceEllipseProp);              // Ellipse 프로퍼티 ID 변환
        _outlineEllipseScaleID = Shader.PropertyToID(outlineEllipseScaleProp);    // Outline EllipseScale ID 변환

        if (portalSurfaceRenderer) _surfaceMatInstance = portalSurfaceRenderer.material; // 표면 머티리얼 인스턴스 생성
        if (portalOutlineRenderer) _outlineMatInstance = portalOutlineRenderer.material; // 아웃라인 머티리얼 인스턴스 생성
    }

    /// <summary>
    /// 포탈 생성 시 호출: 점 상태로 리셋 후, 타원 확장 애니메이션을 재생한다.
    /// </summary>
    public void Play()
    {
        if (_co != null) StopCoroutine(_co);                                      // 기존 코루틴이 있으면 중지

        ApplySurface(surfaceOpenFrom, surfaceEllipse);                            // 표면 시작 상태 적용
        ApplyOutline(outlineScaleFrom);                                           // 아웃라인 시작 상태 적용

        _co = StartCoroutine(CoPlay());                                           // 애니메이션 코루틴 시작
    }

    /// <summary>
    /// duration 동안 SmoothStep으로 open/outlineScale을 보간하여 적용한다.
    /// </summary>
    /// <returns>IEnumerator</returns>
    private IEnumerator CoPlay()
    {
        float t = 0f;                                                             // 경과 시간

        while (t < duration)                                                      // duration 동안 반복
        {
            t += Time.deltaTime;                                                  // 프레임 delta 누적
            float a = Mathf.Clamp01(t / duration);                                // 0~1 정규화

            float s = a * a * (3f - 2f * a);                                      // SmoothStep 보간 계수

            float open = Mathf.Lerp(surfaceOpenFrom, surfaceOpenTo, s);           // open 보간
            Vector2 outlineScale = Vector2.Lerp(outlineScaleFrom, outlineScaleTo, s); // outlineScale 보간

            ApplySurface(open, surfaceEllipse);                                   // 표면 프로퍼티 적용
            ApplyOutline(outlineScale);                                           // 아웃라인 프로퍼티 적용

            yield return null;                                                    // 다음 프레임 대기
        }

        ApplySurface(surfaceOpenTo, surfaceEllipse);                              // 끝값 고정
        ApplyOutline(outlineScaleTo);                                             // 끝값 고정

        _co = null;                                                               // 코루틴 핸들 해제
    }

    /// <summary>
    /// 표면 머티리얼에 open/ellipse 프로퍼티를 설정한다.
    /// </summary>
    /// <param name="open">Open 값</param>
    /// <param name="ellipse">Ellipse 값</param>
    private void ApplySurface(float open, Vector2 ellipse)
    {
        if (_surfaceMatInstance == null) return;                                  // 머티리얼이 없으면 중단

        // 애매하면 애매할수도있는데: ShaderGraph Reference 이름이 다르면 HasProperty가 false가 될 수 있음.
        if (_surfaceMatInstance.HasProperty(_surfaceOpenID))                      // Open 프로퍼티가 존재하면
            _surfaceMatInstance.SetFloat(_surfaceOpenID, open);                   // Open 설정

        if (_surfaceMatInstance.HasProperty(_surfaceEllipseID))                   // Ellipse 프로퍼티가 존재하면
            _surfaceMatInstance.SetVector(_surfaceEllipseID, ellipse);            // Ellipse 설정
    }

    /// <summary>
    /// 아웃라인 머티리얼에 ellipseScale 프로퍼티를 설정한다.
    /// </summary>
    /// <param name="ellipseScale">타원 스케일 값</param>
    private void ApplyOutline(Vector2 ellipseScale)
    {
        if (_outlineMatInstance == null) return;                                  // 머티리얼이 없으면 중단

        if (_outlineMatInstance.HasProperty(_outlineEllipseScaleID))              // 프로퍼티가 존재하면
            _outlineMatInstance.SetVector(_outlineEllipseScaleID, ellipseScale);  // ellipseScale 설정
    }

#if UNITY_EDITOR
    [ContextMenu("TEST Play")]
    private void TestPlay() => Play();                                            // 인스펙터에서 테스트 재생용
#endif
}
