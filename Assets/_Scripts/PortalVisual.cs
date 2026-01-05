using Unity.Android.Gradle;
using UnityEngine;

public class PortalVisual : MonoBehaviour
{
    [Header("포탈 비주얼")]
    [SerializeField] GameObject unlinkedPlane;
    [SerializeField] GameObject linkedPlane;

    PortalLinkState currentState;

    private void Awake()
    {
        SetState(PortalLinkState.Unlinked);
    }

    /// <summary>
    /// 포탈 비주얼 세팅
    /// </summary>
    /// <param name="state"></param>
    public void SetState(PortalLinkState state)
    {
        if(currentState == state) return;
        currentState = state;

        switch (state)
        {
            case PortalLinkState.Unlinked:
                unlinkedPlane.SetActive(true);
                linkedPlane.SetActive(false);
                break;

            case PortalLinkState.Linked:
                unlinkedPlane.SetActive(false);
                linkedPlane.SetActive(true);
                break;
        }
    }
}
