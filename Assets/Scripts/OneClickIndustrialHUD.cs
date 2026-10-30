using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

public class OneClickIndustrialHUD : MonoBehaviour
{
    [Header("1. In-Game HUD Elements (Blurred During Menus)")]
    public CanvasGroup gameplayHudCanvasGroup;
    public TMP_Text batteryLivesLabel;
    public Image[] batteryCellIcons;
    public TMP_Text treeIntegrityLabel;
    public RectTransform treeHealthFillRT;
    public TMP_Text co2DivertedLabel;
    public RectTransform co2FillRT;
    public TMP_Text timerText;
    public TMP_Text scrapSlotText;
    public TMP_Text barrierSlotText;
    public TMP_Text scannerSlotText;
    public Image scannerSlotFrameImg;

    [Header("2. Right Hologram Assistant UI")]
    public GameObject holoMessagePanel;
    public CanvasGroup holoCanvasGroup;
    public TMP_Text messageText;
    public Transform holoHead3D;
    public GameObject holoProjectorConeUI;
    public GameObject customHoloModelPrefab;
    public float displayDuration = 3.5f;
    public float typewriterSpeed = 0.02f;

    [Header("3. Start, Pause & Controls Menus + Blur Overlay")]
    public GameObject menuBlurOverlay;
    public GameObject startMenuPanel;
    public GameObject pauseMenuPanel;
    public GameObject controlsMenuPanel;
    public TMP_Text pauseSustainabilityStatsText;
    public bool showStartMenuOnLaunch = true;

    [Header("4. Live Game Script Links (Auto-Assigned)")]
    public ARIEPlayerController playerController;
    public Level3TreeDefense defenseManager;

    private string lastRawMessage = "";
    private string currentTypedText = "";
    private float hideTimer = 0f;
    private Vector3 holoStartPos;
    private Coroutine typewriterRoutine;
    private bool isStartMenuOpen = false;
    private Volume blurPostProcessVolume;
    private DepthOfField dofComponent;

    // Procedural Artwork Sprites Cache
    private static Sprite _metalPanelSprite;
    private static Sprite _darkWellSprite;
    private static Sprite _buttonNormalSprite;
    private static Sprite _buttonAccentSprite;
    private static Sprite _holoBoxSprite;
    private static Sprite _holoConeSprite;
    private static Sprite _circleBadgeSprite;
    private static Sprite _crosshairSprite;
    private static Sprite _batterySprite;
    private static Sprite _frostedBlurSprite;
    private static Sprite _whiteSprite;

    void Start()
    {
        if (playerController == null)
            playerController = FindFirstObjectByType<ARIEPlayerController>();
        if (defenseManager == null)
            defenseManager = FindFirstObjectByType<Level3TreeDefense>();

        if (holoHead3D != null)
            holoStartPos = holoHead3D.localPosition;

        SetupRuntimeCameraBlurVolume();

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
        // Detect if Start Menu, Pause Menu, or Controls Menu is currently active
        bool anyMenuOpen = isStartMenuOpen ||
                           (pauseMenuPanel != null && pauseMenuPanel.activeSelf) ||
                           (controlsMenuPanel != null && controlsMenuPanel.activeSelf);

        ApplyMenuBlurState(anyMenuOpen);

        // Keep Start Menu paused & cursor unlocked even if user presses ESC
        if (isStartMenuOpen)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (pauseMenuPanel != null && pauseMenuPanel.activeSelf)
                pauseMenuPanel.SetActive(false);
            return;
        }

        // Update live Sustainability Dashboard whenever Pause Menu is open
        if (pauseMenuPanel != null && pauseMenuPanel.activeSelf && pauseSustainabilityStatsText != null)
        {
            float co2Saved = ARIEPlayerController.totalScrapRecycled * 2.4f;
            float treeHP = (defenseManager != null) ? Mathf.CeilToInt(defenseManager.treeHealth) : 100f;
            pauseSustainabilityStatsText.text =
                "<color=#66FFCC><b>ECO-TELEMETRY & IMPACT LOG</b></color>\n\n" +
                $"• <b>E-WASTE DIVERTED:</b> <color=#55FF88>{ARIEPlayerController.totalScrapRecycled} UNITS</color>\n" +
                $"• <b>CO2 EMISSIONS AVOIDED:</b> <color=#FFBB44>{co2Saved:F1} KG</color>\n" +
                $"• <b>DRONES REBOOTED (NON-LETHAL):</b> <color=#66EEFF>{ARIEPlayerController.totalDronesRebooted}</color>\n" +
                $"• <b>SAPLING BIO-INTEGRITY:</b> <color=#55FF88>{treeHP}%</color>\n\n" +
                "<size=82%><color=#99DDCC>CIRCULAR DIRECTIVE: Upcycling industrial scrap into defensive barriers & clean energy cells prevents toxic heavy-metal soil leaching.</color></size>";
        }

        UpdateLiveIndustrialStats();
        UpdateHoloAssistantPopup();
    }

    // ==================== START MENU & BLUR CONTROLLER ====================

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
            TriggerHoloMessage("OBJECTIVE: Scavenge 3 Scrap & Power the Recycler [E] to unlock the Blast Door.");
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
        Debug.Log("All Sustainability Metrics & Keybinds reset to factory defaults!");
    }

    void ApplyMenuBlurState(bool blurred)
    {
        // 1. Toggle frosted scanline blur veil over the screen
        if (menuBlurOverlay != null && menuBlurOverlay.activeSelf != blurred)
            menuBlurOverlay.SetActive(blurred);

        // 2. Dim & fade the background HUD panels so the Pause/Start Menu pops out cleanly
        if (gameplayHudCanvasGroup != null)
        {
            float targetAlpha = blurred ? 0.18f : 1.0f;
            gameplayHudCanvasGroup.alpha = Mathf.MoveTowards(gameplayHudCanvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 8f);
        }

        // 3. Hide the 3D camera hologram head while inside menus so it never overlaps buttons
        if (holoHead3D != null && blurred)
            holoHead3D.gameObject.SetActive(false);

        // 4. Enable real URP Depth-of-Field Bokeh Blur on the 3D world camera
        if (dofComponent != null)
            dofComponent.active = blurred;
    }

    void SetupRuntimeCameraBlurVolume()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            UniversalAdditionalCameraData camData = cam.GetComponent<UniversalAdditionalCameraData>();
            if (camData != null) camData.renderPostProcessing = true;
        }

        GameObject volObj = GameObject.Find("MenuBlur_PostProcessVolume");
        if (volObj == null)
        {
            volObj = new GameObject("MenuBlur_PostProcessVolume");
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

    // ==================== LIVE HUD UPDATER ====================

    void UpdateLiveIndustrialStats()
    {
        int lives = ARIEPlayerController.batteryLives;
        if (batteryLivesLabel != null)
            batteryLivesLabel.text = $"BATTERY LIVES: {lives}/3";

        if (batteryCellIcons != null)
        {
            for (int i = 0; i < batteryCellIcons.Length; i++)
            {
                if (batteryCellIcons[i] != null)
                {
                    batteryCellIcons[i].color = (i < lives)
                        ? new Color(0.25f, 1.0f, 0.55f, 1f)
                        : new Color(0.18f, 0.24f, 0.20f, 0.45f);
                }
            }
        }

        float treeHP = (defenseManager != null) ? Mathf.Clamp(defenseManager.treeHealth, 0f, 100f) : 100f;
        if (treeIntegrityLabel != null)
            treeIntegrityLabel.text = $"TREE INTEGRITY: {Mathf.CeilToInt(treeHP)}%";
        if (treeHealthFillRT != null)
        {
            float targetX = Mathf.Clamp01(treeHP / 100f);
            treeHealthFillRT.anchorMax = new Vector2(Mathf.Lerp(treeHealthFillRT.anchorMax.x, targetX, Time.deltaTime * 8f), 1f);
        }

        float co2Kg = ARIEPlayerController.totalScrapRecycled * 2.4f;
        if (co2DivertedLabel != null)
            co2DivertedLabel.text = $"CO2 DIVERTED: {co2Kg:F1} KG";
        if (co2FillRT != null)
        {
            float targetCO2 = (co2Kg <= 0.01f) ? 0.35f : Mathf.Clamp01(co2Kg / 30f);
            co2FillRT.anchorMax = new Vector2(Mathf.Lerp(co2FillRT.anchorMax.x, targetCO2, Time.deltaTime * 8f), 1f);
        }

        if (playerController != null)
        {
            if (scrapSlotText != null)
                scrapSlotText.text = $"SCRAP: {playerController.scrapCount}";

            if (barrierSlotText != null)
                barrierSlotText.text = $"BARRIERS [{ARIEPlayerController.keyPlaceBarrier}]: {playerController.barrierInventoryCount}";

            if (scannerSlotText != null)
            {
                bool ready = playerController.hasSoilScanner;
                scannerSlotText.text = ready ? "SOIL SCANNER: READY" : "SOIL SCANNER: OFFLINE";
                scannerSlotText.color = ready ? new Color(0.35f, 1f, 0.55f) : new Color(0.75f, 0.82f, 0.78f);

                if (scannerSlotFrameImg != null)
                {
                    scannerSlotFrameImg.color = ready
                        ? new Color(0.25f, 0.98f, 0.48f, 1f)
                        : new Color(0.45f, 0.48f, 0.46f, 1f);
                }
            }
        }
    }

    // ==================== HOLO-ASSISTANT AUTO-HIDE & 3D BOB ====================

    void UpdateHoloAssistantPopup()
    {
        if (messageText == null) return;

        if (messageText.text != lastRawMessage && !messageText.text.Contains("(Auto-hides in"))
        {
            TriggerHoloMessage(messageText.text);
        }

        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;

            string displayString = $"<color=#77FFDD><b>HOLO-ASSIST  HINT:</b></color>\n{currentTypedText}\n<size=82%></size>";
            messageText.text = displayString;
            lastRawMessage = displayString;

            if (holoHead3D != null)
            {
                if (!holoHead3D.gameObject.activeSelf) holoHead3D.gameObject.SetActive(true);
                float bobOffset = Mathf.Sin(Time.unscaledTime * 3.5f) * 0.02f;
                float yawAngle = Mathf.Sin(Time.unscaledTime * 1.6f) * 16f;
                holoHead3D.localPosition = holoStartPos + new Vector3(0f, bobOffset, 0f);
                holoHead3D.localRotation = Quaternion.Euler(0f, 180f + yawAngle, 0f);
            }

            if (holoCanvasGroup != null)
                holoCanvasGroup.alpha = Mathf.Clamp01(hideTimer / 0.35f);

            if (hideTimer <= 0f)
            {
                SetHoloActive(false);
            }
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
        if (holoHead3D != null) holoHead3D.gameObject.SetActive(active);
        if (holoProjectorConeUI != null) holoProjectorConeUI.SetActive(active);
    }

    // ==================== ONE-CLICK MASTER BUILDER ====================

    [ContextMenu("⚡ BUILD HUD + START MENU + PAUSE & CONTROLS MENUS")]
    public void BuildIndustrialHUDNow()
    {
        EnsureProceduralSprites();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("IndustrialHUD_Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
        }

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Clean up any old generated UI objects
        string[] cleanupNames = { "Generated_Industrial_HUD", "MenuBlur_Overlay", "StartMenuPanel", "PauseMenuPanel", "ControlsMenuPanel" };
        foreach (string cName in cleanupNames)
        {
            Transform old = canvas.transform.Find(cName);
            if (old != null) DestroyImmediate(old.gameObject);
        }

        playerController = FindFirstObjectByType<ARIEPlayerController>();
        defenseManager = FindFirstObjectByType<Level3TreeDefense>();

        // =========================================================================
        // PART A: IN-GAMEPLAY HUD LAYER (Fades & Blurs when Pause/Start Menu opens)
        // =========================================================================
        GameObject hudRoot = CreateUIObject("Generated_Industrial_HUD", canvas.transform);
        StretchFullScreen(hudRoot.GetComponent<RectTransform>());
        gameplayHudCanvasGroup = hudRoot.AddComponent<CanvasGroup>();

        GameObject wiresLayer = CreateUIObject("Layer_Industrial_Wires_And_Pipes", hudRoot.transform);
        StretchFullScreen(wiresLayer.GetComponent<RectTransform>());

        CreateConduitPipe(wiresLayer.transform, new Vector2(0f, 1f), new Vector2(68f, -245f), new Vector2(14f, 540f), new Color(0.14f, 0.15f, 0.14f, 0.95f));
        CreateConduitPipe(wiresLayer.transform, new Vector2(0f, 1f), new Vector2(92f, -245f), new Vector2(8f, 540f), new Color(0.28f, 0.22f, 0.14f, 0.9f));

        BuildBezierWire(wiresLayer.transform, "Cable_Upper_Heavy",
            new Vector2(485f, -58f), new Vector2(640f, -155f), new Vector2(805f, -55f),
            10f, new Color(0.11f, 0.12f, 0.12f, 0.98f), false);
        BuildBezierWire(wiresLayer.transform, "Cable_Mid_Braided",
            new Vector2(485f, -95f), new Vector2(635f, -235f), new Vector2(805f, -88f),
            7f, new Color(0.24f, 0.21f, 0.17f, 0.96f), false);
        BuildBezierWire(wiresLayer.transform, "Cable_Glowing_Power",
            new Vector2(485f, -135f), new Vector2(650f, -265f), new Vector2(805f, -110f),
            5.5f, new Color(0.75f, 0.95f, 0.32f, 0.95f), true);
        BuildBezierWire(wiresLayer.transform, "Cable_BottomLeft_Loop",
            new Vector2(54f, -248f), new Vector2(150f, -330f), new Vector2(260f, -248f),
            8f, new Color(0.15f, 0.16f, 0.15f, 0.95f), false);

        GameObject crosshairObj = CreateUIObject("Center_Tactical_Crosshair", hudRoot.transform);
        RectTransform chRT = crosshairObj.GetComponent<RectTransform>();
        chRT.anchorMin = new Vector2(0.5f, 0.5f);
        chRT.anchorMax = new Vector2(0.5f, 0.5f);
        chRT.pivot = new Vector2(0.5f, 0.5f);
        chRT.anchoredPosition = Vector2.zero;
        chRT.sizeDelta = new Vector2(280f, 200f);
        Image chImg = crosshairObj.AddComponent<Image>();
        chImg.sprite = _crosshairSprite;
        chImg.color = new Color(0.75f, 0.95f, 0.88f, 0.38f);
        chImg.raycastTarget = false;

        // Top-Left Status Panel
        GameObject topLeftFrame = CreateBeveledMetalPanel("Panel_TopLeft_Status", hudRoot.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -26f), new Vector2(465f, 236f), true);

        batteryLivesLabel = CreateTMP("Txt_BatteryLives", topLeftFrame.transform,
            "BATTERY LIVES: 3/3", 21, new Vector2(26f, -22f), new Vector2(260f, 36f), new Color(0.84f, 0.96f, 0.90f));

        TMP_Text boltIcon = CreateTMP("Icon_Lightning", topLeftFrame.transform,
            "⚡", 22, new Vector2(292f, -22f), new Vector2(30f, 36f), new Color(0.3f, 1f, 0.55f));
        boltIcon.alignment = TextAlignmentOptions.Center;

        batteryCellIcons = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject cell = CreateUIObject($"BatteryCell_{i + 1}", topLeftFrame.transform);
            RectTransform rt = cell.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(326f + (i * 36f), -16f);
            rt.sizeDelta = new Vector2(28f, 44f);
            Image img = cell.AddComponent<Image>();
            img.sprite = _batterySprite;
            img.color = new Color(0.25f, 1f, 0.55f, 1f);
            batteryCellIcons[i] = img;
        }

        treeIntegrityLabel = CreateTMP("Txt_TreeIntegrity", topLeftFrame.transform,
            "TREE INTEGRITY: 98%", 20, new Vector2(26f, -74f), new Vector2(400f, 28f), new Color(0.84f, 0.96f, 0.90f));
        treeHealthFillRT = CreateIndustrialProgressBar("Bar_TreeIntegrity", topLeftFrame.transform,
            new Vector2(26f, -106f), new Vector2(412f, 24f), new Color(0.28f, 1f, 0.52f, 1f), 0.98f);

        co2DivertedLabel = CreateTMP("Txt_CO2Diverted", topLeftFrame.transform,
            "CO2 DIVERTED: 14.4 KG", 20, new Vector2(26f, -146f), new Vector2(400f, 28f), new Color(0.84f, 0.96f, 0.90f));
        co2FillRT = CreateIndustrialProgressBar("Bar_CO2Diverted", topLeftFrame.transform,
            new Vector2(26f, -178f), new Vector2(412f, 22f), new Color(0.96f, 0.64f, 0.16f, 1f), 0.38f);

        // Top-Center Timer Panel
        GameObject topCenterFrame = CreateBeveledMetalPanel("Panel_TopCenter_Timer", hudRoot.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(310f, 126f), true);
        topCenterFrame.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
        CreateSideBumper(topCenterFrame.transform, new Vector2(-18f, -18f), new Vector2(16f, 90f), true);
        CreateSideBumper(topCenterFrame.transform, new Vector2(18f, -18f), new Vector2(16f, 90f), false);

        TMP_Text ghostTimer = CreateTMP("TimerGhost88", topCenterFrame.transform,
            "88:88", 68, Vector2.zero, new Vector2(280f, 100f), new Color(0.25f, 0.18f, 0.08f, 0.25f));
        StretchFullScreen(ghostTimer.GetComponent<RectTransform>());
        ghostTimer.alignment = TextAlignmentOptions.Center;

        timerText = CreateTMP("TimerText", topCenterFrame.transform,
            "00:45", 68, Vector2.zero, new Vector2(280f, 100f), new Color(1.0f, 0.76f, 0.22f, 1f));
        StretchFullScreen(timerText.GetComponent<RectTransform>());
        timerText.alignment = TextAlignmentOptions.Center;
        Outline timerGlow = timerText.gameObject.AddComponent<Outline>();
        timerGlow.effectColor = new Color(0.95f, 0.45f, 0.05f, 0.55f);
        timerGlow.effectDistance = new Vector2(2f, -2f);

        // Bottom-Left Inventory Panel
        GameObject bottomLeftFrame = CreateBeveledMetalPanel("Panel_BottomLeft_Inventory", hudRoot.transform,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 28f), new Vector2(445f, 242f), true);
        bottomLeftFrame.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);

        CreateInventoryRow(bottomLeftFrame.transform, "Slot_Scrap",
            new Vector2(18f, -18f), new Vector2(408f, 60f), "⚙", "A", false, out scrapSlotText, out _);
        scrapSlotText.text = "SCRAP: 4";

        CreateInventoryRow(bottomLeftFrame.transform, "Slot_Barriers",
            new Vector2(18f, -90f), new Vector2(408f, 60f), "▦", "F", false, out barrierSlotText, out _);
        barrierSlotText.text = "BARRIERS [F]: 2";

        CreateInventoryRow(bottomLeftFrame.transform, "Slot_SoilScanner",
            new Vector2(18f, -162f), new Vector2(408f, 60f), "🌱", "E", true, out scannerSlotText, out scannerSlotFrameImg);
        scannerSlotText.text = "SOIL SCANNER: READY";

        // Right Hologram Assistant Panel
        holoProjectorConeUI = CreateUIObject("HoloProjector_LightCone", hudRoot.transform);
        RectTransform coneRT = holoProjectorConeUI.GetComponent<RectTransform>();
        coneRT.anchorMin = new Vector2(1f, 0.5f);
        coneRT.anchorMax = new Vector2(1f, 0.5f);
        coneRT.pivot = new Vector2(0.5f, 1f);
        coneRT.anchoredPosition = new Vector2(-195f, -205f);
        coneRT.sizeDelta = new Vector2(280f, 190f);
        Image coneImg = holoProjectorConeUI.AddComponent<Image>();
        coneImg.sprite = _holoConeSprite;
        coneImg.color = new Color(0.2f, 1f, 0.85f, 0.65f);
        coneImg.raycastTarget = false;

        holoMessagePanel = CreateUIObject("HoloMessagePanel", hudRoot.transform);
        RectTransform holoRT = holoMessagePanel.GetComponent<RectTransform>();
        holoRT.anchorMin = new Vector2(1f, 0.5f);
        holoRT.anchorMax = new Vector2(1f, 0.5f);
        holoRT.pivot = new Vector2(1f, 0.5f);
        holoRT.anchoredPosition = new Vector2(-36f, -100f);
        holoRT.sizeDelta = new Vector2(445f, 225f);

        Image holoImg = holoMessagePanel.AddComponent<Image>();
        holoImg.sprite = _holoBoxSprite;
        holoImg.color = Color.white;
        holoCanvasGroup = holoMessagePanel.AddComponent<CanvasGroup>();

        messageText = CreateTMP("MessageText", holoMessagePanel.transform,
            "Craft Scrap Barriers [Q] at the Recycler and press [F] to fortify the fertile soil before planting!", 20,
            new Vector2(24f, -20f), new Vector2(398f, 185f), new Color(0.88f, 1f, 0.96f));
        messageText.textWrappingMode = TextWrappingModes.Normal;
        messageText.alignment = TextAlignmentOptions.TopLeft;

        // =========================================================================
        // PART B: FULL-SCREEN FROSTED BLUR VEIL (Activates on ESC Pause or Start)
        // =========================================================================
        menuBlurOverlay = CreateUIObject("MenuBlur_Overlay", canvas.transform);
        StretchFullScreen(menuBlurOverlay.GetComponent<RectTransform>());
        Image blurImg = menuBlurOverlay.AddComponent<Image>();
        blurImg.sprite = _frostedBlurSprite;
        blurImg.color = new Color(0.02f, 0.06f, 0.05f, 0.84f);
        menuBlurOverlay.SetActive(false);

        // =========================================================================
        // PART C: INDUSTRIAL START MENU PANEL (StartMenuPanel)
        // =========================================================================
        BuildStartMenuUI(canvas.transform);

        // =========================================================================
        // PART D: INDUSTRIAL PAUSE MENU + SUSTAINABILITY DASHBOARD (PauseMenuPanel)
        // =========================================================================
        BuildPauseMenuUI(canvas.transform);

        // =========================================================================
        // PART E: INDUSTRIAL 9-KEY REMAPPING MENU (ControlsMenuPanel)
        // =========================================================================
        BuildControlsMenuUI(canvas.transform);

        // =========================================================================
        // PART F: 3D HOLOGRAM BUST & AUTOMATIC SCRIPT WIRING
        // =========================================================================
        Build3DHologramBustOnCamera();
        AutoWireExistingGameScripts();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        EditorUtility.SetDirty(canvas.gameObject);
#endif
        Debug.Log("✅ Complete Industrial Sci-Fi HUD, Start Menu, Pause Menu (with Blur) & Controls Remapper Built!");
    }

    // ==================== MENU BUILDERS (START, PAUSE, CONTROLS) ====================

    void BuildStartMenuUI(Transform canvasTransform)
    {
        startMenuPanel = CreateUIObject("StartMenuPanel", canvasTransform);
        StretchFullScreen(startMenuPanel.GetComponent<RectTransform>());

        // Hanging decorative power cables connected to the Start Menu Console
        BuildBezierWire(startMenuPanel.transform, "StartWire_Left",
            new Vector2(180f, 0f), new Vector2(380f, -280f), new Vector2(560f, -210f),
            11f, new Color(0.12f, 0.13f, 0.13f, 0.98f), false);
        BuildBezierWire(startMenuPanel.transform, "StartWire_RightGlow",
            new Vector2(1740f, 0f), new Vector2(1540f, -310f), new Vector2(1360f, -220f),
            6f, new Color(0.3f, 1.0f, 0.65f, 0.92f), true);

        // Top Main Title Header Frame
        GameObject titleFrame = CreateBeveledMetalPanel("Header_GameTitle", startMenuPanel.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -65f), new Vector2(920f, 165f), true);
        titleFrame.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
        CreateSideBumper(titleFrame.transform, new Vector2(-20f, -22f), new Vector2(18f, 120f), true);
        CreateSideBumper(titleFrame.transform, new Vector2(20f, -22f), new Vector2(18f, 120f), false);

        TMP_Text mainTitle = CreateTMP("Txt_MainTitle", titleFrame.transform,
            "ECHOES OF EDEN", 62, new Vector2(0f, -18f), new Vector2(880f, 75f), new Color(0.35f, 1.0f, 0.62f));
        mainTitle.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1f);
        mainTitle.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
        mainTitle.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
        mainTitle.alignment = TextAlignmentOptions.Center;

        TMP_Text subTitle = CreateTMP("Txt_SubTitle", titleFrame.transform,
            "UNIT AR-IE : THE LAST CUSTODIAN — RESTORATION PROTOCOL", 20,
            new Vector2(0f, -98f), new Vector2(860f, 40f), new Color(1.0f, 0.78f, 0.28f));
        subTitle.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 1f);
        subTitle.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1f);
        subTitle.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
        subTitle.alignment = TextAlignmentOptions.Center;

        // Center Command Console Frame
        GameObject centerConsole = CreateBeveledMetalPanel("StartConsole_Frame", startMenuPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -55f), new Vector2(620f, 470f), true);
        centerConsole.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

        TMP_Text sysReady = CreateTMP("Txt_SysStatus", centerConsole.transform,
            "SYSTEM DIAGNOSTICS: ONLINE  |  BIOSPHERE SEED: SECURED", 17,
            new Vector2(36f, -24f), new Vector2(548f, 30f), new Color(0.65f, 0.95f, 0.82f));
        sysReady.alignment = TextAlignmentOptions.Center;

        // 4 Industrial Start Menu Buttons
        Button btnStart = CreateIndustrialButton(centerConsole.transform, "Btn_StartGame",
            "▶  INITIALIZE CUSTODIAN [START]", new Vector2(50f, -72f), new Vector2(520f, 68f), true, out _);

        Button btnStartControls = CreateIndustrialButton(centerConsole.transform, "Btn_StartControls",
            "⚙  CONFIGURE SYSTEM CONTROLS", new Vector2(50f, -158f), new Vector2(520f, 64f), false, out _);

        Button btnResetData = CreateIndustrialButton(centerConsole.transform, "Btn_ResetSavedStats",
            "♻  RESET ECO-METRICS & DEFAULTS", new Vector2(50f, -240f), new Vector2(520f, 64f), false, out _);

        Button btnQuitStart = CreateIndustrialButton(centerConsole.transform, "Btn_StartQuit",
            "✖  SHUTDOWN SYSTEM [QUIT]", new Vector2(50f, -322f), new Vector2(520f, 64f), false, out _);

        TMP_Text footerNote = CreateTMP("Txt_StartFooter", centerConsole.transform,
            "PRESS [ESC] ANYTIME IN-GAME TO ACCESS PAUSE & SUSTAINABILITY TELEMETRY", 15,
            new Vector2(30f, -412f), new Vector2(560f, 32f), new Color(0.65f, 0.72f, 0.68f));
        footerNote.alignment = TextAlignmentOptions.Center;

        BindButtonAction(btnStart, this.StartGameFromMenu);
        BindButtonAction(btnStartControls, this.OpenControlsFromStartMenu);
        BindButtonAction(btnResetData, this.ResetSavedStatsFromMenu);
        if (playerController != null)
            BindButtonAction(btnQuitStart, playerController.QuitGame);
    }

    void BuildPauseMenuUI(Transform canvasTransform)
    {
        pauseMenuPanel = CreateUIObject("PauseMenuPanel", canvasTransform);
        StretchFullScreen(pauseMenuPanel.GetComponent<RectTransform>());

        // Hanging cables connecting top of screen into the Pause Console
        BuildBezierWire(pauseMenuPanel.transform, "PauseWire_Left",
            new Vector2(390f, 0f), new Vector2(490f, -210f), new Vector2(610f, -160f),
            9f, new Color(0.14f, 0.15f, 0.14f, 0.98f), false);
        BuildBezierWire(pauseMenuPanel.transform, "PauseWire_PowerGlow",
            new Vector2(1530f, 0f), new Vector2(1410f, -230f), new Vector2(1290f, -165f),
            5.5f, new Color(0.95f, 0.72f, 0.2f, 0.95f), true);

        // Main Split-Console Frame (Left: 5 Action Buttons | Right: Hologram Sustainability Dashboard)
        GameObject pauseConsole = CreateBeveledMetalPanel("PauseConsole_MasterFrame", pauseMenuPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 610f), true);
        pauseConsole.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
        CreateSideBumper(pauseConsole.transform, new Vector2(-18f, -45f), new Vector2(16f, 520f), true);
        CreateSideBumper(pauseConsole.transform, new Vector2(18f, -45f), new Vector2(16f, 520f), false);

        // Header Bar
        TMP_Text pauseHeader = CreateTMP("Txt_PauseHeader", pauseConsole.transform,
            "⚙  SYSTEM PAUSED ", 30,
            new Vector2(38f, -26f), new Vector2(980f, 44f), new Color(1.0f, 0.78f, 0.24f));

        // Left Column: 5 Required Pause Buttons
        Button btnResume = CreateIndustrialButton(pauseConsole.transform, "Btn_Resume",
            "▶  RESUME MISSION ", new Vector2(38f, -95f), new Vector2(460f, 68f), true, out _);

        Button btnRestartMission = CreateIndustrialButton(pauseConsole.transform, "Btn_RestartMission",
            "↻  RESTART CURRENT MISSION", new Vector2(38f, -182f), new Vector2(460f, 68f), false, out _);

        Button btnRestartGame = CreateIndustrialButton(pauseConsole.transform, "Btn_RestartGame",
            "⏮  RESTART FULL GAME", new Vector2(38f, -269f), new Vector2(460f, 68f), false, out _);

        Button btnChangeControls = CreateIndustrialButton(pauseConsole.transform, "Btn_ChangeControls",
            "⌨  CHANGE CONTROLS", new Vector2(38f, -356f), new Vector2(460f, 68f), false, out _);

        Button btnQuit = CreateIndustrialButton(pauseConsole.transform, "Btn_Quit",
            "✖  QUIT TO DESKTOP", new Vector2(38f, -443f), new Vector2(460f, 68f), false, out _);

        // Right Column: Holographic Sustainability & Circular Economy Telemetry Box
        GameObject holoDash = CreateUIObject("Holo_Sustainability_Dashboard", pauseConsole.transform);
        RectTransform dashRT = holoDash.GetComponent<RectTransform>();
        dashRT.anchorMin = new Vector2(0f, 1f);
        dashRT.anchorMax = new Vector2(0f, 1f);
        dashRT.pivot = new Vector2(0f, 1f);
        dashRT.anchoredPosition = new Vector2(530f, -95f);
        dashRT.sizeDelta = new Vector2(490f, 416f);

        Image dashImg = holoDash.AddComponent<Image>();
        dashImg.sprite = _holoBoxSprite;
        dashImg.color = Color.white;

        pauseSustainabilityStatsText = CreateTMP("Txt_SustainabilityStats", holoDash.transform,
            "<color=#66FFCC><b> ECO-TELEMETRY & IMPACT LOG</b></color>\n\n" +
            "• <b>E-WASTE DIVERTED:</b> <color=#55FF88>0 UNITS</color>\n" +
            "• <b>CO2 EMISSIONS AVOIDED:</b> <color=#FFBB44>0.0 KG</color>\n" +
            "• <b>DRONES REBOOTED:</b> <color=#66EEFF>0</color>\n" +
            "• <b>SAPLING BIO-INTEGRITY:</b> <color=#55FF88>100%</color>\n\n" +
            "<size=82%><color=#99DDCC>CIRCULAR DIRECTIVE: Upcycling industrial scrap into defensive barriers & clean energy cells prevents toxic heavy-metal soil leaching.</color></size>",
            20, new Vector2(24f, -22f), new Vector2(442f, 372f), new Color(0.88f, 1f, 0.96f));
        pauseSustainabilityStatsText.textWrappingMode = TextWrappingModes.Normal;
        pauseSustainabilityStatsText.alignment = TextAlignmentOptions.TopLeft;

        TMP_Text bottomHint = CreateTMP("Txt_PauseBottomHint", pauseConsole.transform,
            "TIP: YOU CAN ALSO RETURN TO THE START SCREEN OR REMAP ANY KEY IN [CHANGE CONTROLS]", 16,
            new Vector2(38f, -545f), new Vector2(980f, 32f), new Color(0.68f, 0.78f, 0.74f));
        bottomHint.alignment = TextAlignmentOptions.Center;

        if (playerController != null)
        {
            BindButtonAction(btnResume, playerController.ResumeGame);
            BindButtonAction(btnRestartMission, playerController.RestartMission);
            BindButtonAction(btnRestartGame, playerController.RestartGame);
            BindButtonAction(btnChangeControls, playerController.OpenChangeControlsMenu);
            BindButtonAction(btnQuit, playerController.QuitGame);
        }

        // Start hidden so it only appears when ESC is pressed!
        pauseMenuPanel.SetActive(false);
    }

    void BuildControlsMenuUI(Transform canvasTransform)
    {
        controlsMenuPanel = CreateUIObject("ControlsMenuPanel", canvasTransform);
        StretchFullScreen(controlsMenuPanel.GetComponent<RectTransform>());

        GameObject ctrlConsole = CreateBeveledMetalPanel("ControlsConsole_Frame", controlsMenuPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 670f), true);
        ctrlConsole.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);

        TMP_Text header = CreateTMP("Txt_ControlsHeader", ctrlConsole.transform,
            "⌨  KEYBIND RECONFIGURATION MATRIX", 28,
            new Vector2(40f, -24f), new Vector2(1000f, 40f), new Color(0.35f, 1.0f, 0.62f));

        // Status Banner (RebindStatusText)
        GameObject statusWell = CreateUIObject("RebindStatus_Well", ctrlConsole.transform);
        RectTransform swRT = statusWell.GetComponent<RectTransform>();
        swRT.anchorMin = new Vector2(0f, 1f);
        swRT.anchorMax = new Vector2(0f, 1f);
        swRT.pivot = new Vector2(0f, 1f);
        swRT.anchoredPosition = new Vector2(40f, -72f);
        swRT.sizeDelta = new Vector2(1000f, 52f);
        Image swImg = statusWell.AddComponent<Image>();
        swImg.sprite = _holoBoxSprite;

        TMP_Text rebindStatusTxt = CreateTMP("RebindStatusText", statusWell.transform,
            "Click any control slot below, then press a new key on your keyboard (Press ESC to cancel)", 20,
            new Vector2(20f, -8f), new Vector2(960f, 36f), new Color(0.85f, 1.0f, 0.95f));
        rebindStatusTxt.alignment = TextAlignmentOptions.Center;

        // 9 Remappable Controls in a clean 2-Column Industrial Grid
        TMP_Text txtFwd, txtBack, txtLeft, txtRight, txtJump, txtCrouch, txtInteract, txtCraftB, txtPlaceB;

        Button bFwd = CreateIndustrialButton(ctrlConsole.transform, "Btn_Forward", "Forward: W", new Vector2(40f, -142f), new Vector2(485f, 64f), false, out txtFwd);
        Button bBack = CreateIndustrialButton(ctrlConsole.transform, "Btn_Backward", "Backward: S", new Vector2(555f, -142f), new Vector2(485f, 64f), false, out txtBack);
        Button bLeft = CreateIndustrialButton(ctrlConsole.transform, "Btn_Left", "Left: A", new Vector2(40f, -220f), new Vector2(485f, 64f), false, out txtLeft);
        Button bRight = CreateIndustrialButton(ctrlConsole.transform, "Btn_Right", "Right: D", new Vector2(555f, -220f), new Vector2(485f, 64f), false, out txtRight);
        Button bJump = CreateIndustrialButton(ctrlConsole.transform, "Btn_Jump", "Jump: Space", new Vector2(40f, -298f), new Vector2(485f, 64f), false, out txtJump);
        Button bCrouch = CreateIndustrialButton(ctrlConsole.transform, "Btn_Crouch", "Crouch: C", new Vector2(555f, -298f), new Vector2(485f, 64f), false, out txtCrouch);
        Button bInteract = CreateIndustrialButton(ctrlConsole.transform, "Btn_Interact", "Interact: E", new Vector2(40f, -376f), new Vector2(485f, 64f), false, out txtInteract);
        Button bCraftB = CreateIndustrialButton(ctrlConsole.transform, "Btn_CraftBarrier", "Craft Barrier: Q", new Vector2(555f, -376f), new Vector2(485f, 64f), false, out txtCraftB);
        Button bPlaceB = CreateIndustrialButton(ctrlConsole.transform, "Btn_PlaceBarrier", "Place Barrier: F", new Vector2(40f, -454f), new Vector2(1000f, 64f), false, out txtPlaceB);

        // Bottom Row: Reset Defaults & Back to Pause Menu
        Button bReset = CreateIndustrialButton(ctrlConsole.transform, "Btn_ResetDefaults",
            "↻  RESET DEFAULT KEYS", new Vector2(40f, -555f), new Vector2(485f, 70f), false, out _);
        Button bBackMenu = CreateIndustrialButton(ctrlConsole.transform, "Btn_BackToPause",
            "◀  SAVE & RETURN TO PAUSE MENU", new Vector2(555f, -555f), new Vector2(485f, 70f), true, out _);

        if (playerController != null)
        {
            playerController.rebindStatusText = rebindStatusTxt;
            playerController.forwardKeyText = txtFwd;
            playerController.backwardKeyText = txtBack;
            playerController.leftKeyText = txtLeft;
            playerController.rightKeyText = txtRight;
            playerController.jumpKeyText = txtJump;
            playerController.crouchKeyText = txtCrouch;
            playerController.interactKeyText = txtInteract;
            playerController.craftBarrierKeyText = txtCraftB;
            playerController.placeBarrierKeyText = txtPlaceB;

            BindButtonStringAction(bFwd, playerController.StartRebindingKey, "Forward");
            BindButtonStringAction(bBack, playerController.StartRebindingKey, "Backward");
            BindButtonStringAction(bLeft, playerController.StartRebindingKey, "Left");
            BindButtonStringAction(bRight, playerController.StartRebindingKey, "Right");
            BindButtonStringAction(bJump, playerController.StartRebindingKey, "Jump");
            BindButtonStringAction(bCrouch, playerController.StartRebindingKey, "Crouch");
            BindButtonStringAction(bInteract, playerController.StartRebindingKey, "Interact");
            BindButtonStringAction(bCraftB, playerController.StartRebindingKey, "CraftBarrier");
            BindButtonStringAction(bPlaceB, playerController.StartRebindingKey, "PlaceBarrier");

            BindButtonAction(bReset, playerController.ResetDefaultControls);
            BindButtonAction(bBackMenu, playerController.CloseChangeControlsMenu);
        }

        // Start hidden!
        controlsMenuPanel.SetActive(false);
    }

    Button CreateIndustrialButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        bool isPrimaryAccent, out TMP_Text btnText)
    {
        GameObject btnObj = CreateUIObject(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.sprite = isPrimaryAccent ? _buttonAccentSprite : _buttonNormalSprite;
        img.color = Color.white;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.65f, 1.0f, 0.82f, 1f);
        cb.pressedColor = new Color(1.0f, 0.80f, 0.30f, 1f);
        cb.selectedColor = new Color(0.75f, 1.0f, 0.88f, 1f);
        btn.colors = cb;

        Outline shadow = btnObj.AddComponent<Outline>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(3f, -3f);

        btnText = CreateTMP("Label", btnObj.transform, label, 21, Vector2.zero, size,
            isPrimaryAccent ? new Color(0.38f, 1.0f, 0.62f) : new Color(0.90f, 0.95f, 0.92f));
        StretchFullScreen(btnText.GetComponent<RectTransform>());
        btnText.GetComponent<RectTransform>().offsetMin = new Vector2(22f, 0f);
        btnText.GetComponent<RectTransform>().offsetMax = new Vector2(-22f, 0f);
        btnText.alignment = TextAlignmentOptions.MidlineLeft;

        return btn;
    }

    void BindButtonAction(Button btn, UnityAction action)
    {
        if (btn == null || action == null) return;
#if UNITY_EDITOR
        UnityEventTools.AddPersistentListener(btn.onClick, action);
#else
        btn.onClick.AddListener(action);
#endif
    }

    void BindButtonStringAction(Button btn, UnityAction<string> action, string arg)
    {
        if (btn == null || action == null) return;
#if UNITY_EDITOR
        UnityEventTools.AddStringPersistentListener(btn.onClick, action, arg);
#else
        btn.onClick.AddListener(() => action(arg));
#endif
    }

    // ==================== PROCEDURAL TEXTURE & WIRE GENERATORS ====================

    void EnsureProceduralSprites()
    {
        if (_whiteSprite == null)
        {
            Texture2D wt = new Texture2D(4, 4);
            for (int i = 0; i < 16; i++) wt.SetPixel(i % 4, i / 4, Color.white);
            wt.Apply();
            _whiteSprite = Sprite.Create(wt, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }
        if (_metalPanelSprite == null) _metalPanelSprite = GenerateBeveledMetalSprite(256, 128);
        if (_darkWellSprite == null) _darkWellSprite = GenerateRecessedWellSprite(128, 32);
        if (_buttonNormalSprite == null) _buttonNormalSprite = GenerateIndustrialButtonSprite(256, 64, false);
        if (_buttonAccentSprite == null) _buttonAccentSprite = GenerateIndustrialButtonSprite(256, 64, true);
        if (_holoBoxSprite == null) _holoBoxSprite = GenerateHoloFrameSprite(256, 160);
        if (_holoConeSprite == null) _holoConeSprite = GenerateHoloConeSprite(128, 128);
        if (_circleBadgeSprite == null) _circleBadgeSprite = GenerateCircleBadgeSprite(64);
        if (_crosshairSprite == null) _crosshairSprite = GenerateCrosshairSprite(256, 192);
        if (_batterySprite == null) _batterySprite = GenerateSegmentedBatterySprite(32, 48);
        if (_frostedBlurSprite == null) _frostedBlurSprite = GenerateFrostedBlurSprite(128, 128);
    }

    Sprite GenerateIndustrialButtonSprite(int w, int h, bool neonGreenTrim)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int corner = Mathf.Min(x, w - 1 - x) + Mathf.Min(y, h - 1 - y);
                if (corner < 6) { tex.SetPixel(x, y, Color.clear); continue; }

                bool isLeftBar = (x >= 4 && x <= 11 && y >= 5 && y < h - 5);
                bool isBorder = (x < 4 || x >= w - 4 || y < 4 || y >= h - 4);
                float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.04f;

                Color c;
                if (isLeftBar)
                {
                    c = neonGreenTrim ? new Color(0.25f, 1.0f, 0.55f, 1f) : new Color(0.95f, 0.68f, 0.18f, 1f);
                }
                else if (isBorder)
                {
                    c = neonGreenTrim ? new Color(0.22f, 0.78f, 0.45f, 0.98f) : new Color(0.36f, 0.38f, 0.35f, 0.98f);
                    if (y >= h - 2) c += new Color(0.15f, 0.15f, 0.15f, 0f);
                    if (y < 2) c -= new Color(0.15f, 0.15f, 0.15f, 0f);
                }
                else
                {
                    float scan = (y % 3 == 0) ? -0.015f : 0f;
                    c = neonGreenTrim
                        ? new Color(0.06f + noise, 0.20f + noise + scan, 0.14f + scan, 0.96f)
                        : new Color(0.12f + noise, 0.15f + noise + scan, 0.14f + scan, 0.96f);
                }
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    Sprite GenerateFrostedBlurSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
        float maxDist = Vector2.Distance(Vector2.zero, center);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float vignette = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float grain = Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.08f;
                float scan = (y % 2 == 0) ? 0.04f : 0f;
                float alpha = Mathf.Clamp01(0.76f + vignette * 0.20f + grain + scan);
                tex.SetPixel(x, y, new Color(0.03f + grain * 0.3f, 0.08f + grain * 0.4f, 0.07f + grain * 0.3f, alpha));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    void BuildBezierWire(Transform parent, string wireName, Vector2 p0, Vector2 p1, Vector2 p2,
        float thickness, Color wireColor, bool addGlow)
    {
        GameObject wireHolder = CreateUIObject(wireName, parent);
        StretchFullScreen(wireHolder.GetComponent<RectTransform>());

        int segments = 24;
        Vector2 prevPt = p0;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float u = 1f - t;
            Vector2 pt = (u * u * p0) + (2f * u * t * p1) + (t * t * p2);

            Vector2 diff = pt - prevPt;
            float len = diff.magnitude + 1.5f;
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            Vector2 mid = (prevPt + pt) * 0.5f;

            if (addGlow)
            {
                GameObject glowSeg = CreateUIObject("GlowSeg", wireHolder.transform);
                RectTransform gRT = glowSeg.GetComponent<RectTransform>();
                gRT.anchorMin = new Vector2(0f, 1f);
                gRT.anchorMax = new Vector2(0f, 1f);
                gRT.pivot = new Vector2(0.5f, 0.5f);
                gRT.anchoredPosition = mid;
                gRT.sizeDelta = new Vector2(len, thickness * 2.6f);
                gRT.localRotation = Quaternion.Euler(0f, 0f, angle);
                Image gImg = glowSeg.AddComponent<Image>();
                gImg.sprite = _whiteSprite;
                gImg.color = new Color(wireColor.r, wireColor.g, wireColor.b, 0.28f);
                gImg.raycastTarget = false;
            }

            GameObject seg = CreateUIObject("WireSeg", wireHolder.transform);
            RectTransform rt = seg.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = mid;
            rt.sizeDelta = new Vector2(len, thickness);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);

            Image img = seg.AddComponent<Image>();
            img.sprite = _whiteSprite;
            img.color = wireColor;
            img.raycastTarget = false;

            prevPt = pt;
        }
    }

    void CreateConduitPipe(Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color col)
    {
        GameObject pipe = CreateUIObject("ConduitPipe", parent);
        RectTransform rt = pipe.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = pipe.AddComponent<Image>();
        img.sprite = _whiteSprite;
        img.color = col;
        img.raycastTarget = false;
    }

    GameObject CreateBeveledMetalPanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size, bool addCornerBolts)
    {
        GameObject panel = CreateUIObject(name, parent);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = panel.AddComponent<Image>();
        img.sprite = _metalPanelSprite;
        img.color = Color.white;

        Outline dropShadow = panel.AddComponent<Outline>();
        dropShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        dropShadow.effectDistance = new Vector2(4f, -4f);

        if (addCornerBolts)
        {
            Vector2[] boltOffsets = new Vector2[]
            {
                new Vector2(12f, -12f), new Vector2(size.x - 12f, -12f),
                new Vector2(12f, -(size.y - 12f)), new Vector2(size.x - 12f, -(size.y - 12f))
            };
            foreach (Vector2 bPos in boltOffsets)
            {
                GameObject bolt = CreateUIObject("RivetBolt", panel.transform);
                RectTransform bRT = bolt.GetComponent<RectTransform>();
                bRT.anchorMin = new Vector2(0f, 1f);
                bRT.anchorMax = new Vector2(0f, 1f);
                bRT.pivot = new Vector2(0.5f, 0.5f);
                bRT.anchoredPosition = bPos;
                bRT.sizeDelta = new Vector2(10f, 10f);
                Image bImg = bolt.AddComponent<Image>();
                bImg.sprite = _circleBadgeSprite;
                bImg.color = new Color(0.55f, 0.52f, 0.48f, 0.95f);
            }
        }

        return panel;
    }

    void CreateSideBumper(Transform parent, Vector2 pos, Vector2 size, bool isLeft)
    {
        GameObject bumper = CreateUIObject(isLeft ? "LeftBumper" : "RightBumper", parent);
        RectTransform rt = bumper.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(isLeft ? 0f : 1f, 1f);
        rt.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = bumper.AddComponent<Image>();
        img.sprite = _metalPanelSprite;
        img.color = new Color(0.75f, 0.72f, 0.68f, 1f);
    }

    RectTransform CreateIndustrialProgressBar(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
        Color fillColor, float initialRatio)
    {
        GameObject bg = CreateUIObject(name + "_Well", parent);
        RectTransform bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 1f);
        bgRT.anchorMax = new Vector2(0f, 1f);
        bgRT.pivot = new Vector2(0f, 1f);
        bgRT.anchoredPosition = anchoredPos;
        bgRT.sizeDelta = size;

        Image bgImg = bg.AddComponent<Image>();
        bgImg.sprite = _darkWellSprite;
        bgImg.color = Color.white;

        GameObject fill = CreateUIObject(name + "_Fill", bg.transform);
        RectTransform fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(initialRatio, 1f);
        fillRT.offsetMin = new Vector2(4f, 4f);
        fillRT.offsetMax = new Vector2(-4f, -4f);

        Image fillImg = fill.AddComponent<Image>();
        fillImg.sprite = _whiteSprite;
        fillImg.color = fillColor;

        Outline glow = fill.AddComponent<Outline>();
        glow.effectColor = new Color(fillColor.r, fillColor.g, fillColor.b, 0.45f);
        glow.effectDistance = new Vector2(2f, -2f);

        return fillRT;
    }

    GameObject CreateInventoryRow(Transform parent, string name, Vector2 pos, Vector2 size,
        string leftSymbol, string keyBadgeChar, bool highlightGreen, out TMP_Text rowText, out Image frameImg)
    {
        GameObject row = CreateUIObject(name, parent);
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        frameImg = row.AddComponent<Image>();
        frameImg.sprite = _darkWellSprite;
        frameImg.color = highlightGreen ? new Color(0.28f, 0.98f, 0.52f, 1f) : new Color(0.75f, 0.78f, 0.76f, 1f);

        TMP_Text iconTxt = CreateTMP("Icon", row.transform, leftSymbol, 26,
            new Vector2(12f, -10f), new Vector2(42f, 40f),
            highlightGreen ? new Color(0.35f, 1f, 0.55f) : new Color(0.78f, 0.82f, 0.80f));
        iconTxt.alignment = TextAlignmentOptions.Center;

        rowText = CreateTMP("Label", row.transform, "ITEM", 21,
            new Vector2(62f, -12f), new Vector2(285f, 36f),
            highlightGreen ? new Color(0.35f, 1f, 0.55f) : new Color(0.90f, 0.94f, 0.92f));

        GameObject badge = CreateUIObject("KeyBadge", row.transform);
        RectTransform bRT = badge.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(1f, 0.5f);
        bRT.anchorMax = new Vector2(1f, 0.5f);
        bRT.pivot = new Vector2(1f, 0.5f);
        bRT.anchoredPosition = new Vector2(-14f, 0f);
        bRT.sizeDelta = new Vector2(34f, 34f);
        Image bImg = badge.AddComponent<Image>();
        bImg.sprite = _circleBadgeSprite;
        bImg.color = new Color(0.35f, 0.38f, 0.36f, 0.95f);

        TMP_Text keyTxt = CreateTMP("KeyChar", badge.transform, keyBadgeChar, 16,
            Vector2.zero, new Vector2(34f, 34f), new Color(0.85f, 0.9f, 0.88f));
        StretchFullScreen(keyTxt.GetComponent<RectTransform>());
        keyTxt.alignment = TextAlignmentOptions.Center;

        return row;
    }

    // ==================== PROCEDURAL PIXEL SHADERS ====================

    Sprite GenerateBeveledMetalSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        int border = 10;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int cornerDist = Mathf.Min(x, w - 1 - x) + Mathf.Min(y, h - 1 - y);
                if (cornerDist < 8) { tex.SetPixel(x, y, Color.clear); continue; }

                float rustNoise = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.06f;
                bool isOuterFrame = (x < border || x >= w - border || y < border || y >= h - border);

                Color col;
                if (isOuterFrame)
                {
                    col = new Color(0.28f + rustNoise, 0.27f + rustNoise * 0.8f, 0.24f, 0.98f);
                    if (y >= h - 3 || x < 3) col += new Color(0.16f, 0.16f, 0.15f, 0f);
                    else if (y < 3 || x >= w - 3) col -= new Color(0.14f, 0.14f, 0.14f, 0f);
                    else if (x == border - 1 || x == w - border || y == border - 1 || y == h - border)
                        col = new Color(0.05f, 0.06f, 0.06f, 1f);
                }
                else
                {
                    float scanline = (y % 4 == 0) ? -0.018f : 0f;
                    col = new Color(0.09f + rustNoise * 0.4f + scanline, 0.12f + rustNoise * 0.4f + scanline, 0.11f + scanline, 0.95f);
                }
                tex.SetPixel(x, y, col);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    Sprite GenerateRecessedWellSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool isBorder = (x < 3 || x >= w - 3 || y < 3 || y >= h - 3);
                Color c = isBorder ? new Color(0.32f, 0.35f, 0.33f, 0.95f) : new Color(0.05f, 0.07f, 0.07f, 0.96f);
                if (y >= h - 2) c = new Color(0.02f, 0.03f, 0.03f, 1f);
                if (y < 2) c = new Color(0.42f, 0.46f, 0.44f, 1f);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    Sprite GenerateHoloFrameSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        int cornerLen = 28;
        int thick = 4;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float scan = (y % 3 == 0) ? 0.06f : 0f;
                float edgeGlow = (Mathf.Min(x, w - 1 - x) < 2 || Mathf.Min(y, h - 1 - y) < 2) ? 0.25f : 0f;
                Color bg = new Color(0.04f, 0.24f + scan + edgeGlow, 0.22f + scan + edgeGlow, 0.86f);

                bool inCornerX = (x < cornerLen || x >= w - cornerLen);
                bool inCornerY = (y < cornerLen || y >= h - cornerLen);
                bool onOuterEdge = (x < thick || x >= w - thick || y < thick || y >= h - thick);

                if (inCornerX && inCornerY && onOuterEdge)
                    bg = new Color(0.45f, 1.0f, 0.88f, 1.0f);

                tex.SetPixel(x, y, bg);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    Sprite GenerateHoloConeSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)h;
            float halfWidth = Mathf.Lerp(0.08f, 0.48f, t) * w;
            float center = w * 0.5f;

            for (int x = 0; x < w; x++)
            {
                float dist = Mathf.Abs(x - center);
                if (dist <= halfWidth)
                {
                    float horizontalFade = 1f - (dist / halfWidth);
                    float verticalFade = Mathf.Sin(t * Mathf.PI);
                    float rayBeam = 0.7f + 0.3f * Mathf.Sin(x * 0.35f);
                    float alpha = horizontalFade * verticalFade * rayBeam * 0.65f;
                    tex.SetPixel(x, y, new Color(0.25f, 1f, 0.85f, alpha));
                }
                else tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    Sprite GenerateSegmentedBatterySprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (y >= h - 6)
                {
                    bool inCap = (x >= w / 4 && x < (w * 3) / 4);
                    tex.SetPixel(x, y, inCap ? Color.white : Color.clear);
                    continue;
                }
                bool border = (x < 3 || x >= w - 3 || y < 3 || y >= h - 9);
                if (border) tex.SetPixel(x, y, Color.white);
                else
                {
                    bool isGap = (y % 9 < 2);
                    tex.SetPixel(x, y, isGap ? new Color(0f, 0f, 0f, 0.65f) : Color.white);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    Sprite GenerateCircleBadgeSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        Vector2 c = new Vector2(r, r);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                if (d > r) tex.SetPixel(x, y, Color.clear);
                else if (d > r - 3f) tex.SetPixel(x, y, new Color(0.75f, 0.8f, 0.78f, 1f));
                else tex.SetPixel(x, y, new Color(0.22f, 0.25f, 0.24f, 1f));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    Sprite GenerateCrosshairSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        int cx = w / 2, cy = h / 2;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool hLine = (Mathf.Abs(y - cy) == 0 && Mathf.Abs(x - cx) > 12);
                bool vLine = (Mathf.Abs(x - cx) == 0 && Mathf.Abs(y - cy) > 12);
                bool ticks = (Mathf.Abs(y - cy) < 5 && (x - cx) % 24 == 0) || (Mathf.Abs(x - cx) < 5 && (y - cy) % 24 == 0);
                tex.SetPixel(x, y, (hLine || vLine || ticks) ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    // ==================== 3D HOLOGRAM BUST ON CAMERA ====================

    void Build3DHologramBustOnCamera()
    {
        Camera cam = Camera.main;
        if (cam == null && playerController != null && playerController.playerCamera != null)
            cam = playerController.playerCamera.GetComponent<Camera>();
        if (cam == null) return;

        Transform oldHolo = cam.transform.Find("HoloAssistant3D_Head");
        if (oldHolo != null) DestroyImmediate(oldHolo.gameObject);

        GameObject holoRoot = new GameObject("HoloAssistant3D_Head");
        holoRoot.transform.SetParent(cam.transform, false);
        holoRoot.transform.localPosition = new Vector3(0.53f, 0.12f, 1.15f);
        holoRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        holoRoot.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material holoMat = new Material(shader);
        Color cyanGlow = new Color(0.15f, 1.0f, 0.82f, 1f);
        holoMat.color = cyanGlow;
        if (holoMat.HasProperty("_BaseColor")) holoMat.SetColor("_BaseColor", cyanGlow);

        if (customHoloModelPrefab != null)
        {
            GameObject customInstance = Instantiate(customHoloModelPrefab, holoRoot.transform);
            customInstance.name = "Custom_Holo_Mesh";
            customInstance.transform.localPosition = Vector3.zero;
            customInstance.transform.localRotation = Quaternion.identity;
            customInstance.transform.localScale = Vector3.one;
            foreach (Collider col in customInstance.GetComponentsInChildren<Collider>()) DestroyImmediate(col);
            foreach (MeshRenderer rend in customInstance.GetComponentsInChildren<MeshRenderer>())
            {
                Material[] mats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = holoMat;
                rend.sharedMaterials = mats;
            }
        }
        else
        {
            GameObject cranium = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cranium.name = "Holo_Cranium";
            cranium.transform.SetParent(holoRoot.transform, false);
            cranium.transform.localScale = new Vector3(0.95f, 1.15f, 0.95f);
            DestroyImmediate(cranium.GetComponent<Collider>());
            cranium.GetComponent<MeshRenderer>().sharedMaterial = holoMat;

            GameObject brow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            brow.name = "Holo_BrowPlate";
            brow.transform.SetParent(holoRoot.transform, false);
            brow.transform.localPosition = new Vector3(0f, 0.22f, 0.36f);
            brow.transform.localScale = new Vector3(0.65f, 0.16f, 0.3f);
            DestroyImmediate(brow.GetComponent<Collider>());
            brow.GetComponent<MeshRenderer>().sharedMaterial = holoMat;

            GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neck.name = "Holo_Neck";
            neck.transform.SetParent(holoRoot.transform, false);
            neck.transform.localPosition = new Vector3(0f, -0.62f, 0f);
            neck.transform.localScale = new Vector3(0.42f, 0.32f, 0.42f);
            DestroyImmediate(neck.GetComponent<Collider>());
            neck.GetComponent<MeshRenderer>().sharedMaterial = holoMat;

            GameObject shoulders = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shoulders.name = "Holo_ShouldersBust";
            shoulders.transform.SetParent(holoRoot.transform, false);
            shoulders.transform.localPosition = new Vector3(0f, -0.95f, 0f);
            shoulders.transform.localScale = new Vector3(1.65f, 0.28f, 0.65f);
            DestroyImmediate(shoulders.GetComponent<Collider>());
            shoulders.GetComponent<MeshRenderer>().sharedMaterial = holoMat;
        }

        holoHead3D = holoRoot.transform;
    }

    void AutoWireExistingGameScripts()
    {
        if (playerController != null)
        {
            playerController.objectiveMessageText = messageText;
            playerController.pauseMenuPanel = pauseMenuPanel;
            playerController.controlsMenuPanel = controlsMenuPanel;
#if UNITY_EDITOR
            EditorUtility.SetDirty(playerController);
#endif
        }

        if (defenseManager != null)
        {
            defenseManager.timerText = timerText;
            defenseManager.messageText = messageText;
#if UNITY_EDITOR
            EditorUtility.SetDirty(defenseManager);
#endif
        }
    }

    GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    void StretchFullScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    TMP_Text CreateTMP(string name, Transform parent, string content, float fontSize,
        Vector2 anchoredPos, Vector2 size, Color textColor)
    {
        GameObject txtObj = CreateUIObject(name, parent);
        RectTransform rt = txtObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(OneClickIndustrialHUD))]
public class OneClickIndustrialHUDEditor : Editor
{
    public override void OnInspectorGUI()
    {
        OneClickIndustrialHUD builder = (OneClickIndustrialHUD)target;

        GUILayout.Space(8);
        GUI.backgroundColor = new Color(0.2f, 0.95f, 0.55f);
        if (GUILayout.Button("⚡ ONE-CLICK BUILD ALL MENUS + HUD + BLUR", GUILayout.Height(44)))
        {
            builder.BuildIndustrialHUDNow();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.Space(8);

        DrawDefaultInspector();
    }
}
#endif