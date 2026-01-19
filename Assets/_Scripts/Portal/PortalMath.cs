using UnityEngine;

// PortalMath: 포탈 통과 변환(점/방향/벡터/회전)을 담당하는 수학 유틸리티 클래스
public static class PortalMath
{
    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f); // 포탈 통과 시 기본적으로 180도 뒤집는 회전(로컬 기준)

    /// <summary>
    /// 월드 좌표의 "점"을 포탈 입구(inPlane) 기준 로컬로 바꾼 후 180도 뒤집어 출구(outPlane) 월드로 변환한다.
    /// </summary>
    /// <param name="worldPoint">변환할 월드 포인트</param>
    /// <param name="inPlane">입구 포탈 평면 Transform</param>
    /// <param name="outPlane">출구 포탈 평면 Transform</param>
    /// <returns>출구 기준으로 변환된 월드 포인트</returns>
    public static Vector3 TransformPoint(Vector3 worldPoint, Transform inPlane, Transform outPlane)
    {
        Vector3 local = inPlane.InverseTransformPoint(worldPoint);                // 입구 기준 로컬 좌표로 변환
        local = HalfTurn * local;                                                 // 180도 뒤집기(포탈 반대면 처리)
        return outPlane.TransformPoint(local);                                    // 출구 기준 월드 좌표로 변환
    }

    /// <summary>
    /// 레이 방향 등 "방향" 용도: 정규화를 유지해서 반환한다.
    /// </summary>
    /// <param name="worldDir">변환할 월드 방향</param>
    /// <param name="inPlane">입구 포탈 평면 Transform</param>
    /// <param name="outPlane">출구 포탈 평면 Transform</param>
    /// <returns>출구 기준 월드 방향(정규화됨)</returns>
    public static Vector3 TransformDirection(Vector3 worldDir, Transform inPlane, Transform outPlane)
    {
        Vector3 localDir = inPlane.InverseTransformDirection(worldDir);           // 입구 기준 로컬 방향
        localDir = HalfTurn * localDir;                                           // 180도 뒤집기
        return outPlane.TransformDirection(localDir).normalized;                  // 출구 기준 월드 방향으로 변환 후 정규화
    }

    /// <summary>
    /// 속도/힘 등 "벡터" 용도: 크기를 보존하며 정규화하지 않는다.
    /// </summary>
    /// <param name="worldVec">변환할 월드 벡터</param>
    /// <param name="inPlane">입구 포탈 평면 Transform</param>
    /// <param name="outPlane">출구 포탈 평면 Transform</param>
    /// <returns>출구 기준 월드 벡터(크기 보존)</returns>
    public static Vector3 TransformVector(Vector3 worldVec, Transform inPlane, Transform outPlane)
    {
        Vector3 local = inPlane.InverseTransformDirection(worldVec);              // 입구 기준 로컬 벡터
        local = HalfTurn * local;                                                 // 180도 뒤집기
        return outPlane.TransformDirection(local);                                // 출구 기준 월드 벡터(정규화 금지)
    }

    /// <summary>
    /// 월드 회전을 포탈 입구 기준 로컬로 바꾼 후 180도 뒤집어 출구 기준 월드 회전으로 변환한다.
    /// </summary>
    /// <param name="worldRot">변환할 월드 회전</param>
    /// <param name="inPlane">입구 포탈 평면 Transform</param>
    /// <param name="outPlane">출구 포탈 평면 Transform</param>
    /// <returns>출구 기준 월드 회전</returns>
    public static Quaternion TransformRotation(Quaternion worldRot, Transform inPlane, Transform outPlane)
    {
        Quaternion local = Quaternion.Inverse(inPlane.rotation) * worldRot;       // 입구 기준 로컬 회전
        local = HalfTurn * local;                                                // 180도 뒤집기
        return outPlane.rotation * local;                                         // 출구 기준 월드 회전
    }
}
