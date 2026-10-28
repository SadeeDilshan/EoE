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

    [Header("Recycling & Level Puzzles")]
    public float interactRange = 4f;
    public int scrapCount = 0;
    public int energyCells = 0;
    public GameObject securityDoor;
    public TMP_Text hudText;

    [Header("Pause Menu & Controls UI Panels")]
    public GameObject pauseMenuPanel;    // Main Pause Menu (Resume, Restart, Controls, Quit)
    public GameObject controlsMenuPanel; // Sub-panel for remapping keys
    public TMP_Text rebindStatusText;    // Shows "Press any key..." when rebinding
    private bool isPaused = false;

    [Header("Control Binding Button Texts (Optional)")]
    public TMP_Text forwardKeyText;
    public TMP_Text backwardKeyText;
    public TMP_Text leftKeyText;
    public TMP_Text rightKeyText;
    public TMP_Text jumpKeyText;
    public TMP_Text crouchKeyText;
    public TMP_Text interactKeyText;

    [Header("Customizable Controls (Saved in PlayerPrefs)")]
    public static KeyCode keyForward = KeyCode.W;
    public static KeyCode keyBackward = KeyCode.S;
    public static KeyCode keyLeft = KeyCode.A;
    public static KeyCode keyRight = KeyCode.D;
    public static KeyCode keyJump = KeyCode.Space;
    public static KeyCode keyCrouch = KeyCode.C;
    public static KeyCode keyInteract = KeyCode.E;

    private string waitingForKeyAction = ""; // Tracks which control is currently being changed

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
        // 1. If currently waiting for user to press a new key in Controls Menu
        if (!string.IsNullOrEmpty(waitingForKeyAction))
        {
            ListenForKeyRebind();
            return;
        }

        // 2. Press ESC to toggle Pause Menu
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

        // Loads Level1 if it exists, otherwise scene index 0
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

    // Call this from your UI Buttons! Pass in: "Forward", "Backward", "Left", "Right", "Jump", "Crouch", or "Interact"
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
                // Ignore left mouse click (from clicking the UI button itself)
                if (kcode == KeyCode.Mouse0) continue;

                // Cancel rebinding if ESC is pressed
                if (kcode == KeyCode.Escape)
                {
                    waitingForKeyAction = "";
                    if (rebindStatusText != null) rebindStatusText.text = "Rebind cancelled.";
                    return;
                }
                // Auto-find panels by name if you forgot to drag them into the Inspector
                if (pauseMenuPanel == null)
                    pauseMenuPanel = GameObject.Find("PauseMenuPanel");
                if (controlsMenuPanel == null)
                    controlsMenuPanel = GameObject.Find("ControlsMenuPanel");

                // Assign the new key
                switch (waitingForKeyAction)
                {
                    case "Forward": keyForward = kcode; break;
                    case "Backward": keyBackward = kcode; break;
                    case "Left": keyLeft = kcode; break;
                    case "Right": keyRight = kcode; break;
                    case "Jump": keyJump = kcode; break;
                    case "Crouch": keyCrouch = kcode; break;
                    case "Interact": keyInteract = kcode; break;
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

        PlayerPrefs.SetString("Key_Forward", keyForward.ToString());
        PlayerPrefs.SetString("Key_Backward", keyBackward.ToString());
        PlayerPrefs.SetString("Key_Left", keyLeft.ToString());
        PlayerPrefs.SetString("Key_Right", keyRight.ToString());
        PlayerPrefs.SetString("Key_Jump", keyJump.ToString());
        PlayerPrefs.SetString("Key_Crouch", keyCrouch.ToString());
        PlayerPrefs.SetString("Key_Interact", keyInteract.ToString());
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
    }

    KeyCode ParseKey(string prefKey, KeyCode defaultKey)
    {
        string saved = PlayerPrefs.GetString(prefKey, defaultKey.ToString());
        if (Enum.TryParse(saved, out KeyCode parsed))
            return parsed;
        return defaultKey;
    }

    // ==================== PLAYER MOVEMENT & INTERACTION ====================

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

    void HandleInteraction()
    {
        if (Input.GetKeyDown(keyInteract))
        {
            if (playerCamera == null) return;

            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            Debug.DrawRay(ray.origin, ray.direction * interactRange, Color.green, 2f);

            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.35f, interactRange);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject == this.gameObject || hit.collider.transform.IsChildOf(this.transform))
                    continue;

                if (hit.collider.CompareTag("Scrap") || hit.collider.gameObject.name.Contains("Scrap"))
                {
                    scrapCount++;
                    totalScrapRecycled++;
                    SaveProgress();
                    Destroy(hit.collider.gameObject);
                    UpdateHUD();
                    return;
                }
                else if (hit.collider.CompareTag("Recycler") || hit.collider.gameObject.name.Contains("Recycler"))
                {
                    if (scrapCount >= 3)
                    {
                        scrapCount -= 3;
                        energyCells++;
                        UpdateHUD();
                        if (securityDoor != null)
                            securityDoor.SetActive(false);
                    }
                    return;
                }
                else if (hit.collider.CompareTag("Sapling") || hit.collider.gameObject.name.Contains("Sapling"))
                {
                    Destroy(hit.collider.gameObject);
                    if (hudText != null)
                        hudText.text = "SAPLING SECURED! Proceeding to Level 2...";
                    Invoke(nameof(LoadNextLevel), 2f);
                    return;
                }
                else if (hit.collider.CompareTag("SoilPatch") || hit.collider.gameObject.name.Contains("FertileSoil"))
                {
                    if (hudText != null)
                        hudText.text = "SAPLING PLANTED! Proceeding to Level 3...";
                    Invoke(nameof(LoadNextLevel), 2f);
                    return;
                }
                else if (hit.collider.GetComponent<CorpoDroneAI>() != null)
                {
                    hit.collider.GetComponent<CorpoDroneAI>().RebootAndSendBack();
                    totalDronesRebooted++;
                    SaveProgress();
                    UpdateHUD();
                    return;
                }
                else if (hit.collider.CompareTag("Terminal") || hit.collider.gameObject.name.Contains("Terminal"))
                {
                    Level3TreeDefense manager = FindFirstObjectByType<Level3TreeDefense>();
                    if (manager != null)
                        manager.RestoreAllBarricades();
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
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
        else
        {
            batteryLives = 3;
            RestartGame();
        }
    }

    void LoadNextLevel()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
    }

    void UpdateHUD()
    {
        if (hudText != null)
        {
            float co2Saved = totalScrapRecycled * 2.4f;
            hudText.text = $"Lives: {batteryLives} | Scrap: {scrapCount}/3 | CO2 Saved: {co2Saved:F1}kg";
        }
    }

}