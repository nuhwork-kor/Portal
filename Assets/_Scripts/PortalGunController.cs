// PortalGunController.cs
using UnityEngine;
using UnityEngine.InputSystem;

public class PortalGunController : MonoBehaviour
{
    [Header("Ray 세팅")]
    [SerializeField] Camera playerCamera;
    [SerializeField] float maxDistance = 100f;

    [Header("Surface Layers")]
    // 너가 말한대로: 바닥+천장 = Ground 레이어
    [SerializeField] LayerMask surfaceMask; // Wall + Ground 포함해서 넣어

    [Header("Placement")]
    [SerializeField] float surfaceOffset = 0.01f;   // z-fighting 방지용
    [SerializeField] float minUpSqrMag = 0.0001f;   // 업벡터 안전장치

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

    void OnLeftFire(InputAction.CallbackContext ctx) => TryFirePortal(isBlue: true);
    void OnRightFire(InputAction.CallbackContext ctx) => TryFirePortal(isBlue: false);

    void TryFirePortal(bool isBlue)
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Ray ray = playerCamera.ScreenPointToRay(screenCenter);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            PlacePortal(hit, isBlue);
        }
    }

    void PlacePortal(RaycastHit hit, bool isBlue)
    {
        Portal portal = isBlue
            ? PortalPool.Instance.GetBluePortal()
            : PortalPool.Instance.GetOrangePortal();

        // 1) 위치: 표면에 딱 붙이면 z-fighting/끼임 생겨서 살짝 띄움
        Vector3 pos = hit.point + hit.normal * surfaceOffset;

        // 2) 회전: forward는 표면 normal (벽/바닥/천장 공통)
        Vector3 forward = -hit.normal.normalized;

        // 3) “세워지는 방향(up)”은 플레이어 카메라 기준으로 만든다
        //    - 바닥/천장에서 특히 이게 없으면 네 스샷처럼 뒤집히고 눕는다.
        Vector3 up = Vector3.ProjectOnPlane(playerCamera.transform.up, forward);

        // up이 너무 작으면(거의 평행) fallback: 카메라 forward를 평면에 투영해서 up으로 사용
        if (up.sqrMagnitude < minUpSqrMag)
            up = Vector3.ProjectOnPlane(playerCamera.transform.forward, forward);

        // 그래도 작으면 마지막 fallback: 월드 업 기준
        if (up.sqrMagnitude < minUpSqrMag)
            up = Vector3.ProjectOnPlane(Vector3.up, forward);

        up.Normalize();

        Quaternion rot = Quaternion.LookRotation(forward, up);

        // 4) 적용
        portal.Reposition(pos, rot);
    }
}
