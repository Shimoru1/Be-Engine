using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
	public static PauseMenu Instance;
	public bool tutorialFreeze = false;

	[Header("Pause UI")]
	[SerializeField] private GameObject pausePanel;

	[Header("Setting UI")]
	[SerializeField] private GameObject settingPanel;

	[Header("Button Sound")]
	[SerializeField] private AudioSource buttonClickAudio;

	public bool IsPaused { get; private set; } = false;

	private void Awake()
	{
		Instance = this;
	}

	private void Start()
	{
		Time.timeScale = 1f;

		IsPaused = false;

		if (pausePanel != null)
			pausePanel.SetActive(false);

		if (settingPanel != null)
			settingPanel.SetActive(false);

		EnsureOnTop(pausePanel);
		EnsureOnTop(settingPanel);

		LockMouse();
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			if (GameManager.Instance != null &&
				(GameManager.Instance.IsWin() ||
				 GameManager.Instance.IsGameOver()))
			{
				return;
			}

			if (IsPaused &&
				settingPanel != null &&
				settingPanel.activeSelf)
			{
				CloseSetting();
				return;
			}

			TogglePause();
		}
	}

	public void TogglePause()
	{
		if (IsPaused)
		{
			ContinueGame();
		}
		else
		{
			PauseGame();
		}
	}

	public void PauseGame()
	{
		if (GameManager.Instance == null)
			return;

		if (!tutorialFreeze &&
			!GameManager.Instance.IsPlaying() &&
			!GameManager.Instance.IsStarting() &&
			!GameManager.Instance.IsSelecting())
			return;

		IsPaused = true;

		if (!tutorialFreeze)
			GameManager.Instance.PauseGame();

		AudioListener.pause = true;

		if (pausePanel != null) pausePanel.SetActive(true);
		if (settingPanel != null) settingPanel.SetActive(false);

		UnlockMouse();
	}

	public void ContinueGame()
	{
		if (!IsPaused)
			return;

		AudioListener.pause = false;
		PlayButtonSound();
		IsPaused = false;

		if (GameManager.Instance != null && !tutorialFreeze)
			GameManager.Instance.ResumeGame();

		if (pausePanel != null) pausePanel.SetActive(false);
		if (settingPanel != null) settingPanel.SetActive(false);

		if (tutorialFreeze || (GameManager.Instance != null && GameManager.Instance.IsSelecting()))
		{
			UnlockMouse();
		}
		else
		{
			LockMouse();
			StartCoroutine(LockMouseNextFrame());
		}
	}


	public void OpenSetting()
	{
		if (!IsPaused)
			return;

		PlayButtonSound();

		if (pausePanel != null)
			pausePanel.SetActive(false);

		if (settingPanel != null)
			settingPanel.SetActive(true);

		UnlockMouse();

		Debug.Log("SETTING OPEN");
	}

	public void CloseSetting()
	{
		if (!IsPaused)
			return;

		PlayButtonSound();

		if (settingPanel != null)
			settingPanel.SetActive(false);

		if (pausePanel != null)
			pausePanel.SetActive(true);

		UnlockMouse();

		Debug.Log("SETTING CLOSED");
	}

	public void ExitGame()
	{
		if (AudioManager.Instance != null) AudioManager.Instance.DetachCar();
		if (AudioManager.Instance != null) AudioManager.Instance.SilenceAllForSceneChange();
		AudioListener.pause = false;

		PlayButtonSound();

		Time.timeScale = 1f;
		IsPaused = false;

		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		SceneManager.LoadScene("MainMenu");
	}

	public void RestartGame()
	{
		if (AudioManager.Instance != null) AudioManager.Instance.DetachCar();
		if (AudioManager.Instance != null) AudioManager.Instance.SilenceAllForSceneChange();
		AudioListener.pause = false;

		PlayButtonSound();

		Time.timeScale = 1f;
		IsPaused = false;

		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;

		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}

	private void PlayButtonSound()
	{
		if (AudioManager.Instance != null)
		{
			AudioManager.Instance.PlaySFX("ButtonSound1");
		}
		else
		{
			Debug.LogWarning("[PauseMenu] AudioManager.Instance is NULL!");
		}
	}

	public void UnlockMouse()
	{
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}

	public void LockMouse()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}
	private System.Collections.IEnumerator LockMouseNextFrame()
	{
		yield return null;
		LockMouse();

		yield return null;
		LockMouse();
	}
	private void EnsureOnTop(GameObject panel)
	{
		if (panel == null) return;

		var canvas = panel.GetComponentInParent<Canvas>(true);
		if (canvas == null) return;

		var own = panel.GetComponent<Canvas>();
		if (own == null) own = panel.AddComponent<Canvas>();
		own.overrideSorting = true;
		own.sortingOrder = 500;

		if (panel.GetComponent<GraphicRaycaster>() == null)
			panel.AddComponent<GraphicRaycaster>();
	}
}
