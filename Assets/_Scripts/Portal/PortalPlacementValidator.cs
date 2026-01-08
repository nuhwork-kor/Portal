using UnityEngine;

public class PortalPlacementValidator : MonoBehaviour
{
    public struct Request
    {
        public Portal portal;
        public Collider surfaceCollider;

        public Vector3 placePosition;
        public Quaternion placeRotation;

        public LayerMask placeableMask;
        public LayerMask overlapMask;
    }

    [Header("Overlap")]
    [SerializeField] private float overlapShrink = 0.02f;

    [Header("Support Check")]
    [SerializeField] private float supportCheckDepth = 0.2f;
    [SerializeField] private float supportInset = 0.02f;

    public bool CanPlace(in Request req, out string reason)
    {
        reason = "";

        if (!req.portal) { reason = "Portal is null"; return false; }
        if (!req.surfaceCollider) { reason = "Surface collider is null"; return false; }

        if (!CheckOverlap(req, out reason)) return false;
        if (!CheckSupport(req, out reason)) return false;

        return true;
    }

    private bool CheckOverlap(in Request req, out string reason)
    {
        reason = "";

        Vector3 half = req.portal.HalfExtents;
        half = new Vector3(
            Mathf.Max(0.001f, half.x - overlapShrink),
            Mathf.Max(0.001f, half.y - overlapShrink),
            Mathf.Max(0.001f, half.z - overlapShrink)
        );

        Vector3 center = req.placePosition + (req.placeRotation * req.portal.OverlapBoxLocalCenter);

        Collider[] hits = Physics.OverlapBox(
            center,
            half,
            req.placeRotation,
            req.overlapMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (!c) continue;

            if (c == req.surfaceCollider) continue;
            if (c.transform.IsChildOf(req.portal.transform)) continue;

            reason = $"Overlap with {c.name}";
            return false;
        }

        return true;
    }

    private bool CheckSupport(in Request req, out string reason)
    {
        reason = "";

        Vector3 half = req.portal.HalfExtents;

        Vector3[] localCorners =
        {
            new Vector3(-half.x, -half.y, 0f),
            new Vector3(-half.x,  half.y, 0f),
            new Vector3( half.x, -half.y, 0f),
            new Vector3( half.x,  half.y, 0f),
        };

        // 포탈 forward는 -hitNormal로 만들었음.
        // 표면 안쪽으로 쏘고 싶으면 portalForward(=placeRot*forward) 방향으로 쏘면 됨.
        Vector3 portalForward = req.placeRotation * Vector3.forward;
        Vector3 castDir = portalForward;

        for (int i = 0; i < localCorners.Length; i++)
        {
            Vector3 worldCorner = req.placePosition + (req.placeRotation * localCorners[i]);

            // 코너를 표면 안쪽으로 살짝 집어넣고(정밀도/벽두께 대응)
            Vector3 origin = worldCorner - castDir * supportInset;

            bool hit = Physics.Raycast(
                origin,
                castDir,
                out RaycastHit rh,
                supportCheckDepth,
                req.placeableMask,
                QueryTriggerInteraction.Ignore
            );

            if (!hit || rh.collider != req.surfaceCollider)
            {
                reason = $"Corner not supported (idx={i})";
                return false;
            }
        }

        return true;
    }
}
