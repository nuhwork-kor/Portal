using UnityEngine;

// Portal: 포탈 1개의 상태/링크/표면 렌더러/충돌체를 관리하고, 배치(Reposition) 및 표면 표시 여부를 제어하는 클래스
public class Portal : MonoBehaviour
{
    [Header("Link")]
    [SerializeField] private Portal otherPortal;                  // 서로 연결될 반대편 포탈 참조
    public Portal OtherPortal => otherPortal;                     // 외부에서 읽기 전용으로 반대편 포탈 접근

    [Header("Refs")]
    [SerializeField] private Transform plane;                     // 포탈 평면 기준 Transform(보통 PortalPlane)
    public Transform Plane => plane ? plane : transform;          // plane이 없으면 자기 자신 transform을 평면 기준으로 사용

    [SerializeField] private Renderer surfaceRenderer;            // PortalSurface의 Renderer(화면 표시용)
    public Renderer SurfaceRenderer => surfaceRenderer;           // 외부에서 표면 렌더러 접근

    [Header("Colliders")]
    [SerializeField] private BoxCollider surfaceCollider;         // 포탈 표면 콜라이더(포탈을 통과/충돌 무시 등에 사용)
    public BoxCollider SurfaceCollider => surfaceCollider;        // 외부에서 표면 콜라이더 접근

    [SerializeField] private BoxCollider triggerCollider;         // 포탈 트리거 콜라이더(PortalTrigger가 붙는 영역)
    public BoxCollider TriggerCollider => triggerCollider;        // 외부에서 트리거 콜라이더 접근

    public bool IsPlaced { get; private set; }                    // 포탈이 "배치됨(유효)" 상태인지 여부
    public Collider WallColliderCached { get; private set; }      // 포탈을 붙인 벽(표면) 콜라이더 캐시(충돌 무시 복구용)

    /// <summary>
    /// Unity Awake: 필수 참조(plane, surfaceRenderer)를 자동으로 찾아 캐싱한다.
    /// </summary>
    private void Awake()
    {
        if (!plane)                                               // plane 참조가 비어있으면
        {
            var t = transform.Find("PortalPlane");                // 자식 이름 PortalPlane을 찾고
            if (t) plane = t;                                     // 있으면 plane에 할당
        }

        if (!surfaceRenderer)                                     // surfaceRenderer 참조가 비어있으면
        {
            var t = transform.Find("PortalSurface");              // 자식 이름 PortalSurface를 찾고
            if (t) surfaceRenderer = t.GetComponent<Renderer>();  // Renderer를 가져와 할당
        }
    }

    /// <summary>
    /// 반대편 포탈을 연결한다.
    /// </summary>
    /// <param name="other">서로 연결할 반대편 포탈</param>
    public void LinkTo(Portal other) => otherPortal = other;       // otherPortal에 반대편 포탈을 저장

    /// <summary>
    /// 포탈 배치 상태를 설정하고, 콜라이더/표면 렌더러 표시 여부를 갱신한다.
    /// </summary>
    /// <param name="placed">true면 배치됨, false면 미배치(비활성 취급)</param>
    public void SetPlaced(bool placed)
    {
        IsPlaced = placed;                                        // 배치 상태 저장

        if (surfaceCollider) surfaceCollider.enabled = placed;    // 표면 콜라이더는 배치 상태에 따라 on/off
        if (triggerCollider) triggerCollider.enabled = placed;    // 트리거 콜라이더도 배치 상태에 따라 on/off

        // 포탈 화면은 "상대 포탈도 설치되어 있을 때만" 보이게
        if (surfaceRenderer)                                      // 표면 렌더러가 있으면
            surfaceRenderer.enabled = placed                      // 자기 자신이 배치되어 있고
                                     && otherPortal != null       // 반대편 포탈이 존재하며
                                     && otherPortal.IsPlaced;     // 반대편 포탈도 배치되어 있을 때만 표시

        if (!placed) WallColliderCached = null;                   // 미배치로 바뀌면 벽 콜라이더 캐시를 비움
        if (otherPortal) otherPortal.RefreshSurfaceVisibility();  // 반대편 포탈도 표면 표시 여부를 갱신(쌍 상태 동기화)
    }

    /// <summary>
    /// 포탈을 벽(표면)에 배치한다. (위치/회전 적용 + 표면 offset 적용 + 배치 상태 true 처리)
    /// </summary>
    /// <param name="wallCollider">포탈을 붙인 표면(벽/바닥) 콜라이더</param>
    /// <param name="pos">배치 기준 위치(표면 위 점)</param>
    /// <param name="rot">포탈 회전(표면 normal 기준)</param>
    /// <param name="surfaceOffset">표면에 살짝 띄우는 오프셋(깜빡임/z-fighting 방지)</param>
    public void Reposition(Collider wallCollider, Vector3 pos, Quaternion rot, float surfaceOffset)
    {
        WallColliderCached = wallCollider;                                        // 나중에 충돌 무시/복구용으로 표면 콜라이더를 저장
        transform.SetPositionAndRotation(pos + (rot * Vector3.forward) * surfaceOffset, rot); // 표면 바깥쪽으로 offset만큼 밀어서 배치

        if (!gameObject.activeSelf) gameObject.SetActive(true);                   // 비활성이면 활성화(씬에서 보이게)

        SetPlaced(true);                                                          // placed 처리 + 상대 포탈 상태에 따라 surface on/off
    }

    /// <summary>
    /// PortalManager에서 상대 포탈 상태가 바뀐 뒤, 포탈 화면(표면 렌더러) 표시 여부만 갱신한다.
    /// </summary>
    public void RefreshSurfaceVisibility()
    {
        if (surfaceRenderer)                                                      // 표면 렌더러가 있으면
            surfaceRenderer.enabled = IsPlaced                                    // 자기 자신이 배치되어 있고
                                     && otherPortal != null                       // 반대편 포탈이 존재하며
                                     && otherPortal.IsPlaced;                     // 반대편 포탈도 배치되어 있을 때만 표시
    }
}
