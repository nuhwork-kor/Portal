// PortalSystem.cs
using UnityEngine;

public class PortalSystem : MonoBehaviour
{
    public enum PortalType { Blue, Orange }

    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("Placement Mask")]
    [Tooltip("��Ż�� ���� �� �ִ� ���̾�(Wall/Ground ��)")]
    [SerializeField] private LayerMask placeableMask;

    [Tooltip("��ħ �˻翡 ������ ���̾�(���� placeableMask�� ���� ��õ)")]
    [SerializeField] private LayerMask blockingMask;

    [Header("Placement Tuning")]
    [SerializeField] private float surfaceOffset = 0.002f;

    [Header("Fit / Slide On Surface")]
    [Tooltip("��Ż�� �� ������ ������, �� �������� �ڵ����� '�о' ������ ������ ��")]
    [SerializeField] private bool autoSlideToFit = true;

    [Tooltip("���� ��迡�� �̸�ŭ ������ �������� ��ȿ �������� ��(��Ż �׵θ�/����)")]
    [SerializeField] private float fitPadding = 0.02f; // 2cm

    [Header("Optional Overlap Check")]
    [Tooltip("�ֺ� ������Ʈ���� ��ġ�� ��ġ ���� ó��(�ڳ�/���� ħ�� ����). �ʿ� ������ ���.")]
    [SerializeField] private bool useOverlapCheck = true;

    [Tooltip("��Ż ��� ���� �������� ��ħ �˻� �β�(����)")]
    [SerializeField] private float overlapCheckDepth = 0.15f;

    [Tooltip("��ħ �˻翡�� ��Ż ����/���ο� �߰��� ���� ����(����)")]
    [SerializeField] private float overlapPadding = 0.01f;

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;

        // blockingMask �������̸� placeableMask��
        if (blockingMask.value == 0) blockingMask = placeableMask;

        // ��ũ ����
        if (bluePortal && orangePortal)
        {
            bluePortal.LinkTo(orangePortal);
            orangePortal.LinkTo(bluePortal);

            bluePortal.SetPlaced(false);
            orangePortal.SetPlaced(false);
        }
    }

    public bool TryPlacePortal(PortalType type, Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider)
    {
        if (!playerCamera || !hitCollider) return false;

        // ��ġ ���� ���̾����� üũ
        if ((placeableMask.value & (1 << hitCollider.gameObject.layer)) == 0)
            return false;

        Portal target = (type == PortalType.Blue) ? bluePortal : orangePortal;
        if (!target) return false;

        // ȸ�� ���(�� ���� ��� ����)
        Quaternion rot = ComputeRotationFromSurface(hitNormal, playerCamera.transform);

        // �ٽ�: ��Ż�� "������" ������ hitPoint�� �� ������ �����̵�/����
        if (!TryGetFittedSurfacePoint(target, hitCollider, hitPoint, rot, out Vector3 fittedSurfacePoint))
            return false;

        // ���� ��ġ
        target.Reposition(hitCollider, fittedSurfacePoint, rot, surfaceOffset);

        // ��� ��Ż ǥ�� ����(������)
        target.RefreshSurfaceVisibility();
        if (target.OtherPortal) target.OtherPortal.RefreshSurfaceVisibility();

        var openAnim = target.GetComponent<PortalSurfaceOpenAnimator>();
        if (openAnim) openAnim.Play();

        return true;
    }

    private bool TryGetFittedSurfacePoint(Portal portal, Collider surface, Vector3 hitPoint, Quaternion portalRot, out Vector3 fittedPointOnSurface)
    {
        fittedPointOnSurface = hitPoint;

        if (!portal || !portal.SurfaceCollider)
            return false;

        // ��Ż ����/���� ��ġ��(���� ����)
        GetPortalHalfSizeWorld(portal.SurfaceCollider, out float halfW, out float halfH);

        // ��Ż ��� ��(����)
        Vector3 axisR = portalRot * Vector3.right;
        Vector3 axisU = portalRot * Vector3.up;
        Vector3 axisN = portalRot * Vector3.forward;

        // ǥ�� �ݶ��̴��� axisR/axisU�� �������� ���� min/max ����
        if (!TryGetProjectionRange(surface, axisR, out float minR, out float maxR)) return false;
        if (!TryGetProjectionRange(surface, axisU, out float minU, out float maxU)) return false;

        // ��ȿ ����(��Ż ��ġ�� + �е�)
        float loR = minR + halfW + fitPadding;
        float hiR = maxR - halfW - fitPadding;
        float loU = minU + halfH + fitPadding;
        float hiU = maxU - halfH - fitPadding;

        // �ƿ� �� ������ ������ ��ġ �Ұ�
        if (loR > hiR || loU > hiU)
            return false;

        // ���� hitPoint�� ������
        float sR = Vector3.Dot(axisR, hitPoint);
        float sU = Vector3.Dot(axisU, hitPoint);

        if (!autoSlideToFit)
        {
            // �ڵ� ���� ���� "������ ���߸�" ��ġ ���
            if (sR < loR || sR > hiR) return false;
            if (sU < loU || sU > hiU) return false;

            fittedPointOnSurface = hitPoint;
        }
        else
        {
            // �ڵ� ����: ���� ������ Ŭ�����ؼ� X/Z Ȥ�� Y�� �ڵ����� �и�
            float cR = Mathf.Clamp(sR, loR, hiR);
            float cU = Mathf.Clamp(sU, loU, hiU);

            Vector3 shifted = hitPoint + axisR * (cR - sR) + axisU * (cU - sU);
            fittedPointOnSurface = shifted;
        }

        // ����: �ֺ� ������Ʈ�� ħ���ϸ� ���� ó��
        if (useOverlapCheck)
        {
            Vector3 center = fittedPointOnSurface + axisN * surfaceOffset; // ���� ��Ż �߽�(������ ��¦ ���)
            if (IsPlacementBlocked(portal, surface, center, portalRot, halfW, halfH))
                return false;
        }

        return true;
    }

    private bool IsPlacementBlocked(Portal portal, Collider surface, Vector3 center, Quaternion rot, float halfW, float halfH)
    {
        // ��鿡 ���� �پ��ִ� ���� �ڽ��� �ֺ� ħ�� �˻�
        // depth�� ��������, ����/���δ� ��Ż ��ġ�� + ����
        Vector3 halfExtents = new Vector3(halfW + overlapPadding, halfH + overlapPadding, overlapCheckDepth * 0.5f);

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            rot,
            blockingMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Collider c = hits[i];
            if (!c) continue;

            // ���� ǥ�� �ڱ� �ڽ��� ����
            if (c == surface) continue;

            // ��Ż �ڱ� �ݶ��̴� ����(������ ���� ���� �ڽ� ����)
            if (portal.transform == c.transform || c.transform.IsChildOf(portal.transform))
                continue;

            // �ݴ� ��Ż�� ����(��ħ �˻翡 ������ ������)
            if (portal.OtherPortal && (portal.OtherPortal.transform == c.transform || c.transform.IsChildOf(portal.OtherPortal.transform)))
                continue;

            // �� �ܴ� ħ������ �Ǵ�
            return true;
        }

        return false;
    }

    private void GetPortalHalfSizeWorld(BoxCollider portalSurfaceCollider, out float halfW, out float halfH)
    {
        // SurfaceCollider�� local size�� lossyScale�� ���� ũ��� ��ȯ
        Transform t = portalSurfaceCollider.transform;
        Vector3 lossy = t.lossyScale;

        float worldW = Mathf.Abs(portalSurfaceCollider.size.x * lossy.x);
        float worldH = Mathf.Abs(portalSurfaceCollider.size.y * lossy.y);

        halfW = worldW * 0.5f;
        halfH = worldH * 0.5f;
    }

    private bool TryGetProjectionRange(Collider col, Vector3 axis, out float min, out float max)
    {
        // axis�� ����ȭ�Ǿ� ���� �ʿ�� ������, ��ġ �������� ���� normalize ����
        float mag = axis.magnitude;
        if (mag < 1e-6f)
        {
            min = max = 0f;
            return false;
        }
        Vector3 a = axis / mag;

        // BoxCollider�� OBB �ڳʷ� �� ��Ȯ�ϰ�
        if (col is BoxCollider bc)
        {
            Vector3[] corners = GetBoxColliderWorldCorners(bc);
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                float s = Vector3.Dot(a, corners[i]);
                if (s < min) min = s;
                if (s > max) max = s;
            }
            return true;
        }

        // �� �ܴ� bounds(AABB)�� ����(���е� ������ �� ����)
        {
            Vector3[] corners = GetBoundsWorldCorners(col.bounds);
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            for (int i = 0; i < corners.Length; i++)
            {
                float s = Vector3.Dot(a, corners[i]);
                if (s < min) min = s;
                if (s > max) max = s;
            }
            return true;
        }
    }

    private Vector3[] GetBoxColliderWorldCorners(BoxCollider bc)
    {
        Transform t = bc.transform;

        Vector3 c = t.TransformPoint(bc.center);
        Vector3 half = Vector3.Scale(bc.size, t.lossyScale) * 0.5f;

        Vector3 r = t.right * half.x;
        Vector3 u = t.up * half.y;
        Vector3 f = t.forward * half.z;

        // 8 corners
        return new Vector3[]
        {
            c + r + u + f,
            c + r + u - f,
            c + r - u + f,
            c + r - u - f,
            c - r + u + f,
            c - r + u - f,
            c - r - u + f,
            c - r - u - f
        };
    }

    private Vector3[] GetBoundsWorldCorners(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;

        return new Vector3[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };
    }

    private Quaternion ComputeRotationFromSurface(Vector3 surfaceNormal, Transform cam)
    {
        Vector3 forward = surfaceNormal.normalized;

        Vector3 right = Vector3.ProjectOnPlane(cam.right, forward);
        if (right.sqrMagnitude < 1e-6f)
            right = Vector3.ProjectOnPlane(cam.forward, forward);

        right.Normalize();
        Vector3 up = Vector3.Cross(forward, right).normalized;
        right = Vector3.Cross(up, forward).normalized;

        return Quaternion.LookRotation(forward, up);
    }
}
