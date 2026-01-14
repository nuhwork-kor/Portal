using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PortalRenderCamera : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Camera portalCamera;
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("Recursion")]
    [SerializeField, Range(0, 10)] private int iterations = 3;

    [Header("Oblique Near Clip")]
    [SerializeField] private bool useObliqueClip = true;
    [SerializeField] private float clipPlaneOffset = 0.02f;

    [Header("PortalCamera Culling")]
    [SerializeField] private LayerMask portalCameraCullingMask = ~0;

    [Header("Optional tiny push (usually 0)")]
    [SerializeField] private float outPortalCamPush = 0.0f;

    private RenderTexture blueRT;
    private RenderTexture orangeRT;

    private UniversalRenderPipeline.SingleCameraRequest request;

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

        if (blueRT == null || blueRT.width != Screen.width || blueRT.height != Screen.height)
        {
            CreateOrResizeRTs(Screen.width, Screen.height);
            AssignRTsToSurfaces();
        }

        bluePortal.RefreshSurfaceVisibility();
        orangePortal.RefreshSurfaceVisibility();

        portalCamera.fieldOfView = playerCamera.fieldOfView;
        portalCamera.aspect = playerCamera.aspect;
        portalCamera.farClipPlane = playerCamera.farClipPlane;
        portalCamera.nearClipPlane = Mathf.Max(0.01f, playerCamera.nearClipPlane);

        if (bluePortal.SurfaceRenderer && bluePortal.SurfaceRenderer.isVisible)
            RenderPortal(context, inPortal: bluePortal, outPortal: orangePortal, target: blueRT);

        if (orangePortal.SurfaceRenderer && orangePortal.SurfaceRenderer.isVisible)
            RenderPortal(context, inPortal: orangePortal, outPortal: bluePortal, target: orangeRT);
    }

    private void RenderPortal(ScriptableRenderContext context, Portal inPortal, Portal outPortal, RenderTexture target)
    {
        bool prevInSurface = inPortal.SurfaceRenderer && inPortal.SurfaceRenderer.enabled;
        if (inPortal.SurfaceRenderer) inPortal.SurfaceRenderer.enabled = false;

        portalCamera.targetTexture = target;
        request.destination = target;

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
            camT.position = PortalMath.TransformPoint(camT.position, inT, outT);
            camT.rotation = PortalMath.TransformRotation(camT.rotation, inT, outT);
        }

        if (outPortalCamPush != 0f)
            camT.position += outT.forward * outPortalCamPush;
    }

    private void ApplyObliqueClipPlane(Portal outPortal, float offset)
    {
        Transform t = outPortal.Plane;

        Vector3 normal = t.forward;
        if (Vector3.Dot(normal, portalCamera.transform.position - t.position) > 0f)
            normal = -normal;

        float camDist = Mathf.Abs(Vector3.Dot(normal, portalCamera.transform.position - t.position));
        float near = portalCamera.nearClipPlane;

        float maxSafeOffset = Mathf.Max(0f, camDist - near * 0.5f);
        float safeOffset = Mathf.Min(offset, maxSafeOffset);

        Vector3 pos = t.position + normal * safeOffset;

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
