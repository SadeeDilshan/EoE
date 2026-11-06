using UnityEngine;
using UnityEngine.Video;

public class ForestCutsceneTrigger : MonoBehaviour
{
    public VideoClip forestVideoClip;
    private bool hasTriggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered) return;

        if (other.CompareTag("Player") || other.name.Contains("Player"))
        {
            hasTriggered = true;

            if (CutsceneManager.Instance != null && forestVideoClip != null)
            {
                CutsceneManager.Instance.PlayCutscene(forestVideoClip, () =>
                {
                    // Code executed automatically after the video finishes or is skipped!
                    ARIEPlayerController player = other.GetComponent<ARIEPlayerController>();
                    if (player != null)
                        player.ShowPrompt("WASTELAND REACHED: Explore valleys, scan soil, and avoid Corpo-Drones.");

                    gameObject.SetActive(false);
                });
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}