using System.Collections.Generic;
using UnityEngine;

public class PortalTraveller : MonoBehaviour
{
    public Rigidbody rb { get; private set; }
    public Collider[] c {  get; private set; }

    //현재 Ignore 중인 벽들
    HashSet<Collider> ignoreWalls = new HashSet<Collider>();

    private void Awake()
    {
        rb= GetComponent<Rigidbody>();
        c = GetComponentsInChildren<Collider>();
    }

    public void IgnoreWall(Collider Wall, bool ignore)
    {
        foreach(var col in c)
        {
            Physics.IgnoreCollision(col, Wall, ignore);
        }

        if (ignore) ignoreWalls.Add(Wall);
        else ignoreWalls.Remove(Wall);

        Debug.Log($"[Portal] IgnoreWalls Count: {ignoreWalls.Count}");
        foreach (var w in ignoreWalls)
        {
            Debug.Log($"Ignored Wall: {w.name}");
        }
    }

    public void RestoreAllWalls()
    {
        foreach (var wall in ignoreWalls)
        {
            foreach (var col in c)
            {
                Physics.IgnoreCollision(col, wall, false);
            }
        }

        ignoreWalls.Clear();
    }
}
