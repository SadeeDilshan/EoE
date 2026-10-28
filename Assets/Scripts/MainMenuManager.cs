using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    void Start()
    {
        // Make sure the mouse cursor is unlocked on the Main Menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
    }

    public void StartGame()
    {
        ARIEPlayerController.batteryLives = 3;
        SceneManager.LoadScene("Level1");
    }

    public void ResetSavedStats()
    {
        PlayerPrefs.DeleteAll();
        ARIEPlayerController.totalScrapRecycled = 0;
        ARIEPlayerController.totalDronesRebooted = 0;
        Debug.Log("Sustainability stats reset!");
    }

    public void QuitGame()
    {
        Debug.Log("Quitting Game...");
        Application.Quit();
    }
}