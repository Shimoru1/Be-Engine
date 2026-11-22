using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	public enum GameState
	{
		Selecting,  
		Starting,   
		Playing,
		Paused,
		Win,
		GameOver
	}

	[Header("Start")]
	[Tooltip("เวลานับถอยหลังก่อนเริ่มเล่นจริง (วินาที) 0 = เริ่มทันที")]
	public float startDelay = 3f;
	[Tooltip("ติ๊ก = รอหน้าเลือกรถ (CarSelectUI) เรียก BeginCountdown / ไม่ติ๊ก = นับถอยหลังเองทันที")]
	public bool waitForCarSelect = true;

	[Header("Test")]
	[Tooltip("ติ๊ก = โหมดทดสอบ")]
	public bool isTestMode = false;
	public bool IsTestMode => isTestMode;

	public GameState State { get; private set; } = GameState.Selecting;
	public float CountdownRemaining { get; private set; }
	public float RaceTime { get; private set; }

	public event Action<GameState> StateChanged;
	public event Action<int> CountdownTick;  
	public event Action GameStarted;
	public event Action Won;
	public event Action Lost;

	private GameState stateBeforePause = GameState.Playing;
	private Coroutine countdownRoutine;

	// ==============================
	// Lifecycle
	// ==============================
	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
	}

	private void Start()
	{
		Time.timeScale = 1f;

		if (waitForCarSelect) SetState(GameState.Selecting);
		else BeginCountdown();
	}

	private void Update()                              
	{
		if (State == GameState.Playing) RaceTime += Time.deltaTime;   
	}

	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}

	// ==============================
	// Countdown
	// ==============================
	public void BeginCountdown()
	{
		if (countdownRoutine != null) return;
		if (IsWin() || IsGameOver()) return;
		countdownRoutine = StartCoroutine(CountdownRoutine());
	}

	private IEnumerator CountdownRoutine()
	{
		SetState(GameState.Starting);

		float remaining = Mathf.Max(0f, startDelay);
		int lastShown = -1;

		while (remaining > 0f)
		{
			CountdownRemaining = remaining;

			int shown = Mathf.CeilToInt(remaining);
			if (shown != lastShown)
			{
				lastShown = shown;
				CountdownTick?.Invoke(shown);
			}

			remaining -= Time.deltaTime; 
			yield return null;
		}

		CountdownRemaining = 0f;
		CountdownTick?.Invoke(0);

		if (State == GameState.Starting)
		{
			SetState(GameState.Playing);
			GameStarted?.Invoke();
		}

		countdownRoutine = null;
	}

	// ==============================
	// State queries
	// ==============================
	public bool IsSelecting() => State == GameState.Selecting;
	public bool IsStarting() => State == GameState.Starting;
	public bool IsPlaying() => State == GameState.Playing;
	public bool IsPaused() => State == GameState.Paused;
	public bool IsWin() => State == GameState.Win;
	public bool IsGameOver() => State == GameState.GameOver;

	// ==============================
	// Pause / Resume
	// ==============================
	public void PauseGame()
	{
		if (State != GameState.Playing && State != GameState.Starting && State != GameState.Selecting)
			return;

		stateBeforePause = State;
		SetState(GameState.Paused);
		Time.timeScale = 0f;
	}

	public void ResumeGame()
	{
		if (State != GameState.Paused)
			return;

		Time.timeScale = 1f;
		SetState(stateBeforePause);
	}

	// ==============================
	// End game
	// ==============================
	public void WinGame()
	{
		if (IsWin() || IsGameOver()) return;

		Time.timeScale = 1f;
		SetState(GameState.Win);
		Won?.Invoke();
	}

	public void GameOver()
	{
		if (IsWin() || IsGameOver()) return;

		Time.timeScale = 1f;
		SetState(GameState.GameOver);
		Lost?.Invoke();
	}

	// ==============================
	// Scene helpers
	// ==============================
	public void RestartGame()
	{
		ResetForSceneChange();
		SceneManager.LoadScene(SceneManager.GetActiveScene().name);
	}

	public void LoadMainMenu(string menuSceneName = "MainMenu")
	{
		ResetForSceneChange();
		SceneManager.LoadScene(menuSceneName);
	}

	private void ResetForSceneChange()
	{
		if (AudioManager.Instance != null)
		{
			AudioManager.Instance.DetachCar();
			AudioManager.Instance.SilenceAllForSceneChange();
		}
		Time.timeScale = 1f;
		AudioListener.pause = false;
	}

	private void SetState(GameState newState)
	{
		if (State == newState) return;
		State = newState;
		StateChanged?.Invoke(State);
	}
}