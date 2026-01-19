using System.Collections;
using UnityEngine;

// 이 스크립트는 "엘리베이터 1개"가 여러 정차 지점(StopPoint) 사이를 이동하고,
// 문(회전식 DoorLeaf)을 닫았다가 이동 후 다시 여는 흐름을 담당함.
// 옵션으로 "캐빈 이동 델타"만큼 플레이어도 같이 MovePosition 해서 속도 불일치(미끄러짐)를 막고,
// (선택) 포탈 리셋, (선택) 1회 사용 후 잠금도 지원함.
[DisallowMultipleComponent]
public class ElevatorController : MonoBehaviour
{
    [Header("Stops (World Space)")]
    [Tooltip("엘리베이터가 정차할 월드 좌표 포인트들. (Stage0, Stage1, Stage2...)")]
    [SerializeField] private Transform[] stopPoints; // 정차 지점 목록(월드 좌표)

    [Tooltip("시작 시 엘리베이터가 위치할 정차 지점 인덱스")]
    [SerializeField] private int startStopIndex = 0; // 시작 정차 인덱스

    [Tooltip("RideTrigger가 발동되면 이동할 목표 정차 지점 인덱스")]
    [SerializeField] private int targetStopIndex = 1; // 탑승 트리거 발동 시 목표 정차 인덱스

    [System.Serializable]
    private struct DoorLeaf
    {
        public Transform leaf;                  // 문짝 Transform(로컬 Y 회전으로 여닫음)
        [Tooltip("문을 열 때 추가할 로컬 Yaw(도). 예) 왼쪽문 +90, 오른쪽문 -90")]
        public float openYawDeg;                // 문 열림 각도(Y축, 도)
    }

    [Header("Doors (Rotate Y local)")]
    [SerializeField] private DoorLeaf[] doorLeaves;       // 문짝 배열
    [SerializeField] private float doorCloseDuration = 0.35f; // 문 닫는 시간(초)
    [SerializeField] private float doorOpenDuration = 0.55f;  // 문 여는 시간(초)

    [Header("Ride")]
    [SerializeField] private float rideDuration = 5.0f;   // 엘리베이터 이동 시간(초)

    [Header("Cabin Root (Moves in World)")]
    [Tooltip("실제로 이동하는 캐빈 루트. 비어있으면 이 오브젝트(transform)가 이동.")]
    [SerializeField] private Transform cabinRoot;          // 이동 대상 Transform(캐빈 루트)

    [Header("Player Sync (IMPORTANT)")]
    [Tooltip("캐빈 이동 델타만큼 플레이어 Rigidbody를 MovePosition(속도 불일치/미끄러짐 방지).")]
    [SerializeField] private bool syncPlayerByCabinDelta = true; // 캐빈 델타로 플레이어 동기화 여부

    [Header("Portal Reset (Optional)")]
    [SerializeField] private PortalManager portalResetReceiver; // (옵션) 탑승 시작 시 포탈 리셋 수신자

    [Header("One Shot (Optional)")]
    [Tooltip("한 번 이동 후 다시 못 타게 잠금")]
    [SerializeField] private bool lockRideAfterUse = false; // (옵션) 1회 사용 후 잠금

    private bool inRide;                        // 현재 이동(탑승 시퀀스) 중인지
    private bool rideEnabled = true;            // 탑승 가능 여부(1회 잠금용)

    private Quaternion[] doorClosedRot;         // 문 닫힘 로컬 회전 캐시
    private Quaternion[] doorOpenRot;           // 문 열림 로컬 회전 캐시

    private bool doorIsOpen;                    // 현재 문이 열려있는지
    private Coroutine doorCo;                   // 문 여닫기 코루틴 핸들

    /// <summary>
    /// 초기 레퍼런스/캐시를 구성하고, 시작 정차 지점으로 스냅한 뒤 문을 닫힌 상태로 강제 세팅한다.
    /// </summary>
    private void Awake()
    {
        if (!cabinRoot) cabinRoot = transform;                  // cabinRoot 미지정이면 자기 자신을 캐빈 루트로 사용

        CacheDoors();                                           // 문 닫힘/열림 회전값을 캐시

        // 시작 위치 스냅(정차 지점이 존재할 때만)                          // (안전 체크)
        if (stopPoints != null && stopPoints.Length > 0)        // stopPoints가 있고 1개 이상이면
        {
            startStopIndex = Mathf.Clamp(startStopIndex, 0, stopPoints.Length - 1); // 시작 인덱스를 범위 내로 제한
            SnapCabinToStop(startStopIndex);                     // 시작 정차 지점으로 캐빈 위치/회전 스냅
        }

        ForceDoorClosed();                                      // 시작 시 문을 닫힌 상태로 강제 적용
    }

    /// <summary>
    /// doorLeaves 기준으로 "닫힘 회전"과 "열림 회전"을 미리 계산/저장한다.
    /// </summary>
    private void CacheDoors()
    {
        if (doorLeaves == null || doorLeaves.Length == 0) return;   // 문 정보가 없으면 종료

        doorClosedRot = new Quaternion[doorLeaves.Length];          // 닫힘 회전 배열 생성
        doorOpenRot = new Quaternion[doorLeaves.Length];            // 열림 회전 배열 생성

        for (int i = 0; i < doorLeaves.Length; i++)                 // 문짝 개수만큼 반복
        {
            var leaf = doorLeaves[i].leaf;                          // i번째 문짝 Transform 가져오기
            if (!leaf) continue;                                    // 없으면 스킵

            doorClosedRot[i] = leaf.localRotation;                  // 현재 로컬 회전을 "닫힘"으로 저장
            doorOpenRot[i] = leaf.localRotation * Quaternion.Euler(0f, doorLeaves[i].openYawDeg, 0f); // 닫힘 + openYawDeg 만큼 회전 = 열림 회전
        }
    }

    /// <summary>
    /// 지정한 정차 지점(idx)으로 캐빈을 즉시 스냅한다(월드 위치/회전).
    /// </summary>
    /// <param name="idx">정차 지점 인덱스</param>
    private void SnapCabinToStop(int idx)
    {
        if (stopPoints == null || stopPoints.Length == 0) return;   // 정차 지점이 없으면 종료
        idx = Mathf.Clamp(idx, 0, stopPoints.Length - 1);           // 인덱스 범위를 제한
        if (!stopPoints[idx]) return;                               // 해당 포인트 Transform이 없으면 종료

        cabinRoot.position = stopPoints[idx].position;              // 캐빈 위치를 정차 지점 위치로 스냅
        cabinRoot.rotation = stopPoints[idx].rotation;              // 캐빈 회전을 정차 지점 회전으로 스냅
    }

    // =========================================================
    // Trigger API (외부 트리거가 호출)
    // =========================================================

    /// <summary>
    /// "입구 트리거(EnterTrigger)"에서 플레이어가 들어오면 호출된다.
    /// 문을 열어 플레이어가 캐빈에 진입할 수 있게 한다.
    /// </summary>
    /// <param name="playerRoot">플레이어 루트(트리거 쪽에서 전달)</param>
    public void OnEnterTrigger_PlayerEnter(Transform playerRoot)
    {
        if (inRide) return;                                         // 이동 중이면 무시
        OpenDoor();                                                 // 문 열기
    }

    /// <summary>
    /// "탑승 트리거(RideTrigger)"에서 플레이어가 들어오면 호출된다.
    /// 문을 닫고, 캐빈을 목표 정차 지점으로 이동한 후 문을 연다.
    /// </summary>
    /// <param name="playerRoot">플레이어 루트(트리거 쪽에서 전달)</param>
    public void OnRideTrigger_PlayerEnter(Transform playerRoot)
    {
        if (!rideEnabled) return;                                   // 1회 사용 잠금 상태면 무시
        if (inRide) return;                                         // 이미 이동 중이면 무시
        if (stopPoints == null || stopPoints.Length == 0) return;   // 정차 지점이 없으면 불가

        targetStopIndex = Mathf.Clamp(targetStopIndex, 0, stopPoints.Length - 1); // 목표 인덱스 제한
        if (!stopPoints[targetStopIndex]) return;                   // 목표 포인트가 없으면 불가

        BeginRide(playerRoot);                                      // 탑승 시퀀스 시작
    }

    /// <summary>
    /// 탑승(이동) 시퀀스를 시작한다. (옵션) 포탈 리셋 후 코루틴을 실행한다.
    /// </summary>
    /// <param name="playerRoot">플레이어 루트</param>
    private void BeginRide(Transform playerRoot)
    {
        if (inRide) return;                                         // 중복 시작 방지
        inRide = true;                                              // 이동 중 플래그 ON

        portalResetReceiver?.ResetPortals();                         // (옵션) 포탈 리셋 호출(있으면)

        StartCoroutine(CoRide(playerRoot));                          // 탑승/이동 코루틴 실행
    }

    /// <summary>
    /// 탑승 시퀀스 전체 흐름:
    /// 1) 플레이어 Transform/Rigidbody 참조 확보
    /// 2) 문 닫기
    /// 3) 캐빈 이동(필요 시 플레이어를 캐빈 델타로 동기화)
    /// 4) 문 열기
    /// 5) 상태 업데이트(현재 정차 인덱스 갱신, 1회 잠금 처리)
    /// </summary>
    /// <param name="playerRoot">플레이어 루트</param>
    private IEnumerator CoRide(Transform playerRoot)
    {
        Rigidbody playerRb = null;                                  // 플레이어 Rigidbody(있으면 MovePosition에 사용)
        Transform playerTf = null;                                   // 플레이어 Transform(없으면 Transform 이동 fallback)

        ResolvePlayerRefs(playerRoot, out playerTf, out playerRb);   // 플레이어 참조를 안전하게 찾기

        yield return CoCloseDoors();                                 // 문 닫기 코루틴 완료까지 대기
        doorIsOpen = false;                                          // 문 상태 플래그 갱신

        yield return CoMoveCabinToStop_Fixed(targetStopIndex, playerTf, playerRb); // 캐빈 이동(고정 타임스텝 기반)

        yield return CoOpenDoors();                                  // 문 열기 코루틴 완료까지 대기
        doorIsOpen = true;                                           // 문 상태 플래그 갱신

        startStopIndex = targetStopIndex;                            // "현재 정차 지점"을 목표 지점으로 갱신
        inRide = false;                                              // 이동 종료 플래그 OFF

        if (lockRideAfterUse) rideEnabled = false;                   // (옵션) 1회 사용 후 탑승 잠금
    }

    /// <summary>
    /// 트리거가 전달한 Transform(anyRoot)로부터 플레이어 Transform/Rigidbody를 최대한 일관되게 찾는다.
    /// 우선순위: PlayerController -> Rigidbody -> Transform fallback
    /// </summary>
    /// <param name="anyRoot">트리거가 넘긴 플레이어 루트 후보</param>
    /// <param name="playerTf">결정된 플레이어 Transform</param>
    /// <param name="playerRb">결정된 플레이어 Rigidbody(없을 수 있음)</param>
    private void ResolvePlayerRefs(Transform anyRoot, out Transform playerTf, out Rigidbody playerRb)
    {
        playerTf = null;                                             // out 초기화
        playerRb = null;                                             // out 초기화
        if (!anyRoot) return;                                        // 입력이 없으면 종료

        var pc = anyRoot.GetComponentInChildren<PlayerController>(); // PlayerController가 있으면 최우선으로 사용
        if (pc)                                                      // PlayerController가 발견되면
        {
            playerTf = pc.transform;                                 // PlayerController Transform을 플레이어 Transform으로 사용
            playerRb = pc.RB;                                        // PlayerController가 들고 있는 RB 참조 사용(프로젝트 구조에 따라)
        }

        if (!playerRb)                                               // 아직 Rigidbody를 못 찾았으면
        {
            playerRb = anyRoot.GetComponentInChildren<Rigidbody>();  // 자식에서 Rigidbody 탐색
            if (playerRb) playerTf = playerRb.transform;             // Rigidbody가 있으면 그 Transform을 사용
        }

        if (!playerTf) playerTf = anyRoot;                           // 끝까지 못 찾으면 anyRoot를 Transform으로 사용

        if (pc && pc.RB && pc.transform != pc.RB.transform)          // PlayerController와 Rigidbody가 서로 다른 Transform이면
        {
            Debug.LogWarning(                                        // 구조가 복잡하면 동기화가 애매해질 수 있으니 경고
                "[ElevatorController] PlayerController와 Rigidbody가 서로 다른 Transform에 있음. " +
                "가능하면 PlayerController가 붙은 동일 오브젝트에 Rigidbody를 두는 걸 추천."
            );
        }
    }

    /// <summary>
    /// 캐빈을 목표 정차 지점으로 이동한다.
    /// 핵심: FixedUpdate 타이밍(WaitForFixedUpdate)으로 진행하여 물리(플레이어 MovePosition)와 싱크를 맞춘다.
    /// </summary>
    /// <param name="idx">목표 정차 지점 인덱스</param>
    /// <param name="playerTf">플레이어 Transform(없을 수 있음)</param>
    /// <param name="playerRb">플레이어 Rigidbody(있으면 MovePosition)</param>
    private IEnumerator CoMoveCabinToStop_Fixed(int idx, Transform playerTf, Rigidbody playerRb)
    {
        if (stopPoints == null || stopPoints.Length == 0) yield break;      // 정차 지점이 없으면 중단

        idx = Mathf.Clamp(idx, 0, stopPoints.Length - 1);                   // 인덱스 제한
        if (!stopPoints[idx]) yield break;                                   // 목표 포인트가 없으면 중단

        float dur = Mathf.Max(0.01f, rideDuration);                          // 이동 시간 최소값 보정

        Vector3 startPos = cabinRoot.position;                               // 이동 시작 위치
        Quaternion startRot = cabinRoot.rotation;                             // 이동 시작 회전

        Vector3 endPos = stopPoints[idx].position;                           // 이동 목표 위치
        Quaternion endRot = stopPoints[idx].rotation;                         // 이동 목표 회전

        float elapsed = 0f;                                                  // 경과 시간
        Vector3 prevCabinPos = startPos;                                     // 이전 프레임 캐빈 위치(델타 계산용)

        while (elapsed < dur)                                                // 이동 시간이 끝날 때까지 반복
        {
            yield return new WaitForFixedUpdate();                            // 물리 스텝 타이밍에 맞춰 진행

            float dt = Time.fixedDeltaTime;                                  // 물리 프레임 dt
            elapsed += dt;                                                   // 경과 시간 누적

            float t01 = Mathf.Clamp01(elapsed / dur);                         // 0~1 보간 값
            float k = Smooth01(t01);                                         // 스무스 보간(가속/감속)

            Vector3 newCabinPos = Vector3.Lerp(startPos, endPos, k);         // 위치 보간
            Quaternion newCabinRot = Quaternion.Slerp(startRot, endRot, k);  // 회전 보간

            cabinRoot.position = newCabinPos;                                // 캐빈 위치 적용(월드)
            cabinRoot.rotation = newCabinRot;                                // 캐빈 회전 적용(월드)

            if (syncPlayerByCabinDelta && playerTf)                          // 옵션 ON + 플레이어 Transform이 있으면
            {
                Vector3 delta = newCabinPos - prevCabinPos;                  // 캐빈 이동 델타(이만큼 플레이어도 이동)

                if (playerRb)                                                // Rigidbody가 있으면
                {
                    playerRb.MovePosition(playerRb.position + delta);        // 물리 친화적인 이동(충돌 처리 포함)
                }
                else                                                        // Rigidbody가 없으면
                {
                    playerTf.position += delta;                              // Transform 직접 이동(충돌/물리 보장은 약함)
                }
            }

            prevCabinPos = newCabinPos;                                      // 다음 델타 계산을 위해 이전 위치 갱신
        }

        cabinRoot.position = endPos;                                         // 종료 위치 스냅(오차 제거)
        cabinRoot.rotation = endRot;                                         // 종료 회전 스냅(오차 제거)
    }

    // =========================================================
    // Door helpers
    // =========================================================

    /// <summary>
    /// 문을 연다. (이미 열림/이동 중이면 무시)
    /// </summary>
    private void OpenDoor()
    {
        if (doorIsOpen) return;                                              // 이미 열려있으면 종료
        if (inRide) return;                                                  // 이동 중이면 문 조작 금지

        if (doorCo != null) StopCoroutine(doorCo);                           // 진행 중 문 코루틴이 있으면 중지
        doorCo = StartCoroutine(CoOpenDoors());                              // 문 열기 코루틴 시작
        doorIsOpen = true;                                                   // 문 상태 갱신
    }

    /// <summary>
    /// 문을 즉시 닫힌 상태로 강제 적용한다(코루틴 없이 스냅).
    /// </summary>
    private void ForceDoorClosed()
    {
        if (doorLeaves == null || doorLeaves.Length == 0) return;            // 문이 없으면 종료
        if (doorClosedRot == null || doorClosedRot.Length != doorLeaves.Length) CacheDoors(); // 캐시가 없으면 생성

        for (int i = 0; i < doorLeaves.Length; i++)                          // 문짝 전부 순회
        {
            var leaf = doorLeaves[i].leaf;                                   // 문짝 Transform
            if (!leaf) continue;                                             // 없으면 스킵
            leaf.localRotation = doorClosedRot[i];                           // 닫힘 회전으로 스냅
        }

        doorIsOpen = false;                                                  // 문 상태 갱신
    }

    /// <summary>
    /// 문을 닫는 애니메이션 코루틴.
    /// </summary>
    private IEnumerator CoCloseDoors()
    {
        if (doorLeaves == null || doorLeaves.Length == 0) yield break;       // 문이 없으면 종료
        if (doorClosedRot == null || doorClosedRot.Length != doorLeaves.Length) CacheDoors(); // 캐시 보장

        float dur = Mathf.Max(0.01f, doorCloseDuration);                     // 시간 최소값 보정
        float t = 0f;                                                        // 진행도(0~1)

        Quaternion[] start = new Quaternion[doorLeaves.Length];              // 시작 회전 배열
        for (int i = 0; i < doorLeaves.Length; i++)                          // 문짝별 시작 회전 저장
        {
            if (doorLeaves[i].leaf) start[i] = doorLeaves[i].leaf.localRotation; // 현재 로컬 회전 저장
        }

        while (t < 1f)                                                       // 0~1까지 진행
        {
            t += Time.deltaTime / dur;                                       // deltaTime 기반 진행도 증가
            float k = Smooth01(t);                                           // 스무스 보간

            for (int i = 0; i < doorLeaves.Length; i++)                      // 각 문짝에 적용
            {
                var leaf = doorLeaves[i].leaf;                               // 문짝 Transform
                if (!leaf) continue;                                         // 없으면 스킵
                leaf.localRotation = Quaternion.Slerp(start[i], doorClosedRot[i], k); // 시작 -> 닫힘 회전 보간
            }

            yield return null;                                               // 다음 프레임까지 대기
        }

        for (int i = 0; i < doorLeaves.Length; i++)                          // 마지막에 정확히 스냅
        {
            if (doorLeaves[i].leaf) doorLeaves[i].leaf.localRotation = doorClosedRot[i]; // 닫힘 회전 스냅
        }

        doorCo = null;                                                       // 코루틴 핸들 해제
    }

    /// <summary>
    /// 문을 여는 애니메이션 코루틴.
    /// </summary>
    private IEnumerator CoOpenDoors()
    {
        if (doorLeaves == null || doorLeaves.Length == 0) yield break;       // 문이 없으면 종료
        if (doorOpenRot == null || doorOpenRot.Length != doorLeaves.Length) CacheDoors(); // 캐시 보장

        float dur = Mathf.Max(0.01f, doorOpenDuration);                      // 시간 최소값 보정
        float t = 0f;                                                        // 진행도(0~1)

        Quaternion[] start = new Quaternion[doorLeaves.Length];              // 시작 회전 배열
        for (int i = 0; i < doorLeaves.Length; i++)                          // 문짝별 시작 회전 저장
        {
            if (doorLeaves[i].leaf) start[i] = doorLeaves[i].leaf.localRotation; // 현재 로컬 회전 저장
        }

        while (t < 1f)                                                       // 0~1까지 진행
        {
            t += Time.deltaTime / dur;                                       // deltaTime 기반 진행도 증가
            float k = Smooth01(t);                                           // 스무스 보간

            for (int i = 0; i < doorLeaves.Length; i++)                      // 각 문짝에 적용
            {
                var leaf = doorLeaves[i].leaf;                               // 문짝 Transform
                if (!leaf) continue;                                         // 없으면 스킵
                leaf.localRotation = Quaternion.Slerp(start[i], doorOpenRot[i], k); // 시작 -> 열림 회전 보간
            }

            yield return null;                                               // 다음 프레임까지 대기
        }

        for (int i = 0; i < doorLeaves.Length; i++)                          // 마지막에 정확히 스냅
        {
            if (doorLeaves[i].leaf) doorLeaves[i].leaf.localRotation = doorOpenRot[i]; // 열림 회전 스냅
        }

        doorCo = null;                                                       // 코루틴 핸들 해제
    }

    /// <summary>
    /// 0~1 구간 스무스스텝 보간 값을 반환한다. (t*t*(3-2t))
    /// </summary>
    /// <param name="t">0~1 입력</param>
    /// <returns>스무딩된 0~1 출력</returns>
    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);                                                // 범위 제한
        return t * t * (3f - 2f * t);                                        // SmoothStep 수식
    }
}
