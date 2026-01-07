using UnityEngine;

public class PortalRenderer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Portal portal;
    [SerializeField] Renderer screenRenderer;
    [SerializeField] PortalCamera portalCamera;

    RenderTexture rt;
    MaterialPropertyBlock mpb;

    private void Awake()
    {
        //캐싱
        if (!portal) portal = GetComponentInParent<Portal>();
        if (!portalCamera) portalCamera = Object.FindAnyObjectByType<PortalCamera>();

        //렌더 텍스쳐 생성
        rt = new RenderTexture(1088, 1728, 24)
        {
            name = $"{name}_RT"
        };
        mpb = new MaterialPropertyBlock();

        SetTexture(rt);
    }

    private void LateUpdate()
    {
        if (!portal || !portal.IsLinked) return;

        portalCamera.inPortal = portal;
        portalCamera.outPortal = portal.linkedPortal;
        portalCamera.Render(rt);
    }

    void SetTexture(RenderTexture rt)
    {
        screenRenderer.GetPropertyBlock(mpb);
        mpb.SetTexture("_BaseMap", rt);
        screenRenderer.SetPropertyBlock(mpb);
    }
}
