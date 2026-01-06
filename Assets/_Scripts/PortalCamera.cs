using UnityEngine;

public class PortalCamera : MonoBehaviour
{
    private Camera cam;

    [Header("Refs")]
    public Camera playerCam;
    public Transform inPortal;   // in portal plane transform
    public Transform outPortal;  // out portal plane transform

    [Header("Clip")]
    public bool useOblique = true;
    public float clipPlaneOffset = 0.02f;

    [Header("Tuning (Feel)")]
    [Tooltip("1 = Portal1 물리 매핑, 0.6~0.9로 줄이면 '너무 멀리서 찍는 느낌'을 완화(포트폴리오용 감각 옵션)")]
    public float depthScale = 1.0f;

    [Tooltip("출구 포탈 평면 뒤로 카메라를 아주 조금 밀어 z-fighting/클리핑 완화")]
    public float planeBackOffset = 0.03f;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.enabled = false; // 자동 렌더 금지 (수동 RenderTexture 전용)
    }

    private void LateUpdate()
    {
        // enabled 체크 절대 하지 말 것: enabled=false여도 cam.Render()는 가능
        if (playerCam == null || inPortal == null || outPortal == null) return;
        if (cam.targetTexture == null) return; // RT 미지정이면 렌더 의미 없음

        UpdateTransformAndRotation();

        // FOV / Aspect 동기화
        cam.fieldOfView = playerCam.fieldOfView;
        cam.aspect = playerCam.aspect;

        if (useOblique) ApplyObliqueClipping();

        cam.Render();
    }

    private void UpdateTransformAndRotation()
    {
        // 1) 포탈 basis 구성 (축 꼬임 방지: right/up/normal을 직접 만든다)
        BuildBasis(inPortal, out Vector3 inRight, out Vector3 inUp, out Vector3 inNormal);
        BuildBasis(outPortal, out Vector3 outRight, out Vector3 outUp, out Vector3 outNormal);

        // 2) 플레이어 위치를 inPortal 기준 좌표(side, height, depth)로 분해
        Vector3 deltaIn = playerCam.transform.position - inPortal.position;

        float side = Vector3.Dot(deltaIn, inRight);
        float height = Vector3.Dot(deltaIn, inUp);
        float depth = Vector3.Dot(deltaIn, inNormal);

        // 플레이어가 포탈 앞에 있을 때 depth가 양수가 되도록 정규화
        // (포탈Plane forward가 반대로 모델링된 경우 자동 보정)
        if (depth < 0f)
        {
            depth = -depth;
            // normal만 뒤집으면 좌우가 뒤집히는 경우가 있어 right는 유지하고 up을 재구성
            inNormal = -inNormal;
            inUp = Vector3.Cross(inNormal, inRight).normalized;
        }

        // 포트폴리오 감각용(너무 멀리서 찍는 느낌 완화) : depth만 압축
        depth *= Mathf.Max(0.001f, depthScale);

        // 3) outPortal 기준으로 “동일한 상대 위치”에 카메라 배치
        //    포탈 통과이므로 depth 부호를 반전(평면 반대편에 위치)
        Vector3 camPos =
            outPortal.position
            + outRight * side
            + outUp * height
            - outNormal * depth;

        // 출구 포탈 평면에 너무 붙어서 클리핑/깜빡임 나는 걸 방지 (아주 미세)
        camPos -= outNormal * planeBackOffset;

        transform.position = camPos;

        // 4) 회전: 억지 yaw 공식(atan2) 쓰지 말고,
        //    포탈 평면 위의 “대응점”을 바라보게 한다 (이게 좌우=좌우, 앞뒤=줌으로 정상 동작)
        Vector3 lookTarget =
            outPortal.position
            + outRight * side
            + outUp * height;

        Vector3 dir = (lookTarget - camPos).normalized;
        if (dir.sqrMagnitude < 0.0001f) dir = -outNormal;

        transform.rotation = Quaternion.LookRotation(dir, outUp);
    }

    private void ApplyObliqueClipping()
    {
        cam.ResetProjectionMatrix();

        // 평면 노멀은 “포탈 평면에서 카메라로 향하는 방향”이어야 가장 안정적
        Vector3 toCam = (transform.position - outPortal.position).normalized;
        Vector3 planeNormal = toCam;
        Vector3 planePoint = outPortal.position + planeNormal * clipPlaneOffset;

        Vector4 clipPlaneCameraSpace = CameraSpacePlane(cam, planePoint, planeNormal);
        cam.projectionMatrix = cam.CalculateObliqueMatrix(clipPlaneCameraSpace);
    }

    private static Vector4 CameraSpacePlane(Camera cam, Vector3 planePoint, Vector3 planeNormal)
    {
        Matrix4x4 worldToCam = cam.worldToCameraMatrix;

        Vector3 cNormal = worldToCam.MultiplyVector(planeNormal).normalized;
        Vector3 cPoint = worldToCam.MultiplyPoint(planePoint);

        float d = -Vector3.Dot(cPoint, cNormal);
        return new Vector4(cNormal.x, cNormal.y, cNormal.z, d);
    }

    // portal transform의 “축 모델링”이 제각각이어도,
    // right를 portal.right를 평면에 투영해서 만들면 90도 벽에서도 side/depth가 안 섞인다.
    private static void BuildBasis(Transform portal, out Vector3 right, out Vector3 up, out Vector3 normal)
    {
        normal = portal.forward.normalized;

        right = Vector3.ProjectOnPlane(portal.right, normal);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.Cross(Vector3.up, normal);

        right.Normalize();
        up = Vector3.Cross(normal, right).normalized;
    }

    // 연결/해제: enabled 건드리지 말고 refs로 제어
    public void SetUP(Camera player, Transform inP, Transform outP)
    {
        playerCam = player;
        inPortal = inP;
        outPortal = outP;
    }

    public void Clear()
    {
        playerCam = null;
        inPortal = null;
        outPortal = null;
    }
}
