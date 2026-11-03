
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    [Header("Pause Panels")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Audio Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private AudioManager audioManager;

    [Header("Scene Settings")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private const string VOLUME_KEY = "MasterVolume";

    private bool isPaused = false;

    private void Start()
    {
        // ค้นหา AudioManager หากยังไม่ได้กำหนดใน Inspector
        if (audioManager == null)
        {
            audioManager = FindFirstObjectByType<AudioManager>();
        }

        // ซ่อน Pause Menu และ Settings ตอนเริ่มเกม
        pauseMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);

        Time.timeScale = 1f;

        // โหลดระดับเสียงที่บันทึกไว้
        float savedVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(savedVolume);

            masterVolumeSlider.onValueChanged.AddListener(
                ChangeMasterVolume
            );
        }

        if (audioManager != null)
        {
            audioManager.SetMasterVolume(savedVolume);
        }
        else
        {
            Debug.LogWarning("PauseMenuUI: AudioManager not found!");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
            {
                // ถ้าอยู่หน้า Settings ให้กลับหน้า Pause
                if (settingsPanel.activeSelf)
                {
                    CloseSettings();
                }
                else
                {
                    ResumeGame();
                }
            }
            else
            {
                PauseGame();
            }
        }
    }

    // =========================
    // PAUSE MENU
    // =========================

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        pauseMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    // ปุ่ม Continue
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        pauseMenuPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    // =========================
    // SETTINGS
    // =========================

    public void OpenSettings()
    {
        pauseMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        pauseMenuPanel.SetActive(true);
    }

    public void ChangeMasterVolume(float volume)
    {
        if (audioManager != null)
        {
            audioManager.SetMasterVolume(volume);
        }

        PlayerPrefs.SetFloat(VOLUME_KEY, volume);
        PlayerPrefs.Save();
    }

    // =========================
    // BACK TO MAIN MENU
    // =========================

    public void BackToMainMenu()
    {
        // คืนค่าเวลา ก่อนเปลี่ยน Scene
        Time.timeScale = 1f;
        isPaused = false;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
