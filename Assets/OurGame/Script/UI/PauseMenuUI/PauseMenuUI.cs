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
    private bool isPaused;

    private void Start()
    {
        // ใช้ AudioManager ตัวกลางที่อยู่ข้าม Scene ก่อน
        audioManager = AudioManager.Instance;

        // เผื่อกรณีไม่มี Singleton ให้ลองค้นหาใน Scene
        if (audioManager == null)
            audioManager = FindFirstObjectByType<AudioManager>();

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        isPaused = false;
        Time.timeScale = 1f;

        // โหลดค่าระดับเสียงที่บันทึกไว้
        float savedVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);
        savedVolume = Mathf.Clamp01(savedVolume);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.wholeNumbers = false;
            masterVolumeSlider.SetValueWithoutNotify(savedVolume);

            // ป้องกันการเพิ่ม Listener ซ้ำ
            masterVolumeSlider.onValueChanged.RemoveListener(ChangeMasterVolume);
            masterVolumeSlider.onValueChanged.AddListener(ChangeMasterVolume);
            masterVolumeSlider.interactable = true;
        }
        else
        {
            Debug.LogError("PauseMenuUI: ยังไม่ได้กำหนด Master Volume Slider ใน Inspector!");
        }

        ApplyMasterVolume(savedVolume);

        if (audioManager == null)
            Debug.LogError("PauseMenuUI: ไม่พบ AudioManager ใน Scene หรือ AudioManager.Instance!");
    }

    private void OnDestroy()
    {
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveListener(ChangeMasterVolume);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (!isPaused)
        {
            PauseGame();
        }
        else if (settingsPanel != null && settingsPanel.activeSelf)
        {
            CloseSettings();
        }
        else
        {
            ResumeGame();
        }
    }

    // =========================
    // PAUSE MENU
    // =========================

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    // =========================
    // SETTINGS
    // =========================

    public void OpenSettings()
    {
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        // อัปเดตค่า Slider ให้ตรงกับค่าระดับเสียงปัจจุบัน
        float currentVolume = PlayerPrefs.GetFloat(VOLUME_KEY, 1f);

        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(currentVolume);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);
    }

    public void ChangeMasterVolume(float volume)
    {
        ApplyMasterVolume(volume);
    }

    private void ApplyMasterVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);

        // ควบคุมเสียงทั้งหมดทันที
        AudioListener.volume = volume;

        // อัปเดต AudioManager ตัวกลาง
        if (audioManager == null)
            audioManager = AudioManager.Instance;

        if (audioManager != null)
            audioManager.SetMasterVolume(volume);

        // บันทึกค่าไว้ใช้ข้าม Scene และเมื่อเปิดเกมครั้งถัดไป
        PlayerPrefs.SetFloat(VOLUME_KEY, volume);
        PlayerPrefs.Save();

        Debug.Log("Pause Menu Master Volume: " + (volume * 100f).ToString("F0") + "%");
    }

    // =========================
    // BACK TO MAIN MENU
    // =========================

    public void BackToMainMenu()
    {
        Time.timeScale = 1f;
        isPaused = false;

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
