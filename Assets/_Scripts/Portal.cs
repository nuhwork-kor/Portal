using System.Collections.Generic;
using UnityEngine;

public class Portal : MonoBehaviour
{
    [Header("연결된 포탈")]
    public Portal linkedPortal;

    [Header("Refs")]
    public Transform portalPlane;           //논리 평면
    public Collider portalTrigger;          //HalfInsde 판정용
    public PortalVisual portalVisual;
    float previousSide = 0f;

    [Header("포탈 바운더리")]
    public Collider[] boundaryColliders;    //포탈 감싸는 바운더리 콜리더

    List<Collider> behindWalls = new List<Collider>();      //포탈 뒤에 있는 벽들

    [Header("레이어")]
    int playerLayer = 3;

    //초기화
    PortalLinkState linkState = PortalLinkState.Unlinked;
    PortalTravellerState travellerState = PortalTravellerState.Outside;

    //캐싱
    PlayerMouseLook playerMouseLook;
    Transform playerBody;
    Rigidbody rb;

    /// <summary>
    /// 초기화
    /// </summary>
    private void Awake()
    {
        SetLinkState(PortalLinkState.Unlinked);
        SetBoundaryActive(false);
    }

    /// <summary>
    /// 포탈 연결 상태
    /// </summary>
    /// <param name="other"></param>
    public void SetLinkedPortal(Portal other)
    {
        linkedPortal = other;
        SetLinkState(other != null ? PortalLinkState.Linked : PortalLinkState.Unlinked);
    }

    void SetLinkState(PortalLinkState state)
    {
        linkState = state;
        portalVisual.SetState(state);
    }

    /// <summary>
    /// 포탈 바운더리 Active 조절
    /// </summary>
    /// <param name="active"></param>
    void SetBoundaryActive(bool active)
    {
        foreach (var col in boundaryColliders)
        {
            col.enabled = active;
        }
    }

    /// <summary>
    /// 포탈 뒷 벽 처리
    /// </summary>
    public void ResolveBehindWalls()
    {
        behindWalls.Clear();

        Vector3 center =
        portalPlane.position
      - portalPlane.forward * 0.05f;

        Vector3 halfExtents = new Vector3(
            1.0f,
            2.0f,
            0.1f
        );

        Quaternion rot = portalPlane.rotation;

        //디버그 시각화 (씬뷰)
        DebugDrawOverlapBox(center, halfExtents, rot, Color.red, 1f);

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rot,
            LayerMask.GetMask("Wall"),
            QueryTriggerInteraction.Ignore
        );

        foreach (var col in hits)
        {
            if (!behindWalls.Contains(col))
            {
                behindWalls.Add(col);
            }
        }
    }

    public void HandleTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != playerLayer) return;
        EnterHalfInside(other);
        print("PlayerOnTriggerEnter");
    }

    public void HandleTriggerExit(Collider other)
    {
        if (other.gameObject.layer != playerLayer) return;
        ExitHalfInside(other);
        print("PlayerOnTriggerExit");
    }

    public void HandleTriggerStay(Collider other)
    {
        if (!IsPlayer(other)) return;
        if (travellerState != PortalTravellerState.HalfInside) return;
        if (linkState != PortalLinkState.Linked) return;

        float currentSide = GetSide(other);


        bool fullyPassed =
            previousSide < 0f &&
            currentSide >= 0f;

        if (fullyPassed)
        {
            Teleport(other.transform);
            return;
        }

        previousSide = currentSide;
    }

    void EnterHalfInside(Collider player)
    {
        if (travellerState != PortalTravellerState.Outside) return;

        //뒤의 벽 재검색
        ResolveBehindWalls();

        previousSide = GetSide(player);

        travellerState = PortalTravellerState.HalfInside;

        SetBoundaryActive(true);

        foreach (var wall in behindWalls)
        {
            Physics.IgnoreCollision(player, wall, true);
        }
    }

    void ExitHalfInside(Collider player)
    {
        if (travellerState != PortalTravellerState.HalfInside) return;

        travellerState = PortalTravellerState.Outside;

        SetBoundaryActive(false);

        foreach (var wall in behindWalls)
        {
            Physics.IgnoreCollision(player, wall, false);
        }
    }

    void Teleport(Transform player)
    {
        //최초 1회 캐싱
        CachePlayerRefs(player);

        Collider playerCol = player.GetComponent<Collider>();
        ExitHalfInside(playerCol);

        travellerState = PortalTravellerState.Teleported;

        Vector3 localPos = portalPlane.InverseTransformPoint(player.position);              //위치 변환
        localPos.z = -localPos.z;                                                           //앞뒤 반전시켜주기
        Vector3 newWorldPos = linkedPortal.portalPlane.TransformPoint(localPos);            //출구 포탈 기준 월드 위치

        //출구에서 살짝 밀기
        newWorldPos += linkedPortal.portalPlane.forward * 0.6f;

        //rigidbody 안전장치
        if (rb != null)
        {
            rb.position = newWorldPos;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        //텔레포트 후 Pitch재설정

        if (playerMouseLook != null)
        {
            Vector3 fLocal = portalPlane.InverseTransformDirection(playerBody.forward);

            fLocal.x = -fLocal.x;
            fLocal.z = -fLocal.z;

            Vector3 fWorld = linkedPortal.portalPlane.TransformDirection(fLocal);

            Vector3 flat = Vector3.ProjectOnPlane(fWorld, Vector3.up);
            Quaternion newYaw = Quaternion.LookRotation(flat, Vector3.up);

            playerMouseLook.ForceSetYaw(newYaw);
        }

        travellerState = PortalTravellerState.Outside;

        previousSide = 0f;
    }

    void CachePlayerRefs(Transform player)
    {
        if (playerMouseLook != null) return;

        playerMouseLook = player.GetComponentInChildren<PlayerMouseLook>();
        playerBody = playerMouseLook.playerBody;
        rb = player.GetComponent<Rigidbody>();
    }

    public void Reposition(Vector3 pos, Quaternion rot)
    {
        transform.SetPositionAndRotation(pos, rot);
        gameObject.SetActive(true);
    }

    bool IsPlayer(Collider col)
    {
        return col.gameObject.layer == playerLayer;
    }

    float GetSide(Collider player)
    {
        Vector3 pivot = player.bounds.center;

        return Vector3.Dot(
            pivot - portalPlane.position,
            portalPlane.forward
            );
    }

    void DebugDrawOverlapBox(
    Vector3 center,
    Vector3 halfExtents,
    Quaternion rotation,
    Color color,
    float duration)
    {
        Vector3[] points = new Vector3[8];

        Vector3 right = rotation * Vector3.right * halfExtents.x;
        Vector3 up = rotation * Vector3.up * halfExtents.y;
        Vector3 forward = rotation * Vector3.forward * halfExtents.z;

        points[0] = center + right + up + forward;
        points[1] = center + right + up - forward;
        points[2] = center + right - up + forward;
        points[3] = center + right - up - forward;
        points[4] = center - right + up + forward;
        points[5] = center - right + up - forward;
        points[6] = center - right - up + forward;
        points[7] = center - right - up - forward;

        // 12 edges
        Debug.DrawLine(points[0], points[1], color, duration);
        Debug.DrawLine(points[0], points[2], color, duration);
        Debug.DrawLine(points[0], points[4], color, duration);
        Debug.DrawLine(points[7], points[3], color, duration);
        Debug.DrawLine(points[7], points[5], color, duration);
        Debug.DrawLine(points[7], points[6], color, duration);
        Debug.DrawLine(points[1], points[3], color, duration);
        Debug.DrawLine(points[1], points[5], color, duration);
        Debug.DrawLine(points[2], points[3], color, duration);
        Debug.DrawLine(points[2], points[6], color, duration);
        Debug.DrawLine(points[4], points[5], color, duration);
        Debug.DrawLine(points[4], points[6], color, duration);
    }
}
