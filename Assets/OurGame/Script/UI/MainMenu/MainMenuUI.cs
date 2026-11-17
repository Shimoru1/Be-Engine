using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
	[Header("Main Menu")]
	public Image logo;
	public Image Bg;
	public Button play;
	public Button setting;
	public Button quit;

	[HideInInspector]
	public bool buttonsShown = false;

	[Header("Music")]
	[SerializeField] private AudioSource menuMusic;

	[Header("Button Sound")]
	[SerializeField] private AudioSource buttonClickAudio;

	[Header("Play")]
	[Tooltip("ชื่อซีนที่จะโหลดเมื่อกด Play (ต้องอยู่ใน Build Profiles)")]
	public string gameSceneName = "Game";

	private bool isLoading = false;

	private void Start()
	{
		ShowButtonsImmediately();

		if (Bg != null) Bg.gameObject.SetActive(false);
	}

	// ---------------- ปุ่มเมนูหลัก ----------------
	public void ShowButtons()
	{
		ShowButtonsImmediately();
	}

	public void ShowButtonsImmediately()
	{
		play.gameObject.SetActive(true);
		setting.gameObject.SetActive(true);
		quit.gameObject.SetActive(true);

		buttonsShown = true;
	}

	public void HideRemainingButtons()
	{
		play.gameObject.SetActive(false);
		setting.gameObject.SetActive(false);
		quit.gameObject.SetActive(false);
	}

	public void OnPlayClick()
	{
		PlayButtonSound();
	}

	// ผูกกับปุ่ม Play
	public void Play()
	{
		if (isLoading)
			return;

		if (string.IsNullOrEmpty(gameSceneName))
		{
			Debug.LogError("[MainMenuUI] gameSceneName ว่างอยู่");
			return;
		}

		isLoading = true;
		OnPlayClick();

		SceneManager.LoadScene(gameSceneName);
	}

	public void Quit()
	{
		PlayButtonSound();
		Application.Quit();
	}

	private void PlayButtonSound()
	{
		if (AudioManager.Instance != null)
		{
			AudioManager.Instance.PlaySFX("ButtonSound1");
		}
		else
		{
			Debug.LogError("[ButtonManager] AudioManager.Instance is NULL!");
		}
	}
}