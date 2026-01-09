using UnityEngine;
using UnityEngine.Rendering;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;

public class PortalRender : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PortalRenderCamera renderCamera;

    [Header("Portals")]
    [SerializeField] private Portal bluePortal;
    [SerializeField] private Portal orangePortal;

    [Header("RenderTexture")]
    [SerializeField] private int rtWidth = 1024;
    [SerializeField] private int rtHeight = 1024;
    [SerializeField] private int rtDepth = 24;

    private RenderTexture blueRT;
    private RenderTexture orangeRT;

    private void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
        if (!renderCamera) renderCamera = FindAnyObjectByType<PortalRenderCamera>();

        if (!bluePortal || !orangePortal)
        {
            Debug.LogError("[PortalRender] Missing portal refs. Assign Blue/Orange portals.");
            enabled = false;
            return;
        }

        CreateOrResizeRTs();

        // 시작은 검정(닫힘)
        bluePortal.Screen?.SetLinked(false);
        orangePortal.Screen?.SetLinked(false);
    }

    private void OnEnable()
    {
        RenderPipeline.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipeline.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnDestroy()
    {
        ReleaseRT(blueRT);
        ReleaseRT(orangeRT);
    }

    // ★ beginCameraRendering 델리게이트 시그니처는 반드시 이 형태여야 함
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        // 메인 카메라에서만 실행
        if (cam != playerCamera) return;

        // 렌더카메라가 playerCamera를 참조하도록 보정(인스펙터 누락 안전장치)
        if (renderCamera) renderCamera.SetPlayerCamera(playerCamera);

        // 둘 다 배치 + 링크되어야 렌더 가능
        bool ready =
            bluePortal.IsPlaced && orangePortal.IsPlaced &&
            bluePortal.LinkedPortal == orangePortal &&
            orangePortal.LinkedPortal == bluePortal;

        if (!ready)
        {
            bluePortal.Screen?.SetLinked(false);
            orangePortal.Screen?.SetLinked(false);
            return;
        }

        // RT 준비
        CreateOrResizeRTs();

        // 각 포탈 스크린에 RT 연결 + “링크 상태” 표시
        bluePortal.Screen?.SetLinked(true);
        bluePortal.Screen?.SetRenderTexture(blueRT);

        orangePortal.Screen?.SetLinked(true);
        orangePortal.Screen?.SetRenderTexture(orangeRT);

        // “보이는 포탈만” 렌더 (성능)
        if (bluePortal.Screen != null && bluePortal.Screen.ScreenRenderer != null && bluePortal.Screen.ScreenRenderer.isVisible)
        {
            renderCamera.RenderPortal(bluePortal, orangePortal, blueRT);
        }

        if (orangePortal.Screen != null && orangePortal.Screen.ScreenRenderer != null && orangePortal.Screen.ScreenRenderer.isVisible)
        {
            renderCamera.RenderPortal(orangePortal, bluePortal, orangeRT);
        }
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
