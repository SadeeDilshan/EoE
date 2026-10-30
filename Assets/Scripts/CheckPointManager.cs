using UnityEngine;

public class CheckPointManager : MonoBehaviour
{
    public static CheckPointManager Instance;

    [Header("Current Spawn Point")]
    public Transform currentCheckpoint;
    public Transform defaultSpawnPoint;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (currentCheckpoint == null && defaultSpawnPoint != null)
        {
            currentCheckpoint = defaultSpawnPoint;
        }
    }

    public void UpdateCheckpoint(Transform newCheckpoint)
    {
        if (newCheckpoint != null)
        {
            currentCheckpoint = newCheckpoint;
            Debug.Log($"Checkpoint Updated: {newCheckpoint.name}");
        }
    }

    public Vector3 GetSpawnPosition()
    {
        if (currentCheckpoint != null)
            return currentCheckpoint.position;
        return Vector3.zero;
    }
}