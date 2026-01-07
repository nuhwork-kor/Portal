using Unity.Android.Gradle;
using UnityEngine;

public class PortalVisual : MonoBehaviour
{
    [Header("포탈 비주얼")]
    public GameObject unlinked;
    public GameObject linked;

    /// <summary>
    /// 포탈 비주얼 세팅
    /// </summary>
    /// <param name="state"></param>
    public void SetLinked(bool value)
    {
        unlinked.SetActive(!value);
        linked.SetActive(value);
    }
}
