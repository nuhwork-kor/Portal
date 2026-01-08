using UnityEngine;

public class PortalRenderSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera playerCamera;                 // Main Camera
    [SerializeField] private PortalRenderCamera renderCamera;     // PortalRenderCamera 스크립트

    [Header("Portals")]
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("RenderTexture (match Screen aspect 2:3)")]
    // Screen scale X=2, Y=3 -> aspect = 2/3
    // width=1024이면 height=1536이 정확히 2:3 비율
    [SerializeField] private int rtWidth = 1024;
    [SerializeField] private int rtHeight = 1536;
    [SerializeField] private int rtDepth = 24;

    [Header("Performance")]
    [SerializeField] private bool onlyRenderVisibleScreens = true;

    private RenderTexture blueRT;
    private RenderTexture orangeRT;

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!renderCamera) renderCamera = FindAnyObjectByType<PortalRenderCamera>();

        CreateOrResizeRTs();

        // 시작은 무조건 검정
        if (bluePortal?.Screen)
        {
            bluePortal.Screen.SetLinked(false);
            bluePortal.Screen.SetRenderTexture(null);
        }

        if (orangePortal?.Screen)
        {
            orangePortal.Screen.SetLinked(false);
            orangePortal.Screen.SetRenderTexture(null);
        }
    }

    private void OnDestroy()
    {
        ReleaseRT(blueRT);
        ReleaseRT(orangeRT);
    }

    private void LateUpdate()
    {
        // 렌더는 "두 포탈이 배치 + 서로 링크"일 때만
        bool ready =
            bluePortal && orangePortal &&
            bluePortal.IsPlaced && orangePortal.IsPlaced &&
            bluePortal.LinkedPortal == orangePortal &&
            orangePortal.LinkedPortal == bluePortal;

        if (!ready)
        {
            if (bluePortal?.Screen)
            {
                bluePortal.Screen.SetLinked(false);
                bluePortal.Screen.SetRenderTexture(null);
            }

            if (orangePortal?.Screen)
            {
                orangePortal.Screen.SetLinked(false);
                orangePortal.Screen.SetRenderTexture(null);
            }
            return;
        }

        CreateOrResizeRTs();

        // 스크린에 RT 연결
        bluePortal.Screen?.SetLinked(true);
        bluePortal.Screen?.SetRenderTexture(blueRT);

        orangePortal.Screen?.SetLinked(true);
        orangePortal.Screen?.SetRenderTexture(orangeRT);

        // 보이는 포탈만 렌더
        if (!onlyRenderVisibleScreens || IsScreenVisible(bluePortal))
            renderCamera.RenderPortal(bluePortal, orangePortal, blueRT);

        if (!onlyRenderVisibleScreens || IsScreenVisible(orangePortal))
            renderCamera.RenderPortal(orangePortal, bluePortal, orangeRT);
    }

    private bool IsScreenVisible(Portal p)
    {
        if (!p || !p.Screen) return false;
        var r = p.Screen.ScreenRenderer; // PortalScreen에서 노출(아래 수정본 참고)
        return r != null && r.isVisible;
    }

    private void CreateOrResizeRTs()
    {
        if (!blueRT || blueRT.width != rtWidth || blueRT.height != rtHeight)
        {
            ReleaseRT(blueRT);
            blueRT = new RenderTexture(rtWidth, rtHeight, rtDepth, RenderTextureFormat.ARGB32)
            {
                name = "Portal_Blue_RT"
            };
        }

        if (!orangeRT || orangeRT.width != rtWidth || orangeRT.height != rtHeight)
        {
            ReleaseRT(orangeRT);
            orangeRT = new RenderTexture(rtWidth, rtHeight, rtDepth, RenderTextureFormat.ARGB32)
            {
                name = "Portal_Orange_RT"
            };
        }
    }

    private void ReleaseRT(RenderTexture rt)
    {
        if (!rt) return;
        rt.Release();
        Destroy(rt);
    }
}
