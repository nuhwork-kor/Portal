using UnityEngine;

public static class PortalMath
{
    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    public static Vector3 TransformPoint(Vector3 worldPoint, Transform inPlane, Transform outPlane)
    {
        Vector3 local = inPlane.InverseTransformPoint(worldPoint);
        local = HalfTurn * local;
        return outPlane.TransformPoint(local);
    }

    /// <summary>
    /// 레이 방향 등 "방향" 용도(정규화 유지)
    /// </summary>
    public static Vector3 TransformDirection(Vector3 worldDir, Transform inPlane, Transform outPlane)
    {
        Vector3 localDir = inPlane.InverseTransformDirection(worldDir);
        localDir = HalfTurn * localDir;
        return outPlane.TransformDirection(localDir).normalized;
    }

    /// <summary>
    /// 속도/힘 등 "벡터" 용도(크기 보존, 정규화 금지)
    /// </summary>
    public static Vector3 TransformVector(Vector3 worldVec, Transform inPlane, Transform outPlane)
    {
        Vector3 local = inPlane.InverseTransformDirection(worldVec);
        local = HalfTurn * local;
        return outPlane.TransformDirection(local);
    }

    public static Quaternion TransformRotation(Quaternion worldRot, Transform inPlane, Transform outPlane)
    {
        Quaternion local = Quaternion.Inverse(inPlane.rotation) * worldRot;
        local = HalfTurn * local;
        return outPlane.rotation * local;
    }
}
