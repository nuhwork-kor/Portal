using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PortalGunController))]
public class PortalRaycaster : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PortalGunController ctx;

    [Header("Pick Settings")]
    [SerializeField] private float maxInteractDistance = 5.0f;
    [SerializeField] private LayerMask interactableMask = ~0;
    [SerializeField] private LayerMask portalSurfaceMask = 0;
    [SerializeField] private LayerMask occluderMask = 0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Tuning")]
    [SerializeField] private float sphereRadius = 0.10f;
    [SerializeField] private float eps = 0.01f;
    [SerializeField] private float extraRaySlack = 10f;

    public float MaxInteractDistance => maxInteractDistance;
    public LayerMask InteractableMask => interactableMask;
    public LayerMask PortalSurfaceMask => portalSurfaceMask;
    public LayerMask OccluderMask => occluderMask;
    public float SphereRadius => sphereRadius;

    public struct InteractHit
    {
        public bool hit;
        public Rigidbody rb;

        public bool throughPortal;
        public Portal inPortal;
        public Portal outPortal;

        // ✅ “실제 잡힌 오브젝트”에 대한 hit (direct면 1차 hit, through면 2차 hit)
        public RaycastHit rbHit;

        public float totalDistance;
    }

    private void Awake()
    {
        if (!ctx) ctx = GetComponent<PortalGunController>();
    }

    public bool TryGetInteractHit(out InteractHit result)
    {
        result = default;

        Camera cam = ctx ? ctx.PlayerCamera : null;
        if (!cam) return false;

        Vector3 origin = cam.transform.position;
        Vector3 dir = cam.transform.forward;

        int mask = interactableMask | portalSurfaceMask | occluderMask;
        float firstMax = Mathf.Max(maxInteractDistance, 0.01f) + Mathf.Max(extraRaySlack, 0f);

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, firstMax, mask, triggerInteraction);
        if (hits == null || hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit h = hits[i];
            if (!h.collider) continue;

            int layerBit = 1 << h.collider.gameObject.layer;

            // 0) occluder가 먼저면 끝
            if ((occluderMask.value & layerBit) != 0)
                return false;

            // 1) Direct Interactable
            if ((interactableMask.value & layerBit) != 0)
            {
                if (h.distance > maxInteractDistance) return false;

                // 관대하게 스피어캐스트로 RB 확정
                if (!Physics.SphereCast(origin, sphereRadius, dir, out RaycastHit sh, maxInteractDistance, interactableMask, triggerInteraction))
                    return false;

                var rb = sh.collider.attachedRigidbody;
                if (!rb) return false;

                result.hit = true;
                result.rb = rb;
                result.throughPortal = false;
                result.rbHit = sh;
                result.totalDistance = sh.distance;
                return true;
            }

            // 2) PortalSurface hit => Through Portal
            if ((portalSurfaceMask.value & layerBit) != 0)
            {
                Portal inPortal = h.collider.GetComponentInParent<Portal>();
                if (!inPortal) return false;

                Portal outPortal = inPortal.OtherPortal;
                if (!outPortal) return false;
                if (!inPortal.IsPlaced || !outPortal.IsPlaced) return false;

                Vector3 outOrigin = PortalMath.TransformPoint(h.point, inPortal.Plane, outPortal.Plane);
                Vector3 outDir = PortalMath.TransformDirection(dir, inPortal.Plane, outPortal.Plane);
                outOrigin += outDir * eps;

                float secondMax = maxInteractDistance + Mathf.Max(extraRaySlack, 0f);
                int secondMask = interactableMask | occluderMask;

                if (!Physics.SphereCast(outOrigin, sphereRadius, outDir, out RaycastHit outHit, secondMax, secondMask, triggerInteraction))
                    return false;

                int outLayerBit = 1 << outHit.collider.gameObject.layer;
                if ((occluderMask.value & outLayerBit) != 0)
                    return false;

                var rb = outHit.collider.attachedRigidbody;
                if (!rb) return false;

                float total = h.distance + outHit.distance;
                if (total > maxInteractDistance) return false;

                result.hit = true;
                result.rb = rb;
                result.throughPortal = true;
                result.inPortal = inPortal;
                result.outPortal = outPortal;
                result.rbHit = outHit;
                result.totalDistance = total;
                return true;
            }
        }

        return false;
    }
}
