using System.Collections.Generic;
using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    public static CheckpointManager Instance;

    [Header("Current Spawn Point")]
    public Transform currentCheckpoint;
    public Transform defaultSpawnPoint;

    [System.Serializable]
    public struct PlayerStateSnapshot
    {
        public bool hasSnapshot;
        public Vector3 spawnPos;
        public Quaternion spawnRot;
        public int scrapCount;
        public int barrierInventoryCount;
        public bool hasSoilScanner;
        public bool hasSapling;
        public bool isDoorPowered;
        public int totalScrapRecycled;
        public int totalDronesRebooted;
    }

    public PlayerStateSnapshot savedState;

    // Tracks collected scrap objects so respawning restores any scrap picked up AFTER the checkpoint
    private List<GameObject> allSceneScrap = new List<GameObject>();
    private HashSet<GameObject> scrapCollectedAtCheckpoint = new HashSet<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Cache all scrap items in the world so we can reset post-checkpoint pickups on Restart Mission
        foreach (GameObject go in FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go.name.Contains("Scrap_") || go.CompareTag("Scrap"))
                allSceneScrap.Add(go);
        }
    }

    void Start()
    {
        if (currentCheckpoint == null && defaultSpawnPoint != null)
            currentCheckpoint = defaultSpawnPoint;

        ARIEPlayerController player = FindFirstObjectByType<ARIEPlayerController>();
        if (player != null)
            SaveCheckpointState(currentCheckpoint != null ? currentCheckpoint : player.transform, player);
    }

    public void UpdateCheckpoint(Transform newCheckpoint)
    {
        if (newCheckpoint != null)
        {
            currentCheckpoint = newCheckpoint;
            ARIEPlayerController player = FindFirstObjectByType<ARIEPlayerController>();
            if (player != null)
            {
                SaveCheckpointState(newCheckpoint, player);
                player.ShowPrompt("CHECKPOINT REACHED! Location & Inventory Saved.");
            }
            Debug.Log($"Checkpoint Updated & Loot Saved: {newCheckpoint.name}");
        }
    }

    public void SaveCheckpointState(Transform cpTransform, ARIEPlayerController player)
    {
        if (player == null) return;

        savedState.hasSnapshot = true;
        savedState.spawnPos = cpTransform != null ? cpTransform.position : player.transform.position;
        savedState.spawnRot = cpTransform != null ? cpTransform.rotation : player.transform.rotation;
        savedState.scrapCount = player.scrapCount;
        savedState.barrierInventoryCount = player.barrierInventoryCount;
        savedState.hasSoilScanner = player.hasSoilScanner;
        savedState.hasSapling = player.hasSapling;
        savedState.isDoorPowered = player.isDoorPowered;
        savedState.totalScrapRecycled = ARIEPlayerController.totalScrapRecycled;
        savedState.totalDronesRebooted = ARIEPlayerController.totalDronesRebooted;

        scrapCollectedAtCheckpoint.Clear();
        foreach (GameObject sc in allSceneScrap)
        {
            if (sc != null && !sc.activeSelf)
                scrapCollectedAtCheckpoint.Add(sc);
        }
    }

    public void RestorePlayerToCheckpoint(ARIEPlayerController player)
    {
        if (player == null || !savedState.hasSnapshot) return;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.transform.position = savedState.spawnPos;
        player.transform.rotation = savedState.spawnRot;

        if (cc != null) cc.enabled = true;

        // Restore exact loot snapshot from when the checkpoint was triggered
        player.scrapCount = savedState.scrapCount;
        player.barrierInventoryCount = savedState.barrierInventoryCount;
        player.hasSoilScanner = savedState.hasSoilScanner;
        player.hasSapling = savedState.hasSapling;
        player.isDoorPowered = savedState.isDoorPowered;

        ARIEPlayerController.totalScrapRecycled = savedState.totalScrapRecycled;
        ARIEPlayerController.totalDronesRebooted = savedState.totalDronesRebooted;

        if (player.securityDoor != null)
            player.securityDoor.SetActive(!savedState.isDoorPowered);

        // Re-enable any scrap that was picked up AFTER the checkpoint was saved
        foreach (GameObject sc in allSceneScrap)
        {
            if (sc != null)
            {
                bool wasAlreadyPickedUp = scrapCollectedAtCheckpoint.Contains(sc);
                sc.SetActive(!wasAlreadyPickedUp);
            }
        }

        // Remove any temporary barriers placed after the checkpoint
        player.ClearPlacedBarriersSinceCheckpoint();
    }

    public Vector3 GetSpawnPosition()
    {
        if (savedState.hasSnapshot) return savedState.spawnPos;
        if (currentCheckpoint != null) return currentCheckpoint.position;
        return Vector3.zero;
    }
}