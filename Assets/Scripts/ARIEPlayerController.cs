using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System;

[RequireComponent(typeof(CharacterController))]
public class ARIEPlayerController : MonoBehaviour
{
    [Header("Movement & Jump Settings")]
    public float walkSpeed = 5f;
    public float crouchSpeed = 2.5f;
    public float jumpHeight = 1.5f;
    public float gravity = -19.62f;

    [Header("Stealth / Crouch Settings")]
    public float normalHeight = 2.0f;
    public float crouchHeight = 1.0f;
    public bool isCrouching = false;

    [Header("Camera Look Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 2f;
    private float xRotation = 0f;

    [Header("Seamless Map Crafting & Inventory")]
    public float interactRange = 4f;
    public int scrapCount = 0;
    public int energyCells = 0;
    public bool hasSapling = false;
    public bool hasSoilScanner = false;
    public int barrierInventoryCount = 0;

    [Header("Map References & UI")]
    public GameObject securityDoor;          // Drag Scrapyard Blast Door here
    public GameObject barrierPrefab;         // Drag Barrier Prefab (with NavMeshObstacle) here
    public Level3TreeDefense defenseManager; // Drag Level3Manager here
    public TMP_Text hudText;                 // Top-left stats HUD
    public TMP_Text objectiveMessageText;    // Drag MessageText here for live hints

    [Header("Pause Menu & Controls UI Panels")]
    public GameObject pauseMenuPanel;
    public GameObject controlsMenuPanel;
    public TMP_Text rebindStatusText;
    private bool isPaused = false;

    [Header("Control Binding Button Texts (Optional)")]
    public TMP_Text forwardKeyText;
    public TMP_Text backwardKeyText;
    public TMP_Text leftKeyText;
    public TMP_Text rightKeyText;
    public TMP_Text jumpKeyText;
    public TMP_Text crouchKeyText;
    public TMP_Text interactKeyText;
    public TMP_Text craftBarrierKeyText;
    public TMP_Text placeBarrierKeyText;

    [Header("Customizable Controls (Saved in PlayerPrefs)")]
    public static KeyCode keyForward = KeyCode.W;
    public static KeyCode keyBackward = KeyCode.S;
    public static KeyCode keyLeft = KeyCode.A;
    public static KeyCode keyRight = KeyCode.D;
    public static KeyCode keyJump = KeyCode.Space;
    public static KeyCode keyCrouch = KeyCode.C;
    public static KeyCode keyInteract = KeyCode.E;
    public static KeyCode keyCraftBarrier = KeyCode.Q;
    public static KeyCode keyPlaceBarrier = KeyCode.F;

    private string waitingForKeyAction = "";

    [Header("Global Circular Economy Metrics")]
    public static int batteryLives = 3;
    public static int totalScrapRecycled = 0;
    public static int totalDronesRebooted = 0;

    private CharacterController controller;
    private Vector3 velocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (defenseManager == null)
            defenseManager = FindFirstObjectByType<Level3TreeDefense>();

        // Auto-find UI panels if left unassigned in Inspector
        if (pauseMenuPanel == null)
            pauseMenuPanel = GameObject.Find("PauseMenuPanel");
        if (controlsMenuPanel == null)
            controlsMenuPanel = GameObject.Find("ControlsMenuPanel");

        LoadSavedControlsAndStats();

        Time.timeScale = 1f;
        LockCursor(true);

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(false);

        UpdateHUD();
        UpdateKeyUI();
    }

    void Update()
    {
        // 1. Listen for new key press if rebinding controls
        if (!string.IsNullOrEmpty(waitingForKeyAction))
        {
            ListenForKeyRebind();
            return;
        }

        // 2. Toggle Pause Menu with ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }

        if (isPaused) return;

        HandleMouseLook();
        HandleMovementJumpAndCrouch();
        HandleBarrierPlacement();
        HandleInteraction();
    }

    // ==================== PAUSE MENU BUTTON FUNCTIONS ====================

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        LockCursor(false);

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        isPaused = false;
        waitingForKeyAction = "";
        Time.timeScale = 1f;
        LockCursor(true);

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(false);
    }

    public void RestartMission()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        batteryLives = 3;
        totalScrapRecycled = 0;
        totalDronesRebooted = 0;
        SaveProgress();

        if (Application.CanStreamedLevelBeLoaded("Level1"))
            SceneManager.LoadScene("Level1");
        else
            SceneManager.LoadScene(0);
    }

    public void OpenChangeControlsMenu()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(true);
        UpdateKeyUI();
        if (rebindStatusText != null)
            rebindStatusText.text = "Click a control below to change its key";
    }

    public void CloseChangeControlsMenu()
    {
        waitingForKeyAction = "";
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
        Debug.Log("Quitting Game...");
        Application.Quit();
    }

    // ==================== KEY REMAPPING SYSTEM ====================

    public void StartRebindingKey(string actionName)
    {
        waitingForKeyAction = actionName;
        if (rebindStatusText != null)
            rebindStatusText.text = $"Press any key for [{actionName}]... (Press ESC to cancel)";
    }

    void ListenForKeyRebind()
    {
        if (!Input.anyKeyDown) return;

        foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(kcode))
            {
                if (kcode == KeyCode.Mouse0) continue;

                if (kcode == KeyCode.Escape)
                {
                    waitingForKeyAction = "";
                    if (rebindStatusText != null) rebindStatusText.text = "Rebind cancelled.";
                    return;
                }

                switch (waitingForKeyAction)
                {
                    case "Forward": keyForward = kcode; break;
                    case "Backward": keyBackward = kcode; break;
                    case "Left": keyLeft = kcode; break;
                    case "Right": keyRight = kcode; break;
                    case "Jump": keyJump = kcode; break;
                    case "Crouch": keyCrouch = kcode; break;
                    case "Interact": keyInteract = kcode; break;
                    case "CraftBarrier": keyCraftBarrier = kcode; break;
                    case "PlaceBarrier": keyPlaceBarrier = kcode; break;
                }

                PlayerPrefs.SetString("Key_" + waitingForKeyAction, kcode.ToString());
                PlayerPrefs.Save();

                if (rebindStatusText != null)
                    rebindStatusText.text = $"{waitingForKeyAction} bound to [{kcode}]!";

                waitingForKeyAction = "";
                UpdateKeyUI();
                break;
            }
        }
    }

    public void ResetDefaultControls()
    {
        keyForward = KeyCode.W;
        keyBackward = KeyCode.S;
        keyLeft = KeyCode.A;
        keyRight = KeyCode.D;
        keyJump = KeyCode.Space;
        keyCrouch = KeyCode.C;
        keyInteract = KeyCode.E;
        keyCraftBarrier = KeyCode.Q;
        keyPlaceBarrier = KeyCode.F;

        PlayerPrefs.SetString("Key_Forward", keyForward.ToString());
        PlayerPrefs.SetString("Key_Backward", keyBackward.ToString());
        PlayerPrefs.SetString("Key_Left", keyLeft.ToString());
        PlayerPrefs.SetString("Key_Right", keyRight.ToString());
        PlayerPrefs.SetString("Key_Jump", keyJump.ToString());
        PlayerPrefs.SetString("Key_Crouch", keyCrouch.ToString());
        PlayerPrefs.SetString("Key_Interact", keyInteract.ToString());
        PlayerPrefs.SetString("Key_CraftBarrier", keyCraftBarrier.ToString());
        PlayerPrefs.SetString("Key_PlaceBarrier", keyPlaceBarrier.ToString());
        PlayerPrefs.Save();

        UpdateKeyUI();
        if (rebindStatusText != null)
            rebindStatusText.text = "Controls reset to defaults!";
    }

    void UpdateKeyUI()
    {
        if (forwardKeyText != null) forwardKeyText.text = $"Forward: {keyForward}";
        if (backwardKeyText != null) backwardKeyText.text = $"Backward: {keyBackward}";
        if (leftKeyText != null) leftKeyText.text = $"Left: {keyLeft}";
        if (rightKeyText != null) rightKeyText.text = $"Right: {keyRight}";
        if (jumpKeyText != null) jumpKeyText.text = $"Jump: {keyJump}";
        if (crouchKeyText != null) crouchKeyText.text = $"Crouch: {keyCrouch}";
        if (interactKeyText != null) interactKeyText.text = $"Interact: {keyInteract}";
        if (craftBarrierKeyText != null) craftBarrierKeyText.text = $"Craft Barrier: {keyCraftBarrier}";
        if (placeBarrierKeyText != null) placeBarrierKeyText.text = $"Place Barrier: {keyPlaceBarrier}";
    }

    void LoadSavedControlsAndStats()
    {
        totalScrapRecycled = PlayerPrefs.GetInt("SavedScrapRecycled", totalScrapRecycled);
        totalDronesRebooted = PlayerPrefs.GetInt("SavedDronesRebooted", totalDronesRebooted);

        keyForward = ParseKey("Key_Forward", KeyCode.W);
        keyBackward = ParseKey("Key_Backward", KeyCode.S);
        keyLeft = ParseKey("Key_Left", KeyCode.A);
        keyRight = ParseKey("Key_Right", KeyCode.D);
        keyJump = ParseKey("Key_Jump", KeyCode.Space);
        keyCrouch = ParseKey("Key_Crouch", KeyCode.C);
        keyInteract = ParseKey("Key_Interact", KeyCode.E);
        keyCraftBarrier = ParseKey("Key_CraftBarrier", KeyCode.Q);
        keyPlaceBarrier = ParseKey("Key_PlaceBarrier", KeyCode.F);
    }

    KeyCode ParseKey(string prefKey, KeyCode defaultKey)
    {
        string saved = PlayerPrefs.GetString(prefKey, defaultKey.ToString());
        if (Enum.TryParse(saved, out KeyCode parsed))
            return parsed;
        return defaultKey;
    }

    // ==================== PLAYER MOVEMENT & BARRIER PLACEMENT ====================

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleMouseLook()
    {
        if (playerCamera == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleMovementJumpAndCrouch()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        if (Input.GetKeyDown(keyCrouch))
        {
            isCrouching = !isCrouching;
            controller.height = isCrouching ? crouchHeight : normalHeight;
            if (playerCamera != null)
                playerCamera.localPosition = new Vector3(0f, isCrouching ? 0.3f : 0.6f, 0f);
        }

        float x = 0f, z = 0f;
        if (Input.GetKey(keyForward)) z += 1f;
        if (Input.GetKey(keyBackward)) z -= 1f;
        if (Input.GetKey(keyRight)) x += 1f;
        if (Input.GetKey(keyLeft)) x -= 1f;

        float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;
        Vector3 horizontalMove = (transform.right * x + transform.forward * z).normalized * currentSpeed;

        if (Input.GetKeyDown(keyJump) && isGrounded)
        {
            if (isCrouching)
            {
                isCrouching = false;
                controller.height = normalHeight;
                if (playerCamera != null)
                    playerCamera.localPosition = new Vector3(0f, 0.6f, 0f);
            }
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        Vector3 finalMovement = horizontalMove + new Vector3(0f, velocity.y, 0f);
        controller.Move(finalMovement * Time.deltaTime);
    }

    void HandleBarrierPlacement()
    {
        // Press F (or custom keyPlaceBarrier) to deploy a crafted Barrier in front of AR-IE
        if (Input.GetKeyDown(keyPlaceBarrier))
        {
            if (barrierInventoryCount > 0 && barrierPrefab != null)
            {
                Vector3 spawnPos = transform.position + transform.forward * 2.5f;
                spawnPos.y = transform.position.y;
                Quaternion spawnRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

                Instantiate(barrierPrefab, spawnPos, spawnRot);
                barrierInventoryCount--;
                UpdateHUD();

                if (objectiveMessageText != null)
                    objectiveMessageText.text = $"Scrap Barrier Deployed! ({barrierInventoryCount} left in inventory)";
            }
            else if (barrierInventoryCount <= 0 && objectiveMessageText != null)
            {
                objectiveMessageText.text = $"No Barriers in inventory! Return to Recycler with 2 Scrap & press [{keyCraftBarrier}] to craft one.";
            }
        }
    }

    // ==================== SEAMLESS MAP INTERACTIONS ====================

    void HandleInteraction()
    {
        if (playerCamera == null) return;

        // 1. Press Q (or keyCraftBarrier) while aiming at Recycler to craft a Portable Barrier (Costs 2 Scrap)
        if (Input.GetKeyDown(keyCraftBarrier))
        {
            Ray qRay = new Ray(playerCamera.position, playerCamera.forward);
            RaycastHit[] qHits = Physics.SphereCastAll(qRay, 0.35f, interactRange);

            foreach (RaycastHit hit in qHits)
            {
                if (hit.collider.gameObject == this.gameObject || hit.collider.transform.IsChildOf(this.transform))
                    continue;

                if (hit.collider.CompareTag("Recycler") || hit.collider.gameObject.name.Contains("Recycler"))
                {
                    if (scrapCount >= 2)
                    {
                        scrapCount -= 2;
                        barrierInventoryCount++;
                        UpdateHUD();
                        if (objectiveMessageText != null)
                            objectiveMessageText.text = $"Crafted 1x Scrap Barrier! Press [{keyPlaceBarrier}] near the soil patch to place it.";
                    }
                    else if (objectiveMessageText != null)
                    {
                        objectiveMessageText.text = $"Need 2 Scrap to craft a Defensive Barrier [{keyCraftBarrier}]! (Current: {scrapCount}/2)";
                    }
                    return;
                }
            }
        }

        // 2. Press E (or keyInteract) for standard interactions
        if (Input.GetKeyDown(keyInteract))
        {
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            Debug.DrawRay(ray.origin, ray.direction * interactRange, Color.green, 2f);

            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.35f, interactRange);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject == this.gameObject || hit.collider.transform.IsChildOf(this.transform))
                    continue;

                // A. Collect Scrap
                if (hit.collider.CompareTag("Scrap") || hit.collider.gameObject.name.Contains("Scrap"))
                {
                    scrapCount++;
                    totalScrapRecycled++;
                    SaveProgress();
                    Destroy(hit.collider.gameObject);
                    UpdateHUD();
                    return;
                }
                // B. Master Recycler (Stage 1: Open Door -> Stage 2: Craft Soil Scanner)
                else if (hit.collider.CompareTag("Recycler") || hit.collider.gameObject.name.Contains("Recycler"))
                {
                    // Stage 1: Blast Door is still closed (Requires 3 Scrap)
                    if (securityDoor != null && securityDoor.activeSelf)
                    {
                        if (scrapCount >= 3)
                        {
                            scrapCount -= 3;
                            energyCells++;
                            securityDoor.SetActive(false);
                            UpdateHUD();
                            if (objectiveMessageText != null)
                                objectiveMessageText.text = "DOOR OPENED! Follow the glowing dot into the Rock Cave to find the Sapling.";
                        }
                        else if (objectiveMessageText != null)
                        {
                            objectiveMessageText.text = $"Need 3 Scrap to power Blast Door! (Current: {scrapCount}/3)";
                        }
                    }
                    // Stage 2: Door is open, craft Soil Scanner (Requires 2 Scrap)
                    else if (!hasSoilScanner)
                    {
                        if (scrapCount >= 2)
                        {
                            scrapCount -= 2;
                            hasSoilScanner = true;
                            UpdateHUD();
                            if (objectiveMessageText != null)
                                objectiveMessageText.text = $"SOIL SCANNER CRAFTED! Tip: Press [{keyCraftBarrier}] on Recycler (2 Scrap) to craft Barriers before planting!";
                        }
                        else if (objectiveMessageText != null)
                        {
                            objectiveMessageText.text = $"Need 2 Scrap to craft Soil Scanner! (Or press [{keyCraftBarrier}] with 2 Scrap to craft a Barrier)";
                        }
                    }
                    else if (objectiveMessageText != null)
                    {
                        objectiveMessageText.text = $"Door & Scanner complete! Press [{keyCraftBarrier}] here with 2 Scrap to craft extra Barriers.";
                    }
                    return;
                }
                // C. Rescue Sapling inside the Rock Cave
                else if (hit.collider.CompareTag("Sapling") || hit.collider.gameObject.name.Contains("Sapling"))
                {
                    hasSapling = true;
                    Destroy(hit.collider.gameObject);
                    UpdateHUD();
                    if (objectiveMessageText != null)
                        objectiveMessageText.text = "SAPLING SECURED! Gather 2 Scrap & return to the Recycler to craft a Soil Scanner.";
                    return;
                }
                // D. Scan Soil Patch & Plant Sapling
                else if (hit.collider.GetComponent<SoilPatchSlot>() != null || hit.collider.CompareTag("SoilPatch") || hit.collider.gameObject.name.Contains("FertileSoil"))
                {
                    SoilPatchSlot patch = hit.collider.GetComponent<SoilPatchSlot>();

                    if (!hasSapling)
                    {
                        if (objectiveMessageText != null)
                            objectiveMessageText.text = "Find the Sapling inside the Rock Cave first!";
                        return;
                    }
                    if (!hasSoilScanner)
                    {
                        if (objectiveMessageText != null)
                            objectiveMessageText.text = "SOIL QUALITY UNKNOWN! Return to the Zone 1 Recycler with 2 Scrap to craft a Soil Scanner!";
                        return;
                    }
                    if (patch != null && !patch.isScanned)
                    {
                        string scanResult = patch.ScanSoil();
                        if (objectiveMessageText != null)
                            objectiveMessageText.text = scanResult;
                        return;
                    }
                    if (patch == null || patch.isFertile)
                    {
                        // Plant the Sapling & start the 60s Tree Defense event!
                        if (defenseManager != null)
                        {
                            defenseManager.StartDefenseEvent(hit.collider.transform.position);
                        }
                    }
                    else if (objectiveMessageText != null)
                    {
                        objectiveMessageText.text = "This soil is toxic! Find and scan another Soil Patch.";
                    }
                    return;
                }
                // E. Reboot Drone with E
                else if (hit.collider.GetComponent<CorpoDroneAI>() != null)
                {
                    hit.collider.GetComponent<CorpoDroneAI>().RebootAndSendBack();
                    totalDronesRebooted++;
                    SaveProgress();
                    UpdateHUD();
                    return;
                }
                // F. Optional Terminal to repair static barricades
                else if (hit.collider.CompareTag("Terminal") || hit.collider.gameObject.name.Contains("Terminal"))
                {
                    if (defenseManager != null)
                        defenseManager.RestoreAllBarricades();
                    return;
                }
            }
        }
    }

    void SaveProgress()
    {
        PlayerPrefs.SetInt("SavedScrapRecycled", totalScrapRecycled);
        PlayerPrefs.SetInt("SavedDronesRebooted", totalDronesRebooted);
        PlayerPrefs.Save();
    }

    public void LoseBatteryLife()
    {
        batteryLives--;
        if (batteryLives > 0)
        {
            // Respawn instantly at the active checkpoint instead of reloading scene!
            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            if (CheckPointManager.Instance != null)
            {
                transform.position = CheckPointManager.Instance.GetSpawnPosition();
            }

            if (cc != null) cc.enabled = true;

            if (objectiveMessageText != null)
                objectiveMessageText.text = $"BATTERY CRITICAL! Life lost. Remaining lives: {batteryLives}";

            UpdateHUD();
        }
        else
        {
            batteryLives = 3;
            // Out of lives: Reset metrics and return to Start Menu
            if (TryGetComponent<OneClickIndustrialHUD>(out var hudMgr))
            {
                hudMgr.OpenStartMenu();
            }
            else
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
    }

    void UpdateHUD()
    {
        if (hudText != null)
        {
            string scannerStatus = hasSoilScanner ? "YES" : "NO";
            float co2Saved = totalScrapRecycled * 2.4f;
            hudText.text = $"Lives: {batteryLives} | Scrap: {scrapCount} | Barriers [{keyPlaceBarrier}]: {barrierInventoryCount} | Scanner: {scannerStatus} | CO2: {co2Saved:F1}kg";
        }
    }
}