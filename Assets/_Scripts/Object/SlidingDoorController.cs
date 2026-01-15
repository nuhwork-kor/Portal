// SlidingDoor.cs
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class SlidingDoor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform leftPanel;
    [SerializeField] private Transform rightPanel;

    [Header("Motion")]
    [Tooltip("열릴 때 각 패널이 local 기준으로 이동할 거리(양수)")]
    [SerializeField] private float slideDistance = 1.2f;

    [Tooltip("열리고 닫히는 시간(초)")]
    [SerializeField] private float moveDuration = 0.6f;

    [SerializeField] private bool smoothStep = true;

    private Vector3 leftClosed;
    private Vector3 rightClosed;
    private Vector3 leftOpen;
    private Vector3 rightOpen;

    private Coroutine co;
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (!leftPanel || !rightPanel)
        {
            Debug.LogError("[SlidingDoor] leftPanel/rightPanel 누락", this);
            enabled = false;
            return;
        }

        leftClosed = leftPanel.localPosition;
        rightClosed = rightPanel.localPosition;

        // 왼쪽은 왼쪽 벽으로(-X), 오른쪽은 오른쪽 벽으로(+X) 밀려 들어간다고 가정
        leftOpen = leftClosed + Vector3.left * Mathf.Abs(slideDistance);
        rightOpen = rightClosed + Vector3.right * Mathf.Abs(slideDistance);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        StartMove(leftOpen, rightOpen);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        StartMove(leftClosed, rightClosed);
    }

    private void StartMove(Vector3 leftTarget, Vector3 rightTarget)
    {
        if (co != null) StopCoroutine(co);
        co = StartCoroutine(MoveRoutine(leftTarget, rightTarget));
    }

    private IEnumerator MoveRoutine(Vector3 leftTarget, Vector3 rightTarget)
    {
        Vector3 l0 = leftPanel.localPosition;
        Vector3 r0 = rightPanel.localPosition;

        float dur = Mathf.Max(0.0001f, moveDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Mathf.Clamp01(t);
            if (smoothStep) k = k * k * (3f - 2f * k);

            leftPanel.localPosition = Vector3.LerpUnclamped(l0, leftTarget, k);
            rightPanel.localPosition = Vector3.LerpUnclamped(r0, rightTarget, k);
            yield return null;
        }

        leftPanel.localPosition = leftTarget;
        rightPanel.localPosition = rightTarget;
        co = null;
    }
}
