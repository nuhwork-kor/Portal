using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PortalGunController : MonoBehaviour
{
    [Header("Ray 세팅")]
    [SerializeField] Camera playerCamera;
    [SerializeField] float maxDistance = 100f;
    [SerializeField] LayerMask wallLayer;


    PlayerInput playerInput;
    InputAction leftFireAction;
    InputAction rightFireAction;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        leftFireAction = playerInput.actions["LeftFire"];
        rightFireAction = playerInput.actions["RightFire"];
    }

    private void OnEnable()
    {
        leftFireAction.performed += OnLeftFire;
        rightFireAction.performed += OnRightFire;
    }

    private void OnDisable()
    {
        leftFireAction.performed -= OnLeftFire;
        rightFireAction.performed -= OnRightFire;
    }

    void OnLeftFire(InputAction.CallbackContext ctx)
    {
        TryFirePortal(isBlue : true);
    }

    void OnRightFire(InputAction.CallbackContext ctx)
    {
        TryFirePortal(isBlue : false);
    }

    /// <summary>
    /// 포탈 쏘기
    /// </summary>
    /// <param name="isBlue"></param>
    void TryFirePortal(bool isBlue)
    {
        Vector2 screenCenter = new Vector2(
            Screen.width * 0.5f,
            Screen.height * 0.5f
                );

        Ray ray = playerCamera.ScreenPointToRay(screenCenter);

        if(Physics.Raycast(ray, out RaycastHit hit, maxDistance, wallLayer))
        {
            PlacePortal(hit, isBlue);
        }
    }

    /// <summary>
    /// 포탈 배치
    /// </summary>
    /// <param name="hit"></param>
    /// <param name="isBlue"></param>
    private void PlacePortal(RaycastHit hit, bool isBlue)
    {
        Portal portal = isBlue
            ? PortalPool.Instance.GetBluePortal()
            : PortalPool.Instance.GetOrangePortal();

        Vector3 pos = hit.point;
        Quaternion rot = Quaternion.LookRotation(hit.normal);      //포탈의 forward 방향 = 벽의 바깥쪽

        portal.Reposition(pos, rot);
    }
}
