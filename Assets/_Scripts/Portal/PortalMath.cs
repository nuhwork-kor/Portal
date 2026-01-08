using UnityEngine;

public class PortalMath
{
    //포탈 통과용 로컬 z 반전 행렬
    static readonly Matrix4x4 MirrorZ = Matrix4x4.Scale(new Vector3(1f, 1f, -1f));

    /// <summary>
    /// inPlane을 기준으로 worldPos를 outPlane 너머의 위치로 변환
    /// </summary>
    /// <param name="inPlane"></param>
    /// <param name="outPlane"></param>
    /// <param name="worldPos"></param>
    /// <returns></returns>
    public static Vector3 TransformPoint(Transform inPlane, Transform outPlane, Vector3 worldPos)
    {
        Matrix4x4 m = outPlane.localToWorldMatrix * MirrorZ * inPlane.worldToLocalMatrix;
        return m.MultiplyPoint3x4(worldPos);
    }

    /// <summary>
    /// 방향 벡터 변환
    /// </summary>
    /// <param name="inPlane"></param>
    /// <param name="outPlane"></param>
    /// <param name="worldDir"></param>
    /// <returns></returns>
    public static Vector3 TransformDirection(Transform inPlane, Transform outPlane, Vector3 worldDir)
    {
        Matrix4x4 m = outPlane.localToWorldMatrix * MirrorZ * inPlane.worldToLocalMatrix;
        return m.MultiplyVector(worldDir);
    }

    /// <summary>
    /// 회전 변환
    /// </summary>
    /// <param name="inPlane"></param>
    /// <param name="outPlane"></param>
    /// <param name="worldRot"></param>
    /// <returns></returns>
    public static Quaternion TransformRotation(Transform inPlane, Transform outPlane, Quaternion worldRot)
    {
        //회전은 방향벡터 2개 변환으로 만든다 (안전하게)
        Vector3 forward = worldRot * Vector3.forward;
        Vector3 up = worldRot * Vector3.up;

        Vector3 newForward = TransformDirection(inPlane, outPlane, forward);
        Vector3 newUp = TransformDirection(inPlane, outPlane, up);

        if (newForward.sqrMagnitude < 1e-6f) newForward = outPlane.forward;
        if(newUp.sqrMagnitude < 1e-6f) newUp = outPlane.up;

        return Quaternion.LookRotation(newForward.normalized, newUp.normalized);
    }

    /// <summary>
    /// 포탈 카메라/시선용 : playerCam을 inPortal에서 outPortal로 변환한 "동일 시점" 포즈
    /// </summary>
    /// <param name="inPlane"></param>
    /// <param name="outPlane"></param>
    /// <param name="srcPos"></param>
    /// <param name="srcRot"></param>
    /// <param name="dstPos"></param>
    /// <param name="dstRot"></param>
    public static void TransformPose(Transform inPlane, Transform outPlane, Vector3 srcPos, Quaternion srcRot, out Vector3 dstPos, out Quaternion dstRot)
    {
        dstPos = TransformPoint(inPlane, outPlane, srcPos);
        dstRot = TransformRotation(inPlane, outPlane, srcRot);
    }
}
