using System;
using UnityEngine;

public class Portal : MonoBehaviour
{
    [Header("Link")]
    [SerializeField] private Portal linkedPortal;
    public Portal LinkedPortal => linkedPortal;
    public bool IsLinked => linkedPortal != null;

    [Header("Core")]
    [SerializeField] private Transform plane;
    public Transform Plane => plane;

    [Header("Screen")]
    [SerializeField] private PortalScreen screen;
    public PortalScreen Screen => screen;

    [Header("Placement State")]
    [SerializeField] private bool isPlaced;
    public bool IsPlaced => isPlaced;

    [Header("Placement Validation")]
    [SerializeField] private Vector3 halfExtents = new Vector3(0.9f, 1.9f, 0.05f);
    public Vector3 HalfExtents => halfExtents;

    [SerializeField] private Vector3 overlapBoxLocalCenter = new Vector3(0f, 0f, -0.05f);
    public Vector3 OverlapBoxLocalCenter => overlapBoxLocalCenter;

    public event Action Repositioned;

    private void Awake()
    {
        if (!plane) plane = transform;
        if (!screen) screen = GetComponentInChildren<PortalScreen>(true);

        // 시작은 항상 “배치 안 됨 + 검정”
        isPlaced = false;
        screen?.SetLinked(false);
        screen?.SetRenderTexture(null);
    }

    public void LinkTo(Portal other)
    {
        linkedPortal = other;
        // 링크 상태는 PortalSystem/PortalRender 쪽에서 “둘 다 배치됨” 조건을 포함해 판단하는 게 안전함
    }

    public void ClearLink()
    {
        linkedPortal = null;
        screen?.SetLinked(false);
        screen?.SetRenderTexture(null);
    }

    public void SetPlaced(bool placed)
    {
        isPlaced = placed;
        if (!isPlaced)
        {
            screen?.SetLinked(false);
            screen?.SetRenderTexture(null);
        }
    }

    /// <summary>PortalSystem에서 배치 확정 시 호출</summary>
    public void Reposition(Vector3 worldPos, Quaternion worldRot)
    {
        transform.SetPositionAndRotation(worldPos, worldRot);

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        isPlaced = true;
        Repositioned?.Invoke();
    }
}
