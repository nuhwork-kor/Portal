using System.Collections.Generic;
using UnityEngine;

public partial class PortalGunController
{
    [Header("Portal Refs (권장: 인스펙터에 직접 할당)")]
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("Pick Settings")]
    [SerializeField] private float maxInteractDistance = 5.0f;
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private LayerMask portalSurfaceMask = 0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Hold/Drop")]
    [SerializeField] private float extraForwardVelocity = 0.0f;
    [SerializeField] private bool dropUseYawOnly = true;
    [SerializeField] private bool alignHeldToPlayerYaw = true;
    [SerializeField] private bool ignoreCollisionWithPlayerWhileHolding = true;
    [SerializeField] private bool dropIfLineBroken = true;

    // runtime
    private Rigidbody heldRb;
    private readonly List<Collider> heldCols = new();
    private readonly List<Collider> playerCols = new();

    private bool heldPrevKinematic;
    private bool heldPrevUseGravity;

    // through-portal state
    private bool holdingThroughPortal;
    private Portal holdingInPortal;   // 내가 바라본(가까운) 포탈
    private Portal holdingOutPortal;  // 반대편 포탈

    private const float EPS = 0.01f;
    private static readonly Quaternion HalfTurn = Quaternion.Euler(0f, 180f, 0f);

    private void InitInteraction()
    {
        // 플레이어 콜라이더 캐싱
        if (playerRigidbody != null)
            playerRigidbody.GetComponentsInChildren(true, playerCols);

        // 포탈 자동탐색(애매하면 애매할수도있는데, 이름 규칙 다르면 못 찾음 -> 인스펙터 할당 권장)
        if (!bluePortal || !orangePortal)
        {
            var portals = Object.FindObjectsByType<Portal>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            foreach (var p in portals)
            {
                string n = p.name.ToLower();
                if (!bluePortal && n.Contains("blue")) bluePortal = p;
                else if (!orangePortal && n.Contains("orange")) orangePortal = p;
            }
        }
    }

    private void BindInteractionInput(bool bind)
    {
        if (bind) InputManager.OnInteract += HandleInteract;
        else InputManager.OnInteract -= HandleInteract;
    }

    private void HandleInteract()
    {
        Debug.Log("[PortalGun] Interact pressed");
        ToggleHold();
    }

    private void ToggleHold()
    {
        if (IsHolding) Drop();
        else TryPickup();
    }

    private void FixedUpdate()
    {
        // holding 업데이트는 물리 타이밍에서 처리 (튐 줄이기)
        if (IsHolding) TickHoldingFixed();

        if (IsHolding && dropIfLineBroken)
        {
            if (!IsHoldLineStillValid())
                Drop();
        }
    }

    private void TickHoldingFixed()
    {
        if (!heldRb || !holdPoint) return;

        if (!holdingThroughPortal)
        {
            // 일반 집기: 내 손앞에 고정
            heldRb.MovePosition(holdPoint.position);
            if (alignHeldToPlayerYaw)
                heldRb.MoveRotation(GetPlayerYawRotation());
            return;
        }

        // 포탈 집기: holdPoint를 포탈 변환해서 “반대편 공간의 대응 위치”로 고정
        if (!holdingInPortal || !holdingOutPortal || !holdingInPortal.IsPlaced || !holdingOutPortal.IsPlaced)
        {
            Drop();
            return;
        }

        Vector3 targetPos = TransformPointThroughPortals(
            holdPoint.position,
            holdingInPortal.Plane,
            holdingOutPortal.Plane
        );

        heldRb.MovePosition(targetPos);

        if (alignHeldToPlayerYaw)
        {
            Quaternion nearYaw = GetPlayerYawRotation();
            Quaternion targetRot = TransformRotationThroughPortals(
                nearYaw,
                holdingInPortal.Plane,
                holdingOutPortal.Plane
            );
            heldRb.MoveRotation(targetRot);
        }
    }

    private void TryPickup()
    {
        if (!playerCamera) return;

        Vector3 origin = playerCamera.transform.position;
        Vector3 dir = playerCamera.transform.forward;

        int mask = interactableMask | portalSurfaceMask;

        var hits = Physics.RaycastAll(origin, dir, maxInteractDistance + 5f, mask, triggerInteraction);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var first = hits[0];

        // 1) 바로 앞 오브젝트면 바로 집기
        if (((1 << first.collider.gameObject.layer) & interactableMask) != 0)
        {
            var rb = first.collider.attachedRigidbody;
            if (!rb) return;
            if (first.distance > maxInteractDistance) return;

            Pickup(rb, throughPortal: false, null, null);
            return;
        }

        // 2) 포탈 표면이면 포탈 통해 집기
        if (((1 << first.collider.gameObject.layer) & portalSurfaceMask) != 0)
        {
            var inPortal = first.collider.GetComponentInParent<Portal>();
            if (!inPortal) return;

            Portal outPortal = (inPortal == bluePortal) ? orangePortal : bluePortal;
            if (!outPortal) return;
            if (!inPortal.IsPlaced || !outPortal.IsPlaced) return;

            if (TryPickupThroughPortal(first, inPortal, outPortal, out var pickedRb))
                Pickup(pickedRb, throughPortal: true, inPortal, outPortal);
        }
    }

    private bool TryPickupThroughPortal(RaycastHit inHit, Portal inPortal, Portal outPortal, out Rigidbody picked)
    {
        picked = null;

        // (A) 카메라 -> inPortal 표면까지 거리
        float d1 = inHit.distance;

        // (B) 포탈로 레이 변환: 원점은 "포탈 표면 hit.point" 기준이 훨씬 안정적
        Vector3 outOrigin = TransformPointThroughPortals(inHit.point, inPortal.Plane, outPortal.Plane);
        Vector3 outDir = TransformDirectionThroughPortals(playerCamera.transform.forward, inPortal.Plane, outPortal.Plane);

        outOrigin += outDir * EPS;

        if (!Physics.Raycast(outOrigin, outDir, out RaycastHit outHit, maxInteractDistance + 10f, interactableMask, triggerInteraction))
            return false;

        var rb = outHit.collider.attachedRigidbody;
        if (!rb) return false;

        // (C) in거리 + out거리 합산
        float d2 = outHit.distance;
        float total = d1 + d2;
        if (total > maxInteractDistance) return false;

        picked = rb;
        return true;
    }

    private void Pickup(Rigidbody rb, bool throughPortal, Portal inPortal, Portal outPortal)
    {
        heldRb = rb;

        holdingThroughPortal = throughPortal;
        holdingInPortal = inPortal;
        holdingOutPortal = outPortal;

        heldCols.Clear();
        rb.GetComponentsInChildren(true, heldCols);

        heldPrevKinematic = rb.isKinematic;
        heldPrevUseGravity = rb.useGravity;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;

        if (ignoreCollisionWithPlayerWhileHolding)
            SetIgnorePlayerCollision(true);
    }

    public void Drop()
    {
        if (!heldRb) return;

        if (ignoreCollisionWithPlayerWhileHolding)
            SetIgnorePlayerCollision(false);

        heldRb.isKinematic = heldPrevKinematic;
        heldRb.useGravity = heldPrevUseGravity;

        // 놓는 순간: 플레이어 속도 + 전방 약간
        Vector3 baseVel = playerRigidbody ? playerRigidbody.linearVelocity : Vector3.zero;

        Vector3 forward = playerCamera ? playerCamera.transform.forward : transform.forward;

        if (dropUseYawOnly)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
            forward.Normalize();
        }

        heldRb.linearVelocity = baseVel + forward * extraForwardVelocity;

        heldRb = null;
        heldCols.Clear();

        holdingThroughPortal = false;
        holdingInPortal = null;
        holdingOutPortal = null;
    }

    private bool IsHoldLineStillValid()
    {
        if (!heldRb || !playerCamera) return false;

        Vector3 origin = playerCamera.transform.position;
        Vector3 dir = playerCamera.transform.forward;

        if (holdingThroughPortal && holdingInPortal && holdingOutPortal && holdingInPortal.IsPlaced && holdingOutPortal.IsPlaced)
        {
            // 1) 먼저 가까운 포탈 표면이 계속 맞는지
            if (!Physics.Raycast(origin, dir, out RaycastHit inHit, maxInteractDistance + 5f, portalSurfaceMask, triggerInteraction))
                return false;

            var hitPortal = inHit.collider.GetComponentInParent<Portal>();
            if (hitPortal != holdingInPortal) return false;

            // 2) 변환 레이로 오브젝트가 계속 맞는지
            Vector3 outOrigin = TransformPointThroughPortals(inHit.point, holdingInPortal.Plane, holdingOutPortal.Plane);
            Vector3 outDir = TransformDirectionThroughPortals(dir, holdingInPortal.Plane, holdingOutPortal.Plane);
            outOrigin += outDir * EPS;

            if (!Physics.Raycast(outOrigin, outDir, out RaycastHit outHit, maxInteractDistance + 10f, interactableMask, triggerInteraction))
                return false;

            var rb = outHit.collider.attachedRigidbody;
            if (!rb || rb != heldRb) return false;

            float total = inHit.distance + outHit.distance;
            return total <= maxInteractDistance;
        }

        // 일반 집기: 레이가 계속 그 오브젝트를 먼저 맞춰야 유지
        if (!Physics.Raycast(origin, dir, out RaycastHit hit, maxInteractDistance + 1f, interactableMask | portalSurfaceMask, triggerInteraction))
            return false;

        var rb2 = hit.collider.attachedRigidbody;
        return rb2 != null && rb2 == heldRb;
    }

    private Quaternion GetPlayerYawRotation()
    {
        Vector3 f = playerCamera ? playerCamera.transform.forward : transform.forward;
        f = Vector3.ProjectOnPlane(f, Vector3.up);
        if (f.sqrMagnitude < 0.0001f) f = transform.forward;
        f.Normalize();
        return Quaternion.LookRotation(f, Vector3.up);
    }

    private void SetIgnorePlayerCollision(bool ignore)
    {
        if (heldCols.Count == 0 || playerCols.Count == 0) return;

        for (int i = 0; i < heldCols.Count; i++)
        {
            var hc = heldCols[i];
            if (!hc) continue;

            for (int j = 0; j < playerCols.Count; j++)
            {
                var pc = playerCols[j];
                if (!pc) continue;

                Physics.IgnoreCollision(hc, pc, ignore);
            }
        }
    }

    // ===== portal transform helpers =====
    private static Vector3 TransformPointThroughPortals(Vector3 pointWorld, Transform inPlane, Transform outPlane)
    {
        Vector3 local = inPlane.InverseTransformPoint(pointWorld);
        local = HalfTurn * local;
        return outPlane.TransformPoint(local);
    }

    private static Vector3 TransformDirectionThroughPortals(Vector3 dirWorld, Transform inPlane, Transform outPlane)
    {
        Vector3 localDir = inPlane.InverseTransformDirection(dirWorld);
        localDir = HalfTurn * localDir;
        return outPlane.TransformDirection(localDir).normalized;
    }

    private static Quaternion TransformRotationThroughPortals(Quaternion rotWorld, Transform inPlane, Transform outPlane)
    {
        Quaternion local = Quaternion.Inverse(inPlane.rotation) * rotWorld;
        local = HalfTurn * local;
        return outPlane.rotation * local;
    }
}
