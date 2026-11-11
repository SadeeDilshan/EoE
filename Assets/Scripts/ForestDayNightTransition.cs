using UnityEngine;
using UnityEngine.Rendering;

public class ForestDayNightTransition : MonoBehaviour
{
    [Header("1. Progression Timing (Starts From Very Beginning!)")]
    [Tooltip("Seconds to gradually transition from Day (0%) to Dusk (82%) while exploring.")]
    public float sunsetDurationSeconds = 180f;
    [Range(0f, 1f)]
    public float currentNightProgress = 0f;

    [Header("2. Visibility & Brightness Multiplier (Tweak Anytime!)")]
    [Range(0.5f, 3.0f)]
    [Tooltip("Increase this slider if you want the night terrain even brighter!")]
    public float nightVisibilityBoost = 1.35f;

    [Header("3. Scene References (Auto-Assigned on Start)")]
    public Light directionalSunLight;
    public Level3TreeDefense defenseManager;
    public Transform playerTransform;

    [Header("4. Daytime Look (Start of Game)")]
    public float dayLightIntensity = 1.35f;
    public Color dayLightColor = new Color(1.0f, 0.94f, 0.82f);
    public Color daySkyTint = new Color(0.52f, 0.62f, 0.78f);
    public Color dayAmbientColor = new Color(0.62f, 0.58f, 0.52f);
    public Color dayFogColor = new Color(0.62f, 0.50f, 0.38f);
    public float dayAtmosphereThickness = 1.0f;
    public float daySkyboxExposure = 1.15f;

    [Header("5. Sunset / Dusk Look (Mid-Transition)")]
    public Color sunsetLightColor = new Color(1.0f, 0.55f, 0.28f);
    public Color sunsetSkyTint = new Color(0.35f, 0.22f, 0.36f);
    public Color sunsetFogColor = new Color(0.28f, 0.18f, 0.26f);

    [Header("6. Clear Moonlight Night Look (After Planting Sapling)")]
    [Tooltip("Directional light acts as bright silver-blue Moonlight so you can clearly see the ground!")]
    public float nightMoonlightIntensity = 0.68f;
    public Color nightMoonlightColor = new Color(0.52f, 0.72f, 1.0f);
    public Color nightSkyTint = new Color(0.04f, 0.07f, 0.16f);
    [Tooltip("Ambient light illuminates dark shadows so nothing is ever pitch black.")]
    public Color nightAmbientColor = new Color(0.26f, 0.34f, 0.48f);
    public Color nightFogColor = new Color(0.07f, 0.11f, 0.20f);
    public float nightAtmosphereThickness = 0.28f;
    public float nightSkyboxExposure = 0.25f;

    [Header("7. Twinkling Starfield Settings")]
    public int starCount = 450;
    public float starDomeRadius = 260f;
    public float twinkleSpeed = 3.2f;
    public float starsAppearThreshold = 0.30f;

    private Material runtimeSkyboxMat;
    private Light treeBioLight;
    private Light playerNightFlashlight;
    private float defaultFogDensity = 0.004f;

    // Procedural Twinkling Starfield Data
    private ParticleSystem starParticleSystem;
    private ParticleSystem.Particle[] starParticles;
    private float[] starBaseSizes;
    private float[] starTwinklePhases;
    private Color[] starBaseColors;
    private Transform starDomeTransform;

    void Start()
    {
        if (directionalSunLight == null)
        {
            if (RenderSettings.sun != null)
                directionalSunLight = RenderSettings.sun;
            else
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional)
                    {
                        directionalSunLight = l;
                        break;
                    }
                }
            }
        }

        if (defenseManager == null)
            defenseManager = FindFirstObjectByType<Level3TreeDefense>();

        if (playerTransform == null)
        {
            ARIEPlayerController p = FindFirstObjectByType<ARIEPlayerController>();
            if (p != null) playerTransform = p.transform;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        if (RenderSettings.fog)
            defaultFogDensity = RenderSettings.fogDensity;

        if (RenderSettings.skybox != null)
        {
            runtimeSkyboxMat = new Material(RenderSettings.skybox);
            RenderSettings.skybox = runtimeSkyboxMat;
        }

        BuildTwinklingStarfieldDome();

        currentNightProgress = 0f;
        ApplyEnvironmentLighting(0f);
    }

    void Update()
    {
        if (Time.timeScale > 0f)
        {
            bool saplingPlanted = (defenseManager != null && defenseManager.defenseStarted);

            if (saplingPlanted)
            {
                currentNightProgress = Mathf.MoveTowards(currentNightProgress, 1.0f, Time.deltaTime * 0.45f);
            }
            else
            {
                float step = Time.deltaTime / Mathf.Max(10f, sunsetDurationSeconds);
                currentNightProgress = Mathf.MoveTowards(currentNightProgress, 0.82f, step);
            }
        }

        // Turn on AR-IE's tactical headlamp & Sapling bio-glow as soon as dusk starts (> 45%)
        if (currentNightProgress > 0.45f)
        {
            EnsureNightGameplayLights();
        }

        ApplyEnvironmentLighting(currentNightProgress);
        UpdateTwinklingStars(currentNightProgress);
    }

    // ==================== 1. CLEAR MOONLIGHT NIGHT & SUNSET LIGHTING ====================

    void ApplyEnvironmentLighting(float t)
    {
        float smoothT = Mathf.SmoothStep(0f, 1f, t);

        Color currentLightCol;
        Color currentSkyCol;
        Color currentFogCol;

        if (smoothT < 0.5f)
        {
            float halfT = smoothT * 2f;
            currentLightCol = Color.Lerp(dayLightColor, sunsetLightColor, halfT);
            currentSkyCol = Color.Lerp(daySkyTint, sunsetSkyTint, halfT);
            currentFogCol = Color.Lerp(dayFogColor, sunsetFogColor, halfT);
        }
        else
        {
            float halfT = (smoothT - 0.5f) * 2f;
            currentLightCol = Color.Lerp(sunsetLightColor, nightMoonlightColor, halfT);
            currentSkyCol = Color.Lerp(sunsetSkyTint, nightSkyTint, halfT);
            currentFogCol = Color.Lerp(sunsetFogColor, nightFogColor, halfT);
        }

        // 1. Keep Directional Light ABOVE the horizon (38 degrees at night) so it acts as bright Moonlight!
        if (directionalSunLight != null)
        {
            float targetNightIntensity = nightMoonlightIntensity * nightVisibilityBoost;
            directionalSunLight.intensity = Mathf.Lerp(dayLightIntensity, targetNightIntensity, smoothT);
            directionalSunLight.color = currentLightCol;

            // Pitch goes from 55° (Noon) -> 22° (Sunset) -> 38° (High Moon overhead so terrain is well-lit!)
            float sunPitch = (smoothT < 0.5f)
                ? Mathf.Lerp(55f, 22f, smoothT * 2f)
                : Mathf.Lerp(22f, 38f, (smoothT - 0.5f) * 2f);

            float sunYaw = Mathf.Lerp(-30f, 35f, smoothT);
            directionalSunLight.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);
        }

        // 2. Strong Moonlight Blue Ambient Fill so shadows and valleys are always clearly visible
        Color boostedNightAmbient = nightAmbientColor * nightVisibilityBoost;
        boostedNightAmbient.a = 1f;
        RenderSettings.ambientLight = Color.Lerp(dayAmbientColor, boostedNightAmbient, smoothT);
        RenderSettings.ambientIntensity = Mathf.Lerp(1.0f, 0.85f * nightVisibilityBoost, smoothT);
        RenderSettings.reflectionIntensity = Mathf.Lerp(1.0f, 0.55f, smoothT);

        // 3. Reduce Fog Density at night so distant trees/harvesters aren't hidden in black fog
        if (RenderSettings.fog)
        {
            RenderSettings.fogColor = currentFogCol;
            RenderSettings.fogDensity = Mathf.Lerp(defaultFogDensity, defaultFogDensity * 0.35f, smoothT);
        }

        // 4. Darken ONLY the Skybox Dome Overhead so the Twinkling Stars pop out clearly!
        if (runtimeSkyboxMat != null)
        {
            // Disables the bright sun disc in the skybox at night even though the Directional Light stays angled down
            if (runtimeSkyboxMat.HasProperty("_SunSize"))
                runtimeSkyboxMat.SetFloat("_SunSize", Mathf.Lerp(0.04f, 0.0f, smoothT));

            if (runtimeSkyboxMat.HasProperty("_Exposure"))
                runtimeSkyboxMat.SetFloat("_Exposure", Mathf.Lerp(daySkyboxExposure, nightSkyboxExposure, smoothT));

            if (runtimeSkyboxMat.HasProperty("_AtmosphereThickness"))
                runtimeSkyboxMat.SetFloat("_AtmosphereThickness", Mathf.Lerp(dayAtmosphereThickness, nightAtmosphereThickness, smoothT));

            if (runtimeSkyboxMat.HasProperty("_SkyTint"))
                runtimeSkyboxMat.SetColor("_SkyTint", currentSkyCol);

            if (runtimeSkyboxMat.HasProperty("_GroundColor"))
                runtimeSkyboxMat.SetColor("_GroundColor", Color.Lerp(dayFogColor, nightFogColor, smoothT));

            if (runtimeSkyboxMat.HasProperty("_Tint"))
                runtimeSkyboxMat.SetColor("_Tint", Color.Lerp(Color.gray, nightSkyTint, smoothT));
        }

        Camera cam = Camera.main;
        if (cam != null)
            cam.backgroundColor = currentSkyCol;
    }

    // ==================== 2. TWINKLING 3D STARFIELD DOME ====================

    void BuildTwinklingStarfieldDome()
    {
        GameObject domeObj = GameObject.Find("NightSky_TwinklingStars");
        if (domeObj != null) Destroy(domeObj);

        domeObj = new GameObject("NightSky_TwinklingStars");
        starDomeTransform = domeObj.transform;

        starParticleSystem = domeObj.AddComponent<ParticleSystem>();
        var main = starParticleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = starCount;
        main.startSpeed = 0f;
        main.startLifetime = float.MaxValue;

        var emission = starParticleSystem.emission;
        emission.enabled = false;

        var shape = starParticleSystem.shape;
        shape.enabled = false;

        ParticleSystemRenderer psr = domeObj.GetComponent<ParticleSystemRenderer>();
        Shader starShader = Shader.Find("Particles/Standard Unlit");
        if (starShader == null) starShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (starShader == null) starShader = Shader.Find("Sprites/Default");

        Material starMat = new Material(starShader);
        starMat.SetTexture("_MainTex", GenerateSoftStarTexture(32));
        if (starMat.HasProperty("_BaseMap")) starMat.SetTexture("_BaseMap", starMat.GetTexture("_MainTex"));
        psr.material = starMat;
        psr.shadowCastingMode = ShadowCastingMode.Off;
        psr.receiveShadows = false;

        starParticles = new ParticleSystem.Particle[starCount];
        starBaseSizes = new float[starCount];
        starTwinklePhases = new float[starCount];
        starBaseColors = new Color[starCount];

        for (int i = 0; i < starCount; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.90f + 0.10f;
            dir.Normalize();

            starParticles[i].position = dir * starDomeRadius;
            starParticles[i].remainingLifetime = 999999f;
            starParticles[i].startLifetime = 999999f;

            starBaseSizes[i] = Random.Range(1.8f, 4.8f);
            starTwinklePhases[i] = Random.Range(0f, Mathf.PI * 2f);

            float colorPick = Random.value;
            if (colorPick < 0.6f)
                starBaseColors[i] = new Color(0.90f, 0.96f, 1.0f, 1f);
            else if (colorPick < 0.85f)
                starBaseColors[i] = new Color(0.68f, 0.86f, 1.0f, 1f);
            else
                starBaseColors[i] = new Color(1.0f, 0.92f, 0.72f, 1f);

            starParticles[i].startSize = 0f;
            starParticles[i].startColor = new Color(1f, 1f, 1f, 0f);
        }

        starParticleSystem.SetParticles(starParticles, starCount);
    }

    void UpdateTwinklingStars(float nightAmount)
    {
        if (starParticleSystem == null || starParticles == null) return;

        if (playerTransform != null && starDomeTransform != null)
            starDomeTransform.position = new Vector3(playerTransform.position.x, 0f, playerTransform.position.z);

        float starVisibility = Mathf.Clamp01((nightAmount - starsAppearThreshold) / (1f - starsAppearThreshold));
        if (starVisibility <= 0.001f)
        {
            if (starParticleSystem.gameObject.activeSelf)
                starParticleSystem.gameObject.SetActive(false);
            return;
        }

        if (!starParticleSystem.gameObject.activeSelf)
            starParticleSystem.gameObject.SetActive(true);

        float t = Time.unscaledTime * twinkleSpeed;

        for (int i = 0; i < starCount; i++)
        {
            float wave1 = Mathf.Sin(t + starTwinklePhases[i]);
            float wave2 = Mathf.Cos(t * 1.73f + starTwinklePhases[i] * 2.1f);
            float twinkleFactor = 0.55f + 0.45f * (wave1 * 0.65f + wave2 * 0.35f);

            Color c = starBaseColors[i];
            c.a = Mathf.Clamp01(starVisibility * twinkleFactor);

            starParticles[i].startColor = c;
            starParticles[i].startSize = starBaseSizes[i] * (0.75f + 0.35f * twinkleFactor) * starVisibility;
        }

        starParticleSystem.SetParticles(starParticles, starCount);
    }

    Texture2D GenerateSoftStarTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float maxRadius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y) - center;
                float dist = p.magnitude / maxRadius;

                float core = Mathf.Pow(Mathf.Clamp01(1f - dist), 2.5f);
                float spikeX = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(p.y) / (maxRadius * 0.25f)), 3f) * Mathf.Clamp01(1f - Mathf.Abs(p.x) / maxRadius);
                float spikeY = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(p.x) / (maxRadius * 0.25f)), 3f) * Mathf.Clamp01(1f - Mathf.Abs(p.y) / maxRadius);

                float alpha = Mathf.Clamp01(core + (spikeX + spikeY) * 0.45f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    // ==================== 3. BRIGHT NIGHT HEADLAMP & SAPLING GLOW ====================

    void EnsureNightGameplayLights()
    {
        // 1. Bright emerald bioluminescent light on the Growing Tree
        if (treeBioLight == null && defenseManager != null && defenseManager.growingTree != null)
        {
            GameObject lightObj = new GameObject("Sapling_NightBioLight");
            lightObj.transform.SetParent(defenseManager.growingTree, false);
            lightObj.transform.localPosition = new Vector3(0f, 2.5f, 0f);

            treeBioLight = lightObj.AddComponent<Light>();
            treeBioLight.type = LightType.Point;
            treeBioLight.color = new Color(0.25f, 1.0f, 0.65f);
            treeBioLight.range = 32f;
            treeBioLight.intensity = 7.5f;
            treeBioLight.shadows = LightShadows.None;
        }

        // 2. Wide, high-visibility tactical headlamp on AR-IE's camera
        if (playerNightFlashlight == null && Camera.main != null)
        {
            GameObject lampObj = new GameObject("ARIE_NightHeadlamp");
            lampObj.transform.SetParent(Camera.main.transform, false);
            lampObj.transform.localPosition = Vector3.zero;
            lampObj.transform.localRotation = Quaternion.identity;

            playerNightFlashlight = lampObj.AddComponent<Light>();
            playerNightFlashlight.type = LightType.Spot;
            playerNightFlashlight.color = new Color(0.82f, 0.96f, 1.0f);
            playerNightFlashlight.range = 48f;
            playerNightFlashlight.spotAngle = 85f;
            playerNightFlashlight.intensity = 5.5f;
            playerNightFlashlight.shadows = LightShadows.None;
        }
    }
}