using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class Level3TreeDefense : MonoBehaviour
{
    [Header("Tree & Timer Settings")]
    public Transform growingTree;
    public float survivalTime = 60f;
    private float currentTimer;
    public float treeHealth = 100f;
    public bool isGameWon = false;

    [Header("Tree Growth Scale")]
    public Vector3 startScale = new Vector3(0.5f, 1f, 0.5f);
    public Vector3 fullGrownScale = new Vector3(3f, 8f, 3f);

    [Header("Separate UI Texts")]
    public TMP_Text timerText;   // Shows ONLY decreasing time (00:59)
    public TMP_Text messageText; // Shows status messages & Tree Health

    [Header("Barricade Defense System")]
    public GameObject[] barricades;
    public float breakdownInterval = 15f;
    private float breakdownTimer;

    [Header("Victory Eco-Jet Arrival")]
    public GameObject ecoJet;
    public Transform jetLandingSpot;

    void Start()
    {
        currentTimer = survivalTime;
        breakdownTimer = breakdownInterval;

        if (growingTree != null)
            growingTree.localScale = startScale;

        if (ecoJet != null)
            ecoJet.SetActive(false);

        if (messageText != null)
            messageText.text = $"PROTECT THE SAPLING! | Tree Health: 100%";
    }

    void Update()
    {
        if (isGameWon)
        {
            if (ecoJet != null && jetLandingSpot != null)
            {
                ecoJet.transform.position = Vector3.MoveTowards(
                    ecoJet.transform.position,
                    jetLandingSpot.position,
                    8f * Time.deltaTime
                );
            }
            return;
        }

        // 1. Decrease Timer & Format as MM:SS (e.g., 00:45)
        currentTimer -= Time.deltaTime;
        if (currentTimer < 0f) currentTimer = 0f;

        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTimer / 60f);
            int seconds = Mathf.FloorToInt(currentTimer % 60f);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }

        // 2. Smoothly grow the tree
        float growthProgress = 1f - (currentTimer / survivalTime);
        if (growingTree != null)
        {
            growingTree.localScale = Vector3.Lerp(startScale, fullGrownScale, growthProgress);
        }

        // 3. Periodically drop a barricade so player must guard/repair
        breakdownTimer -= Time.deltaTime;
        if (breakdownTimer <= 0f && barricades.Length > 0)
        {
            breakdownTimer = breakdownInterval;
            int randomIndex = Random.Range(0, barricades.Length);
            if (barricades[randomIndex] != null && barricades[randomIndex].activeSelf)
            {
                barricades[randomIndex].SetActive(false);
                if (messageText != null)
                    messageText.text = $"ALERT: Barricade Down! Press E on Terminal or Drones! | Tree HP: {Mathf.CeilToInt(treeHealth)}%";
            }
        }

        // 4. Win Condition
        if (currentTimer <= 0f)
        {
            TriggerVictory();
        }
    }

    public void DamageTree(float amount)
    {
        if (isGameWon) return;

        treeHealth -= amount * Time.deltaTime;
        if (messageText != null)
        {
            messageText.text = $"WARNING: DRONE DAMAGING TREE! (Press E on Drone to Reboot!) | Tree HP: {Mathf.CeilToInt(treeHealth)}%";
        }

        if (treeHealth <= 0f)
        {
            Debug.Log("THE SAPLING WAS DESTROYED! Restarting Level 3...");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void RestoreAllBarricades()
    {
        foreach (GameObject b in barricades)
        {
            if (b != null) b.SetActive(true);
        }
        if (messageText != null)
            messageText.text = $"BARRICADES REPAIRED! Protect the Sapling! | Tree HP: {Mathf.CeilToInt(treeHealth)}%";
    }

    void TriggerVictory()
    {
        isGameWon = true;
        if (timerText != null) timerText.text = "00:00";
        if (messageText != null) messageText.text = "ECOSYSTEM RESTORED! Eco-Alliance Jet Arriving... YOU WIN!";

        if (ecoJet != null) ecoJet.SetActive(true);

        CorpoDroneAI[] drones = FindObjectsByType<CorpoDroneAI>(FindObjectsSortMode.None);
        foreach (CorpoDroneAI drone in drones)
        {
            drone.gameObject.SetActive(false);
        }
    }
}