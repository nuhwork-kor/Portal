using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PortalRenderCamera : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera portalCamera;
    [SerializeField] private Camera playerCamera;

    [Header("Recursion")]
    [SerializeField, Range(0, 10)] private int iterations = 2;

    [Header("Oblique Near Clip")]
    [SerializeField] private bool useObliqueClip = true;
    [SerializeField] private float clipPlaneOffset = 0.002f;

    [Header("Render Mask Fix (IMPORTANT)")]
    [Tooltip("PortalCamera가 절대 렌더하면 안 되는 레이어(PortalScreen/PortalMask)를 제외한 마스크를 여기에 지정.")]
    [SerializeField] private LayerMask portalCameraCullingMask = ~0;

    // URP 렌더 요청(새 API)
    private UniversalRenderPipeline.SingleCameraRequest request;

    private void Awake()
    {
        if (!portalCamera) portalCamera = GetComponent<Camera>();
        if (!playerCamera) playerCamera = Camera.main;

        portalCamera.enabled = false; // 자동 렌더 금지
        request = new UniversalRenderPipeline.SingleCameraRequest();
    }
    public void SetPlayerCamera(Camera cam)
    {
        if (cam) playerCamera = cam;
        else if (!playerCamera) playerCamera = Camera.main;
    }

    public void RenderPortal(Portal inPortal, Portal outPortal, RenderTexture targetRT)
    {
        if (!playerCamera || !portalCamera) return;
        if (!inPortal || !outPortal) return;
        if (!targetRT) return;

        // playerCamera 기준 값 복사(FOV, clip, post, culling 등)
        portalCamera.CopyFrom(playerCamera);
        //RT비율 강제로 맞추기
        portalCamera.targetTexture = targetRT;
        portalCamera.aspect = (float)targetRT.width / targetRT.height;
        portalCamera.cullingMask = portalCameraCullingMask;
        portalCamera.ResetProjectionMatrix();

        // 중요: PortalScreen 레이어는 PortalCamera에서 제외(피드백 방지)
        // -> 이건 인스펙터에서 portalCamera.cullingMask로 처리
        // 렌더 요청 대상
        request.destination = targetRT;

        // 지원 여부 체크(안 하면 특정 파이프라인/카메라 조합에서 실패 가능)
        if (!RenderPipeline.SupportsRenderRequest(portalCamera, request))
            return;

        int maxIter = Mathf.Max(1, iterations);

        // 재귀: 깊은 단계부터 그려서 얕은 단계에 반영
        for (int i = iterations; i >= 1; --i)
        {
            if (!TryBuildPose(inPortal, outPortal, i, out Vector3 pos, out Quaternion rot, out Transform clipPlane))
                continue;

            portalCamera.transform.SetPositionAndRotation(pos, rot);

            if (useObliqueClip && clipPlane)
                ApplyObliqueClip(clipPlane);

            RenderPipeline.SubmitRenderRequest(portalCamera, request);

            portalCamera.ResetProjectionMatrix();
        }
    }

    // i단계 재귀 포즈(진짜 "포탈-포탈-포탈..." 느낌으로 링크를 따라가며 누적)
    private bool TryBuildPose(
        Portal startIn, Portal startOut, int iteration,
        out Vector3 dstPos, out Quaternion dstRot,
        out Transform clipPlane)
    {
        dstPos = playerCamera.transform.position;
        dstRot = playerCamera.transform.rotation;
        clipPlane = startOut.Plane;

        Portal a = startIn;
        Portal b = startOut;

        for (int k = 0; k < iteration; k++)
        {
            if (!a || !b) return false;

            PortalMath.TransformPose(a.Plane, b.Plane, dstPos, dstRot, out dstPos, out dstRot);

            // 다음 단계는 "출구가 다음 입구"가 됨
            clipPlane = b.Plane;

            Portal nextA = b;
            Portal nextB = b.LinkedPortal;
            a = nextA;
            b = nextB;
        }

        return true;
    }

    private void ApplyObliqueClip(Transform portalPlane)
    {
        Vector3 normal = portalPlane.forward;

        // 카메라 쪽을 향하도록 보정
        if (Vector3.Dot(normal, portalCamera.transform.forward) > 0f)
            normal = -normal;

        Vector3 point = portalPlane.position + normal * clipPlaneOffset;

        Vector4 clipPlaneCameraSpace = CameraSpacePlane(portalCamera, point, normal);
        portalCamera.projectionMatrix = portalCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
    }

    private static Vector4 CameraSpacePlane(Camera cam, Vector3 point, Vector3 normal)
    {
        Matrix4x4 worldToCam = cam.worldToCameraMatrix;

        Vector3 camNormal = worldToCam.MultiplyVector(normal).normalized;
        Vector3 camPoint = worldToCam.MultiplyPoint(point);

        float d = -Vector3.Dot(camPoint, camNormal);
        return new Vector4(camNormal.x, camNormal.y, camNormal.z, d);
    }
}
