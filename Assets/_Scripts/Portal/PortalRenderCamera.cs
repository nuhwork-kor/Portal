using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PortalRenderCamera : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera playerCamera;   // 보통 이 스크립트가 붙은 카메라
    [SerializeField] private Camera portalCamera;   // 렌더 전용 카메라(Enabled 꺼둘 것)
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("Recursion")]
    [SerializeField, Range(0, 10)] private int iterations = 3;

    [Header("Oblique Near Clip")]
    [SerializeField] private bool useObliqueClip = true;
    [SerializeField] private float clipPlaneOffset = 0.02f;

    [Header("PortalCamera Culling")]
    [Tooltip("PortalCamera가 렌더할 레이어. PortalSurface/PortalTrigger/Outline 등은 제외 추천.")]
    [SerializeField] private LayerMask portalCameraCullingMask = ~0;

    [Header("Optional tiny push (usually 0)")]
    [Tooltip("오블리크가 제대로면 0으로 두는게 정석. (땜빵용)")]
    [SerializeField] private float outPortalCamPush = 0.0f;

    private RenderTexture blueRT;
    private RenderTexture orangeRT;

    private UniversalRenderPipeline.SingleCameraRequest request;

    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private void Awake()
    {
        if (!playerCamera) playerCamera = GetComponent<Camera>();

        CreateOrResizeRTs(Screen.width, Screen.height);
        AssignRTsToSurfaces();

        if (portalCamera)
        {
            portalCamera.enabled = false;
            portalCamera.cullingMask = portalCameraCullingMask;
        }

        request = new UniversalRenderPipeline.SingleCameraRequest();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnDestroy()
    {
        ReleaseRT(ref blueRT);
        ReleaseRT(ref orangeRT);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam != playerCamera) return;

        if (!bluePortal || !orangePortal || !portalCamera) return;
        if (!bluePortal.IsPlaced || !orangePortal.IsPlaced) return;

        // RT 리사이즈
        if (blueRT == null || blueRT.width != Screen.width || blueRT.height != Screen.height)
        {
            CreateOrResizeRTs(Screen.width, Screen.height);
            AssignRTsToSurfaces();
        }

        // Portal 표면 표시 여부 갱신 (네 Portal.cs에 있는 함수 기준)
        bluePortal.RefreshSurfaceVisibility();
        orangePortal.RefreshSurfaceVisibility();

        // 플레이어 카메라 설정 일부 동기화 (FOV/Aspect 안 맞으면 어색해짐)
        portalCamera.fieldOfView = playerCamera.fieldOfView;
        portalCamera.aspect = playerCamera.aspect;
        portalCamera.farClipPlane = playerCamera.farClipPlane;
        portalCamera.nearClipPlane = Mathf.Max(0.01f, playerCamera.nearClipPlane); // oblique 쓰더라도 너무 크면 불리

        if (bluePortal.SurfaceRenderer && bluePortal.SurfaceRenderer.isVisible)
            RenderPortal(context, inPortal: bluePortal, outPortal: orangePortal, target: blueRT);

        if (orangePortal.SurfaceRenderer && orangePortal.SurfaceRenderer.isVisible)
            RenderPortal(context, inPortal: orangePortal, outPortal: bluePortal, target: orangeRT);
    }

    private void RenderPortal(ScriptableRenderContext context, Portal inPortal, Portal outPortal, RenderTexture target)
    {
        // inPortal 표면은 끄고 렌더(피드백 루프 방지)
        bool prevInSurface = inPortal.SurfaceRenderer && inPortal.SurfaceRenderer.enabled;
        if (inPortal.SurfaceRenderer) inPortal.SurfaceRenderer.enabled = false;

        portalCamera.targetTexture = target;
        request.destination = target;

        // 깊은 것부터 렌더
        for (int i = iterations - 1; i >= 0; --i)
        {
            SetPortalCameraTransform(inPortal, outPortal, i);

            portalCamera.ResetProjectionMatrix();
            if (useObliqueClip)
                ApplyObliqueClipPlane(outPortal, clipPlaneOffset);

            if (RenderPipeline.SupportsRenderRequest(portalCamera, request))
                RenderPipeline.SubmitRenderRequest(portalCamera, request);
        }

        portalCamera.targetTexture = null;
        if (inPortal.SurfaceRenderer) inPortal.SurfaceRenderer.enabled = prevInSurface;
    }

    private void SetPortalCameraTransform(Portal inPortal, Portal outPortal, int iteration)
    {
        Transform inT = inPortal.Plane;
        Transform outT = outPortal.Plane;

        Transform camT = portalCamera.transform;
        camT.SetPositionAndRotation(playerCamera.transform.position, playerCamera.transform.rotation);

        for (int i = 0; i <= iteration; ++i)
        {
            Vector3 relativePos = inT.InverseTransformPoint(camT.position);
            relativePos = HalfTurn * relativePos;
            camT.position = outT.TransformPoint(relativePos);

            Quaternion relativeRot = Quaternion.Inverse(inT.rotation) * camT.rotation;
            relativeRot = HalfTurn * relativeRot;
            camT.rotation = outT.rotation * relativeRot;
        }

        if (outPortalCamPush != 0f)
            camT.position += outT.forward * outPortalCamPush;
    }

    private void ApplyObliqueClipPlane(Portal outPortal, float offset)
    {
        Transform t = outPortal.Plane;

        // ★핵심: normal이 "항상 portalCamera 쪽을 향하도록" 강제
        Vector3 normal = t.forward;
        if (Vector3.Dot(normal, portalCamera.transform.position - t.position) > 0f)
            normal = -normal;

        // offset도 카메라쪽으로 살짝 당겨서(z-fighting/경계 깜빡임 완화)
        Vector3 pos = t.position + normal * offset;

        Vector4 clipPlaneCameraSpace = CameraSpacePlane(portalCamera, pos, normal);
        portalCamera.projectionMatrix = portalCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
    }

    private static Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal)
    {
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(pos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized;
        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    private void AssignRTsToSurfaces()
    {
        if (bluePortal && bluePortal.SurfaceRenderer)
            bluePortal.SurfaceRenderer.material.mainTexture = blueRT;

        if (orangePortal && orangePortal.SurfaceRenderer)
            orangePortal.SurfaceRenderer.material.mainTexture = orangeRT;
    }

    private void CreateOrResizeRTs(int w, int h)
    {
        ReleaseRT(ref blueRT);
        ReleaseRT(ref orangeRT);

        blueRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "PortalRT_Blue" };
        orangeRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "PortalRT_Orange" };
    }

    private void ReleaseRT(ref RenderTexture rt)
    {
        if (!rt) return;
        rt.Release();
        Destroy(rt);
        rt = null;
    }
}
