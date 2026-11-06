using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class ARIEPlayerController : MonoBehaviour
{
    [Header("Movement & Shift Sprint (5s Sprint / 3s Cooldown)")]
    public float walkSpeed = 5.2f;
    public float sprintSpeed = 9.5f;
    public float crouchSpeed = 2.6f;
    public float maxSprintDuration = 5.0f;
    public float sprintCooldownDuration = 3.0f;
    public float jumpHeight = 1.4f;
    public float gravity = -19.62f;
    public Transform playerCamera;
    public float mouseSensitivity = 2.0f;

    [Header("Sprint Live Telemetry")]
    public float currentSprintTime = 5.0f;
    public float currentSprintCooldown = 0f;
    public bool isSprintCoolingDown = false;
    public bool IsCrouching { get; private set; }

    [Header("Right-Click EMP Short-Circuit")]
    public float empRange = 9.5f;

    [Header("Free Fire Style 2-Click Barrier Placement")]
    public GameObject barrierPrefab;
    public float barrierPreviewDistance = 4.2f;
    private bool isPreviewingBarrier = false;
    private GameObject barrierGhostInstance;
    private List<GameObject> placedBarriersSinceCheckpoint = new List<GameObject>();

    [Header("Inventory & Progression State")]
    public int scrapCount = 0;
    public int barrierInventoryCount = 0;
    public bool hasSoilScanner = false;
    public bool hasSapling = false;
    public bool isDoorPowered = false;

    public static int batteryLives = 3;
    public static int totalScrapRecycled = 0;
    public static int totalDronesRebooted = 0;

    [Header("World References")]
    public GameObject securityDoor;
    public Level3TreeDefense defenseManager;
    public TMP_Text objectiveMessageText;
    public GameObject pauseMenuPanel;
    public GameObject controlsMenuPanel;
    public TMP_Text jumpKeyText, crouchKeyText, sprintKeyText, interactKeyText, craftBarrierKeyText, placeBarrierKeyText;

    [Header("Remappable Keybinds & UI Texts")]
    public static KeyCode keyForward = KeyCode.W;
    public static KeyCode keyBackward = KeyCode.S;
    public static KeyCode keyLeft = KeyCode.A;
    public static KeyCode keyRight = KeyCode.D;
    public static KeyCode keyJump = KeyCode.Space;
    public static KeyCode keyCrouch = KeyCode.C;
    public static KeyCode keySprint = KeyCode.LeftShift;
    public static KeyCode keyInteract = KeyCode.E;
    public static KeyCode keyCraftBarrier = KeyCode.Q;
    public static KeyCode keyPlaceBarrier = KeyCode.Mouse0; // Left Click!

    public TMP_Text rebindStatusText;
    public TMP_Text forwardKeyText, backwardKeyText, leftKeyText, rightKeyText;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;
    private float groundedGraceTimer = 0f; // Ensures reliable jumping even when 100% idle!
    private bool isPaused = false;
    private string waitingForKeyAction = "";

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller != null)
        {
            // Setting minMoveDistance to 0 prevents CharacterController from ignoring idle vertical steps!
            controller.minMoveDistance = 0f;
        }
        currentSprintTime = maxSprintDuration;
        LoadSavedControls();
        UpdateControlsUI();
    }

    void Update()
    {
        if (!string.IsNullOrEmpty(waitingForKeyAction))
        {
            ListenForKeyRebind();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPreviewingBarrier)
            {
                CancelBarrierPreview();
                return;
            }
            if (isPaused) ResumeGame();
            else PauseGame();
        }

        if (isPaused || Time.timeScale == 0f) return;

        HandleMouseLook();
        HandleMovementAndSprint();
        HandleFreeFireBarrierPlacement();
        HandleRightClickEMP();
        HandleInteractions();
    }

    // ==================== 1. MOVEMENT, IDLE JUMP + 5s SHIFT SPRINT ====================

    bool CheckIsGroundedReliable()
    {
        if (controller == null) return false;
        if (controller.isGrounded) return true;

        // Secondary sphere check at the bottom of the CharacterController so standing still on uneven terrain still registers grounded!
        Vector3 bottomSpherePos = transform.position + controller.center - Vector3.up * (controller.height * 0.5f - controller.radius + 0.08f);
        Collider[] hits = Physics.OverlapSphere(bottomSpherePos, controller.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider col in hits)
        {
            if (col != null && col.transform != transform && !col.transform.IsChildOf(transform))
            {
                if (barrierGhostInstance != null && col.transform.IsChildOf(barrierGhostInstance.transform))
                    continue;
                return true;
            }
        }
        return false;
    }

    void HandleMovementAndSprint()
    {
        if (controller == null || !controller.enabled) return;

        bool groundedNow = CheckIsGroundedReliable();
        if (groundedNow)
        {
            groundedGraceTimer = 0.15f; // 0.15s grace window guarantees instant jumping from idle
            if (velocity.y < 0f)
                velocity.y = -2.5f;
        }
        else
        {
            groundedGraceTimer -= Time.deltaTime;
        }

        if (Input.GetKeyDown(keyCrouch))
        {
            IsCrouching = !IsCrouching;
            controller.height = IsCrouching ? 1.1f : 2.0f;
        }

        float moveX = 0f, moveZ = 0f;
        if (Input.GetKey(keyForward)) moveZ += 1f;
        if (Input.GetKey(keyBackward)) moveZ -= 1f;
        if (Input.GetKey(keyRight)) moveX += 1f;
        if (Input.GetKey(keyLeft)) moveX -= 1f;

        Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized;
        bool isMoving = move.sqrMagnitude > 0.01f;

        // Shift Sprint Logic: Sprints up to 5 seconds, then cools down for 3 seconds
        bool wantsToSprint = Input.GetKey(keySprint) && isMoving && !IsCrouching;
        bool isSprinting = false;

        if (isSprintCoolingDown)
        {
            currentSprintCooldown -= Time.deltaTime;
            if (currentSprintCooldown <= 0f)
            {
                isSprintCoolingDown = false;
                currentSprintTime = maxSprintDuration;
            }
        }
        else if (wantsToSprint && currentSprintTime > 0f)
        {
            isSprinting = true;
            currentSprintTime -= Time.deltaTime;
            if (currentSprintTime <= 0f)
            {
                currentSprintTime = 0f;
                isSprintCoolingDown = true;
                currentSprintCooldown = sprintCooldownDuration;
                ShowPrompt("SPRINT DEPLETED! Recharging servos (3s)...");
            }
        }
        else if (currentSprintTime < maxSprintDuration)
        {
            currentSprintCooldown += Time.deltaTime;
            if (currentSprintCooldown >= sprintCooldownDuration)
            {
                currentSprintTime = maxSprintDuration;
                currentSprintCooldown = 0f;
            }
        }

        // Jump works both while walking AND while 100% idle!
        if (Input.GetKeyDown(keyJump) && groundedGraceTimer > 0f)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            groundedGraceTimer = 0f;
        }

        velocity.y += gravity * Time.deltaTime;

        // Combine horizontal + vertical velocity into a SINGLE Move() call so CharacterController never stalls while idle!
        float activeSpeed = IsCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
        Vector3 finalDisplacement = (move * activeSpeed) + new Vector3(0f, velocity.y, 0f);
        controller.Move(finalDisplacement * Time.deltaTime);
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    // ==================== 2. FREE FIRE 2-CLICK GLOO-WALL BARRIER PLACEMENT (WORKS IN IDLE & MOVING) ====================

    void HandleFreeFireBarrierPlacement()
    {
        bool placePressed = Input.GetKeyDown(keyPlaceBarrier) || (keyPlaceBarrier == KeyCode.Mouse0 && Input.GetMouseButtonDown(0));

        if (isPreviewingBarrier)
        {
            // Continuously update wall hologram position/rotation even when standing completely still!
            UpdateBarrierGhostTransform();

            // 2nd Click: Deploy real barrier at the illusion's exact position
            if (placePressed)
            {
                ConfirmDeployBarrier();
                return;
            }

            // Right-Click cancels the wall preview
            if (Input.GetMouseButtonDown(1))
            {
                CancelBarrierPreview();
                ShowPrompt("Barrier placement preview cancelled.");
                return;
            }
        }
        else
        {
            // 1st Click: Spawn the Free Fire style translucent wall illusion (works while idle or moving!)
            if (placePressed)
            {
                if (barrierInventoryCount > 0 && barrierPrefab != null)
                {
                    StartBarrierPreview();
                }
            }
        }
    }

    void StartBarrierPreview()
    {
        isPreviewingBarrier = true;
        barrierGhostInstance = Instantiate(barrierPrefab);
        barrierGhostInstance.name = "Barrier_Holo_Illusion";
        barrierGhostInstance.SetActive(true);

        // Disable colliders & NavMeshObstacle on the illusion so it never collides with the player or raycasts
        foreach (Collider c in barrierGhostInstance.GetComponentsInChildren<Collider>())
            c.enabled = false;
        foreach (UnityEngine.AI.NavMeshObstacle obs in barrierGhostInstance.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>())
            obs.enabled = false;

        // Apply glowing translucent Cyan Free Fire Gloo-Wall preview shader
        Shader holoShader = Shader.Find("Sprites/Default");
        if (holoShader == null) holoShader = Shader.Find("Universal Render Pipeline/Unlit");
        Material ghostMat = new Material(holoShader);
        Color cyanIllusion = new Color(0.15f, 0.95f, 1.0f, 0.45f);
        ghostMat.color = cyanIllusion;
        if (ghostMat.HasProperty("_BaseColor")) ghostMat.SetColor("_BaseColor", cyanIllusion);

        foreach (MeshRenderer mr in barrierGhostInstance.GetComponentsInChildren<MeshRenderer>())
        {
            mr.enabled = true;
            Material[] mats = new Material[mr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = ghostMat;
            mr.sharedMaterials = mats;
        }

        UpdateBarrierGhostTransform();
        ShowPrompt("WALL PREVIEW ACTIVE: Left-Click again to deploy wall, or Right-Click to cancel.");
    }

    void UpdateBarrierGhostTransform()
    {
        if (barrierGhostInstance == null) return;

        Transform cam = playerCamera != null ? playerCamera : transform;
        Vector3 forwardFlat = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (forwardFlat.sqrMagnitude < 0.001f) forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        // Default preview point 4.2m directly in front of player
        Vector3 targetPos = transform.position + forwardFlat * barrierPreviewDistance;

        // Raycast from camera, explicitly IGNORING the Player's own CharacterController and Trigger colliders!
        RaycastHit[] camHits = Physics.RaycastAll(cam.position, cam.forward, barrierPreviewDistance + 2.5f, ~0, QueryTriggerInteraction.Ignore);
        float closestDist = float.MaxValue;
        bool foundAimSurface = false;

        foreach (RaycastHit h in camHits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
            if (barrierGhostInstance != null && h.transform.IsChildOf(barrierGhostInstance.transform)) continue;
            // Ignore hitting minimum close range (< 1.2m) so wall never spawns inside player's face while idle
            if (h.distance < 1.2f) continue;

            if (h.distance < closestDist)
            {
                closestDist = h.distance;
                targetPos = h.point;
                foundAimSurface = true;
            }
        }

        // If looking straight ahead/up into the air while idle, snap the bottom of the wall cleanly to the terrain below!
        if (!foundAimSurface)
        {
            RaycastHit[] groundHits = Physics.RaycastAll(targetPos + Vector3.up * 8f, Vector3.down, 25f, ~0, QueryTriggerInteraction.Ignore);
            float highestGroundY = transform.position.y;
            bool foundGround = false;

            foreach (RaycastHit gh in groundHits)
            {
                if (gh.transform == transform || gh.transform.IsChildOf(transform)) continue;
                if (barrierGhostInstance != null && gh.transform.IsChildOf(barrierGhostInstance.transform)) continue;

                if (!foundGround || gh.point.y > highestGroundY)
                {
                    highestGroundY = gh.point.y;
                    foundGround = true;
                }
            }

            if (foundGround)
                targetPos.y = highestGroundY;
        }

        barrierGhostInstance.transform.position = targetPos;
        barrierGhostInstance.transform.rotation = Quaternion.LookRotation(forwardFlat, Vector3.up);
    }

    void ConfirmDeployBarrier()
    {
        if (barrierGhostInstance == null) return;

        Vector3 deployPos = barrierGhostInstance.transform.position;
        Quaternion deployRot = barrierGhostInstance.transform.rotation;

        Destroy(barrierGhostInstance);
        isPreviewingBarrier = false;

        GameObject realWall = Instantiate(barrierPrefab, deployPos, deployRot);
        placedBarriersSinceCheckpoint.Add(realWall);
        barrierInventoryCount--;

        ShowPrompt($"SCRAP BARRIER DEPLOYED! Remaining Barriers: {barrierInventoryCount}");
    }

    void CancelBarrierPreview()
    {
        if (barrierGhostInstance != null) Destroy(barrierGhostInstance);
        isPreviewingBarrier = false;
    }

    public void ClearPlacedBarriersSinceCheckpoint()
    {
        CancelBarrierPreview();
        foreach (GameObject b in placedBarriersSinceCheckpoint)
        {
            if (b != null) Destroy(b);
        }
        placedBarriersSinceCheckpoint.Clear();
    }

    // ==================== 3. RIGHT-CLICK EMP SHORT-CIRCUIT DRONES ====================

    void HandleRightClickEMP()
    {
        if (isPreviewingBarrier) return; // Right-click cancels preview if holding wall illusion

        if (Input.GetMouseButtonDown(1))
        {
            Transform cam = playerCamera != null ? playerCamera : transform;
            CorpoDroneAI targetDrone = null;

            RaycastHit[] hits = Physics.RaycastAll(cam.position, cam.forward, empRange, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                CorpoDroneAI d = hit.collider.GetComponentInParent<CorpoDroneAI>();
                if (d != null && !d.isShortCircuited)
                {
                    targetDrone = d;
                    break;
                }
            }

            if (targetDrone == null)
            {
                CorpoDroneAI[] allDrones = FindObjectsByType<CorpoDroneAI>(FindObjectsSortMode.None);
                float bestAngle = 35f;
                foreach (CorpoDroneAI d in allDrones)
                {
                    if (d == null || d.isShortCircuited) continue;
                    float dist = Vector3.Distance(transform.position, d.transform.position);
                    if (dist <= empRange)
                    {
                        float ang = Vector3.Angle(cam.forward, (d.transform.position - cam.position).normalized);
                        if (ang < bestAngle)
                        {
                            bestAngle = ang;
                            targetDrone = d;
                        }
                    }
                }
            }

            if (targetDrone != null)
            {
                targetDrone.ShortCircuitEMP();
                ShowPrompt("EMP PULSE FIRED [RIGHT-CLICK]: Corpo-Drone short-circuited & rebooting!");
            }
            else
            {
                ShowPrompt("EMP PULSE: No active Corpo-Drone within 9m range.");
            }
        }
    }

    // ==================== 4. SCAVENGING, RECYCLER & SOIL INTERACTIONS ====================

    void HandleInteractions()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 3.2f);
        foreach (Collider col in hits)
        {
            if (col == null) continue;
            GameObject obj = col.gameObject;

            // Collect Scrap
            if (obj.CompareTag("Scrap") || obj.name.Contains("Scrap_"))
            {
                if (Input.GetKeyDown(keyInteract))
                {
                    scrapCount++;
                    obj.SetActive(false);
                    ShowPrompt($"Picked up E-Waste Scrap! Total Scrap: {scrapCount}");
                    return;
                }
            }

            // Collect Cryo-Sapling
            if (obj.CompareTag("Sapling") || obj.name == "Sapling")
            {
                if (Input.GetKeyDown(keyInteract))
                {
                    hasSapling = true;
                    obj.SetActive(false);
                    ShowPrompt("CRYO-SAPLING ACQUIRED! Exit the Cave and craft a Soil Scanner [E] or Barriers [Q].");
                    return;
                }
            }

            // Master Recycler ([E] for Door/Scanner, [Q] for Scrap Barrier)
            if (obj.CompareTag("Recycler") || obj.name == "Recycler")
            {
                if (Input.GetKeyDown(keyInteract))
                {
                    if (!isDoorPowered)
                    {
                        if (scrapCount >= 3)
                        {
                            scrapCount -= 3;
                            totalScrapRecycled += 3;
                            isDoorPowered = true;
                            if (securityDoor != null) securityDoor.SetActive(false);
                            if (CheckpointManager.Instance != null) CheckpointManager.Instance.SaveCheckpointState(transform, this);
                            ShowPrompt("BLAST DOOR UNLOCKED! Enter the North Rock Cave to rescue the Sapling.");
                        }
                        else ShowPrompt($"Need 3 Scrap to power the Blast Door (Have: {scrapCount}/3).");
                    }
                    else if (!hasSoilScanner)
                    {
                        if (scrapCount >= 3)
                        {
                            scrapCount -= 3;
                            totalScrapRecycled += 3;
                            hasSoilScanner = true;
                            ShowPrompt("SOIL SCANNER ONLINE! Craft Barriers with [Q] (2 Scrap) and Left-Click to preview/place.");
                        }
                        else ShowPrompt($"Need 3 Scrap to craft the Soil Scanner (Have: {scrapCount}/3).");
                    }
                    else ShowPrompt("Soil Scanner already active! Press [Q] at Recycler to craft Barriers (2 Scrap).");
                    return;
                }

                if (Input.GetKeyDown(keyCraftBarrier))
                {
                    if (scrapCount >= 2)
                    {
                        scrapCount -= 2;
                        totalScrapRecycled += 2;
                        barrierInventoryCount++;
                        ShowPrompt($"CRAFTED SCRAP BARRIER! Left-Click once to preview illusion, Left-Click again to place (Have: {barrierInventoryCount}).");
                    }
                    else ShowPrompt($"Need 2 Scrap to craft a Scrap Barrier (Have: {scrapCount}/2).");
                    return;
                }
            }

            // Soil Patch Scanning & Sapling Planting
            SoilPatchSlot patch = col.GetComponent<SoilPatchSlot>();
            if (patch != null && Input.GetKeyDown(keyInteract))
            {
                if (!hasSoilScanner)
                {
                    ShowPrompt("SOIL UNKNOWN! Craft the Soil Scanner at the Recycler first.");
                    return;
                }

                if (!patch.isScanned)
                {
                    patch.isScanned = true;
                    MeshRenderer mr = patch.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        if (patch.isFertile && patch.scannedGoodMat != null)
                            mr.sharedMaterial = patch.scannedGoodMat;
                        else if (!patch.isFertile && patch.scannedBadMat != null)
                            mr.sharedMaterial = patch.scannedBadMat;
                    }

                    if (patch.isFertile)
                        ShowPrompt("SCAN RESULT: FERTILE BIO-SOIL! Press [E] again to plant the Sapling (Place barriers with Left-Click first!).");
                    else
                        ShowPrompt("SCAN RESULT: TOXIC HEAVY-METAL SLUDGE! Search another valley.");
                }
                else if (patch.isFertile)
                {
                    if (hasSapling)
                    {
                        hasSapling = false;
                        placedBarriersSinceCheckpoint.Clear();
                        if (defenseManager != null)
                            defenseManager.StartDefenseSequence();
                        ShowPrompt("SAPLING PLANTED! Use Left-Click to place barriers & Right-Click to short-circuit rushing drones!");
                    }
                    else ShowPrompt("Retrieve the Cryo-Sapling from the Rock Cave first!");
                }
                return;
            }
        }
    }

    // ==================== 5. CHECKPOINT RESPAWN & PAUSE MENU ACTIONS ====================

    public void LoseBatteryLife()
    {
        CancelBarrierPreview();
        batteryLives--;

        if (batteryLives > 0)
        {
            if (CheckpointManager.Instance != null)
                CheckpointManager.Instance.RestorePlayerToCheckpoint(this);

            ShowPrompt($"DETECTED BY CORPO-DRONE! Respawned at Checkpoint with saved loot. Lives: {batteryLives}/3");
        }
        else
        {
            batteryLives = 3;
            RestartGame();
        }
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(false);
    }

    public void RestartMission()
    {
        ResumeGame();
        if (CheckpointManager.Instance != null && CheckpointManager.Instance.savedState.hasSnapshot)
        {
            CheckpointManager.Instance.RestorePlayerToCheckpoint(this);
            ShowPrompt("MISSION RESTARTED AT CHECKPOINT: Inventory & Loot restored!");
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        batteryLives = 3;
        totalScrapRecycled = 0;
        totalDronesRebooted = 0;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    // ==================== KEY REBINDING SYSTEM ====================

    public void OpenChangeControlsMenu()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(true);
        UpdateControlsUI();
    }

    public void CloseChangeControlsMenu()
    {
        waitingForKeyAction = "";
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
    }

    public void StartRebindingKey(string actionName)
    {
        waitingForKeyAction = actionName;
        if (rebindStatusText != null)
            rebindStatusText.text = $"Press any key/mouse button for [{actionName}] (ESC to cancel)...";
    }

    void ListenForKeyRebind()
    {
        if (!Input.anyKeyDown) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            waitingForKeyAction = "";
            if (rebindStatusText != null) rebindStatusText.text = "Rebind cancelled.";
            return;
        }

        foreach (KeyCode k in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(k))
            {
                AssignKey(waitingForKeyAction, k);
                PlayerPrefs.SetString("Key_" + waitingForKeyAction, k.ToString());
                PlayerPrefs.Save();
                waitingForKeyAction = "";
                UpdateControlsUI();
                if (rebindStatusText != null) rebindStatusText.text = $"Bound to [{k}]!";
                break;
            }
        }
    }

    void AssignKey(string action, KeyCode k)
    {
        switch (action)
        {
            case "Forward": keyForward = k; break;
            case "Backward": keyBackward = k; break;
            case "Left": keyLeft = k; break;
            case "Right": keyRight = k; break;
            case "Jump": keyJump = k; break;
            case "Crouch": keyCrouch = k; break;
            case "Interact": keyInteract = k; break;
            case "CraftBarrier": keyCraftBarrier = k; break;
            case "PlaceBarrier": keyPlaceBarrier = k; break;
            case "Sprint": keySprint = k; break;
        }
    }

    public void ResetDefaultControls()
    {
        keyForward = KeyCode.W; keyBackward = KeyCode.S; keyLeft = KeyCode.A; keyRight = KeyCode.D;
        keyJump = KeyCode.Space; keyCrouch = KeyCode.C; keySprint = KeyCode.LeftShift;
        keyInteract = KeyCode.E; keyCraftBarrier = KeyCode.Q; keyPlaceBarrier = KeyCode.Mouse0;
        UpdateControlsUI();
    }

    void LoadSavedControls()
    {
        keyPlaceBarrier = KeyCode.Mouse0;
    }

    void UpdateControlsUI()
    {
        if (forwardKeyText != null) forwardKeyText.text = $"Forward: {keyForward}";
        if (backwardKeyText != null) backwardKeyText.text = $"Backward: {keyBackward}";
        if (leftKeyText != null) leftKeyText.text = $"Left: {keyLeft}";
        if (rightKeyText != null) rightKeyText.text = $"Right: {keyRight}";
        if (jumpKeyText != null) jumpKeyText.text = $"Jump: {keyJump}";
        if (crouchKeyText != null) crouchKeyText.text = $"Crouch: {keyCrouch}";
        if (interactKeyText != null) interactKeyText.text = $"Interact: {keyInteract}";
        if (craftBarrierKeyText != null) craftBarrierKeyText.text = $"Craft Barrier: {keyCraftBarrier}";
        if (placeBarrierKeyText != null) placeBarrierKeyText.text = $"Place Wall (2-Click): {keyPlaceBarrier}";
        if (sprintKeyText != null) sprintKeyText.text = $"Servo Sprint (5s): {keySprint}";
    }

    public void ShowPrompt(string msg)
    {
        if (objectiveMessageText != null)
            objectiveMessageText.text = msg;
    }

    public void UpdateHUD() { }
}