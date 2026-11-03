
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private AudioManager audioManager;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";

    private const string VOLUME_KEY = "MasterVolume";

    private void Start()
    {
        // เริ่มต้นที่หน้า Main Menu
        mainMenuPanel.SetActive(true);
        settingsPanel.SetActive(false);

        // โหลดระดับเสียงที่บันทึกไว้
        float savedVolume = PlayerPrefs.GetFloat(
            VOLUME_KEY, 1f
        );

        masterVolumeSlider.value = savedVolume;

        // นำค่าไปใช้กับ AudioManager
        audioManager.SetMasterVolume(savedVolume);

        // เชื่อม Slider กับระบบเสียง
        masterVolumeSlider.onValueChanged.AddListener(
            ChangeMasterVolume
        );
    }

    // ปุ่ม Play
    public void PlayGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // เปิด Settings
    public void OpenSettings()
    {
        mainMenuPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    // กลับ Main Menu
    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // ปรับเสียง Master Volume
    public void ChangeMasterVolume(float volume)
    {
        audioManager.SetMasterVolume(volume);

        PlayerPrefs.SetFloat(VOLUME_KEY, volume);
        PlayerPrefs.Save();
    }

    // ปุ่ม Quit
    public void QuitGame()
    {
        PlayerPrefs.Save();

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
