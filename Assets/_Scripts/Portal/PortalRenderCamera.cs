using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// PortalRenderCamera: URP beginCameraRendering 훅에서 포탈 카메라를 렌더하여 RenderTexture를 포탈 표면에 출력하는 클래스
public class PortalRenderCamera : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera playerCamera;                                 // 플레이어 메인 카메라
    [SerializeField] private Camera portalCamera;                                 // 포탈 렌더 전용 카메라(보통 비활성)
    [SerializeField] private Portal bluePortal;                                   // 블루 포탈
    [SerializeField] private Portal orangePortal;                                 // 오렌지 포탈

    [Header("Recursion")]
    [SerializeField, Range(0, 10)] private int iterations = 3;                    // 재귀 렌더 횟수(깊이)

    [Header("Oblique Near Clip")]
    [SerializeField] private bool useObliqueClip = true;                          // Oblique near clip plane 사용 여부
    [SerializeField] private float clipPlaneOffset = 0.02f;                       // 클립 평면을 살짝 밀어 Z-fighting/깜빡임 방지

    [Header("PortalCamera Culling")]
    [SerializeField] private LayerMask portalCameraCullingMask = ~0;              // 포탈 카메라 컬링 마스크

    [Header("Optional tiny push (usually 0)")]
    [SerializeField] private float outPortalCamPush = 0.0f;                       // 출구 포탈 방향으로 카메라를 미세하게 밀어주는 값(대부분 0)

    private RenderTexture blueRT;                                                  // 블루 포탈 렌더 텍스처
    private RenderTexture orangeRT;                                                // 오렌지 포탈 렌더 텍스처

    private UniversalRenderPipeline.SingleCameraRequest request;                   // URP 단일 카메라 렌더 요청 구조체

    /// <summary>
    /// Unity Awake: RT 생성/할당, 포탈 카메라 기본 설정, 렌더 요청 구조체 초기화.
    /// </summary>
    private void Awake()
    {
        if (!playerCamera) playerCamera = GetComponent<Camera>();                 // playerCamera가 없으면 현재 오브젝트의 Camera 사용

        CreateOrResizeRTs(Screen.width, Screen.height);                           // 화면 크기에 맞춰 RT 생성
        AssignRTsToSurfaces();                                                    // RT를 포탈 표면 머티리얼에 할당

        if (portalCamera)                                                         // portalCamera가 있으면
        {
            portalCamera.enabled = false;                                         // 직접 렌더 호출로만 사용(자동 렌더 방지)
            portalCamera.cullingMask = portalCameraCullingMask;                   // 컬링 마스크 설정
        }

        request = new UniversalRenderPipeline.SingleCameraRequest();              // URP 렌더 요청 객체 생성
    }

    /// <summary>
    /// Unity OnEnable: URP beginCameraRendering 이벤트 구독.
    /// </summary>
    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;     // 카메라 렌더 시작 훅 등록
    }

    /// <summary>
    /// Unity OnDisable: URP beginCameraRendering 이벤트 구독 해제.
    /// </summary>
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;     // 훅 해제
    }

    /// <summary>
    /// Unity OnDestroy: 생성한 RenderTexture 자원 해제.
    /// </summary>
    private void OnDestroy()
    {
        ReleaseRT(ref blueRT);                                                    // 블루 RT 해제
        ReleaseRT(ref orangeRT);                                                  // 오렌지 RT 해제
    }

    /// <summary>
    /// URP beginCameraRendering 콜백: 플레이어 카메라 렌더 타이밍에 포탈 렌더를 수행한다.
    /// </summary>
    /// <param name="context">스크립터블 렌더 컨텍스트</param>
    /// <param name="cam">현재 렌더 중인 카메라</param>
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam != playerCamera) return;                                          // 플레이어 카메라 렌더 시점만 처리

        if (!bluePortal || !orangePortal || !portalCamera) return;                // 필수 참조가 없으면 중단
        if (!bluePortal.IsPlaced || !orangePortal.IsPlaced) return;               // 두 포탈이 모두 배치되어야 렌더

        if (blueRT == null || blueRT.width != Screen.width || blueRT.height != Screen.height) // 해상도 변경 등으로 RT가 불일치하면
        {
            CreateOrResizeRTs(Screen.width, Screen.height);                       // 새 RT 생성
            AssignRTsToSurfaces();                                                // 표면에 재할당
        }

        bluePortal.RefreshSurfaceVisibility();                                    // 표면 표시 상태 갱신
        orangePortal.RefreshSurfaceVisibility();                                  // 표면 표시 상태 갱신

        portalCamera.fieldOfView = playerCamera.fieldOfView;                      // FOV 동기화
        portalCamera.aspect = playerCamera.aspect;                                // 화면 비율 동기화
        portalCamera.farClipPlane = playerCamera.farClipPlane;                    // far 동기화
        portalCamera.nearClipPlane = Mathf.Max(0.01f, playerCamera.nearClipPlane);// near 동기화(최소값 보장)

        if (bluePortal.SurfaceRenderer && bluePortal.SurfaceRenderer.isVisible)   // 블루 포탈 표면이 카메라에 보일 때만 렌더
            RenderPortal(context, inPortal: bluePortal, outPortal: orangePortal, target: blueRT); // 블루->오렌지 방향 렌더

        if (orangePortal.SurfaceRenderer && orangePortal.SurfaceRenderer.isVisible) // 오렌지 표면이 보일 때만 렌더
            RenderPortal(context, inPortal: orangePortal, outPortal: bluePortal, target: orangeRT); // 오렌지->블루 방향 렌더
    }

    /// <summary>
    /// 한 방향(inPortal -> outPortal)의 포탈 화면을 target RenderTexture로 렌더링한다.
    /// </summary>
    /// <param name="context">렌더 컨텍스트</param>
    /// <param name="inPortal">입구 포탈</param>
    /// <param name="outPortal">출구 포탈</param>
    /// <param name="target">렌더 결과를 쓸 RenderTexture</param>
    private void RenderPortal(ScriptableRenderContext context, Portal inPortal, Portal outPortal, RenderTexture target)
    {
        bool prevInSurface = inPortal.SurfaceRenderer && inPortal.SurfaceRenderer.enabled; // 원래 표면 enabled 상태 저장
        if (inPortal.SurfaceRenderer) inPortal.SurfaceRenderer.enabled = false;   // 자기 표면을 끄고 렌더(자기 자신을 보는 재귀/피드백 방지)

        portalCamera.targetTexture = target;                                      // 포탈 카메라 출력 타겟 지정
        request.destination = target;                                             // URP 요청 대상 지정

        for (int i = iterations - 1; i >= 0; --i)                                 // 깊은 재귀부터 역순으로 렌더
        {
            SetPortalCameraTransform(inPortal, outPortal, i);                     // i 단계 재귀 변환으로 포탈 카메라 포즈 설정

            portalCamera.ResetProjectionMatrix();                                 // 프로젝션 초기화(이전 oblique 영향 제거)
            if (useObliqueClip)                                                   // oblique 클립 사용이면
                ApplyObliqueClipPlane(outPortal, clipPlaneOffset);                // 출구 포탈 평면 기준으로 near clip 조정

            if (RenderPipeline.SupportsRenderRequest(portalCamera, request))      // URP 렌더 요청 지원 여부 확인
                RenderPipeline.SubmitRenderRequest(portalCamera, request);        // 단일 카메라 렌더 요청 제출
        }

        portalCamera.targetTexture = null;                                        // 타겟 해제
        if (inPortal.SurfaceRenderer) inPortal.SurfaceRenderer.enabled = prevInSurface; // 표면 enabled 상태 복구
    }

    /// <summary>
    /// 재귀 렌더 단계(iteration)에 맞춰 포탈 카메라의 위치/회전을 변환한다.
    /// </summary>
    /// <param name="inPortal">입구 포탈</param>
    /// <param name="outPortal">출구 포탈</param>
    /// <param name="iteration">재귀 단계</param>
    private void SetPortalCameraTransform(Portal inPortal, Portal outPortal, int iteration)
    {
        Transform inT = inPortal.Plane;                                           // 입구 포탈 평면
        Transform outT = outPortal.Plane;                                         // 출구 포탈 평면

        Transform camT = portalCamera.transform;                                  // 포탈 카메라 Transform
        camT.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation); // 시작은 플레이어 카메라 포즈

        for (int i = 0; i <= iteration; ++i)                                      // iteration 단계만큼 연속 변환
        {
            camT.position = PortalMath.TransformPoint(camT.position, inT, outT);  // 점 변환(포지션)
            camT.rotation = PortalMath.TransformRotation(camT.rotation, inT, outT); // 회전 변환
        }

        if (outPortalCamPush != 0f)                                               // 필요하면
            camT.position += outT.forward * outPortalCamPush;                     // 출구 포탈 방향으로 미세 push
    }

    /// <summary>
    /// Oblique clip plane을 설정하여 포탈 평면에 의해 화면이 "잘리도록" 투영행렬을 수정한다.
    /// </summary>
    /// <param name="outPortal">출구 포탈</param>
    /// <param name="offset">클립 평면 오프셋</param>
    private void ApplyObliqueClipPlane(Portal outPortal, float offset)
    {
        Transform t = outPortal.Plane;                                            // 출구 포탈 평면 Transform

        Vector3 normal = t.forward;                                               // 평면 노멀
        if (Vector3.Dot(normal, portalCamera.transform.position - t.position) > 0f) // 카메라가 노멀 앞쪽에 있으면
            normal = -normal;                                                     // 노멀을 반대로(카메라를 향하게)

        float camDist = Mathf.Abs(Vector3.Dot(normal, portalCamera.transform.position - t.position)); // 카메라-평면 거리
        float near = portalCamera.nearClipPlane;                                  // near clip

        float maxSafeOffset = Mathf.Max(0f, camDist - near * 0.5f);               // near를 뚫지 않도록 오프셋 상한 계산
        float safeOffset = Mathf.Min(offset, maxSafeOffset);                      // 안전 오프셋 적용

        Vector3 pos = t.position + normal * safeOffset;                           // 최종 클립 평면 위치

        Vector4 clipPlaneCameraSpace = CameraSpacePlane(portalCamera, pos, normal);// 카메라 공간 클립 평면 계산
        portalCamera.projectionMatrix = portalCamera.CalculateObliqueMatrix(clipPlaneCameraSpace); // oblique 투영행렬 적용
    }

    /// <summary>
    /// 월드 평면(pos, normal)을 카메라 공간으로 변환해 oblique clip에 사용할 Vector4를 만든다.
    /// </summary>
    /// <param name="cam">대상 카메라</param>
    /// <param name="pos">월드 평면 위 한 점</param>
    /// <param name="normal">월드 평면 노멀</param>
    /// <returns>카메라 공간 평면(Vector4)</returns>
    private static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal)
    {
        Matrix4x4 m = cam.worldToCameraMatrix;                                    // 월드->카메라 변환 행렬
        Vector3 cpos = m.MultiplyPoint(pos);                                      // 점을 카메라 공간으로 변환
        Vector3 cnormal = m.MultiplyVector(normal).normalized;                    // 노멀을 카메라 공간으로 변환 후 정규화
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal)); // 평면 방정식 계수
    }

    /// <summary>
    /// 생성된 RenderTexture들을 각 포탈 표면 머티리얼의 mainTexture에 할당한다.
    /// </summary>
    private void AssignRTsToSurfaces()
    {
        if (bluePortal && bluePortal.SurfaceRenderer)                             // 블루 표면 렌더러가 있으면
            bluePortal.SurfaceRenderer.material.mainTexture = blueRT;             // 블루 RT 할당

        if (orangePortal && orangePortal.SurfaceRenderer)                         // 오렌지 표면 렌더러가 있으면
            orangePortal.SurfaceRenderer.material.mainTexture = orangeRT;         // 오렌지 RT 할당
    }

    /// <summary>
    /// 화면 크기에 맞춰 RenderTexture를 생성하거나 재생성한다.
    /// </summary>
    /// <param name="w">너비</param>
    /// <param name="h">높이</param>
    private void CreateOrResizeRTs(int w, int h)
    {
        ReleaseRT(ref blueRT);                                                    // 기존 블루 RT 해제
        ReleaseRT(ref orangeRT);                                                  // 기존 오렌지 RT 해제

        blueRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "PortalRT_Blue" };     // 블루 RT 생성
        orangeRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "PortalRT_Orange" }; // 오렌지 RT 생성
    }

    /// <summary>
    /// RenderTexture를 안전하게 Release/Destroy하고 null로 만든다.
    /// </summary>
    /// <param name="rt">해제할 RenderTexture 참조</param>
    private void ReleaseRT(ref RenderTexture rt)
    {
        if (!rt) return;                                                          // null이면 중단
        rt.Release();                                                             // GPU 리소스 해제
        Destroy(rt);                                                              // 오브젝트 파괴
        rt = null;                                                                // 참조 해제
    }
}
