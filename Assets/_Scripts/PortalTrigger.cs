using UnityEngine;

/// <summary>
/// Collider Event
/// </summary>
public class PortalTrigger : MonoBehaviour
{
    Portal portal;

    private void Awake()
    {
        portal = GetComponentInParent<Portal>();
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        portal.physics.OnEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        portal.physics.OnStay(other);
    }
}
