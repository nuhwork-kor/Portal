using System;
using UnityEngine;

public class PortalPool : MonoBehaviour
{
    public static PortalPool Instance {  get; private set; }

    [Header("포탈 프리팹")]
    [SerializeField] Portal bluePortalPrefab;
    [SerializeField] Portal orangePortalPrefab;

    Portal bluePortalInstance;
    Portal orangePortalInstance;

    private void Awake()
    {
        if(Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CreatePortals();
        LinkPortals();
    }

    private void CreatePortals()
    {
        bluePortalInstance = Instantiate(bluePortalPrefab);
        orangePortalInstance = Instantiate(orangePortalPrefab);

        bluePortalInstance.gameObject.SetActive(false);
        orangePortalInstance.gameObject.SetActive(false);
    }

    private void LinkPortals()
    {
        bluePortalInstance.SetLinkedPortal(orangePortalInstance);
        orangePortalInstance.SetLinkedPortal(bluePortalInstance);
    }


    /// <summary>
    /// public API
    /// </summary>
    /// <returns></returns>
    public Portal GetBluePortal() => bluePortalInstance;
    public Portal GetOrangePortal() => orangePortalInstance;
}
