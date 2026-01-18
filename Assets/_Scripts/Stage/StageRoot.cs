using UnityEngine;

[DisallowMultipleComponent]
public class StageRoot : MonoBehaviour
{
    [Header("Identity")]
    [Min(0)]
    [SerializeField] private int stageIndex = 0;
    public int StageIndex => stageIndex;

    [Header("Respawn")]
    [Tooltip("이 스테이지에서 죽었을 때 부활할 위치")]
    [SerializeField] private Transform respawnPoint;
    public Transform RespawnPoint => respawnPoint;
}
