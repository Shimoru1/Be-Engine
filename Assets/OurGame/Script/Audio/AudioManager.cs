using System.Collections;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
	[System.Serializable]
	public class GearSound
	{
		[Header("Engine (เสียงรถปกติ)")]
		public AudioClip engineClip;
		[Range(0f, 1f)] public float engineVolume = 1f;
		public float enginePitchMin = 0.8f;
		public float enginePitchMax = 1.6f;

		[Header("Layer (เสียงซ้อน - ใช้กับเกียร์ 4,5,6)")]
		public AudioClip layerClip;
		[Range(0f, 1f)] public float layerVolume = 0.7f;
		public float layerPitchMin = 0.9f;
		public float layerPitchMax = 1.5f;
	}

	public CarController car;
	[Range(0f, 1f)] public float masterVolume = 1f;

	[Tooltip("ช่อง 0 = เกียร์ 1 ... ช่อง 5 = เกียร์ 6")]
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

	[Header("Brake")]
	public AudioClip brakeClip;
	[Range(0f, 1f)] public float brakeVolume = 1f;
	[Tooltip("ยิ่งมาก เสียงเบรคยิ่งเบาลงเร็วเมื่อความเร็วลด (1 = ลดเป็นเส้นตรง)")]
	public float brakeFadeCurve = 2f;
	[Tooltip("pitch ของเสียงเบรคตอนความเร็วใกล้ 0")]
	[Range(0.1f, 1f)] public float brakeMinPitch = 0.6f;
	[Tooltip("pitch ของเสียงเบรคตอนความเร็วสูงสุดนับจากเริ่มเบรค")]
	[Range(0.5f, 3f)] public float brakeMaxPitch = 1.3f;
	[Tooltip("วนลูปคลิปเบรคตอนกด S ค้าง")]
	public bool loopBrakeWhileHeld = true;
	[Tooltip("ความเร็วที่ pitch เสียงเบรคเคลื่อนเข้าหาค่าตามความเร็วรถ (pitch ต่อวินาที)")]
	public float brakePitchSpeed = 3f;
	[Tooltip("ความเร็วที่ความดังเสียงเบรคเพิ่มขึ้น (ความดัง 0-1 ต่อวินาที)")]
	public float brakeVolumeRiseSpeed = 6f;

	[Header("Brake release (เร่งระหว่างเบรค)")]
	[Tooltip("เวลาที่เสียงเบรคใช้จางหายตอนเร่ง (วินาที)")]
	public float brakeReleaseTime = 0.8f;

	AudioSource[] engine;
	AudioSource[] layer;
	AudioSource brake;
	float pitchT;
	float heldTime;
	float brakeRefSpeed = 1f;
	bool brakeFading;
	float volumeMul = 1f;
	bool layerAllowed;
	bool warming;

	void Awake()
	{
		int n = gearSounds != null ? gearSounds.Length : 0;
		engine = new AudioSource[n];
		layer = new AudioSource[n];
		for (int i = 0; i < n; i++)
		{
			if (gearSounds[i] == null) continue;
			engine[i] = MakeSource("Engine G" + (i + 1), gearSounds[i].engineClip, true);
			layer[i] = MakeSource("Layer G" + (i + 1), gearSounds[i].layerClip, true);
		}
		brake = MakeSource("Brake", brakeClip, false);
	}

	void Start()
	{
		StartCoroutine(WarmUp());
		if (car == null)
		{
			var go = GameObject.FindGameObjectWithTag("Player");
			if (go != null) car = go.GetComponent<CarController>();
		}
		if (car != null)
		{
			car.BrakeStarted += OnBrakeStarted;
			car.GearChanged += OnGearChanged;
		}
	}

	void OnDestroy()
	{
		if (car == null) return;
		car.BrakeStarted -= OnBrakeStarted;
		car.GearChanged -= OnGearChanged;
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
		return s;
	}

	// ---------------- events จากรถ ----------------
	void OnBrakeStarted()
	{
		if (brake == null) return;
		brakeFading = false;

		// เสียงเบรคยังเล่นอยู่ (รวมช่วงกำลังจาง) = เล่นต่อจาก pitch/ความดังปัจจุบัน ไม่เริ่มใหม่
		if (brake.isPlaying)
		{
			brake.loop = loopBrakeWhileHeld;
			return;
		}

		// เริ่มใหม่จริงๆ (เสียงจบไปแล้ว)
		brakeRefSpeed = Mathf.Max(1f, car.SpeedKph);
		brake.loop = loopBrakeWhileHeld;
		brake.time = 0f;
		brake.pitch = brakeMaxPitch;
		brake.volume = brakeVolume * masterVolume;
		brake.Play();
	}

	void OnGearChanged(int oldGear, int newGear)
	{
		layerAllowed = newGear > oldGear;
		if (newGear > oldGear)
		{
			heldTime = 0f;
			pitchT = TargetRatio();                 // ขึ้นเกียร์: เริ่มที่ระดับตามความเร็ว
		}
		else
		{
			// ลดเกียร์: pitch = ratio ของเกียร์ใหม่ทันที แล้วลดลงตามความเร็วต่อเลย (ไม่รอ hold)
			pitchT = TargetRatio();
			heldTime = releaseHoldTime + 1f;
		}
	}

	float TargetRatio()
	{
		return Mathf.Clamp01(car.SpeedKph / Mathf.Max(1f, car.CurrentGear.maxSpeedKph));
	}

	// ---------------- per-frame ----------------
	void Update()
	{
		if (car == null || warming) return;
		float dt = Time.deltaTime;
		float speed = car.SpeedKph;
		bool tapping = car.TimeSinceLastPress <= car.idleDelay;
		bool audible = car.Started && (speed > 0.5f || tapping);

		// ความดังเสียงเครื่อง: กดเร่ง = เต็ม / ปล่อย = เบาลงตาม releaseVolumeFactor
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

		// เสียงเบรค: pitch ตามความเร็วจริงต่อเนื่อง (เบรคลด / เร่งเพิ่ม) ไม่รีเซ็ต
		if (brake != null && brake.isPlaying)
		{
			if (speed < 0.5f)
			{
				brake.loop = false;
				brake.Stop();
			}
			else
			{
				// ความเร็วอ้างอิง = สูงสุดนับจากเริ่มเสียง (ขึ้นได้ตอนเร่ง ไม่ถูกรีเซ็ตตอนเบรคซ้ำ)
				brakeRefSpeed = Mathf.Max(brakeRefSpeed, speed);
				float r = Mathf.Clamp01(speed / brakeRefSpeed);

				float targetPitch = Mathf.Lerp(brakeMinPitch, brakeMaxPitch, r);
				brake.pitch = Mathf.MoveTowards(brake.pitch, targetPitch, brakePitchSpeed * dt);

				float full = brakeVolume * masterVolume;

				// เริ่มจางเมื่อเร่ง และค้างสถานะไว้จนกว่าจะกดเบรคใหม่
				if (tapping && !car.BrakeHeld) brakeFading = true;
				if (car.BrakeHeld) brakeFading = false;

				if (brakeFading)
				{
					brake.loop = false;
					float fadeStep = full / Mathf.Max(0.05f, brakeReleaseTime) * dt;
					brake.volume = Mathf.MoveTowards(brake.volume, 0f, fadeStep);
					if (brake.volume <= 0.001f)
					{
						brake.Stop();
						brakeFading = false;
					}
				}
				else
				{
					brake.loop = loopBrakeWhileHeld && car.BrakeHeld;
					float tv = full * Mathf.Pow(r, brakeFadeCurve);
					brake.volume = Mathf.MoveTowards(brake.volume, tv, brakeVolumeRiseSpeed * dt);
				}
			}
		}

		int g = car.GearIndex;
		for (int i = 0; i < engine.Length; i++)
		{
			var gs = gearSounds[i];
			if (gs == null) continue;
			bool active = audible && i == g;
			UpdateSource(engine[i], active, gs.engineVolume * volumeMul, gs.enginePitchMin, gs.enginePitchMax, dt);
			UpdateSource(layer[i], active && layerAllowed, gs.layerVolume, gs.layerPitchMin, gs.layerPitchMax, dt);
		}
	}

	void UpdateSource(AudioSource s, bool active, float vol, float pMin, float pMax, float dt)
	{
		if (s == null) return;
		if (active && !s.isPlaying) s.Play();

		float tv = active ? vol * masterVolume : 0f;
		float step = gearFadeTime <= 0.0001f ? 1f : dt / gearFadeTime;
		s.volume = Mathf.MoveTowards(s.volume, tv, step);

		if (active) s.pitch = Mathf.Lerp(pMin, pMax, pitchT);
		if (!active && s.isPlaying && s.volume <= 0.001f) s.Stop();
	}

	IEnumerator WarmUp()
	{
		warming = true;
		foreach (var s in engine) if (s != null) s.Play();
		foreach (var s in layer) if (s != null) s.Play();
		if (brake != null) brake.Play();      // volume ตั้งต้นเป็น 0 อยู่แล้ว
		yield return null;
		foreach (var s in engine) if (s != null) s.Stop();
		foreach (var s in layer) if (s != null) s.Stop();
		if (brake != null) brake.Stop();
		warming = false;
	}
}