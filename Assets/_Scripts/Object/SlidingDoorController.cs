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
    [SerializeField] private float slideDistance = 2.6f;
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

        leftOpen = leftClosed + Vector3.left * Mathf.Abs(slideDistance);
        rightOpen = rightClosed + Vector3.right * Mathf.Abs(slideDistance);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;

        // ✅ SFX: 문 인터랙트(3D로 문 위치에서)
        SoundManager.PlaySFX(SfxId.Door_Interact, worldPos: transform.position);

        StartMove(leftOpen, rightOpen);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;

        // ✅ SFX: 문 인터랙트
        SoundManager.PlaySFX(SfxId.Door_Interact, worldPos: transform.position);

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
