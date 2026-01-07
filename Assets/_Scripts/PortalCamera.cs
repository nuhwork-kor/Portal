using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 플레이어 카메라 > 포탈 카메라 변환
/// RenderTexture용
/// </summary>
public class PortalCamera : MonoBehaviour
{
    Camera cam;

    [Header("Refs")]
    public Camera playerCamera;
    public Portal inPortal;  
    public Portal outPortal;

    [Header("Recursion")]
    [SerializeField] int maxRecursionDepth = 2;

    [Header("Oblique Near Clip")]
    [SerializeField] bool useObliqueClip = true;
    [SerializeField] float clipPlaneOffset = 0.02f;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.enabled = false; // 자동 렌더 금지 (수동 RenderTexture 전용)
    }


    /// <summary>
    /// 외부 진입점 (PortalRenderer가 호출)
    /// </summary>
    /// <param name="rt"></param>
    public void Render(RenderTexture rt)
    {
        RenderInternal(rt, playerCamera.transform.position, 0);
    }

    void RenderInternal(RenderTexture rt, Vector3 sourcePos, int depth)
    {
        if (depth >= maxRecursionDepth) return;
        if (!playerCamera || !inPortal || !outPortal) return;

        cam.targetTexture = rt;

        Vector3 camPos =
        PortalMath.TransformPosition(
            inPortal.portalPlane,
            outPortal.portalPlane,
            sourcePos
            );

        Quaternion camRot =
            PortalMath.GetPortalViewRotation(
                inPortal.portalPlane,
                outPortal.portalPlane,
                sourcePos
                );

        transform.SetPositionAndRotation(camPos, camRot);

        cam.fieldOfView = playerCamera.fieldOfView;
        cam.aspect = rt.width / (float)rt.height;

        //Oblique Near Clip
        if (useObliqueClip) ApplyObliqueClip(outPortal.portalPlane);

        cam.Render();

        //projection matrix 복구
        cam.ResetProjectionMatrix();

        //연결된 포탈이 있을 때
        if (outPortal.linkedPortal != null)
        {
            Portal nextIn = outPortal;
            Portal nextOut = outPortal.linkedPortal;

            inPortal = nextIn;
            outPortal = nextOut;

            RenderInternal(rt, camPos, depth + 1);
        }

        cam.targetTexture = null;
    }

    void ApplyObliqueClip(Transform portalPlane)
    {
        // 포탈 평면 > 카메라를 향하는 노멀
        Vector3 planeNormal = portalPlane.forward;

        //카메라를 향하도록 보정
        if(Vector3.Dot(planeNormal, transform.forward) > 0f)
        {
            planeNormal = -planeNormal;
        }

        Vector3 planePoint = portalPlane.position + planeNormal * clipPlaneOffset;

        Vector4 clipPlaneCameraSpace = CameraSpacePlane(cam, planePoint, planeNormal);
        
        cam.projectionMatrix = playerCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
    }

    static Vector4 CameraSpacePlane(Camera cam, Vector3 point, Vector3 normal)
    {
        Matrix4x4 worldToCam = cam.worldToCameraMatrix;

        Vector3 camNormal = worldToCam.MultiplyVector(normal).normalized;
        Vector3 camPoint = worldToCam.MultiplyPoint(point);

        float d = -Vector3.Dot(camPoint, camNormal);
        return new Vector4(camNormal.x, camNormal.y, camNormal.z, d);
    }
}
