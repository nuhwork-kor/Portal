// PressurePlateTrigger.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PressurePlateTrigger : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("눌려서 내려갈 '빨간 원판' 오브젝트(visual)")]
    [SerializeField] private Transform buttonUp;

    [Header("Press Motion")]
    [Tooltip("얼마나 아래로 내려갈지 (local Y 기준, 양수 입력)")]
    [SerializeField] private float pressDepth = 0.06f;

    [Tooltip("내려가고 올라오는 시간(초)")]
    [SerializeField] private float moveDuration = 0.5f;

    [Tooltip("눌림/해제를 부드럽게(곡선)")]
    [SerializeField] private bool smoothStep = true;

    [Header("Activator Filter")]
    [Tooltip("이 레이어만 버튼을 누를 수 있음 (Player, Interactable 등)")]
    [SerializeField] private LayerMask activatorMask = ~0;

    public bool IsPressed { get; private set; }

    // 버튼 상태 변화를 외부(문)에서 구독
    public event Action<PressurePlateTrigger, bool> PressedChanged;

    private Vector3 upLocalPos;
    private Vector3 downLocalPos;

    // 여러 콜라이더/복수 접촉에도 “오브젝트 단위”로 1개만 카운트
    private readonly HashSet<int> occupiers = new();
    private Coroutine moveCo;

    private void Awake()
    {
        if (!buttonUp)
        {
            Debug.LogError($"[PressurePlateTrigger] buttonUp이 비어있음: {name}", this);
            enabled = false;
            return;
        }

        upLocalPos = buttonUp.localPosition;
        downLocalPos = upLocalPos + Vector3.down * Mathf.Abs(pressDepth);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsActivator(other)) return;

        int key = GetOccupierKey(other);
        if (key == 0) return;

        if (occupiers.Add(key))
            EvaluatePressed();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsActivator(other)) return;

        int key = GetOccupierKey(other);
        if (key == 0) return;

        if (occupiers.Remove(key))
            EvaluatePressed();
    }

    private bool IsActivator(Collider other)
    {
        // 레이어 필터
        int layerBit = 1 << other.gameObject.layer;
        if ((activatorMask.value & layerBit) == 0)
            return false;

        // “밟는” 대상은 보통 Rigidbody가 있음(플레이어/큐브)
        // Rigidbody 없으면 무시하고 싶으면 아래 주석 해제
        // if (other.attachedRigidbody == null) return false;

        return true;
    }

    // 같은 오브젝트가 콜라이더 여러 개여도 1개로 카운트되게 “대표 키”를 만든다.
    private int GetOccupierKey(Collider other)
    {
        // Rigidbody가 있으면 그 Rigidbody 기준(가장 안정적)
        if (other.attachedRigidbody != null)
            return other.attachedRigidbody.GetInstanceID();

        // Rigidbody 없으면 루트 Transform 기준
        return other.transform.root.GetInstanceID();
    }

    private void EvaluatePressed()
    {
        bool newPressed = occupiers.Count > 0;
        if (newPressed == IsPressed) return;

        IsPressed = newPressed;

        // 비주얼 이동
        StartMove(IsPressed ? downLocalPos : upLocalPos);

        // 이벤트 발송
        PressedChanged?.Invoke(this, IsPressed);
    }

    private void StartMove(Vector3 targetLocal)
    {
        if (moveCo != null) StopCoroutine(moveCo);
        moveCo = StartCoroutine(MoveRoutine(targetLocal));
    }

    private IEnumerator MoveRoutine(Vector3 targetLocal)
    {
        Vector3 start = buttonUp.localPosition;
        float t = 0f;

        float dur = Mathf.Max(0.0001f, moveDuration);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Mathf.Clamp01(t);
            if (smoothStep) k = k * k * (3f - 2f * k); // SmoothStep

            buttonUp.localPosition = Vector3.LerpUnclamped(start, targetLocal, k);
            yield return null;
        }

        buttonUp.localPosition = targetLocal;
        moveCo = null;
    }

    // 문에서 강제로 초기화하고 싶을 때 쓸 수도 있음
    public void ForceSetPressed(bool pressed)
    {
        occupiers.Clear();
        IsPressed = pressed;
        if (moveCo != null) StopCoroutine(moveCo);
        buttonUp.localPosition = pressed ? downLocalPos : upLocalPos;
        PressedChanged?.Invoke(this, IsPressed);
    }
}
