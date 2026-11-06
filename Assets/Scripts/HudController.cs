using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("1. Top-Left Telemetry & Status Bars")]
    public CanvasGroup gameplayHudCanvasGroup;
    public TMP_Text batteryLivesLabel;
    public Image[] batteryCellIcons;
    public TMP_Text treeIntegrityLabel;
    public RectTransform treeHealthFillRT;
    public TMP_Text co2DivertedLabel;
    public RectTransform co2FillRT;
    public TMP_Text sprintStatusLabel;
    public RectTransform sprintFillRT;
    public Image sprintFillImage;

    [Header("2. Top-Center Defense Timer (Standby Until Level 3)")]
    public CanvasGroup timerHousingGroup;
    public TMP_Text timerStatusLabel;
    public TMP_Text timerText;
    public Image timerLedBorder;

    [Header("3. Bottom-Left Inventory Slots")]
    public TMP_Text scrapSlotText;
    public TMP_Text barrierSlotText;
    public TMP_Text scannerSlotText;
    public Image scannerSlotFrameImg;
    public Image scannerSlotIconImg;

    [Header("4. Right Hologram Assistant & 3D Camera Mount")]
    public GameObject holoMessagePanel;
    public CanvasGroup holoCanvasGroup;
    public TMP_Text messageText;
    public Transform holoModelMount; // Drag your custom Blender 3D Hologram inside this mount!
    public GameObject holoProjectorConeUI;
    public float displayDuration = 4.0f;
    public float typewriterSpeed = 0.018f;

    [Header("5. Menus & Post-Process Bokeh Blur")]
    public GameObject menuBlurOverlay;
    public GameObject startMenuPanel;
    public GameObject pauseMenuPanel;
    public GameObject controlsMenuPanel;
    public TMP_Text pauseSustainabilityStatsText;
    public bool showStartMenuOnLaunch = true;

    [Header("6. Scene References")]
    public ARIEPlayerController playerController;
    public Level3TreeDefense defenseManager;

    private string lastRawMessage = "";
    private string currentTypedText = "";
    private float hideTimer = 0f;
    private Vector3 holoStartLocalPos;
    private Coroutine typewriterRoutine;
    private bool isStartMenuOpen = false;
    private Volume blurPostProcessVolume;
    private DepthOfField dofComponent;

    void Start()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<ARIEPlayerController>();
        if (defenseManager == null)
            defenseManager = FindFirstObjectByType<Level3TreeDefense>();

        if (holoModelMount != null)
            holoStartLocalPos = holoModelMount.localPosition;

        InitializeCameraBlurVolume();

        if (showStartMenuOnLaunch && startMenuPanel != null)
        {
            OpenStartMenu();
        }
        else
        {
            if (startMenuPanel != null) startMenuPanel.SetActive(false);
            ApplyMenuBlurState(false);

            if (messageText != null && !string.IsNullOrEmpty(messageText.text))
                TriggerHoloMessage(messageText.text);
            else
                SetHoloActive(false);
        }
    }

    void Update()
    {
        bool anyMenuOpen = isStartMenuOpen ||
                           (pauseMenuPanel != null && pauseMenuPanel.activeSelf) ||
                           (controlsMenuPanel != null && controlsMenuPanel.activeSelf);

        ApplyMenuBlurState(anyMenuOpen);

        if (isStartMenuOpen)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (pauseMenuPanel != null && pauseMenuPanel.activeSelf)
                pauseMenuPanel.SetActive(false);
            return;
        }

        if (pauseMenuPanel != null && pauseMenuPanel.activeSelf && pauseSustainabilityStatsText != null)
        {
            float co2Saved = ARIEPlayerController.totalScrapRecycled * 2.4f;
            float treeHP = (defenseManager != null) ? Mathf.CeilToInt(defenseManager.treeHealth) : 100f;
            pauseSustainabilityStatsText.text =
                "<color=#55FFAA><b>ECO-TELEMETRY & IMPACT LOG</b></color>\n\n" +
                $"<b>E-WASTE DIVERTED:</b> <color=#55FF88>{ARIEPlayerController.totalScrapRecycled} UNITS</color>\n" +
                $"<b>CO2 EMISSIONS AVOIDED:</b> <color=#FFBB44>{co2Saved:F1} KG</color>\n" +
                $"<b>DRONES REBOOTED (EMP):</b> <color=#55EEFF>{ARIEPlayerController.totalDronesRebooted}</color>\n" +
                $"<b>SAPLING BIO-INTEGRITY:</b> <color=#55FF88>{treeHP}%</color>\n\n" +
                "<size=84%><color=#99C8B8>CIRCULAR DIRECTIVE: Upcycling industrial scrap into defensive barriers and clean energy cells prevents heavy-metal soil contamination.</color></size>";
        }

        UpdateLiveTelemetry();
        UpdateLevel3TimerDisplay();
        UpdateHoloAssistant();
    }

    // ==================== 1. LIVE TELEMETRY & SPRINT BAR ====================

    void UpdateLiveTelemetry()
    {
        int lives = ARIEPlayerController.batteryLives;
        if (batteryLivesLabel != null)
            batteryLivesLabel.text = $"CORE CELLS: {lives}/3";

        if (batteryCellIcons != null)
        {
            for (int i = 0; i < batteryCellIcons.Length; i++)
            {
                if (batteryCellIcons[i] != null)
                {
                    batteryCellIcons[i].color = (i < lives)
                        ? new Color(0.22f, 0.98f, 0.52f, 1f)
                        : new Color(0.16f, 0.20f, 0.18f, 0.35f);
                }
            }
        }

        float treeHP = (defenseManager != null) ? Mathf.Clamp(defenseManager.treeHealth, 0f, 100f) : 100f;
        if (treeIntegrityLabel != null)
            treeIntegrityLabel.text = $"BIO-INTEGRITY: {Mathf.CeilToInt(treeHP)}%";
        if (treeHealthFillRT != null)
        {
            float targetX = Mathf.Clamp01(treeHP / 100f);
            treeHealthFillRT.anchorMax = new Vector2(Mathf.Lerp(treeHealthFillRT.anchorMax.x, targetX, Time.deltaTime * 8f), 1f);
        }

        float co2Kg = ARIEPlayerController.totalScrapRecycled * 2.4f;
        if (co2DivertedLabel != null)
            co2DivertedLabel.text = $"CO2 OFFSET: {co2Kg:F1} KG";
        if (co2FillRT != null)
        {
            float targetCO2 = Mathf.Clamp01(co2Kg / 30f);
            co2FillRT.anchorMax = new Vector2(Mathf.Lerp(co2FillRT.anchorMax.x, Mathf.Max(0.04f, targetCO2), Time.deltaTime * 8f), 1f);
        }

        if (playerController != null)
        {
            // Servo Sprint Bar (5s sprint / 3s recharge)
            if (sprintFillRT != null)
            {
                float ratio = playerController.isSprintCoolingDown
                    ? 1f - (playerController.currentSprintCooldown / Mathf.Max(0.1f, playerController.sprintCooldownDuration))
                    : (playerController.currentSprintTime / Mathf.Max(0.1f, playerController.maxSprintDuration));

                sprintFillRT.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);

                if (sprintFillImage != null)
                {
                    sprintFillImage.color = playerController.isSprintCoolingDown
                        ? new Color(0.95f, 0.45f, 0.15f, 1f)
                        : new Color(0.25f, 0.88f, 0.98f, 1f);
                }

                if (sprintStatusLabel != null)
                {
                    sprintStatusLabel.text = playerController.isSprintCoolingDown
                        ? $"SERVO COOLING ({playerController.currentSprintCooldown:F1}S)"
                        : $"SERVO SPRINT [SHIFT]: {playerController.currentSprintTime:F1}S";
                }
            }

            if (scrapSlotText != null)
                scrapSlotText.text = $"E-WASTE SCRAP: {playerController.scrapCount}";

            if (barrierSlotText != null)
                barrierSlotText.text = $"BARRIERS [L-CLICK]: {playerController.barrierInventoryCount}";

            if (scannerSlotText != null)
            {
                bool ready = playerController.hasSoilScanner;
                scannerSlotText.text = ready ? "SOIL SCANNER: ONLINE" : "SOIL SCANNER: OFFLINE";
                scannerSlotText.color = ready ? new Color(0.32f, 1f, 0.58f) : new Color(0.62f, 0.68f, 0.65f);

                if (scannerSlotFrameImg != null)
                    scannerSlotFrameImg.color = ready ? new Color(0.25f, 0.95f, 0.52f, 1f) : new Color(0.55f, 0.58f, 0.56f, 1f);
                if (scannerSlotIconImg != null)
                    scannerSlotIconImg.color = ready ? new Color(0.32f, 1f, 0.58f, 1f) : new Color(0.52f, 0.56f, 0.54f, 0.7f);
            }
        }
    }

    // ==================== 2. LEVEL 3 DEFENSE TIMER (STANDBY UNTIL PLANTED) ====================

    void UpdateLevel3TimerDisplay()
    {
        bool level3Active = (defenseManager != null && defenseManager.defenseStarted);

        if (!level3Active)
        {
            // Keep Timer Panel in low-power Standby mode until Level 3 begins!
            if (timerHousingGroup != null)
                timerHousingGroup.alpha = 0.48f;
            if (timerStatusLabel != null)
            {
                timerStatusLabel.text = "DEFENSE CHRONO // STANDBY";
                timerStatusLabel.color = new Color(0.55f, 0.62f, 0.58f, 0.8f);
            }
            if (timerText != null)
            {
                timerText.text = "--:--";
                timerText.color = new Color(0.42f, 0.46f, 0.44f, 0.55f);
            }
            if (timerLedBorder != null)
                timerLedBorder.color = new Color(0.25f, 0.28f, 0.26f, 0.6f);
        }
        else
        {
            // Ignite the Timer Display when the Sapling is planted in Level 3!
            if (timerHousingGroup != null)
                timerHousingGroup.alpha = 1.0f;
            if (timerStatusLabel != null)
            {
                timerStatusLabel.text = "SAPLING DEFENSE ACTIVE";
                timerStatusLabel.color = new Color(1.0f, 0.78f, 0.22f, 1f);
            }
            if (timerText != null)
            {
                float t = Mathf.Max(0f, defenseManager.survivalTime);
                int mins = Mathf.FloorToInt(t / 60f);
                int secs = Mathf.FloorToInt(t % 60f);
                timerText.text = $"{mins:00}:{secs:00}";
                timerText.color = (t < 15f && Mathf.FloorToInt(Time.time * 4f) % 2 == 0)
                    ? new Color(1f, 0.3f, 0.25f, 1f)
                    : new Color(1.0f, 0.78f, 0.22f, 1f);
            }
            if (timerLedBorder != null)
                timerLedBorder.color = new Color(0.95f, 0.65f, 0.15f, 1f);
        }
    }

    // ==================== 3. HOLOGRAM ASSISTANT & 3D MOUNT ====================

    void UpdateHoloAssistant()
    {
        if (messageText == null) return;

        if (messageText.text != lastRawMessage && !messageText.text.StartsWith("<color=#66FFDD>"))
        {
            TriggerHoloMessage(messageText.text);
        }

        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;

            string formatted = $"<color=#66FFDD><b>CUSTODIAN COMMMANDS:</b></color>\n{currentTypedText}";
            messageText.text = formatted;
            lastRawMessage = formatted;

            if (holoModelMount != null)
            {
                if (!holoModelMount.gameObject.activeSelf)
                    holoModelMount.gameObject.SetActive(true);

                float bob = Mathf.Sin(Time.unscaledTime * 3.2f) * 0.018f;
                float yaw = Mathf.Sin(Time.unscaledTime * 1.4f) * 14f;
                holoModelMount.localPosition = holoStartLocalPos + new Vector3(0f, bob, 0f);
                holoModelMount.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f);
            }

            if (holoCanvasGroup != null)
                holoCanvasGroup.alpha = Mathf.Clamp01(hideTimer / 0.35f);

            if (hideTimer <= 0f)
                SetHoloActive(false);
        }
    }

    public void TriggerHoloMessage(string rawMessage)
    {
        hideTimer = displayDuration;
        SetHoloActive(true);

        if (holoCanvasGroup != null)
            holoCanvasGroup.alpha = 1f;

        if (typewriterRoutine != null)
            StopCoroutine(typewriterRoutine);

        typewriterRoutine = StartCoroutine(TypeSentence(rawMessage));
    }

    IEnumerator TypeSentence(string sentence)
    {
        currentTypedText = "";
        for (int i = 0; i <= sentence.Length; i++)
        {
            currentTypedText = sentence.Substring(0, i);
            yield return new WaitForSecondsRealtime(typewriterSpeed);
        }
    }

    void SetHoloActive(bool active)
    {
        if (holoMessagePanel != null) holoMessagePanel.SetActive(active);
        if (holoModelMount != null) holoModelMount.gameObject.SetActive(active);
        if (holoProjectorConeUI != null) holoProjectorConeUI.SetActive(active);
    }

    // ==================== 4. MENU ACTIONS & URP CAMERA BLUR ====================

    public void OpenStartMenu()
    {
        isStartMenuOpen = true;
        if (startMenuPanel != null) startMenuPanel.SetActive(true);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(false);
        SetHoloActive(false);

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ApplyMenuBlurState(true);
    }

    public void StartGameFromMenu()
    {
        isStartMenuOpen = false;
        if (startMenuPanel != null) startMenuPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (controlsMenuPanel != null) controlsMenuPanel.SetActive(false);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        ApplyMenuBlurState(false);

        if (messageText != null)
            TriggerHoloMessage("DIRECTIVE: Scavenge 3 E-Waste Scrap & power the Recycler [E] to unlock the North Blast Door.");
    }

    public void OpenControlsFromStartMenu()
    {
        isStartMenuOpen = false;
        if (startMenuPanel != null) startMenuPanel.SetActive(false);
        if (playerController != null)
        {
            playerController.PauseGame();
            playerController.OpenChangeControlsMenu();
        }
    }

    public void ResetSavedStatsFromMenu()
    {
        PlayerPrefs.DeleteAll();
        ARIEPlayerController.totalScrapRecycled = 0;
        ARIEPlayerController.totalDronesRebooted = 0;
        if (playerController != null)
            playerController.ResetDefaultControls();
    }

    void ApplyMenuBlurState(bool blurred)
    {
        if (menuBlurOverlay != null && menuBlurOverlay.activeSelf != blurred)
            menuBlurOverlay.SetActive(blurred);

        if (gameplayHudCanvasGroup != null)
        {
            float targetAlpha = blurred ? 0.12f : 1.0f;
            gameplayHudCanvasGroup.alpha = Mathf.MoveTowards(gameplayHudCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 8f);
        }

        if (holoModelMount != null && blurred)
            holoModelMount.gameObject.SetActive(false);

        if (dofComponent != null)
            dofComponent.active = blurred;
    }

    void InitializeCameraBlurVolume()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            UniversalAdditionalCameraData camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = true;
        }

        GameObject volObj = GameObject.Find("UI_DepthOfField_Volume");
        if (volObj == null)
        {
            volObj = new GameObject("UI_DepthOfField_Volume");
            volObj.transform.SetParent(this.transform, false);
        }

        blurPostProcessVolume = volObj.GetComponent<Volume>();
        if (blurPostProcessVolume == null)
            blurPostProcessVolume = volObj.AddComponent<Volume>();

        blurPostProcessVolume.isGlobal = true;
        blurPostProcessVolume.priority = 99f;

        if (blurPostProcessVolume.profile == null)
            blurPostProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

        if (!blurPostProcessVolume.profile.TryGet(out dofComponent))
            dofComponent = blurPostProcessVolume.profile.Add<DepthOfField>(true);

        dofComponent.mode.Override(DepthOfFieldMode.Bokeh);
        dofComponent.focusDistance.Override(0.1f);
        dofComponent.focalLength.Override(180f);
        dofComponent.aperture.Override(3.5f);
        dofComponent.active = false;
    }
}