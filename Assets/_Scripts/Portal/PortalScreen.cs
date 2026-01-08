using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PortalScreen : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private Renderer screenRenderer;

    [Header("Properties")]
    [SerializeField] private string textureProperty = "_BaseMap";   // URP Lit/Unlit 대부분 _BaseMap
    [SerializeField] private string colorProperty = "_BaseColor"; // URP Lit/Unlit 대부분 _BaseColor

    [Header("Unlinked Look")]
    [SerializeField] private Color unlinkedColor = Color.black;

    private RenderTexture linkedRT;
    private bool isLinked;

    private MaterialPropertyBlock mpb;

    public Renderer ScreenRenderer => screenRenderer;

    private void Awake()
    {
        if (!screenRenderer) screenRenderer = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        Apply();
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

        screenRenderer.GetPropertyBlock(mpb);

        // 링크 안 됨 OR RT 없음 => 검정
        if (!isLinked || linkedRT == null)
        {
            mpb.SetTexture(textureProperty, null);
            mpb.SetColor(colorProperty, unlinkedColor);
            screenRenderer.SetPropertyBlock(mpb);
            return;
        }

        // 링크 됨 + RT 있음 => RT 표시
        mpb.SetTexture(textureProperty, linkedRT);
        // 색은 기본값 유지(흰색이 보통). 원하면 여기서 흰색 고정도 가능.
        screenRenderer.SetPropertyBlock(mpb);
    }
}
