using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public class AudioManager : MonoBehaviour
{
	public static AudioManager Instance { get; private set; }

	AudioSource enginePreviewSource;
	Coroutine enginePreviewRoutine;
	bool musicPausedByPreview;

	// ===== PlayerPrefs / Mixer keys =====
	private const string MasterKey = "MasterVolume";
	private const string MusicKey = "MusicVolume";
	private const string SFXKey = "SFXVolume";
	private const string EngineKey = "EngineVolume";

	private const string MasterParam = "MasterVolume";
	private const string MusicParam = "MusicVolume";
	private const string SFXParam = "SFXVolume";
	private const string EngineParam = "EngineVolume";

	private const bool UseBootstrap = true;

	private float masterVolume = 1f;
	private float musicVolume = 1f;
	private float sfxVolume = 1f;
	private float engineVolumeSetting = 1f;

	// ==============================
	// Data classes
	// ==============================
	[Serializable]
	public class Sound
	{
		public string id;
		public AudioClip clip;
		[Range(0f, 1f)] public float volume = 1f;
		public bool loop;
	}

	[Serializable]
	public class SceneMusic
	{
		public string sceneName;
		public string musicId;
	}

	[Serializable]
	public class GearSound
	{
		[Header("Engine (เสียงรถปกติ)")]
		public AudioClip engineClip;
		[Range(0f, 1f)] public float engineVolume = 1f;
		[Tooltip("1 = ตรงไฟล์ ช่วงแคบ เช่น 0.95 ถึง 1.1 จะไม่เพี้ยน")]
		public float enginePitchMin = 0.95f;
		public float enginePitchMax = 1.1f;
		[Tooltip("ความดังตอนรอบต่ำ เทียบกับรอบสูง (1 = ไม่เปลี่ยน) เช่น 0.75")]
		[Range(0.3f, 1f)] public float lowRevVolume = 0.75f;
	}

	// ==============================
	// Inspector
	// ==============================
	[Header("Sound Lists")]
	public List<Sound> musicList = new List<Sound>();
	public List<Sound> sfxList = new List<Sound>();

	[Header("Scene Music")]
	[SerializeField] private List<SceneMusic> sceneMusics = new List<SceneMusic>();

	[Header("Audio Mixer")]
	[SerializeField] private AudioMixer mainMixer;
	[SerializeField] private AudioMixerGroup musicGroup;
	[SerializeField] private AudioMixerGroup sfxGroup;
	[SerializeField] private AudioMixerGroup engineGroup;
	public AudioMixerGroup MusicOutputGroup => musicGroup;
	public AudioMixerGroup SFXOutputGroup => sfxGroup;
	public AudioMixerGroup EngineOutputGroup => engineGroup;

	[Header("Audio Sources")]
	[FormerlySerializedAs("bgmSource")]
	[SerializeField] private AudioSource musicAudioSource;
	[FormerlySerializedAs("sfxSource")]
	[SerializeField] private AudioSource sfxAudioSource;

	[Header("Options")]
	[Tooltip("ติ๊ก = ไม่เล่นเพลงตามซีนอัตโนมัติ (ใช้ตอนเทส)")]
	public bool disableAutoMusic = false;

	[Header("Car")]
	public CarController car;

	[Tooltip("ชุดเสียง default (ใช้เมื่อรถไม่มี Sound Set) ช่อง 0 = เกียร์ 1 ... ช่อง 5 = เกียร์ 6")]
	public GearSound[] gearSounds = new GearSound[6];

	[Header("Pitch behaviour")]
	[Tooltip("ความเร็วที่ pitch ไต่ขึ้น (หน่วย 0-1 ต่อวินาที)")]
	public float pitchRiseSpeed = 2.5f;
	[Tooltip("ความเร็วที่ pitch ตกลงหลังหมดเวลาคง pitch")]
	public float pitchFallSpeed = 0.8f;
	public bool holdPitchOnRelease = true;
	[Tooltip("ปล่อยปุ่มเร่งแล้วคง pitch ไว้กี่วินาที")]
	public float releaseHoldTime = 1f;
	[Tooltip("เวลา fade ตอนเปลี่ยนเกียร์ (วินาที) ตั้ง 0 = ตัดทันที")]
	public float gearFadeTime = 0.05f;
	[Tooltip("ตอนปล่อยปุ่มเร่ง pitch จะตกไปที่ (ระดับตามความเร็ว x ค่านี้)")]
	[Range(0.2f, 1f)] public float coastPitchFactor = 0.65f;
	[Tooltip("ความเร็วที่ pitch ตกตอนกดเบรค")]
	public float brakePitchFallSpeed = 3f;

	[Header("Release volume (ปล่อยปุ่มเร่ง)")]
	[Tooltip("ความดังของเสียงเครื่องตอนปล่อยปุ่มเร่ง (1 = ไม่เบาลง, 0 = เงียบ)")]
	[Range(0f, 1f)] public float releaseVolumeFactor = 0.6f;
	[Tooltip("ความเร็วที่ความดังเปลี่ยน (หน่วย 0-1 ต่อวินาที) ยิ่งมากยิ่งเปลี่ยนเร็ว")]
	public float releaseVolumeFadeSpeed = 3f;

	// ==============================
	// Internals
	// ==============================
	AudioSource[] engine;
	AudioSource repairingAudioSource;

	CarController boundCar;
	float pitchT;
	float heldTime;
	float volumeMul = 1f;
	bool warming;
	bool silenced;

	GearSound[] activeGearSounds;

	string currentSFXId;
	string currentRepairingSFXId;
	string currentMusicId;

	// ==============================
	// Unity lifecycle
	// ==============================
	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogWarning($"[AudioManager] Duplicate destroyed: {gameObject.name}");
			Destroy(gameObject);
			return;
		}

		Instance = this;
		DontDestroyOnLoad(gameObject);

		EnsurePersistentAudioSources();
		LoadVolume();
		BuildVehicleSources();
	}

	void OnEnable()
	{
		SceneManager.sceneLoaded += OnSceneLoaded;
	}

	void OnDisable()
	{
		SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	void Start()
	{
		if (Instance != this) return;

		ApplyOutputGroups();
		ApplyVolumes();

		BindCar();

		if (disableAutoMusic)
		{
			StopMusic();
			return;
		}
		HandleSceneMusic(SceneManager.GetActiveScene());
	}

	void OnDestroy()
	{
		UnbindCar();

		if (Instance == this)
			Instance = null;
	}

	// ==============================
	// per-frame (เสียงรถ)
	// ==============================
	void Update()
	{
		if (Instance != this || warming || engine == null || activeGearSounds == null) return;

		if (car == null)
		{
			if (!silenced) StopVehicleSounds();
			return;
		}
		float dt = Time.deltaTime;
		float speed = car.SpeedKph;
		bool tapping = car.TimeSinceLastPress <= car.idleDelay;
		bool audible = car.Started && (speed > 0.5f || tapping);

		float volGoal = tapping ? 1f : releaseVolumeFactor;
		volumeMul = Mathf.MoveTowards(volumeMul, volGoal, releaseVolumeFadeSpeed * dt);

		float target = TargetRatio();
		if (tapping)
		{
			heldTime = 0f;
			pitchT = Mathf.MoveTowards(pitchT, target, pitchRiseSpeed * dt);
		}
		else if (car.BrakeHeld)
		{
			pitchT = Mathf.MoveTowards(pitchT, Mathf.Min(target, pitchT), brakePitchFallSpeed * dt);
		}
		else
		{
			heldTime += dt;
			if (!holdPitchOnRelease || heldTime > releaseHoldTime)
			{
				float goal = Mathf.Min(target * coastPitchFactor, pitchT);
				pitchT = Mathf.MoveTowards(pitchT, goal, pitchFallSpeed * dt);
			}
		}

		int g = car.GearIndex;
		for (int i = 0; i < engine.Length && i < activeGearSounds.Length; i++)
		{
			var gs = activeGearSounds[i];
			if (gs == null) continue;
			bool active = audible && i == g;

			float revVol = Mathf.Lerp(gs.lowRevVolume, 1f, pitchT);

			UpdateSource(engine[i], active, gs.engineVolume * volumeMul * revVol, gs.enginePitchMin, gs.enginePitchMax, dt);
		}
	}

	void UpdateSource(AudioSource s, bool active, float vol, float pMin, float pMax, float dt)
	{
		if (s == null) return;
		if (active && !s.isPlaying) s.Play();

		float tv = active ? vol : 0f;
		float step = gearFadeTime <= 0.0001f ? 1f : dt / gearFadeTime;
		s.volume = Mathf.MoveTowards(s.volume, tv, step);

		if (active) s.pitch = Mathf.Lerp(pMin, pMax, pitchT);
		if (!active && s.isPlaying && s.volume <= 0.001f) s.Stop();
	}

	IEnumerator WarmUp()
	{
		if (engine == null) yield break;

		var e = engine;

		warming = true;
		foreach (var s in e) if (s != null) s.Play();  
		yield return null;
		foreach (var s in e) if (s != null) s.Stop();
		warming = false;
	}

	// ==============================
	// Volume (Mixer + PlayerPrefs)
	// ==============================
	private void LoadVolume()
	{
		masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
		musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 1f));
		sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SFXKey, 1f));
		engineVolumeSetting = Mathf.Clamp01(PlayerPrefs.GetFloat(EngineKey, 1f));
	}

	private bool SetMixerVolume(string parameter, float value)
	{
		if (mainMixer == null) return false;

		float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
		bool success = mainMixer.SetFloat(parameter, db);

		if (!success)
			Debug.LogError($"AudioManager: SetFloat('{parameter}') ล้มเหลว ตรวจสอบ Exposed Parameters และ Mixer");

		return success;
	}

	private void ApplyVolumes()
	{
		if (mainMixer != null)
		{
			AudioListener.volume = 1f;
			SetMixerVolume(MasterParam, masterVolume);
			SetMixerVolume(MusicParam, musicVolume);
			SetMixerVolume(SFXParam, sfxVolume);
			SetMixerVolume(EngineParam, engineVolumeSetting);
		}
		else
		{
			AudioListener.volume = masterVolume;
		}
	}

	private void ApplyOutputGroups()
	{
		if (musicAudioSource != null && musicGroup != null)
			musicAudioSource.outputAudioMixerGroup = musicGroup;
		if (sfxAudioSource != null && sfxGroup != null)
			sfxAudioSource.outputAudioMixerGroup = sfxGroup;
		if (repairingAudioSource != null && sfxGroup != null)
			repairingAudioSource.outputAudioMixerGroup = sfxGroup;
		var eg = engineGroup != null ? engineGroup : sfxGroup;
		if (enginePreviewSource != null && eg != null)
			enginePreviewSource.outputAudioMixerGroup = eg;
	}

	public void SetMasterVolume(float value)
	{
		masterVolume = Mathf.Clamp01(value);
		if (mainMixer != null) SetMixerVolume(MasterParam, masterVolume);
		else AudioListener.volume = masterVolume;

		PlayerPrefs.SetFloat(MasterKey, masterVolume);
		PlayerPrefs.Save();
	}

	public float GetMasterVolume() => masterVolume;

	public void SetMusicVolume(float value)
	{
		musicVolume = Mathf.Clamp01(value);
		SetMixerVolume(MusicParam, musicVolume);

		PlayerPrefs.SetFloat(MusicKey, musicVolume);
		PlayerPrefs.Save();
	}

	public float GetMusicVolume() => musicVolume;

	public void SetSFXVolume(float value)
	{
		sfxVolume = Mathf.Clamp01(value);
		SetMixerVolume(SFXParam, sfxVolume);

		PlayerPrefs.SetFloat(SFXKey, sfxVolume);
		PlayerPrefs.Save();
	}

	public float GetSFXVolume() => sfxVolume;

	public void SetEngineVolume(float value)
	{
		engineVolumeSetting = Mathf.Clamp01(value);
		SetMixerVolume(EngineParam, engineVolumeSetting);

		PlayerPrefs.SetFloat(EngineKey, engineVolumeSetting);
		PlayerPrefs.Save();
	}

	public float GetEngineVolume() => engineVolumeSetting;

	// ==============================
	// Audio sources
	// ==============================
	private void EnsurePersistentAudioSources()
	{
		AudioSource[] sources = GetComponents<AudioSource>();
		int idx = 0;

		if (musicAudioSource == null)
			musicAudioSource = idx < sources.Length ? sources[idx++] : gameObject.AddComponent<AudioSource>();

		if (sfxAudioSource == null)
		{
			while (idx < sources.Length && sources[idx] == musicAudioSource) idx++;
			sfxAudioSource = idx < sources.Length ? sources[idx++] : gameObject.AddComponent<AudioSource>();
		}

		musicAudioSource.playOnAwake = false;
		musicAudioSource.spatialBlend = 0f;

		sfxAudioSource.playOnAwake = false;
		sfxAudioSource.spatialBlend = 0f;
		sfxAudioSource.loop = false;

		repairingAudioSource = gameObject.AddComponent<AudioSource>();
		repairingAudioSource.playOnAwake = false;
		repairingAudioSource.spatialBlend = 0f;
		repairingAudioSource.loop = true;

		enginePreviewSource = gameObject.AddComponent<AudioSource>();
		enginePreviewSource.playOnAwake = false;
		enginePreviewSource.spatialBlend = 0f;
		enginePreviewSource.loop = true;

		ApplyOutputGroups();
	}

	private void BuildVehicleSources()
	{
		BuildSourcesFor(gearSounds);
	}

	private void BuildSourcesFor(GearSound[] sounds)
	{
		DestroyVehicleSources();

		int n = sounds != null ? sounds.Length : 0;
		engine = new AudioSource[n];

		for (int i = 0; i < n; i++)
		{
			if (sounds[i] == null) continue;
			engine[i] = MakeSource("Engine G" + (i + 1), sounds[i].engineClip, true);
		}

		activeGearSounds = sounds;
	}

	private void DestroyVehicleSources()
	{
		if (engine != null)
			foreach (var s in engine) if (s != null) Destroy(s.gameObject);
		engine = null;
	}

	AudioSource MakeSource(string name, AudioClip clip, bool loop)
	{
		if (clip == null) return null;
		clip.LoadAudioData();
		var go = new GameObject(name);
		go.transform.SetParent(transform, false);
		var s = go.AddComponent<AudioSource>();
		s.clip = clip;
		s.loop = loop;
		s.playOnAwake = false;
		s.spatialBlend = 0f;
		s.volume = 0f;

		var grp = engineGroup != null ? engineGroup : sfxGroup;
		if (grp != null) s.outputAudioMixerGroup = grp;
		return s;
	}

	// ==============================
	// Music / SFX
	// ==============================
	private Sound FindMusic(string id)
	{
		if (string.IsNullOrWhiteSpace(id)) return null;
		return musicList.Find(s => s != null &&
			string.Equals(s.id?.Trim(), id.Trim(), StringComparison.OrdinalIgnoreCase));
	}

	private Sound FindSFX(string id)
	{
		if (string.IsNullOrWhiteSpace(id)) return null;
		return sfxList.Find(s => s != null &&
			string.Equals(s.id?.Trim(), id.Trim(), StringComparison.OrdinalIgnoreCase));
	}

	private void HandleSceneMusic(Scene scene)
	{
		SceneMusic match = sceneMusics.Find(m => m != null &&
			string.Equals(m.sceneName?.Trim(), scene.name, StringComparison.OrdinalIgnoreCase));

		if (match == null || string.IsNullOrWhiteSpace(match.musicId))
		{
			if (currentMusicId != null) StopMusic();
			return;
		}

		PlayMusic(match.musicId.Trim());
	}

	public void PlayMusic(string id)
	{
		Sound sound = FindMusic(id);

		if (sound == null)
		{
			Debug.LogError($"[AudioManager] ไม่พบ Music ID '{id}' ใน musicList");
			StopMusic();
			return;
		}
		if (sound.clip == null)
		{
			Debug.LogError($"[AudioManager] Music ID '{id}' ไม่มี AudioClip");
			StopMusic();
			return;
		}
		if (musicAudioSource == null)
		{
			Debug.LogError("[AudioManager] Music AudioSource is missing!");
			return;
		}

		if (currentMusicId == sound.id && musicAudioSource.clip == sound.clip && musicAudioSource.isPlaying) return;

		musicAudioSource.Stop();

		currentMusicId = sound.id;
		if (musicGroup != null) musicAudioSource.outputAudioMixerGroup = musicGroup;
		musicAudioSource.clip = sound.clip;
		musicAudioSource.loop = sound.loop;
		musicAudioSource.volume = sound.volume;
		musicAudioSource.Play();
	}

	public void PlayBGM(AudioClip clip)
	{
		if (clip == null || musicAudioSource == null) return;

		if (musicAudioSource.clip == clip && musicAudioSource.isPlaying) return;

		currentMusicId = null;
		musicAudioSource.clip = clip;
		musicAudioSource.loop = true;
		musicAudioSource.volume = 1f;
		musicAudioSource.Play();
	}

	public void StopMusic()
	{
		if (musicAudioSource != null)
		{
			musicAudioSource.Stop();
			musicAudioSource.clip = null;
		}
		currentMusicId = null;
	}

	public void SilenceAllForSceneChange()
	{
		if (musicAudioSource != null)
		{
			musicAudioSource.volume = 0f;
			musicAudioSource.Stop();
			musicAudioSource.clip = null;
		}
		currentMusicId = null;

		if (repairingAudioSource != null)
		{
			repairingAudioSource.volume = 0f;
			repairingAudioSource.Stop();
			repairingAudioSource.clip = null;
		}
		currentRepairingSFXId = null;

		StopEnginePreview();
	}

	public void PauseMusic()
	{
		if (musicAudioSource != null && musicAudioSource.isPlaying)
			musicAudioSource.Pause();
	}

	public void ResumeMusic()
	{
		if (musicAudioSource != null && musicAudioSource.clip != null)
			musicAudioSource.UnPause();
	}

	public void PlayEnginePreview(AudioClip clip, float pitchMin, float pitchMax, float duration = 3f)
	{
		if (clip == null || enginePreviewSource == null) return;
		StopEnginePreview();

		if (musicAudioSource != null && musicAudioSource.isPlaying)
		{
			musicAudioSource.Pause();
			musicPausedByPreview = true;
		}

		enginePreviewRoutine = StartCoroutine(EnginePreviewRoutine(clip, pitchMin, pitchMax, duration));
	}

	public void StopEnginePreview()
	{
		if (enginePreviewRoutine != null)
		{
			StopCoroutine(enginePreviewRoutine);
			enginePreviewRoutine = null;
		}
		if (enginePreviewSource != null) enginePreviewSource.Stop();

		if (musicPausedByPreview)
		{
			musicPausedByPreview = false;
			if (musicAudioSource != null && musicAudioSource.clip != null)
				musicAudioSource.UnPause();
		}
	}

	IEnumerator EnginePreviewRoutine(AudioClip clip, float pMin, float pMax, float sweepSeconds)
	{
		var s = enginePreviewSource;
		s.clip = clip;
		s.loop = false;
		s.pitch = pMin;
		s.volume = 1f;
		s.time = 0f;
		s.Play();

		const float fade = 0.4f;
		float sweep = Mathf.Max(0.5f, Mathf.Min(sweepSeconds, clip.length));

		while (s.isPlaying)
		{
			float k = Mathf.Clamp01(s.time / sweep);
			s.pitch = Mathf.Lerp(pMin, pMax, k);

			float left = clip.length - s.time;
			s.volume = left < fade ? Mathf.Clamp01(left / fade) : 1f;
			yield return null;
		}

		enginePreviewRoutine = null;
		StopEnginePreview();
	}

	public IEnumerator FadeOutMusic(float duration)
	{
		if (musicAudioSource == null) yield break;

		float startVolume = musicAudioSource.volume;
		float timer = 0f;

		while (timer < duration)
		{
			timer += Time.unscaledDeltaTime;
			musicAudioSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(timer / duration));
			yield return null;
		}

		musicAudioSource.volume = 0f;
		StopMusic();
		musicAudioSource.volume = startVolume;
	}

	public void PlaySFX(string id)
	{
		if (sfxAudioSource == null)
		{
			Debug.LogError("[AudioManager] SFX AudioSource is NULL!");
			return;
		}

		Sound sound = FindSFX(id);
		if (sound == null || sound.clip == null)
		{
			//Debug.LogWarning($"[AudioManager] SFX not found: {id}");
			return;
		}

		if (sfxGroup != null) sfxAudioSource.outputAudioMixerGroup = sfxGroup;
		sfxAudioSource.loop = false;
		sfxAudioSource.PlayOneShot(sound.clip, Mathf.Clamp01(sound.volume));
	}

	public void PlaySFX(AudioClip clip)
	{
		if (clip == null || sfxAudioSource == null) return;
		sfxAudioSource.PlayOneShot(clip);
	}

	public void PlaySFXAtPoint(string id, Vector3 position)
	{
		Sound sound = FindSFX(id);
		if (sound == null || sound.clip == null)
		{
			Debug.LogWarning("ไม่พบ SFX ID: " + id);
			return;
		}

		GameObject obj = new GameObject("SFX_" + id);
		obj.transform.position = position;

		AudioSource source = obj.AddComponent<AudioSource>();
		source.clip = sound.clip;
		source.volume = sound.volume;
		source.spatialBlend = 1f;
		if (sfxGroup != null) source.outputAudioMixerGroup = sfxGroup;
		source.Play();

		Destroy(obj, sound.clip.length + 0.1f);
	}

	public void PlayLoopSFX(string id)
	{
		if (repairingAudioSource == null)
		{
			Debug.LogError("[AudioManager] Repairing AudioSource is NULL!");
			return;
		}

		Sound sound = FindSFX(id);
		if (sound == null || sound.clip == null)
		{
			Debug.LogWarning("[AudioManager] SFX not found: " + id);
			return;
		}

		if (currentRepairingSFXId == sound.id && repairingAudioSource.isPlaying)
			return;

		repairingAudioSource.Stop();

		currentRepairingSFXId = sound.id;
		repairingAudioSource.clip = sound.clip;
		repairingAudioSource.volume = sound.volume;
		repairingAudioSource.loop = true;
		if (sfxGroup != null) repairingAudioSource.outputAudioMixerGroup = sfxGroup;
		repairingAudioSource.Play();
	}

	public void StopSFX(string id)
	{
		if (string.Equals(currentRepairingSFXId, id, StringComparison.OrdinalIgnoreCase))
		{
			if (repairingAudioSource != null)
			{
				repairingAudioSource.Stop();
				repairingAudioSource.clip = null;
				repairingAudioSource.loop = false;
			}
			currentRepairingSFXId = null;
		}

		if (string.Equals(currentSFXId, id, StringComparison.OrdinalIgnoreCase))
		{
			if (sfxAudioSource != null)
			{
				sfxAudioSource.Stop();
				sfxAudioSource.clip = null;
				sfxAudioSource.loop = false;
			}
			currentSFXId = null;
		}
	}

	// ==============================
	// Scene change
	// ==============================
	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (Instance != this) return;
		StartCoroutine(RefreshAfterSceneLoad(scene));
	}

	private IEnumerator RefreshAfterSceneLoad(Scene scene)
	{
		yield return null;

		if (Instance != this) yield break;

		ApplyOutputGroups();
		ApplyVolumes();
		BindCar();

		if (disableAutoMusic)
		{
			StopMusic();
			yield break;
		}
		HandleSceneMusic(scene);
	}

	// ==============================
	// Car binding
	// ==============================

	public void RefreshCar()
	{
		BindCar();
	}

	private void BindCar()
	{
		CarController newCar = null;

		GameObject go = GameObject.FindGameObjectWithTag("Player");
		if (go != null) newCar = go.GetComponent<CarController>();

		if (newCar == null && car != null) newCar = car;

		if (newCar == boundCar && newCar != null) return;

		UnbindCar();

		car = newCar;
		boundCar = newCar;

		if (car != null)
		{
			ApplySoundSet(car.soundSet);
			car.GearChanged += OnGearChanged;
			silenced = false;
		}
		else
		{
			StopVehicleSounds();
		}

		pitchT = 0f;
		heldTime = 0f;
	}

	private void UnbindCar()
	{
		if (boundCar != null)
			boundCar.GearChanged -= OnGearChanged;
		boundCar = null;
	}

	private void ApplySoundSet(CarSoundSet set)
	{
		if (set != null)
			BuildSourcesFor(set.gearSounds);
		else
			BuildSourcesFor(gearSounds);  

		StartCoroutine(WarmUp());
	}

	public void StopVehicleSounds()
	{
		if (engine != null)
			foreach (var s in engine)
				if (s != null) { s.volume = 0f; s.Stop(); }
		silenced = true;
	}

	public void DetachCar()
	{
		StopVehicleSounds();
		UnbindCar();
		car = null;
		pitchT = 0f;
		heldTime = 0f;
	}

	// ==============================
	// events จากรถ
	// ==============================
	void OnGearChanged(int oldGear, int newGear)
	{
		pitchT = TargetRatio();
		if (newGear > oldGear) heldTime = 0f;
		else heldTime = releaseHoldTime + 1f;
	}

	float TargetRatio()
	{
		return Mathf.Clamp01(car.SpeedKph / Mathf.Max(1f, car.CurrentGear.maxSpeedKph));
	}

	// ==============================
	// Bootstrap
	// ==============================
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void BootstrapAudioManager()
	{
		if (!UseBootstrap || Instance != null) return;

		AudioManager prefab = Resources.Load<AudioManager>("AudioManager");
		if (prefab == null)
		{
			Debug.LogError("[AudioManager] ไม่พบ Prefab ที่ Assets/Resources/AudioManager.prefab");
			return;
		}

		Instantiate(prefab);
	}
}