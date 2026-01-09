using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class PortalScreen : MonoBehaviour
{
    [Header("Renderer")]
    [SerializeField] private Renderer screenRenderer;
    public Renderer ScreenRenderer => screenRenderer;

    [Header("Unlinked Look")]
    [Tooltip("비워두면 MPB로 검정 처리만 함(추천). 필요하면 검정 머티리얼 넣어도 됨.")]
    [SerializeField] private Material unlinkedMaterial;

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

        // 시작 상태를 확실히 반영 (null 텍스처 SetTexture 안 함)
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

        // 0) 링크 안 됨 OR RT 없음 => 검정
        if (!isLinked || linkedRT == null)
        {
            if (unlinkedMaterial != null)
            {
                // 머티리얼로 검정 처리하고, MPB는 해제(이전 RT override 제거)
                screenRenderer.sharedMaterial = unlinkedMaterial;
                screenRenderer.SetPropertyBlock(null);
                return;
            }

            // 핵심: null 텍스처를 SetTexture로 넣지 말고 "Clear"로 override 제거
            mpb.Clear();
            // 색만 검정으로 (머티리얼이 Unlit/Lit 어떤 거든 일단 어두워짐)
            if (!string.IsNullOrEmpty(colorProperty))
                mpb.SetColor(colorProperty, unlinkedColor);

            screenRenderer.SetPropertyBlock(mpb);
            return;
        }

        // 1) 링크 됨 + RT 있음 => RT 표시
        mpb.Clear();

        // 텍스처 세팅
        if (!string.IsNullOrEmpty(textureProperty))
            mpb.SetTexture(textureProperty, linkedRT);

        // ✅ 색이 검정이면 RT도 검정으로 보일 수 있어서 흰색으로 강제
        if (!string.IsNullOrEmpty(colorProperty))
            mpb.SetColor(colorProperty, Color.white);

        screenRenderer.SetPropertyBlock(mpb);
    }
}
