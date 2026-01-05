using UnityEngine;

public class PortalTrigger : MonoBehaviour
{
    Portal portal;

    private void Awake()
    {
        portal = GetComponentInParent<Portal>();
    }

    private void OnTriggerEnter(Collider other)
    {
        portal.HandleTriggerEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        portal.HandleTriggerStay(other);
    }

    private void OnTriggerExit(Collider other)
    {
        portal.HandleTriggerExit(other);
    }
}
