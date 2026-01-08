using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PortalScreen : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private Renderer screenRenderer;
    public Renderer ScreenRenderer => screenRenderer;

    [Header("Unlinked Look")]
    [SerializeField] private Material unlinkedMaterial; // 있으면 이걸로 "검정" 처리

    [Header("Properties")]
    [SerializeField] private string textureProperty = "_BaseMap";
    [SerializeField] private string colorProperty = "_BaseColor";
    [SerializeField] private Color unlinkedColor = Color.black;

    private RenderTexture linkedRT;
    private bool isLinked;
    private MaterialPropertyBlock mpb;

    private void Awake()
    {
        if (!screenRenderer) screenRenderer = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    public void SetLinked(bool value)
    {
        isLinked = value;
        Apply();
    }

    public void SetRenderTexture(RenderTexture rt)
    {
        linkedRT = rt;
        Apply();
    }

    private void Apply()
    {
        if (!screenRenderer) return;

        // 링크 안 됨 or RT 없음 -> 검정
        if (!isLinked || linkedRT == null)
        {
            if (unlinkedMaterial != null)
            {
                screenRenderer.sharedMaterial = unlinkedMaterial;
                return;
            }

            // 머티리얼 교체 없이 색/텍스처만 처리
            screenRenderer.GetPropertyBlock(mpb);
            mpb.SetTexture(textureProperty, null);
            mpb.SetColor(colorProperty, unlinkedColor);
            screenRenderer.SetPropertyBlock(mpb);
            return;
        }

        // 링크 됨 + RT 있음 -> RT 표시
        screenRenderer.GetPropertyBlock(mpb);
        mpb.SetTexture(textureProperty, linkedRT);
        screenRenderer.SetPropertyBlock(mpb);
    }
}
