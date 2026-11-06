using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class Level3TreeDefense : MonoBehaviour
{
    [Header("Defense Settings")]
    public bool defenseStarted = false;
    public float survivalTime = 90f;
    private float initialSurvivalTime = 90f;
    public float treeHealth = 100f;
    public float maxTreeHealth = 100f;

    [Header("World References")]
    public Transform growingTree;
    public GameObject ecoJet;
    public Transform jetLandingSpot;

    [Header("UI References")]
    public TMP_Text timerText;
    public TMP_Text messageText;

    [Header("Victory Return Settings")]
    public float delayBeforeMainMenu = 4.0f;
    private bool isVictory = false;

    void Start()
    {
        initialSurvivalTime = Mathf.Max(10f, survivalTime);
        if (growingTree != null)
        {
            growingTree.localScale = new Vector3(0.2f, 0.2f, 0.2f);
            growingTree.gameObject.SetActive(false);
        }
        if (ecoJet != null)
        {
            ecoJet.SetActive(false);
        }
    }

    // Supports ARIEPlayerController calling StartDefenseEvent(patch.transform.position)
    public void StartDefenseEvent(Vector3 plantPosition)
    {
        if (growingTree != null)
            growingTree.position = plantPosition;

        StartDefenseSequence();
    }

    public void StartDefenseSequence()
    {
        if (defenseStarted) return;
        defenseStarted = true;

        if (growingTree != null)
        {
            growingTree.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            growingTree.gameObject.SetActive(true);
        }

        // Command all CorpoDrones to rush the newly planted Sapling!
        CorpoDroneAI[] drones = FindObjectsByType<CorpoDroneAI>(FindObjectsSortMode.None);
        foreach (CorpoDroneAI d in drones)
        {
            if (d != null)
            {
                d.targetTree = growingTree;
                d.level3Manager = this;
                d.attackTreeInLevel3 = true;
            }
        }

        if (messageText != null)
            messageText.text = "DEFEND THE SAPLING! Place barriers [Left-Click] & short-circuit drones [Right-Click]!";
    }

    void Update()
    {
        if (!defenseStarted || isVictory) return;

        if (survivalTime > 0f)
        {
            survivalTime -= Time.deltaTime;
            float progress = 1f - (survivalTime / initialSurvivalTime);

            // Slowly grow the tree as timer counts down
            if (growingTree != null)
                growingTree.localScale = Vector3.Lerp(new Vector3(0.3f, 0.3f, 0.3f), new Vector3(1.5f, 1.5f, 1.5f), Mathf.Clamp01(progress));

            if (timerText != null)
            {
                int mins = Mathf.FloorToInt(Mathf.Max(0f, survivalTime) / 60f);
                int secs = Mathf.FloorToInt(Mathf.Max(0f, survivalTime) % 60f);
                timerText.text = $"{mins:00}:{secs:00}";
            }
        }
        else
        {
            TriggerVictory();
        }
    }

    public void DamageTree(float amount)
    {
        if (!defenseStarted || isVictory) return;

        treeHealth = Mathf.Max(0f, treeHealth - amount);
        if (messageText != null)
            messageText.text = $"WARNING: CORPO-DRONE DAMAGING SAPLING! | Tree Health: {Mathf.CeilToInt(treeHealth)}%";

        if (treeHealth <= 0f)
        {
            defenseStarted = false;
            treeHealth = maxTreeHealth;
            survivalTime = initialSurvivalTime;

            if (growingTree != null)
                growingTree.gameObject.SetActive(false);

            // Return drones to normal patrol until player plants the sapling again
            CorpoDroneAI[] drones = FindObjectsByType<CorpoDroneAI>(FindObjectsSortMode.None);
            foreach (CorpoDroneAI d in drones)
            {
                if (d != null) d.attackTreeInLevel3 = false;
            }

            if (messageText != null)
                messageText.text = "SAPLING DESTROYED! Respawning at Checkpoint...";

            ARIEPlayerController player = FindFirstObjectByType<ARIEPlayerController>();
            if (player != null)
            {
                player.hasSapling = true; // Restore sapling so player can try planting again
                player.LoseBatteryLife();
            }
        }
    }

    void TriggerVictory()
    {
        if (isVictory) return;
        isVictory = true;
        survivalTime = 0f;

        if (timerText != null)
            timerText.text = "00:00";

        // 1. Permanently reboot all CorpoDrones across the map
        CorpoDroneAI[] allDrones = FindObjectsByType<CorpoDroneAI>(FindObjectsSortMode.None);
        foreach (CorpoDroneAI d in allDrones)
        {
            if (d != null)
                d.PermanentVictoryReboot();
        }

        // 2. Deploy the Eco-Alliance Extraction Jet
        if (ecoJet != null)
        {
            ecoJet.SetActive(true);
            if (jetLandingSpot != null)
                ecoJet.transform.position = jetLandingSpot.position;
        }

        if (messageText != null)
            messageText.text = "MISSION ACCOMPLISHED! All drones permanently rebooted & Sapling restored! Returning to Main Menu...";

        // 3. Return to Main Start Menu Panel after delay
        StartCoroutine(ReturnToMainMenuRoutine());
    }

    IEnumerator ReturnToMainMenuRoutine()
    {
        yield return new WaitForSeconds(delayBeforeMainMenu);

        ARIEPlayerController.batteryLives = 3;
        ARIEPlayerController.totalScrapRecycled = 0;
        ARIEPlayerController.totalDronesRebooted = 0;

        HUDController hud = FindFirstObjectByType<HUDController>();
        if (hud != null)
            hud.showStartMenuOnLaunch = true;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}