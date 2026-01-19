using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 이 스크립트는 "압력판(버튼)" 트리거다.
// - activatorMask에 해당하는 오브젝트가 올라오면 IsPressed=true, 내려가면 IsPressed=false
// - 여러 오브젝트가 동시에 올라올 수 있으므로 occupiers(HashSet)로 점유자를 추적한다.
// - 눌림/해제 시 버튼 모델(buttonUp)을 pressDepth만큼 내려/올리는 애니메이션을 수행한다.
// - PressedChanged 이벤트로 외부(DoorButtonGroup 등)에 상태 변화를 알린다.
[DisallowMultipleComponent]
public class PressurePlateTrigger : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform buttonUp;                                       // 움직일 버튼 메쉬/루트(로컬 위치를 이동)

    [Header("Press Motion")]
    [SerializeField] private float pressDepth = 0.06f;                                 // 눌릴 때 내려가는 깊이(로컬 -Y)
    [SerializeField] private float moveDuration = 0.5f;                                // 버튼 이동 애니메이션 시간(초)
    [SerializeField] private bool smoothStep = true;                                   // true면 SmoothStep 곡선으로 보간

    [Header("Activator Filter")]
    [SerializeField] private LayerMask activatorMask = ~0;                             // 버튼을 누를 수 있는 레이어 마스크

    public bool IsPressed { get; private set; }                                        // 현재 눌림 상태(외부 읽기 가능)
    public event Action<PressurePlateTrigger, bool> PressedChanged;                    // 눌림 상태 변경 이벤트(자기 자신, 눌림 여부)

    private Vector3 upLocalPos;                                                        // 버튼의 원래(올라온) 로컬 위치
    private Vector3 downLocalPos;                                                      // 버튼의 눌린(내려간) 로컬 위치

    private readonly HashSet<int> occupiers = new();                                   // 점유자 키 목록(RB 또는 root 기준)
    private Coroutine moveCo;                                                          // 이동 코루틴 핸들(중복 방지)

    /// <summary>
    /// 유니티 생명주기: 버튼 기준 위치를 캐시하고 눌림 위치를 계산한다.
    /// </summary>
    private void Awake()
    {
        if (!buttonUp)                                                                 // 버튼 트랜스폼이 비어있으면
        {
            Debug.LogError($"[PressurePlateTrigger] buttonUp이 비어있음: {name}", this); // 에러 로그
            enabled = false;                                                           // 스크립트 비활성화
            return;                                                                    // 종료
        }

        upLocalPos = buttonUp.localPosition;                                           // 올라온 위치 캐시
        downLocalPos = upLocalPos + Vector3.down * Mathf.Abs(pressDepth);              // 눌린 위치 계산(항상 아래로)
    }

    /// <summary>
    /// 트리거 진입: activatorMask에 해당하면 점유자로 등록하고 눌림 상태를 평가한다.
    /// </summary>
    /// <param name="other">트리거에 들어온 콜라이더</param>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsActivator(other)) return;                                               // 누를 수 있는 대상이 아니면 무시

        int key = GetOccupierKey(other);                                               // 점유자 키 생성
        if (key == 0) return;                                                          // 비정상 키면 무시

        if (occupiers.Add(key))                                                        // 새 점유자가 추가되었으면
            EvaluatePressed();                                                         // 눌림 상태 재평가
    }

    /// <summary>
    /// 트리거 이탈: activatorMask에 해당하면 점유자에서 제거하고 눌림 상태를 평가한다.
    /// </summary>
    /// <param name="other">트리거에서 나간 콜라이더</param>
    private void OnTriggerExit(Collider other)
    {
        if (!IsActivator(other)) return;                                               // 누를 수 있는 대상이 아니면 무시

        int key = GetOccupierKey(other);                                               // 점유자 키 생성
        if (key == 0) return;                                                          // 비정상 키면 무시

        if (occupiers.Remove(key))                                                     // 점유자가 제거되었으면
            EvaluatePressed();                                                         // 눌림 상태 재평가
    }

    /// <summary>
    /// 해당 콜라이더가 버튼을 누를 수 있는 대상인지(레이어 마스크) 판정한다.
    /// </summary>
    /// <param name="other">판정할 콜라이더</param>
    /// <returns>activatorMask에 포함되면 true</returns>
    private bool IsActivator(Collider other)
    {
        int layerBit = 1 << other.gameObject.layer;                                    // 콜라이더 레이어 비트
        if ((activatorMask.value & layerBit) == 0)                                     // 마스크에 없으면
            return false;                                                              // 비활성 대상

        return true;                                                                   // 활성 대상
    }

    /// <summary>
    /// 점유자 식별 키를 만든다.
    /// - Rigidbody가 있으면 Rigidbody 기준(복수 콜라이더를 하나로 묶기)
    /// - 없으면 root Transform 기준(계층 전체를 하나로 묶기)
    /// </summary>
    /// <param name="other">키를 만들 콜라이더</param>
    /// <returns>점유자 키(InstanceID)</returns>
    private int GetOccupierKey(Collider other)
    {
        if (other.attachedRigidbody != null)                                           // RB가 있으면
            return other.attachedRigidbody.GetInstanceID();                            // RB ID를 사용

        return other.transform.root.GetInstanceID();                                   // RB가 없으면 root ID 사용
    }

    /// <summary>
    /// occupiers 개수로 눌림 상태를 결정하고, 변경되면 이동/이벤트/SFX를 처리한다.
    /// </summary>
    private void EvaluatePressed()
    {
        bool newPressed = occupiers.Count > 0;                                         // 점유자가 있으면 눌림
        if (newPressed == IsPressed) return;                                           // 상태 변화가 없으면 종료

        IsPressed = newPressed;                                                        // 눌림 상태 갱신

        // ✅ SFX: 버튼 눌림/원복(둘 다 같은 소리면 OK)
        SoundManager.PlaySFX(SfxId.Button_Interact, worldPos: transform.position);     // 버튼 사운드 재생(3D)

        StartMove(IsPressed ? downLocalPos : upLocalPos);                              // 눌림/해제에 따른 위치로 이동 시작
        PressedChanged?.Invoke(this, IsPressed);                                       // 이벤트 발송(구독자에게 알림)
    }

    /// <summary>
    /// 버튼 모델을 목표 로컬 위치로 이동시키는 코루틴을 시작한다.
    /// </summary>
    /// <param name="targetLocal">목표 로컬 위치</param>
    private void StartMove(Vector3 targetLocal)
    {
        if (moveCo != null) StopCoroutine(moveCo);                                     // 기존 코루틴이 있으면 중지
        moveCo = StartCoroutine(MoveRoutine(targetLocal));                             // 새 코루틴 시작
    }

    /// <summary>
    /// 버튼 모델을 start -> targetLocal로 보간 이동시키는 코루틴.
    /// </summary>
    /// <param name="targetLocal">목표 로컬 위치</param>
    /// <returns>IEnumerator(코루틴)</returns>
    private IEnumerator MoveRoutine(Vector3 targetLocal)
    {
        Vector3 start = buttonUp.localPosition;                                        // 시작 로컬 위치
        float t = 0f;                                                                  // 정규화 시간(0~1)

        float dur = Mathf.Max(0.0001f, moveDuration);                                  // 0 방지로 최소값 보정

        while (t < 1f)                                                                 // 1까지 진행
        {
            t += Time.deltaTime / dur;                                                 // 정규화 진행
            float k = Mathf.Clamp01(t);                                                // 0~1 클램프
            if (smoothStep) k = k * k * (3f - 2f * k);                                 // SmoothStep 보정

            buttonUp.localPosition = Vector3.LerpUnclamped(start, targetLocal, k);     // 위치 보간 적용
            yield return null;                                                         // 다음 프레임까지 대기
        }

        buttonUp.localPosition = targetLocal;                                          // 최종 위치 고정
        moveCo = null;                                                                 // 코루틴 핸들 해제
    }

    /// <summary>
    /// 외부에서 강제로 눌림 상태를 세팅한다(점유자 무시).
    /// - 애니메이션 중지 후 즉시 위치 반영
    /// - SFX + PressedChanged 이벤트 발송
    /// </summary>
    /// <param name="pressed">강제 설정할 눌림 상태</param>
    public void ForceSetPressed(bool pressed)
    {
        occupiers.Clear();                                                             // 점유자 목록 초기화
        IsPressed = pressed;                                                           // 상태 강제 세팅
        if (moveCo != null) StopCoroutine(moveCo);                                     // 이동 코루틴 중지
        buttonUp.localPosition = pressed ? downLocalPos : upLocalPos;                  // 위치 즉시 반영

        SoundManager.PlaySFX(SfxId.Button_Interact, worldPos: transform.position);     // 버튼 사운드 재생(3D)
        PressedChanged?.Invoke(this, IsPressed);                                       // 이벤트 발송
    }
}
