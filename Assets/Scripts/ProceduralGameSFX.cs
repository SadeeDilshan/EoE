using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ProceduralGameSFX : MonoBehaviour
{
    public static ProceduralGameSFX Instance;

    [Header("Tag Settings")]
    public string soilTag = "SoilPatch";
    public string scrapTag = "Scrap";

    [Header("Auto-Generated Industrial SFX Clips (Keep Set to None to Auto-Generate)")]
    public AudioClip arieFootstepClip;
    public AudioClip arieJumpClip;
    public AudioClip arieLandClip;
    public AudioClip wallPreviewClip;
    public AudioClip wallDeployClip;
    public AudioClip scrapCollectClip;
    public AudioClip soilDetectClip;
    public AudioClip soilCheckAnalyzeClip;
    public AudioClip droneRebootEMPClip;
    public AudioClip droneHoverLoopClip;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float footstepVolume = 0.78f;
    [Range(0f, 1f)] public float jumpVolume = 0.85f;
    [Range(0f, 1f)] public float landVolume = 0.90f;
    [Range(0f, 1f)] public float wallDeployVolume = 1.00f;
    [Range(0f, 1f)] public float scrapCollectVolume = 0.95f;
    [Range(0f, 1f)] public float soilDetectVolume = 0.80f;
    [Range(0f, 1f)] public float soilCheckVolume = 0.95f;
    [Range(0f, 1f)] public float empRebootVolume = 0.95f;
    [Range(0f, 1f)] public float droneHoverVolume = 0.55f;

    [Header("Soil Scanner Rules")]
    [Tooltip("Auto-populated using the 'SoilPatch' tag.")]
    public List<Transform> allSoilSpots = new List<Transform>();
    public float soilScannerMaxRange = 12.0f;
    public float soilCheckInteractRange = 4.2f;

    [Tooltip("Automatically turns true when player collects the Soil Detector, or check manually for testing.")]
    public bool playerHasSoilDetector = false;

    private ARIEPlayerController player;
    private CharacterController charController;
    private Level3TreeDefense defenseManager;
    private AudioSource playerAudioSource;

    private float stepTimer = 0f;
    private bool isLeftFoot = false;
    private bool wasGrounded = true;
    private float airborneTimer = 0f;
    private int prevBarrierCount = -1;
    private int prevDronesRebooted = -1;
    private float soilPingTimer = 0f;
    private float rescanTimer = 0f;
    private float lastScrapSoundTime = -10f;
    private float lastSoilCheckSoundTime = -10f;
    private Vector3 lastPlayerPos;

    // Tracks SoilPatch objects that have ALREADY been tested (good or bad) so they never blink again
    private HashSet<Transform> checkedSoilSpots = new HashSet<Transform>();

    // Tracks Soil Detector pickup object or boolean fields in player/defense scripts
    private List<GameObject> sceneDetectorPickups = new List<GameObject>();
    private bool hadDetectorPickupInScene = false;
    private List<FieldInfo> detectorBoolFields = new List<FieldInfo>();
    private List<MonoBehaviour> detectorFieldOwners = new List<MonoBehaviour>();

    // Tracks all objects with the "Scrap" tag
    private HashSet<GameObject> trackedScrapObjects = new HashSet<GameObject>();
    private List<FieldInfo> playerIntFields = new List<FieldInfo>();
    private Dictionary<FieldInfo, int> prevPlayerIntValues = new Dictionary<FieldInfo, int>();

    // Drone 3D Audio Tracking
    private List<AudioSource> droneAudioSources = new List<AudioSource>();
    private Dictionary<Transform, Vector3> lastDronePositions = new Dictionary<Transform, Vector3>();

    void Awake()
    {
        Instance = this;

        // Force-clear cached clips so all upgraded heavy waveforms generate fresh
        droneRebootEMPClip = null;
        wallPreviewClip = null;
        wallDeployClip = null;
        scrapCollectClip = null;
        soilDetectClip = null;
        soilCheckAnalyzeClip = null;

        GenerateAllProceduralClips();
    }

    void Start()
    {
        player = FindFirstObjectByType<ARIEPlayerController>();
        defenseManager = FindFirstObjectByType<Level3TreeDefense>();

        if (player != null)
        {
            charController = player.GetComponent<CharacterController>();
            playerAudioSource = player.gameObject.GetComponent<AudioSource>();
            if (playerAudioSource == null)
                playerAudioSource = player.gameObject.AddComponent<AudioSource>();

            playerAudioSource.spatialBlend = 0f;
            prevBarrierCount = player.barrierInventoryCount;
            lastPlayerPos = player.transform.position;

            SetupPlayerIntFieldWatchers();
        }

        prevDronesRebooted = ARIEPlayerController.totalDronesRebooted;

        SetupSoilDetectorWatchers();
        ScanTaggedScrapObjects();
        LocateAllTaggedSoilPatches();
        AttachHoverSoundToAllDrones();
    }

    void Update()
    {
        if (player == null || Time.timeScale == 0f) return;

        // Periodically rescan for newly spawned Scrap or SoilPatch objects every 1.0s
        rescanTimer -= Time.deltaTime;
        if (rescanTimer <= 0f)
        {
            rescanTimer = 1.0f;
            ScanTaggedScrapObjects();
            if (allSoilSpots.Count == 0)
                LocateAllTaggedSoilPatches();
        }

        Vector3 flatDelta = player.transform.position - lastPlayerPos;
        flatDelta.y = 0f;
        float horizontalSpeed = flatDelta.magnitude / Mathf.Max(0.0001f, Time.deltaTime);
        lastPlayerPos = player.transform.position;

        bool isGrounded = (charController != null && charController.isGrounded);

        // 0. Heavy Mechanical Landing After Jump / Fall
        if (!isGrounded)
        {
            airborneTimer += Time.deltaTime;
        }
        else if (!wasGrounded && airborneTimer > 0.12f)
        {
            playerAudioSource.pitch = Random.Range(0.88f, 0.96f);
            playerAudioSource.PlayOneShot(arieLandClip, landVolume);
            stepTimer = 0.28f;
            airborneTimer = 0f;
        }
        else
        {
            airborneTimer = 0f;
        }
        wasGrounded = isGrounded;

        // 1. AR-IE Walking / Sprinting Footsteps
        if (isGrounded && horizontalSpeed > 0.8f)
        {
            float stepInterval = (horizontalSpeed > 6.5f) ? 0.32f : (player.IsCrouching ? 0.64f : 0.48f);
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                stepTimer = stepInterval;
                isLeftFoot = !isLeftFoot;

                float basePitch = isLeftFoot ? 0.88f : 0.94f;
                if (horizontalSpeed > 6.5f) basePitch += 0.06f;

                playerAudioSource.pitch = basePitch + Random.Range(-0.02f, 0.02f);
                playerAudioSource.PlayOneShot(arieFootstepClip, footstepVolume);
            }
        }
        else
        {
            stepTimer = 0.06f;
        }

        // 2. AR-IE Hydraulic Jump Sound
        if (Input.GetKeyDown(ARIEPlayerController.keyJump) && isGrounded)
        {
            playerAudioSource.pitch = Random.Range(0.95f, 1.03f);
            playerAudioSource.PlayOneShot(arieJumpClip, jumpVolume);
        }

        // 3. Check if Player has collected the Soil Detector
        UpdateSoilDetectorOwnership();

        // 4. Press [E] to Check / Analyze a 'SoilPatch' (Marks that SoilPatch as checked so it stops blinking!)
        CheckForSoilAnalyzeInput();

        // 5. Detect 'Scrap' Tag Collection
        CheckForScrapCollection();

        // 6. Wall Deploying & Wall Preview
        if (prevBarrierCount != player.barrierInventoryCount)
        {
            if (player.barrierInventoryCount > prevBarrierCount)
            {
                PlayScrapCollectSound();
            }
            else if (player.barrierInventoryCount < prevBarrierCount)
            {
                playerAudioSource.pitch = Random.Range(0.85f, 0.92f);
                playerAudioSource.PlayOneShot(wallDeployClip, wallDeployVolume);
            }
            prevBarrierCount = player.barrierInventoryCount;
        }
        else if (Input.GetKeyDown(ARIEPlayerController.keyPlaceBarrier) && player.barrierInventoryCount > 0)
        {
            playerAudioSource.pitch = Random.Range(0.90f, 0.96f);
            playerAudioSource.PlayOneShot(wallPreviewClip, wallDeployVolume * 0.7f);
        }

        // 7. Soil Detecting Radar Ping (ONLY near unchecked 'SoilPatch' objects when player has detector)
        UpdateAllSoilDetectionScanner();

        // 8. Drone EMP Short-Circuit & Rebooting Sound
        if (ARIEPlayerController.totalDronesRebooted > prevDronesRebooted)
        {
            prevDronesRebooted = ARIEPlayerController.totalDronesRebooted;
            playerAudioSource.pitch = Random.Range(0.96f, 1.04f);
            playerAudioSource.PlayOneShot(droneRebootEMPClip, empRebootVolume);
        }

        // 9. Dynamic Drone Rotor Pitch Shift
        UpdateDroneHoverDynamics();
    }

    // ==================== EXACT 'SoilPatch' & 'Scrap' TAG LOGIC ====================

    void LocateAllTaggedSoilPatches()
    {
        allSoilSpots.Clear();
        try
        {
            GameObject[] patches = GameObject.FindGameObjectsWithTag(soilTag);
            for (int i = 0; i < patches.Length; i++)
            {
                if (patches[i] != null && !allSoilSpots.Contains(patches[i].transform))
                {
                    allSoilSpots.Add(patches[i].transform);
                }
            }
        }
        catch (UnityException)
        {
            Debug.LogWarning("ProceduralGameSFX: Tag '" + soilTag + "' is not defined in Tag Manager yet.");
        }
    }

    void ScanTaggedScrapObjects()
    {
        try
        {
            GameObject[] scraps = GameObject.FindGameObjectsWithTag(scrapTag);
            for (int i = 0; i < scraps.Length; i++)
            {
                if (scraps[i] != null && scraps[i].activeInHierarchy)
                {
                    trackedScrapObjects.Add(scraps[i]);
                }
            }
        }
        catch (UnityException)
        {
            Debug.LogWarning("ProceduralGameSFX: Tag '" + scrapTag + "' is not defined in Tag Manager yet.");
        }
    }

    void CheckForScrapCollection()
    {
        bool scrapPickedUp = false;

        // 1. Check if any 'Scrap' tagged GameObject was destroyed or deactivated (picked up)
        List<GameObject> collectedList = null;
        foreach (GameObject scrapObj in trackedScrapObjects)
        {
            if (scrapObj == null || !scrapObj.activeInHierarchy)
            {
                if (collectedList == null) collectedList = new List<GameObject>();
                collectedList.Add(scrapObj);
                scrapPickedUp = true;
            }
        }

        if (collectedList != null)
        {
            for (int i = 0; i < collectedList.Count; i++)
                trackedScrapObjects.Remove(collectedList[i]);
        }

        // 2. Also check if any scrap counter variable on ARIEPlayerController increased
        for (int i = 0; i < playerIntFields.Count; i++)
        {
            FieldInfo f = playerIntFields[i];
            int currentVal = (int)f.GetValue(f.IsStatic ? null : player);
            int prevVal = prevPlayerIntValues[f];

            if (currentVal > prevVal)
            {
                scrapPickedUp = true;
            }
            prevPlayerIntValues[f] = currentVal;
        }

        if (scrapPickedUp)
        {
            PlayScrapCollectSound();
        }
    }

    Transform GetClosestUncheckedSoilSpot(out float closestDist)
    {
        closestDist = float.MaxValue;
        Transform closestSpot = null;

        for (int i = allSoilSpots.Count - 1; i >= 0; i--)
        {
            Transform spot = allSoilSpots[i];
            if (spot == null || !spot.gameObject.activeInHierarchy) continue;

            // Skip if this SoilPatch was already tested (good or bad)
            if (checkedSoilSpots.Contains(spot)) continue;

            // Also check if a script on the SoilPatch has a boolean like isChecked / isScanned / isTested set to true
            if (IsSoilSpotAlreadyCheckedOnScript(spot.gameObject))
            {
                checkedSoilSpots.Add(spot);
                continue;
            }

            float d = Vector3.Distance(player.transform.position, spot.position);
            if (d < closestDist)
            {
                closestDist = d;
                closestSpot = spot;
            }
        }

        return closestSpot;
    }

    bool IsSoilSpotAlreadyCheckedOnScript(GameObject soilObj)
    {
        MonoBehaviour[] scripts = soilObj.GetComponents<MonoBehaviour>();
        for (int i = 0; i < scripts.Length; i++)
        {
            MonoBehaviour mb = scripts[i];
            if (mb == null || mb == this || mb == defenseManager) continue;

            FieldInfo[] fields = mb.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            for (int j = 0; j < fields.Length; j++)
            {
                FieldInfo f = fields[j];
                if (f.FieldType == typeof(bool))
                {
                    string fn = f.Name.ToLower();
                    if (fn.Contains("checked") || fn.Contains("scanned") || fn.Contains("tested") || fn.Contains("analyzed") || fn.Contains("discovered"))
                    {
                        if ((bool)f.GetValue(mb))
                            return true;
                    }
                }
            }
        }
        return false;
    }

    void CheckForSoilAnalyzeInput()
    {
        // Must have Soil Detector first, and defense must not have started yet
        if (!playerHasSoilDetector) return;
        if (defenseManager != null && defenseManager.defenseStarted) return;

        bool pressedInteract = Input.GetKeyDown(KeyCode.E);
        try
        {
            if (Input.GetKeyDown(ARIEPlayerController.keyInteract))
                pressedInteract = true;
        }
        catch { }

        if (!pressedInteract) return;

        float closestDist;
        Transform targetSoil = GetClosestUncheckedSoilSpot(out closestDist);

        if (targetSoil != null && closestDist <= soilCheckInteractRange)
        {
            // 1. Play the heavy soil probe & chemical analysis sound
            PlaySoilCheckAnalyzeSound();

            // 2. Permanently mark this SoilPatch as CHECKED so it never blinks/pings again!
            checkedSoilSpots.Add(targetSoil);
            soilPingTimer = 1.0f;
        }
    }

    void UpdateAllSoilDetectionScanner()
    {
        // Rule 1: Do NOT blink if player hasn't picked up the Soil Detector yet
        if (!playerHasSoilDetector) return;

        // Rule 2: Do NOT blink after the sapling is planted
        if (defenseManager != null && defenseManager.defenseStarted) return;

        float closestDist;
        Transform closestUncheckedSoil = GetClosestUncheckedSoilSpot(out closestDist);

        // Rule 3: Only blink near UNCHECKED 'SoilPatch' objects
        if (closestUncheckedSoil != null && closestDist <= soilScannerMaxRange)
        {
            float proximity = 1f - Mathf.Clamp01(closestDist / soilScannerMaxRange);
            float pingInterval = Mathf.Lerp(1.20f, 0.24f, proximity * proximity);

            soilPingTimer -= Time.deltaTime;
            if (soilPingTimer <= 0f)
            {
                soilPingTimer = pingInterval;
                PlaySoilDetectSound(proximity);
            }
        }
        else
        {
            soilPingTimer = 0.12f;
        }
    }

    // Call this from your SoilPatch script if you ever want to manually mark a Transform as checked
    public void MarkSoilPatchChecked(Transform soilTransform)
    {
        if (soilTransform != null)
            checkedSoilSpots.Add(soilTransform);
    }

    void SetupSoilDetectorWatchers()
    {
        detectorBoolFields.Clear();
        detectorFieldOwners.Clear();
        sceneDetectorPickups.Clear();

        MonoBehaviour[] allScripts = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in allScripts)
        {
            if (mb == null || mb == this) continue;
            FieldInfo[] fields = mb.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (FieldInfo f in fields)
            {
                if (f.FieldType == typeof(bool))
                {
                    string fn = f.Name.ToLower();
                    if ((fn.Contains("detector") || fn.Contains("scanner") || fn.Contains("sensor")) &&
                        (fn.Contains("has") || fn.Contains("collect") || fn.Contains("unlock") || fn.Contains("equip") || fn.Contains("got") || fn.Contains("soil")))
                    {
                        detectorBoolFields.Add(f);
                        detectorFieldOwners.Add(mb);
                    }
                }
            }
        }

        GameObject[] allObjs = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in allObjs)
        {
            if (go == null || !go.activeInHierarchy) continue;
            if (go.GetComponent<RectTransform>() != null) continue;

            string n = go.name.ToLower();
            if ((n.Contains("soildetector") || n.Contains("soil_detector") || n.Contains("soilscanner") || n.Contains("detector"))
                && (player == null || !go.transform.IsChildOf(player.transform)))
            {
                sceneDetectorPickups.Add(go);
                hadDetectorPickupInScene = true;
            }
        }
    }

    void UpdateSoilDetectorOwnership()
    {
        if (playerHasSoilDetector) return;

        for (int i = 0; i < detectorBoolFields.Count; i++)
        {
            FieldInfo f = detectorBoolFields[i];
            MonoBehaviour owner = detectorFieldOwners[i];
            if (f != null && (f.IsStatic || owner != null))
            {
                bool val = (bool)f.GetValue(f.IsStatic ? null : owner);
                if (val)
                {
                    playerHasSoilDetector = true;
                    return;
                }
            }
        }

        if (hadDetectorPickupInScene)
        {
            for (int i = sceneDetectorPickups.Count - 1; i >= 0; i--)
            {
                if (sceneDetectorPickups[i] == null || !sceneDetectorPickups[i].activeInHierarchy)
                {
                    playerHasSoilDetector = true;
                    PlayScrapCollectSound();
                    return;
                }
            }
        }
    }

    void SetupPlayerIntFieldWatchers()
    {
        playerIntFields.Clear();
        prevPlayerIntValues.Clear();

        if (player == null) return;

        FieldInfo[] fields = player.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (FieldInfo f in fields)
        {
            if (f.FieldType == typeof(int))
            {
                string n = f.Name.ToLower();
                if (n.Contains("scrap") || n.Contains("metal") || n.Contains("part") || n.Contains("piece") || n.Contains("collect") || n.Contains("resource"))
                {
                    int val = (int)f.GetValue(f.IsStatic ? null : player);
                    playerIntFields.Add(f);
                    prevPlayerIntValues[f] = val;
                }
            }
        }
    }

    public void PlayScrapCollectSound()
    {
        if (Time.unscaledTime - lastScrapSoundTime < 0.12f) return;
        if (playerAudioSource == null || scrapCollectClip == null) return;

        lastScrapSoundTime = Time.unscaledTime;
        playerAudioSource.pitch = Random.Range(0.94f, 1.05f);
        playerAudioSource.PlayOneShot(scrapCollectClip, scrapCollectVolume);
    }

    public void PlaySoilDetectSound(float proximity01 = 1f)
    {
        if (playerAudioSource == null || soilDetectClip == null) return;
        playerAudioSource.pitch = Mathf.Lerp(0.88f, 1.22f, proximity01);
        playerAudioSource.PlayOneShot(soilDetectClip, soilDetectVolume * Mathf.Lerp(0.45f, 1.0f, proximity01));
    }

    public void PlaySoilCheckAnalyzeSound()
    {
        if (Time.unscaledTime - lastSoilCheckSoundTime < 0.25f) return;
        if (playerAudioSource == null || soilCheckAnalyzeClip == null) return;

        lastSoilCheckSoundTime = Time.unscaledTime;
        playerAudioSource.pitch = Random.Range(0.95f, 1.04f);
        playerAudioSource.PlayOneShot(soilCheckAnalyzeClip, soilCheckVolume);
    }

    void AttachHoverSoundToAllDrones()
    {
        droneAudioSources.Clear();
        lastDronePositions.Clear();

        CorpoDroneAI[] drones = FindObjectsByType<CorpoDroneAI>(FindObjectsSortMode.None);
        foreach (CorpoDroneAI d in drones)
        {
            if (d == null) continue;
            AudioSource dSource = d.GetComponent<AudioSource>();
            if (dSource == null)
                dSource = d.gameObject.AddComponent<AudioSource>();

            dSource.clip = droneHoverLoopClip;
            dSource.loop = true;
            dSource.playOnAwake = true;
            dSource.spatialBlend = 1.0f;
            dSource.dopplerLevel = 0.6f;
            dSource.minDistance = 3.5f;
            dSource.maxDistance = 32.0f;
            dSource.rolloffMode = AudioRolloffMode.Linear;
            dSource.volume = droneHoverVolume;
            dSource.pitch = Random.Range(0.94f, 1.04f);
            dSource.time = Random.Range(0f, droneHoverLoopClip.length);
            dSource.Play();

            droneAudioSources.Add(dSource);
            lastDronePositions[d.transform] = d.transform.position;
        }
    }

    void UpdateDroneHoverDynamics()
    {
        for (int i = 0; i < droneAudioSources.Count; i++)
        {
            AudioSource dSource = droneAudioSources[i];
            if (dSource == null || !dSource.gameObject.activeInHierarchy) continue;

            Transform dt = dSource.transform;
            Vector3 lastPos = lastDronePositions.ContainsKey(dt) ? lastDronePositions[dt] : dt.position;
            float speed = (dt.position - lastPos).magnitude / Mathf.Max(0.0001f, Time.deltaTime);
            lastDronePositions[dt] = dt.position;

            float targetPitch = Mathf.Lerp(0.92f, 1.25f, Mathf.Clamp01(speed / 9f));
            dSource.pitch = Mathf.Lerp(dSource.pitch, targetPitch, Time.deltaTime * 5f);

            float targetVol = (speed < 0.1f) ? (droneHoverVolume * 0.45f) : droneHoverVolume;
            dSource.volume = Mathf.Lerp(dSource.volume, targetVol, Time.deltaTime * 4f);
        }
    }

    // ==================== PROCEDURAL SOUND WAVEFORM SYNTHESIZER ====================

    void GenerateAllProceduralClips()
    {
        int sampleRate = 44100;

        // 1. AR-IE Realistic Heavy Robot Footstep (0.36s)
        if (arieFootstepClip == null)
        {
            int samples = (int)(sampleRate * 0.36f);
            float[] data = new float[samples];
            float subPhase = 0f;
            float servoPhase = 0f;
            float subServoPhase = 0f;
            float lpNoise = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float servoEnv = (t < 0.055f) ? Mathf.SmoothStep(0f, 1f, t / 0.055f) : Mathf.Exp(-(t - 0.055f) * 11f);
                float servoFreq = (t < 0.055f) ? Mathf.Lerp(85f, 168f, t / 0.055f) : Mathf.Lerp(168f, 58f, Mathf.Clamp01((t - 0.055f) / 0.22f));

                servoPhase += servoFreq / sampleRate;
                subServoPhase += (servoFreq * 0.5f) / sampleRate;

                float servoMotor = (Mathf.Sin(2f * Mathf.PI * servoPhase) * 0.55f
                                 + Mathf.Sin(4f * Mathf.PI * servoPhase) * 0.25f
                                 + Mathf.Sin(2f * Mathf.PI * subServoPhase) * 0.70f) * servoEnv * 0.42f;

                float impactMix = 0f;
                if (t >= 0.045f)
                {
                    float it = t - 0.045f;
                    float subEnv = Mathf.Exp(-it * 13.5f);
                    float subFreq = Mathf.Lerp(64f, 25f, Mathf.Clamp01(it * 4.5f));
                    subPhase += subFreq / sampleRate;
                    float subThud = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * subPhase) * 2.3) * subEnv * 0.88f;

                    float clankEnv = Mathf.Exp(-it * 34f);
                    float armorClank = (Mathf.Sin(2f * Mathf.PI * 138f * it)
                                      + Mathf.Sin(2f * Mathf.PI * 214f * it) * 0.7f
                                      + Mathf.Sin(2f * Mathf.PI * 342f * it) * 0.4f) * clankEnv * 0.26f;

                    lpNoise = Mathf.Lerp(lpNoise, Random.value * 2f - 1f, 0.22f);
                    float earthAndHiss = lpNoise * (Mathf.Exp(-it * 24f) * 0.45f + Mathf.Clamp01((it - 0.03f) / 0.02f) * Mathf.Exp(-it * 16f) * 0.25f);

                    impactMix = subThud + armorClank + earthAndHiss;
                }

                data[i] = (float)System.Math.Tanh((servoMotor + impactMix) * 1.35);
            }
            arieFootstepClip = CreateClip("SFX_ARIE_RealisticFootstep", data, sampleRate);
        }

        // 2. AR-IE Realistic Hydraulic Jump (0.52s)
        if (arieJumpClip == null)
        {
            int samples = (int)(sampleRate * 0.52f);
            float[] data = new float[samples];
            float subPhase = 0f;
            float servoPhase1 = 0f;
            float servoPhase2 = 0f;
            float filteredAir = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float launchEnv = Mathf.Exp(-t * 16f);
                float launchFreq = Mathf.Lerp(85f, 30f, Mathf.Clamp01(t * 5f));
                subPhase += launchFreq / sampleRate;
                float pushOffThud = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * subPhase) * 2.0) * launchEnv * 0.75f;

                float latchClank = (Mathf.Sin(2f * Mathf.PI * 195f * t) + Mathf.Sin(2f * Mathf.PI * 315f * t)) * Mathf.Exp(-t * 38f) * 0.35f;

                float servoEnv = Mathf.Sin(Mathf.Clamp01(t / 0.46f) * Mathf.PI) * Mathf.Exp(-t * 3.2f);
                float freq1 = Mathf.Lerp(78f, 295f, Mathf.Pow(Mathf.Clamp01(t / 0.42f), 0.65f));
                float freq2 = freq1 * 1.5f;

                servoPhase1 += freq1 / sampleRate;
                servoPhase2 += freq2 / sampleRate;

                float actuatorWave = (Mathf.Sin(2f * Mathf.PI * servoPhase1) * 0.65f
                                    + Mathf.Sin(2f * Mathf.PI * servoPhase2) * 0.35f
                                    + Mathf.Sin(4f * Mathf.PI * servoPhase1) * 0.20f) * servoEnv * 0.55f;

                filteredAir = Mathf.Lerp(filteredAir, Random.value * 2f - 1f, 0.28f);
                float pneumaticBlast = filteredAir * (Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 7.5f)) * 0.48f;

                data[i] = (float)System.Math.Tanh((pushOffThud + latchClank + actuatorWave + pneumaticBlast) * 1.3);
            }
            arieJumpClip = CreateClip("SFX_ARIE_RealisticJump", data, sampleRate);
        }

        // 3. AR-IE Heavy Landing Impact (0.45s)
        if (arieLandClip == null)
        {
            int samples = (int)(sampleRate * 0.45f);
            float[] data = new float[samples];
            float subPhase = 0f;
            float servoPhase = 0f;
            float lpNoise = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float subEnv = Mathf.Exp(-t * 9.5f);
                float subFreq = Mathf.Lerp(72f, 22f, Mathf.Clamp01(t * 3.5f));
                subPhase += subFreq / sampleRate;
                float slam = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * subPhase) * 2.5) * subEnv * 0.92f;

                float servoEnv = Mathf.Exp(-t * 7.5f);
                float servoFreq = Mathf.Lerp(190f, 52f, Mathf.Clamp01(t / 0.35f));
                servoPhase += servoFreq / sampleRate;
                float suspensionGroan = (Mathf.Sin(2f * Mathf.PI * servoPhase) + Mathf.Sin(Mathf.PI * servoPhase) * 0.8f) * servoEnv * 0.40f;

                float clank = (Mathf.Sin(2f * Mathf.PI * 125f * t) + Mathf.Sin(2f * Mathf.PI * 240f * t)) * Mathf.Exp(-t * 24f) * 0.35f;
                lpNoise = Mathf.Lerp(lpNoise, Random.value * 2f - 1f, 0.25f);
                float shockHiss = lpNoise * Mathf.Exp(-t * 11f) * 0.42f;

                data[i] = (float)System.Math.Tanh((slam + suspensionGroan + clank + shockHiss) * 1.4);
            }
            arieLandClip = CreateClip("SFX_ARIE_HeavyLand", data, sampleRate);
        }

        // 4. Heavy Mechanical Brace Clunk on 1st Click (0.24s)
        if (wallPreviewClip == null)
        {
            int samples = (int)(sampleRate * 0.24f);
            float[] data = new float[samples];
            float phase = 0f;
            float lpNoise = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float env = Mathf.Exp(-t * 18f);
                float freq = Mathf.Lerp(88f, 36f, Mathf.Clamp01(t * 8f));
                phase += freq / sampleRate;
                float lowClunk = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * phase) * 2.2) * env * 0.8f;

                float gearLock = (Mathf.Sin(2f * Mathf.PI * 115f * t) + Mathf.Sin(2f * Mathf.PI * 172f * t)) * Mathf.Exp(-t * 32f) * 0.35f;
                lpNoise = Mathf.Lerp(lpNoise, Random.value * 2f - 1f, 0.18f);

                data[i] = (float)System.Math.Tanh((lowClunk + gearLock + lpNoise * Mathf.Exp(-t * 26f) * 0.35f) * 1.3);
            }
            wallPreviewClip = CreateClip("SFX_Wall_HeavyLatchPreview", data, sampleRate);
        }

        // 5. MASSIVE 2-TON BARRIER FLOOR SLAM (0.85s)
        if (wallDeployClip == null)
        {
            int samples = (int)(sampleRate * 0.85f);
            float[] data = new float[samples];
            float subPhase = 0f;
            float bodyPhase = 0f;
            float lockPhase = 0f;
            float lpEarth = 0f;
            float lpDust = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float subEnv = Mathf.Exp(-t * 5.8f);
                float subFreq = Mathf.Lerp(66f, 18f, Mathf.Clamp01(t * 3.8f));
                subPhase += subFreq / sampleRate;
                float seismicSub = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * subPhase) * 3.0) * subEnv * 0.95f;

                float bodyEnv = Mathf.Exp(-t * 14f);
                float bodyFreq = Mathf.Lerp(105f, 38f, Mathf.Clamp01(t * 6.5f));
                bodyPhase += bodyFreq / sampleRate;
                float heavyBody = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * bodyPhase) * 2.2) * bodyEnv * 0.75f;

                float ironEnv = Mathf.Exp(-t * 24f);
                float dullIron = (Mathf.Sin(2f * Mathf.PI * 82f * t) * 0.6f
                                + Mathf.Sin(2f * Mathf.PI * 128f * t) * 0.45f
                                + Mathf.Sin(2f * Mathf.PI * 176f * t) * 0.3f) * ironEnv * 0.45f;

                float rawNoise = Random.value * 2f - 1f;
                lpEarth = Mathf.Lerp(lpEarth, rawNoise, 0.09f);
                lpDust = Mathf.Lerp(lpDust, rawNoise, 0.22f);
                float earthCrush = (lpEarth * Mathf.Exp(-t * 7.5f) * 0.85f) + (lpDust * Mathf.Exp(-t * 18f) * 0.55f);

                float anchorMix = 0f;
                if (t >= 0.14f)
                {
                    float lt = t - 0.14f;
                    float lockEnv = Mathf.Exp(-lt * 12f);
                    float lockFreq = Mathf.Lerp(78f, 24f, Mathf.Clamp01(lt * 5.5f));
                    lockPhase += lockFreq / sampleRate;
                    float spikeSlam = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * lockPhase) * 2.5) * lockEnv * 0.70f;
                    float ventHiss = lpDust * Mathf.Clamp01(lt / 0.02f) * Mathf.Exp(-lt * 9.5f) * 0.48f;
                    anchorMix = spikeSlam + ventHiss;
                }

                data[i] = (float)System.Math.Tanh((seismicSub + heavyBody + dullIron + earthCrush + anchorMix) * 1.55);
            }
            wallDeployClip = CreateClip("SFX_Wall_MassiveGroundSlam", data, sampleRate);
        }

        // 6. HEAVY SCRAP METAL COLLECTING (0.48s)
        if (scrapCollectClip == null)
        {
            int samples = (int)(sampleRate * 0.48f);
            float[] data = new float[samples];
            float magPhase = 0f;
            float lockPhase = 0f;
            float lpScrape = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;

                float magEnv = Mathf.Sin(Mathf.Clamp01(t / 0.22f) * Mathf.PI);
                float magFreq = Mathf.Lerp(92f, 195f, Mathf.Clamp01(t / 0.22f));
                magPhase += magFreq / sampleRate;
                float magPull = (Mathf.Sin(2f * Mathf.PI * magPhase) * 0.6f + Mathf.Sin(Mathf.PI * magPhase) * 0.5f) * magEnv * 0.42f;

                float clatterMix = 0f;
                float[] hitTimes = { 0.02f, 0.09f, 0.17f };
                float[] hitBaseFreqs = { 148f, 182f, 126f };

                for (int h = 0; h < 3; h++)
                {
                    if (t >= hitTimes[h])
                    {
                        float ht = t - hitTimes[h];
                        float cEnv = Mathf.Exp(-ht * 32f);
                        float f0 = hitBaseFreqs[h];
                        float ironHit = (Mathf.Sin(2f * Mathf.PI * f0 * ht) * 0.65f
                                       + Mathf.Sin(2f * Mathf.PI * (f0 * 1.46f) * ht) * 0.45f
                                       + Mathf.Sin(2f * Mathf.PI * (f0 * 2.12f) * ht) * 0.25f) * cEnv * 0.48f;
                        clatterMix += ironHit;
                    }
                }

                lpScrape = Mathf.Lerp(lpScrape, Random.value * 2f - 1f, 0.24f);
                float scrapeGrit = lpScrape * Mathf.Exp(-t * 14f) * 0.38f;

                float compactorLock = 0f;
                if (t >= 0.21f)
                {
                    float lt = t - 0.21f;
                    float lEnv = Mathf.Exp(-lt * 15f);
                    float lFreq = Mathf.Lerp(108f, 32f, Mathf.Clamp01(lt * 6.5f));
                    lockPhase += lFreq / sampleRate;
                    float subLatch = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * lockPhase) * 2.4) * lEnv * 0.85f;
                    float airSeal = lpScrape * Mathf.Exp(-lt * 18f) * 0.35f;
                    compactorLock = subLatch + airSeal;
                }

                data[i] = (float)System.Math.Tanh((magPull + clatterMix + scrapeGrit + compactorLock) * 1.45);
            }
            scrapCollectClip = CreateClip("SFX_Scrap_HeavyCollect", data, sampleRate);
        }

        // 7. GROUND-PENETRATING RADAR SOIL DETECTING PING (0.34s)
        if (soilDetectClip == null)
        {
            int samples = (int)(sampleRate * 0.34f);
            float[] data = new float[samples];
            float subPhase = 0f;
            float sonarPhase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;

                float subEnv = Mathf.Exp(-t * 18f);
                float subFreq = Mathf.Lerp(84f, 36f, Mathf.Clamp01(t * 5f));
                subPhase += subFreq / sampleRate;
                float seismicPulse = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * subPhase) * 1.8) * subEnv * 0.65f;

                float sonarFreq = (t < 0.08f) ? 280f : 420f;
                float localT = (t < 0.08f) ? t : (t - 0.08f);
                float sonarEnv = Mathf.Sin(Mathf.Clamp01(localT / 0.015f) * (Mathf.PI * 0.5f)) * Mathf.Exp(-localT * 13f);
                sonarPhase += sonarFreq / sampleRate;

                float bioPing = (Mathf.Sin(2f * Mathf.PI * sonarPhase) * 0.7f
                               + Mathf.Sin(4f * Mathf.PI * sonarPhase) * 0.2f) * sonarEnv * 0.55f;

                data[i] = (float)System.Math.Tanh((seismicPulse + bioPing) * 1.25);
            }
            soilDetectClip = CreateClip("SFX_Soil_RadarPing", data, sampleRate);
        }

        // 8. PRESS [E] SOIL PROBE & CHEMICAL ANALYSIS SOUND (0.68s)
        if (soilCheckAnalyzeClip == null)
        {
            int samples = (int)(sampleRate * 0.68f);
            float[] data = new float[samples];
            float plungePhase = 0f;
            float drillPhase = 0f;
            float scanPhase = 0f;
            float lpSoil = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;

                float plungeEnv = Mathf.Exp(-t * 16f);
                float plungeFreq = Mathf.Lerp(108f, 26f, Mathf.Clamp01(t * 6f));
                plungePhase += plungeFreq / sampleRate;
                float spikeThud = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * plungePhase) * 2.4) * plungeEnv * 0.85f;

                lpSoil = Mathf.Lerp(lpSoil, Random.value * 2f - 1f, 0.20f);
                float soilCrunch = lpSoil * Mathf.Exp(-t * 14f) * 0.55f;

                float drillMix = 0f;
                if (t >= 0.10f && t < 0.46f)
                {
                    float dt = t - 0.10f;
                    float drillEnv = Mathf.Sin(Mathf.Clamp01(dt / 0.34f) * Mathf.PI);
                    float drillFreq = Mathf.Lerp(115f, 245f, dt / 0.34f);
                    drillPhase += drillFreq / sampleRate;

                    float vibrato = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 28f * dt);
                    drillMix = (Mathf.Sin(2f * Mathf.PI * drillPhase) * 0.6f
                              + Mathf.Sin(Mathf.PI * drillPhase) * 0.55f) * vibrato * drillEnv * 0.48f;
                }

                float readoutMix = 0f;
                if (t >= 0.42f)
                {
                    float rt = t - 0.42f;
                    bool secondPulse = (rt >= 0.11f);
                    float localT = secondPulse ? (rt - 0.11f) : rt;
                    float pulseFreq = secondPulse ? 440f : 330f;
                    float pEnv = Mathf.Exp(-localT * 18f);
                    scanPhase += pulseFreq / sampleRate;

                    readoutMix = (Mathf.Sin(2f * Mathf.PI * scanPhase) * 0.65f
                                + Mathf.Sin(3f * Mathf.PI * scanPhase) * 0.25f) * pEnv * 0.50f;
                }

                data[i] = (float)System.Math.Tanh((spikeThud + soilCrunch + drillMix + readoutMix) * 1.4);
            }
            soilCheckAnalyzeClip = CreateClip("SFX_Soil_ProbeAnalyze", data, sampleRate);
        }

        // 9. INTENSE ELECTRIC SHORT-CIRCUIT EMP & REBOOT (1.05s)
        if (droneRebootEMPClip == null)
        {
            int samples = (int)(sampleRate * 1.05f);
            float[] data = new float[samples];
            float empSubPhase = 0f;
            float turbinePhase = 0f;
            float arcPhase = 0f;
            float sparkFilter = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;

                float empEnv = Mathf.Exp(-t * 10f);
                float empFreq = Mathf.Lerp(140f, 22f, Mathf.Clamp01(t * 4.0f));
                empSubPhase += empFreq / sampleRate;
                float empPulse = (float)System.Math.Tanh(Mathf.Sin(2f * Mathf.PI * empSubPhase) * 2.8) * empEnv * 0.90f;

                float sparkEnv = Mathf.Exp(-t * 5.5f);
                float arcMod = Mathf.Sin(2f * Mathf.PI * 45f * t) * 0.5f + 0.5f;
                float rawSpark = (Random.value > 0.65f) ? (Random.value * 2f - 1f) : 0f;
                sparkFilter = Mathf.Lerp(sparkFilter, rawSpark, 0.35f);

                float arcFreq = Mathf.Lerp(2400f, 450f, t * 3.0f) + Mathf.Sin(2f * Mathf.PI * 180f * t) * 300f;
                arcPhase += arcFreq / sampleRate;

                float electricArc = (Mathf.Sin(2f * Mathf.PI * arcPhase) * 0.4f + sparkFilter * 1.2f) * arcMod * sparkEnv * 0.85f;

                float turbEnv = Mathf.Exp(-t * 3.0f);
                float turbFreq = Mathf.Lerp(950f, 45f, Mathf.Pow(Mathf.Clamp01(t / 0.85f), 0.7f));
                turbinePhase += turbFreq / sampleRate;
                float dyingTurbine = (Mathf.Sin(2f * Mathf.PI * turbinePhase) * 0.6f + Mathf.Sin(4f * Mathf.PI * turbinePhase) * 0.3f) * turbEnv * 0.48f;

                float rebootChime = 0f;
                if (t >= 0.62f)
                {
                    float ct = t - 0.62f;
                    float noteFreq = (ct < 0.15f) ? 523.25f : 783.99f;
                    float noteLocalT = (ct < 0.15f) ? ct : (ct - 0.15f);
                    float chimeEnv = Mathf.Exp(-noteLocalT * 12f);
                    rebootChime = (Mathf.Sin(2f * Mathf.PI * noteFreq * ct) * 0.7f + Mathf.Sin(4f * Mathf.PI * noteFreq * ct) * 0.25f) * chimeEnv * 0.45f;
                }

                data[i] = (float)System.Math.Tanh((empPulse + electricArc + dyingTurbine + rebootChime) * 1.4);
            }
            droneRebootEMPClip = CreateClip("SFX_Drone_EMP_ShortCircuit", data, sampleRate);
        }

        // 10. Realistic 3D Drone Quad-Rotor Chop Loop (2.0s Seamless Loop)
        if (droneHoverLoopClip == null)
        {
            int samples = sampleRate * 2;
            float[] data = new float[samples];
            float filteredWash = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float bladeChop = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 18.0f * t);
                float hoverSway = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 2.0f * t);

                float bassRotor = Mathf.Sin(2f * Mathf.PI * 64.0f * t) * 0.50f
                                + Mathf.Sin(2f * Mathf.PI * 128.0f * t) * 0.30f
                                + Mathf.Sin(2f * Mathf.PI * 192.0f * t) * 0.15f;

                float motorWhine = Mathf.Sin(2f * Mathf.PI * 512.0f * t) * 0.08f;
                float rawAir = Mathf.Sin(2f * Mathf.PI * 317.0f * t + Mathf.Sin(2f * Mathf.PI * 89.0f * t) * 3.5f);
                filteredWash = Mathf.Lerp(filteredWash, rawAir, 0.18f);

                float sample = (bassRotor * bladeChop + motorWhine + filteredWash * bladeChop * 0.28f) * hoverSway;
                data[i] = (float)System.Math.Tanh(sample * 1.25);
            }
            droneHoverLoopClip = CreateClip("SFX_Drone_RealisticHover_Loop", data, sampleRate);
        }
    }

    AudioClip CreateClip(string clipName, float[] samples, int sampleRate)
    {
        AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}