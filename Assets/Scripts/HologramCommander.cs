using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HologramCommander : MonoBehaviour
{
    public static HologramCommander Instance;

    [Header("Hologram UI Elements")]
    public Image hologramPanelImage;
    public TMP_Text hologramText;
    public float displayDuration = 3.5f;

    private Coroutine hideRoutine;

    void Awake()
    {
        Instance = this;
        if (hologramPanelImage == null)
        {
            hologramPanelImage = GetComponent<Image>();
            if (hologramPanelImage == null)
                hologramPanelImage = gameObject.AddComponent<Image>();
        }
    }

    public void ShowMessage(string message, bool isAlert = false)
    {
        gameObject.SetActive(true);

        if (hologramPanelImage != null)
        {
            hologramPanelImage.color = isAlert
                ? new Color(0.85f, 0.15f, 0.15f, 0.55f)
                : new Color(0.05f, 0.65f, 0.85f, 0.45f);
        }

        if (hologramText != null)
            hologramText.text = message;

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(AutoHideRoutine());
    }

    IEnumerator AutoHideRoutine()
    {
        yield return new WaitForSeconds(displayDuration);
        if (hologramPanelImage != null)
            hologramPanelImage.color = new Color(0f, 0f, 0f, 0f);
        if (hologramText != null)
            hologramText.text = "";
    }
}