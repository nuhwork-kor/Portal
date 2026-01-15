// DoorButtonGroup.cs
using UnityEngine;

[DisallowMultipleComponent]
public class DoorButtonGroup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SlidingDoor door;

    [Header("Required Buttons (1~N)")]
    [SerializeField] private PressurePlateTrigger[] requiredButtons;

    [Header("Behavior")]
    [Tooltip("한 번 열리면 다시 안 닫힘")]
    [SerializeField] private bool stayOpenOnceUnlocked = false;

    private bool unlocked;

    private void Awake()
    {
        if (!door) door = GetComponent<SlidingDoor>();
    }

    private void OnEnable()
    {
        Bind(true);
        Evaluate();
    }

    private void OnDisable()
    {
        Bind(false);
    }

    private void Bind(bool on)
    {
        if (requiredButtons == null) return;

        for (int i = 0; i < requiredButtons.Length; i++)
        {
            var b = requiredButtons[i];
            if (!b) continue;

            if (on) b.PressedChanged += OnButtonChanged;
            else b.PressedChanged -= OnButtonChanged;
        }
    }

    private void OnButtonChanged(PressurePlateTrigger _, bool __)
    {
        Evaluate();
    }

    private void Evaluate()
    {
        if (!door) return;

        if (stayOpenOnceUnlocked && unlocked)
            return;

        bool allPressed = true;

        if (requiredButtons == null || requiredButtons.Length == 0)
            allPressed = false;

        for (int i = 0; i < requiredButtons.Length; i++)
        {
            var b = requiredButtons[i];
            if (!b || !b.IsPressed)
            {
                allPressed = false;
                break;
            }
        }

        if (allPressed)
        {
            door.Open();
            if (stayOpenOnceUnlocked) unlocked = true;
        }
        else
        {
            if (!stayOpenOnceUnlocked)
                door.Close();
        }
    }
}
