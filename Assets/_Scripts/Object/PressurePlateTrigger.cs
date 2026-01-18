// PressurePlateTrigger.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PressurePlateTrigger : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform buttonUp;

    [Header("Press Motion")]
    [SerializeField] private float pressDepth = 0.06f;
    [SerializeField] private float moveDuration = 0.5f;
    [SerializeField] private bool smoothStep = true;

    [Header("Activator Filter")]
    [SerializeField] private LayerMask activatorMask = ~0;

    public bool IsPressed { get; private set; }
    public event Action<PressurePlateTrigger, bool> PressedChanged;

    private Vector3 upLocalPos;
    private Vector3 downLocalPos;

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
        int layerBit = 1 << other.gameObject.layer;
        if ((activatorMask.value & layerBit) == 0)
            return false;

        return true;
    }

    private int GetOccupierKey(Collider other)
    {
        if (other.attachedRigidbody != null)
            return other.attachedRigidbody.GetInstanceID();

        return other.transform.root.GetInstanceID();
    }

    private void EvaluatePressed()
    {
        bool newPressed = occupiers.Count > 0;
        if (newPressed == IsPressed) return;

        IsPressed = newPressed;

        // ✅ SFX: 버튼 눌림/원복(둘 다 같은 소리면 OK)
        SoundManager.PlaySFX(SfxId.Button_Interact, worldPos: transform.position);

        StartMove(IsPressed ? downLocalPos : upLocalPos);
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
            if (smoothStep) k = k * k * (3f - 2f * k);

            buttonUp.localPosition = Vector3.LerpUnclamped(start, targetLocal, k);
            yield return null;
        }

        buttonUp.localPosition = targetLocal;
        moveCo = null;
    }

    public void ForceSetPressed(bool pressed)
    {
        occupiers.Clear();
        IsPressed = pressed;
        if (moveCo != null) StopCoroutine(moveCo);
        buttonUp.localPosition = pressed ? downLocalPos : upLocalPos;

        SoundManager.PlaySFX(SfxId.Button_Interact, worldPos: transform.position);
        PressedChanged?.Invoke(this, IsPressed);
    }
}
