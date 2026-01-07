using UnityEngine;

public class PortalMath
{
    public static Vector3 TransformPosition(
        Transform inPlane,
        Transform outPlane,
        Vector3 worldPos,
        float exitOffset = 0f)
    {
        Vector3 localPos = inPlane.InverseTransformPoint(worldPos);
        localPos.z = -localPos.z;

        Vector3 dst = outPlane.TransformPoint(localPos);

        if (exitOffset != 0f)
            dst += outPlane.forward * exitOffset;

        return dst;
    }

    public static Quaternion TransformRotation(
        Transform inPlane,
        Transform outPlane,
        Quaternion worldRot)
    {
        Quaternion localRot = Quaternion.Inverse(inPlane.rotation) * worldRot;
        localRot = Quaternion.AngleAxis(180f, Vector3.up) * localRot;
        return outPlane.rotation * localRot;
    }

    /// <summary>
    /// "플레이어가 마우스로 시야를 돌리는 것"은 절대 사용하지 않음.
    /// 오직 (플레이어 위치) -> (포탈 중심) 방향으로만 포탈 카메라가 바라보게 함.
    /// </summary>
    public static Quaternion GetPortalViewRotation(
        Transform inPlane,
        Transform outPlane,
        Vector3 sourcePos)
    {
        // 플레이어(또는 sourcePos)에서 "입구 포탈 중심"을 보는 방향
        Vector3 viewDir = (inPlane.position - sourcePos);

        // 이 방향을 입구 포탈 로컬로 옮긴 뒤 Z 반전(포탈 통과)
        Vector3 localDir = inPlane.InverseTransformDirection(viewDir);
        localDir.z = -localDir.z;

        // 출구 포탈 월드 방향으로 변환
        Vector3 worldDir = outPlane.TransformDirection(localDir);

        // 포탈이 바닥/천장일 수도 있으니 up은 outPlane.up을 사용
        if (worldDir.sqrMagnitude < 0.0001f)
            return outPlane.rotation;

        return Quaternion.LookRotation(worldDir.normalized, outPlane.up);
    }
}
