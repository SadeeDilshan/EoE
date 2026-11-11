using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Music Fade Settings")]
    public float fadeOutSpeed = 2.5f;
    public float fadeInSpeed = 1.2f;

    // Tracks all looping background music AudioSources in the scene and their original volumes
    private Dictionary<AudioSource, float> trackedMusicSources = new Dictionary<AudioSource, float>();
    private float currentFadeMultiplier = 1f;
    private ARIEPlayerController player;

    void Awake()
    {
        AudioListener.pause = false;
        if (AudioListener.volume <= 0.01f)
            AudioListener.volume = 1f;

        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    void Start()
    {
        player = FindFirstObjectByType<ARIEPlayerController>();
        ScanForMusicSources();
    }

    void ScanForMusicSources()
    {
        AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (AudioSource src in allSources)
        {
            if (src == null) continue;

            // Ignore 3D drone hover sounds or VideoPlayer audio sources
            if (src.GetComponent<CorpoDroneAI>() != null) continue;
            if (src.GetComponent<VideoPlayer>() != null) continue;

            // Identify 2D looping background music sources (or the source on this object)
            if (src.gameObject == this.gameObject || (src.loop && src.spatialBlend < 0.5f))
            {
                if (!trackedMusicSources.ContainsKey(src))
                {
                    float startVol = src.volume > 0.05f ? src.volume : 0.6f;
                    trackedMusicSources.Add(src, startVol);
                    src.ignoreListenerPause = true;
                }
            }
        }
    }

    void Update()
    {
        // Re-scan occasionally in case a music object was enabled late
        if (trackedMusicSources.Count == 0)
            ScanForMusicSources();

        bool shouldMuteMusic = IsCutsceneCurrentlyActive();

        // Smoothly move multiplier toward 0 (during cutscene) or 1 (during gameplay)
        float targetMultiplier = shouldMuteMusic ? 0f : 1f;
        float speed = shouldMuteMusic ? fadeOutSpeed : fadeInSpeed;

        currentFadeMultiplier = Mathf.MoveTowards(
            currentFadeMultiplier,
            targetMultiplier,
            speed * Time.unscaledDeltaTime
        );

        // Apply fade to every background music AudioSource in the scene
        foreach (var kvp in trackedMusicSources)
        {
            AudioSource src = kvp.Key;
            float baseVolume = kvp.Value;
            if (src != null)
            {
                src.volume = baseVolume * currentFadeMultiplier;
            }
        }
    }

    private bool IsCutsceneCurrentlyActive()
    {
        // Check 1: Is ANY VideoPlayer currently playing or prepared?
        VideoPlayer[] vps = FindObjectsByType<VideoPlayer>(FindObjectsSortMode.None);
        for (int i = 0; i < vps.Length; i++)
        {
            if (vps[i] != null && vps[i].gameObject.activeInHierarchy)
            {
                // If it's a dynamic cutscene VideoPlayer or actively playing
                if (vps[i].isPlaying || vps[i].name.Contains("VideoPlayer") || vps[i].name.Contains("Cutscene"))
                {
                    return true;
                }
            }
        }

        // Check 2: Did a cutscene script pause Time.timeScale (and it's NOT the normal Pause Menu or Start Menu)?
        if (Time.timeScale == 0f)
        {
            bool pauseMenuOpen = (player != null && player.pauseMenuPanel != null && player.pauseMenuPanel.activeInHierarchy);
            HUDController hud = FindFirstObjectByType<HUDController>();
            bool startMenuOpen = (hud != null && hud.startMenuPanel != null && hud.startMenuPanel.activeInHierarchy);

            if (!pauseMenuOpen && !startMenuOpen)
            {
                return true;
            }
        }

        return false;
    }

    // Kept for backward compatibility so PlantingScene.cs compiles without errors
    public void FadeOutMusic(float duration = 1f) { currentFadeMultiplier = 0f; }
    public void FadeInMusic(float duration = 1f) { }
}