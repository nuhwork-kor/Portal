using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ElevatorController : MonoBehaviour
{
    [Header("Stops (World Space)")]
    [Tooltip("엘레베이터가 멈춰야 하는 월드 좌표 지점들. (Stage0, Stage1, Stage2...)")]
    [SerializeField] private Transform[] stopPoints;

    [Tooltip("현재 엘레베이터가 시작하는 정지 지점 인덱스")]
    [SerializeField] private int startStopIndex = 0;

    [Tooltip("RideTrigger를 밟았을 때 이동할 목적지 인덱스")]
    [SerializeField] private int targetStopIndex = 1;

    [System.Serializable]
    private struct DoorLeaf
    {
        public Transform leaf;
        [Tooltip("닫힘 기준에서 열릴 Yaw(도). 예) 왼쪽 +90, 오른쪽 -90")]
        public float openYawDeg;
    }

    [Header("Doors (Rotate Y local)")]
    [SerializeField] private DoorLeaf[] doorLeaves;
    [SerializeField] private float doorCloseDuration = 0.35f;
    [SerializeField] private float doorOpenDuration = 0.55f;

    [Header("Ride")]
    [SerializeField] private float rideDuration = 5.0f;

    [Header("Cabin Root (Moves in World)")]
    [Tooltip("실제로 이동할 엘레베이터 캐빈 루트. 비우면 이 오브젝트(transform)가 이동.")]
    [SerializeField] private Transform cabinRoot;

    [Header("Player Sync (IMPORTANT)")]
    [Tooltip("캐빈이 움직인 델타만큼 플레이어 Rigidbody도 같이 MovePosition (플레이어 조작/중력 유지 가능)")]
    [SerializeField] private bool syncPlayerByCabinDelta = true;

    [Header("Portal Reset (Optional)")]
    [SerializeField] private PortalManager portalResetReceiver;

    [Header("One Shot (Optional)")]
    [Tooltip("한 번 이동 후 다시 못 타게 막기")]
    [SerializeField] private bool lockRideAfterUse = false;

    private bool inRide;
    private bool rideEnabled = true;

    private Quaternion[] doorClosedRot;
    private Quaternion[] doorOpenRot;

    private bool doorIsOpen;
    private Coroutine doorCo;

    private void Awake()
    {
        if (!cabinRoot) cabinRoot = transform;

        CacheDoors();

        // 시작 위치 스냅
        if (stopPoints != null && stopPoints.Length > 0)
        {
            startStopIndex = Mathf.Clamp(startStopIndex, 0, stopPoints.Length - 1);
            SnapCabinToStop(startStopIndex);
        }

        ForceDoorClosed();
    }

    private void CacheDoors()
    {
        if (doorLeaves == null || doorLeaves.Length == 0) return;

        doorClosedRot = new Quaternion[doorLeaves.Length];
        doorOpenRot = new Quaternion[doorLeaves.Length];

        for (int i = 0; i < doorLeaves.Length; i++)
        {
            var leaf = doorLeaves[i].leaf;
            if (!leaf) continue;

            doorClosedRot[i] = leaf.localRotation;
            doorOpenRot[i] = leaf.localRotation * Quaternion.Euler(0f, doorLeaves[i].openYawDeg, 0f);
        }
    }

    private void SnapCabinToStop(int idx)
    {
        if (stopPoints == null || stopPoints.Length == 0) return;
        idx = Mathf.Clamp(idx, 0, stopPoints.Length - 1);
        if (!stopPoints[idx]) return;

        cabinRoot.position = stopPoints[idx].position;
        cabinRoot.rotation = stopPoints[idx].rotation;
    }

    // =========================================================
    // Trigger API (외부 트리거가 호출)
    // =========================================================

    // OpenTrigger Enter: 문 열고 유지
    public void OnEnterTrigger_PlayerEnter(Transform playerRoot)
    {
        if (inRide) return;
        OpenDoor();
    }

    // CloseTrigger Enter: 문 닫고 이동 시작
    public void OnRideTrigger_PlayerEnter(Transform playerRoot)
    {
        if (!rideEnabled) return;
        if (inRide) return;

        if (stopPoints == null || stopPoints.Length == 0) return;

        targetStopIndex = Mathf.Clamp(targetStopIndex, 0, stopPoints.Length - 1);
        if (!stopPoints[targetStopIndex]) return;

        BeginRide(playerRoot);
    }

    private void BeginRide(Transform playerRoot)
    {
        if (inRide) return;
        inRide = true;

        // 포탈 리셋(원하면 끄면 됨)
        portalResetReceiver?.ResetPortals();

        StartCoroutine(CoRide(playerRoot));
    }

    private IEnumerator CoRide(Transform playerRoot)
    {
        // 플레이어는 "제어 안 함" (스크립트 OFF / Kinematic / 속도 0 같은 거 전부 안 함)
        Rigidbody playerRb = null;
        Transform playerTf = null;

        ResolvePlayerRefs(playerRoot, out playerTf, out playerRb);

        // 문 닫기
        yield return CoCloseDoors();
        doorIsOpen = false;

        // 캐빈 이동 + (선택) 플레이어 동기화
        yield return CoMoveCabinToStop_Fixed(targetStopIndex, playerTf, playerRb);

        // 문 열기
        yield return CoOpenDoors();
        doorIsOpen = true;

        // 현재 위치 갱신
        startStopIndex = targetStopIndex;

        inRide = false;

        if (lockRideAfterUse)
            rideEnabled = false;
    }

    private void ResolvePlayerRefs(Transform anyRoot, out Transform playerTf, out Rigidbody playerRb)
    {
        playerTf = null;
        playerRb = null;
        if (!anyRoot) return;

        // 1) PlayerController가 있으면 그 기준
        var pc = anyRoot.GetComponentInChildren<PlayerController>();
        if (pc)
        {
            playerTf = pc.transform;
            playerRb = pc.RB;
        }

        // 2) 없으면 Rigidbody 탐색
        if (!playerRb)
        {
            playerRb = anyRoot.GetComponentInChildren<Rigidbody>();
            if (playerRb) playerTf = playerRb.transform;
        }

        // 3) fallback
        if (!playerTf) playerTf = anyRoot;

        // 경고: PlayerController와 Rigidbody가 서로 다른 오브젝트에 붙어있으면 동기화가 꼬일 수 있음
        if (pc && pc.RB && pc.transform != pc.RB.transform)
        {
            Debug.LogWarning(
                "[ElevatorController] PlayerController와 Rigidbody가 서로 다른 Transform에 있음. " +
                "가능하면 PlayerController가 붙은 오브젝트에 Rigidbody도 같이 두는 걸 추천."
            );
        }
    }

    private IEnumerator CoMoveCabinToStop_Fixed(int idx, Transform playerTf, Rigidbody playerRb)
    {
        if (stopPoints == null || stopPoints.Length == 0) yield break;

        idx = Mathf.Clamp(idx, 0, stopPoints.Length - 1);
        if (!stopPoints[idx]) yield break;

        float dur = Mathf.Max(0.01f, rideDuration);

        Vector3 startPos = cabinRoot.position;
        Quaternion startRot = cabinRoot.rotation;

        Vector3 endPos = stopPoints[idx].position;
        Quaternion endRot = stopPoints[idx].rotation;

        float elapsed = 0f;
        Vector3 prevCabinPos = startPos;

        // 물리/리짓바디랑 엮이니까 FixedUpdate 타이밍에 맞춰서 이동
        while (elapsed < dur)
        {
            yield return new WaitForFixedUpdate();

            float dt = Time.fixedDeltaTime;
            elapsed += dt;

            float t01 = Mathf.Clamp01(elapsed / dur);
            float k = Smooth01(t01);

            Vector3 newCabinPos = Vector3.Lerp(startPos, endPos, k);
            Quaternion newCabinRot = Quaternion.Slerp(startRot, endRot, k);

            // 캐빈 이동
            cabinRoot.position = newCabinPos;
            cabinRoot.rotation = newCabinRot;

            // 캐빈 델타만큼 플레이어도 이동(플레이어는 계속 움직일 수 있음)
            if (syncPlayerByCabinDelta && playerTf)
            {
                Vector3 delta = newCabinPos - prevCabinPos;

                if (playerRb)
                {
                    // 물리적으로 자연스러운 이동(충돌 처리)
                    playerRb.MovePosition(playerRb.position + delta);
                }
                else
                {
                    // RB 없으면 그냥 Transform 이동
                    playerTf.position += delta;
                }
            }

            prevCabinPos = newCabinPos;
        }

        cabinRoot.position = endPos;
        cabinRoot.rotation = endRot;
    }

    // =========================================================
    // Door helpers
    // =========================================================
    private void OpenDoor()
    {
        if (doorIsOpen) return;
        if (inRide) return;

        if (doorCo != null) StopCoroutine(doorCo);
        doorCo = StartCoroutine(CoOpenDoors());
        doorIsOpen = true;
    }

    private void ForceDoorClosed()
    {
        if (doorLeaves == null || doorLeaves.Length == 0) return;
        if (doorClosedRot == null || doorClosedRot.Length != doorLeaves.Length) CacheDoors();

        for (int i = 0; i < doorLeaves.Length; i++)
        {
            var leaf = doorLeaves[i].leaf;
            if (!leaf) continue;
            leaf.localRotation = doorClosedRot[i];
        }

        doorIsOpen = false;
    }

    private IEnumerator CoCloseDoors()
    {
        if (doorLeaves == null || doorLeaves.Length == 0) yield break;
        if (doorClosedRot == null || doorClosedRot.Length != doorLeaves.Length) CacheDoors();

        float dur = Mathf.Max(0.01f, doorCloseDuration);
        float t = 0f;

        Quaternion[] start = new Quaternion[doorLeaves.Length];
        for (int i = 0; i < doorLeaves.Length; i++)
            if (doorLeaves[i].leaf) start[i] = doorLeaves[i].leaf.localRotation;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Smooth01(t);

            for (int i = 0; i < doorLeaves.Length; i++)
            {
                var leaf = doorLeaves[i].leaf;
                if (!leaf) continue;
                leaf.localRotation = Quaternion.Slerp(start[i], doorClosedRot[i], k);
            }
            yield return null;
        }

        for (int i = 0; i < doorLeaves.Length; i++)
            if (doorLeaves[i].leaf) doorLeaves[i].leaf.localRotation = doorClosedRot[i];

        doorCo = null;
    }

    private IEnumerator CoOpenDoors()
    {
        if (doorLeaves == null || doorLeaves.Length == 0) yield break;
        if (doorOpenRot == null || doorOpenRot.Length != doorLeaves.Length) CacheDoors();

        float dur = Mathf.Max(0.01f, doorOpenDuration);
        float t = 0f;

        Quaternion[] start = new Quaternion[doorLeaves.Length];
        for (int i = 0; i < doorLeaves.Length; i++)
            if (doorLeaves[i].leaf) start[i] = doorLeaves[i].leaf.localRotation;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Smooth01(t);

            for (int i = 0; i < doorLeaves.Length; i++)
            {
                var leaf = doorLeaves[i].leaf;
                if (!leaf) continue;
                leaf.localRotation = Quaternion.Slerp(start[i], doorOpenRot[i], k);
            }
            yield return null;
        }

        for (int i = 0; i < doorLeaves.Length; i++)
            if (doorLeaves[i].leaf) doorLeaves[i].leaf.localRotation = doorOpenRot[i];

        doorCo = null;
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
