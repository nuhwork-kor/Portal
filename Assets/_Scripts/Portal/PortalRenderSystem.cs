using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PortalRenderSystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera playerCamera;                 // Main Camera
    [SerializeField] private PortalRenderCamera renderCamera;     // PortalRenderCamera 오브젝트의 스크립트

    [Header("Portals")]
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("RenderTexture")]
    [SerializeField] private int rtWidth = 1024;
    [SerializeField] private int rtHeight = 1024;
    [SerializeField] private int rtDepth = 24;

    [Header("Performance")]
    [SerializeField] private bool onlyRenderVisibleScreens = true;

    private RenderTexture blueRT;
    private RenderTexture orangeRT;

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!renderCamera) renderCamera = FindAnyObjectByType<PortalRenderCamera>();

        // Portal 참조 자동 보완(원하면 수동 할당해도 됨)
        if (!bluePortal || !orangePortal)
        {
            var portals = FindObjectsByType<Portal>(FindObjectsSortMode.None);
            for (int i = 0; i < portals.Length; i++)
            {
                // 네가 Blue/Orange를 어떻게 구분하는지 모르니
                // 여기 자동 구분 로직은 넣지 않고 "둘 다 할당 권장"으로 간다.
            }
        }

        CreateOrResizeRTs();

        // 시작은 무조건 검정
        bluePortal?.Screen?.SetLinked(false);
        bluePortal?.Screen?.SetRenderTexture(null);

        orangePortal?.Screen?.SetLinked(false);
        orangePortal?.Screen?.SetRenderTexture(null);
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnDestroy()
    {
        ReleaseRT(blueRT);
        ReleaseRT(orangeRT);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        // 메인(플레이어) 카메라에서만
        if (cam != playerCamera) return;

        // 포탈 2개가 "배치됨 + 링크됨" 상태여야만 렌더
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

        // 스크린에 RT 연결 + "링크됨" 표시
        bluePortal.Screen?.SetLinked(true);
        bluePortal.Screen?.SetRenderTexture(blueRT);

        orangePortal.Screen?.SetLinked(true);
        orangePortal.Screen?.SetRenderTexture(orangeRT);

        // 보이는 포탈만 렌더(성능)
        if (!onlyRenderVisibleScreens || IsScreenVisible(bluePortal))
        {
            renderCamera.RenderPortal(bluePortal, orangePortal, blueRT);
        }

        if (!onlyRenderVisibleScreens || IsScreenVisible(orangePortal))
        {
            renderCamera.RenderPortal(orangePortal, bluePortal, orangeRT);
        }
    }

    private bool IsScreenVisible(Portal p)
    {
        if (!p || !p.Screen) return false;

        // 네 PortalScreen에 Renderer를 노출해두면 가장 깔끔함.
        // (PortalScreen.ScreenRenderer 프로퍼티를 만들어두면 여기서 바로 접근 가능)
        var r = p.Screen.GetComponent<Renderer>();
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
