using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    public Transform checkpointTransform;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponent<ARIEPlayerController>() != null)
        {
            if (CheckPointManager.Instance != null)
            {
                CheckPointManager.Instance.UpdateCheckpoint(checkpointTransform != null ? checkpointTransform : transform);
            }
            // Disable trigger after activation so it only fires once
            gameObject.SetActive(false);
        }
    }
}