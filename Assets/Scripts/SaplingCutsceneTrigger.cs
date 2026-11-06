using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

public class SaplingCutsceneTrigger : MonoBehaviour
{
    [Header("Cutscene Video Settings")]
    public VideoClip saplingCutsceneClip; // Drag your video clip here in the Inspector

    private bool hasTriggered = false;
    private bool videoFinished = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player") || other.name.Contains("Player"))
        {
            hasTriggered = true;
            StartCoroutine(PlayCutsceneAndRewardSapling(other.gameObject));
        }
    }

    IEnumerator PlayCutsceneAndRewardSapling(GameObject playerObj)
    {
        if (saplingCutsceneClip == null)
        {
            Debug.LogError("SaplingCutsceneTrigger: no video clip assigned in the Inspector!");
            hasTriggered = false;
            yield break;
        }

        Debug.Log("Cutscene triggered, preparing video...");

        // 1. Pause game time and hide pause menu
        Time.timeScale = 0f;
        ARIEPlayerController player = playerObj.GetComponent<ARIEPlayerController>();
        if (player != null && player.pauseMenuPanel != null)
            player.pauseMenuPanel.SetActive(false);

        // 2. Build a fullscreen overlay canvas with a RawImage
        GameObject canvasObj = new GameObject("Cutscene_Canvas");
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
        fitter.aspectRatio = (float)saplingCutsceneClip.width / saplingCutsceneClip.height;

        // 3. Setup Video Player
        RenderTexture rt = new RenderTexture((int)saplingCutsceneClip.width, (int)saplingCutsceneClip.height, 0);
        rawImage.texture = rt;

        GameObject videoObj = new GameObject("Active_VideoPlayer_Object");
        videoObj.transform.SetParent(transform, false);
        VideoPlayer vp = videoObj.AddComponent<VideoPlayer>();
        AudioSource audioSource = videoObj.AddComponent<AudioSource>();

        vp.playOnAwake = false;
        vp.source = VideoSource.VideoClip;
        vp.clip = saplingCutsceneClip;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        vp.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime; // keeps playing while timeScale = 0
        vp.audioOutputMode = VideoAudioOutputMode.AudioSource;
        vp.EnableAudioTrack(0, true);
        vp.SetTargetAudioSource(0, audioSource);
        audioSource.ignoreListenerPause = true;

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
            Debug.Log("Playing Sapling Cutscene Video...");

            // 4. Wait until video ends, errors, or player skips
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

        // 6. Resume game and give Sapling
        Time.timeScale = 1f;

        if (player != null)
        {
            player.hasSapling = true;
            player.ShowPrompt("CRYO-SAPLING ACQUIRED! Exit the Cave and find Fertile Soil.");
        }

        gameObject.SetActive(false);
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        videoFinished = true;
    }
}