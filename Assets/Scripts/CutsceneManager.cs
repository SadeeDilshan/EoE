using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;

public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance;

    [Header("Optional Cinematic Text")]
    public string skipPromptMessage = "Press [SPACE] or [ESC] to Skip";

    private bool isCutscenePlaying = false;
    private bool videoFinished = false;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // Call this from any trigger or script: CutsceneManager.Instance.PlayCutscene(myClip, () => { ... });
    public void PlayCutscene(VideoClip clip, System.Action onCompleteCallback = null)
    {
        if (isCutscenePlaying || clip == null) return;
        StartCoroutine(PlayCutsceneRoutine(clip, onCompleteCallback));
    }

    IEnumerator PlayCutsceneRoutine(VideoClip clip, System.Action onCompleteCallback)
    {
        isCutscenePlaying = true;
        Debug.Log("CutsceneManager: Preparing cinematic video...");

        // 1. Pause game time and hide any active pause menus
        Time.timeScale = 0f;
        ARIEPlayerController player = FindFirstObjectByType<ARIEPlayerController>();
        if (player != null && player.pauseMenuPanel != null)
            player.pauseMenuPanel.SetActive(false);

        // Unlock cursor during cutscene
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 2. Build fullscreen overlay canvas with a RawImage and letterbox background
        GameObject canvasObj = new GameObject("Cutscene_Master_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject bgObj = new GameObject("Background_Letterbox");
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
        fitter.aspectRatio = (float)clip.width / clip.height;

        // Optional Skip Prompt text overlay at the bottom
        GameObject txtObj = new GameObject("SkipPromptText");
        txtObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI skipTxt = txtObj.AddComponent<TextMeshProUGUI>();
        skipTxt.text = skipPromptMessage;
        skipTxt.fontSize = 18;
        skipTxt.color = new Color(0.85f, 0.95f, 0.9f, 0.75f);
        skipTxt.alignment = TextAlignmentOptions.Center;
        RectTransform txtRect = skipTxt.rectTransform;
        txtRect.anchorMin = new Vector2(0f, 0f);
        txtRect.anchorMax = new Vector2(1f, 0f);
        txtRect.offsetMin = new Vector2(0f, 24f);
        txtRect.offsetMax = new Vector2(0f, 64f);

        // 3. Setup RenderTexture & VideoPlayer
        RenderTexture rt = new RenderTexture((int)clip.width, (int)clip.height, 0);
        rawImage.texture = rt;

        GameObject videoObj = new GameObject("Active_VideoPlayer_Object");
        videoObj.transform.SetParent(transform, false);
        VideoPlayer vp = videoObj.AddComponent<VideoPlayer>();
        AudioSource audioSource = videoObj.AddComponent<AudioSource>();

        vp.playOnAwake = false;
        vp.source = VideoSource.VideoClip;
        vp.clip = clip;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        vp.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime; // Plays smoothly while timeScale = 0
        vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
        vp.EnableAudioTrack(0, true);
        vp.SetTargetAudioSource(0, audioSource);
        audioSource.ignoreListenerPause = true;

        videoFinished = false;
        bool videoFailed = false;
        vp.loopPointReached += OnVideoFinished;
        vp.errorReceived += (source, message) =>
        {
            Debug.LogError("CutsceneManager Video Error: " + message);
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
            Debug.Log("Playing Cinematic Cutscene...");

            // 4. Wait until video ends, errors, or player presses Space/Escape to skip
            while (!videoFinished && !videoFailed)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
                    break;
                yield return null;
            }
        }

        // 5. Cleanup video objects and render textures
        vp.loopPointReached -= OnVideoFinished;
        vp.Stop();
        Destroy(videoObj);
        Destroy(canvasObj);
        rt.Release();
        Destroy(rt);

        // 6. Resume game time and lock cursor back for first-person gameplay
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 7. Invoke post-cutscene logic callback (e.g., spawn enemies, give items, change scene)
        onCompleteCallback?.Invoke();

        isCutscenePlaying = false;
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        videoFinished = true;
    }
}