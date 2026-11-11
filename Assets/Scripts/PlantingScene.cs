using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

public class PlantingScene : MonoBehaviour
{
    [Header("Cutscene Video Settings")]
    public VideoClip plantingCutsceneClip;
    public Level3TreeDefense defenseManager;

    [Header("Background Music Fade Settings")]
    public AudioSource bgmAudioSource;
    public float musicFadeOutTime = 0.8f;
    public float musicFadeInTime = 1.5f;

    private bool wasDefenseStarted = false;
    private bool videoFinished = false;
    private float savedBgmVolume = 0.6f;

    void Start()
    {
        // Ensure global audio is never stuck muted
        AudioListener.pause = false;
        if (AudioListener.volume <= 0.01f)
            AudioListener.volume = 1f;

        if (defenseManager == null)
            defenseManager = FindFirstObjectByType<Level3TreeDefense>();

        if (defenseManager != null)
            wasDefenseStarted = defenseManager.defenseStarted;

        if (bgmAudioSource == null)
        {
            if (MusicManager.Instance != null)
            {
                bgmAudioSource = MusicManager.Instance.GetComponent<AudioSource>();
            }
            else
            {
                GameObject bgmObj = GameObject.Find("BackgroundMusicManager");
                if (bgmObj != null)
                    bgmAudioSource = bgmObj.GetComponent<AudioSource>();
            }
        }
    }

    void Update()
    {
        if (defenseManager == null) return;

        if (defenseManager.defenseStarted && !wasDefenseStarted)
        {
            wasDefenseStarted = true;
            StartCoroutine(PlayPlantingCutsceneRoutine());
        }
        else if (!defenseManager.defenseStarted)
        {
            wasDefenseStarted = false;
        }
    }

    IEnumerator PlayPlantingCutsceneRoutine()
    {
        if (plantingCutsceneClip == null)
        {
            Debug.LogError("PlantingScene: No video clip assigned in the Inspector!");
            yield break;
        }

        // 1. Pause game time
        Time.timeScale = 0f;
        AudioListener.pause = false; // Keep AudioListener active for video & fade
        ARIEPlayerController player = FindFirstObjectByType<ARIEPlayerController>();
        if (player != null && player.pauseMenuPanel != null)
            player.pauseMenuPanel.SetActive(false);

        // 2. Fade out BGM in parallel (does not block video preparation)
        StartCoroutine(FadeOutBGMRoutine());

        // 3. Build fullscreen overlay canvas with RawImage
        GameObject canvasObj = new GameObject("PlantingCutscene_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = Color.black;
        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        GameObject rawObj = new GameObject("VideoImage");
        rawObj.transform.SetParent(canvasObj.transform, false);
        RawImage rawImage = rawObj.AddComponent<RawImage>();
        RectTransform rawRect = rawImage.rectTransform;
        rawRect.anchorMin = Vector2.zero;
        rawRect.anchorMax = Vector2.one;
        rawRect.offsetMin = Vector2.zero;
        rawRect.offsetMax = Vector2.zero;
        AspectRatioFitter fitter = rawObj.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = (float)plantingCutsceneClip.width / plantingCutsceneClip.height;

        // 4. Setup RenderTexture & VideoPlayer
        RenderTexture rt = new RenderTexture((int)plantingCutsceneClip.width, (int)plantingCutsceneClip.height, 0);
        rawImage.texture = rt;

        GameObject videoObj = new GameObject("Active_PlantingVideoPlayer");
        videoObj.transform.SetParent(transform, false);
        VideoPlayer vp = videoObj.AddComponent<VideoPlayer>();
        AudioSource audioSource = videoObj.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.volume = 1f;
        audioSource.ignoreListenerPause = true;

        vp.playOnAwake = false;
        vp.source = VideoSource.VideoClip;
        vp.clip = plantingCutsceneClip;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        vp.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
        vp.EnableAudioTrack(0, true);
        vp.SetTargetAudioSource(0, audioSource);

        videoFinished = false;
        bool videoFailed = false;
        vp.loopPointReached += OnVideoFinished;
        vp.errorReceived += (source, message) =>
        {
            Debug.LogError("Video error: " + message);
            videoFailed = true;
        };

        vp.Prepare();
        while (!vp.isPrepared && !videoFailed)
        {
            yield return null;
        }

        if (!videoFailed)
        {
            vp.Play();
            yield return null;

            while (!videoFinished && !videoFailed)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
                    break;
                yield return null;
            }
        }

        // 5. Clean up
        vp.loopPointReached -= OnVideoFinished;
        vp.Stop();
        Destroy(videoObj);
        Destroy(canvasObj);
        rt.Release();
        Destroy(rt);

        // 6. Resume game time and restore BGM
        Time.timeScale = 1f;
        AudioListener.pause = false;
        AudioListener.volume = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        StartCoroutine(FadeInBGMRoutine());
    }

    private IEnumerator FadeOutBGMRoutine()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.FadeOutMusic(musicFadeOutTime);
        }
        else if (bgmAudioSource != null)
        {
            if (bgmAudioSource.volume > 0.05f)
                savedBgmVolume = bgmAudioSource.volume;

            float startVol = bgmAudioSource.volume;
            float elapsed = 0f;

            while (elapsed < musicFadeOutTime)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmAudioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / Mathf.Max(0.01f, musicFadeOutTime));
                yield return null;
            }
            bgmAudioSource.volume = 0f;
        }
    }

    private IEnumerator FadeInBGMRoutine()
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.FadeInMusic(musicFadeInTime);
        }
        else if (bgmAudioSource != null)
        {
            if (!bgmAudioSource.isPlaying && bgmAudioSource.clip != null)
                bgmAudioSource.Play();

            float startVol = bgmAudioSource.volume;
            float elapsed = 0f;

            while (elapsed < musicFadeInTime)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmAudioSource.volume = Mathf.Lerp(startVol, savedBgmVolume, elapsed / Mathf.Max(0.01f, musicFadeInTime));
                yield return null;
            }
            bgmAudioSource.volume = savedBgmVolume;
        }
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        videoFinished = true;
    }
}