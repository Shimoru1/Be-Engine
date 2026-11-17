using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	public enum GameState
	{
		Starting,   // นับถอยหลัง / intro
		Playing,
		Paused,
		Win,
		GameOver
	}

	[Header("Start")]
	[Tooltip("เวลาช่วง Starting ก่อนเริ่มเล่นจริง (วินาที) 0 = เริ่มเล่นทันที")]
	public float startDelay = 3f;

	[Header("Test")]
	[Tooltip("ติ๊ก = โหมดทดสอบ (AudioManager เดิมของโปรเจกต์อื่นเคยเช็คค่านี้)")]
	public bool isTestMode = false;
	public bool IsTestMode => isTestMode;

	public GameState State { get; private set; } = GameState.Starting;

	// สคริปต์อื่นสมัครฟังได้
	public event Action<GameState> StateChanged;
	public event Action GameStarted;
	public event Action Won;
	public event Action Lost;

	private GameState stateBeforePause = GameState.Playing;

	// ==============================
	// Lifecycle
	// ==============================
	private void Awake()
	{
		// GameManager อยู่ประจำซีนเกม ไม่ใช้ DontDestroyOnLoad
		// (ถ้าอยากให้ข้ามซีน ให้เปิดบรรทัดข้างล่างและวางไว้ที่ root)
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		// DontDestroyOnLoad(gameObject);
	}

	private void Start()
	{
		Time.timeScale = 1f;
		StartCoroutine(StartRoutine());
	}

	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
	}

	private IEnumerator StartRoutine()
	{
		SetState(GameState.Starting);

		if (startDelay > 0f)
			yield return new WaitForSecondsRealtime(startDelay);

		if (State == GameState.Starting)
		{
			SetState(GameState.Playing);
			GameStarted?.Invoke();
		}
	}

	// ==============================
	// State queries (PauseMenu ใช้)
	// ==============================
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
		if (State != GameState.Playing && State != GameState.Starting)
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
		Time.timeScale = 1f;
		AudioListener.pause = false;
	}

	// ==============================
	// Internal
	// ==============================
	private void SetState(GameState newState)
	{
		if (State == newState) return;
		State = newState;
		StateChanged?.Invoke(State);
	}
}