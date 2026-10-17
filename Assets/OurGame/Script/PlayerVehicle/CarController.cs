using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// รถผู้เล่น
///  - RPM = ความถี่การกดจริง: กด 37 ครั้ง/วินาที = 2220 RPM ทันที
///    พอปล่อยปุ่ม RPM จะค่อยๆ ลดลงพร้อมความเร็ว จนถึง 0 เมื่อ KPH = 0
///  - ความเร็ว: กด 1 ครั้งเพิ่ม kphPerPress x accelMultiplier ของเกียร์นั้น, ปล่อยปุ่ม = ค่อยๆ ลด
///  - รถเลี้ยวเอง (pure pursuit ไล่เส้นที่วาด) ตลอดทั้งในและนอกโซน
///  - นอกโซน: ASD+JKL เร่ง, Space ขึ้นเกียร์ (ปล่อยปุ่มแล้ว = ลดเกียร์)
///  - ในโซน (CornerZone): JKL เร่ง, S เบรค (กดค้างตอนหยุดนิ่ง = ถอยหลัง), Space ลดเกียร์
///  - เร็วเกิน grip => เลี้ยวไม่ทัน หลุดออกนอกเส้นแต่ยังพยายามกลับเข้าเส้น
///  - รถเกาะพื้นได้ทุกที่ (raycast ลงพื้น ไม่มีขอบสนาม/ไม่มีการชน)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
	[System.Serializable]
	public class Gear
	{
		[Tooltip("ความเร็วสูงสุดของเกียร์นี้ (kph)")]
		public float maxSpeedKph = 100f;
		[Tooltip("ตัวคูณอัตราเร่งต่อการกด 1 ครั้ง (kph ที่ได้ = kphPerPress x ค่านี้)")]
		public float accelMultiplier = 10f;
		public Gear() { }
		public Gear(float max, float acc) { maxSpeedKph = max; accelMultiplier = acc; }
	}

	[Header("References")]
	public PathDrawer path;

	[Header("Gears (6 speed)")]
	public Gear[] gears =
	{
		new Gear(70f, 20f),
		new Gear(120f, 14f),
		new Gear(170f, 10f),
		new Gear(220f, 8f),
		new Gear(270f, 6f),
		new Gear(320f, 5f),
	};

	[Header("RPM (วัดจากความถี่การกด)")]
	[Tooltip("RPM = จำนวนครั้งที่กดต่อวินาที x ค่านี้ (60 => 37 ครั้ง/วิ = 2220 RPM)")]
	public float rpmPerTapPerSecond = 60f;
	[Tooltip("ช่วงเวลา (วินาที) ที่ใช้นับความถี่การกด - สั้น = ตอบสนองไว, ยาว = นิ่งกว่า")]
	public float rpmWindow = 0.4f;
	[Tooltip("ไม่กดนานกว่านี้ (วินาที) ถือว่าปล่อยปุ่มแล้ว -> RPM เริ่มลดตามความเร็ว")]
	public float rpmReleaseGap = 0.25f;

	[Header("Speed / Tap throttle")]
	[Tooltip("kph ที่ได้ต่อการกด 1 ครั้ง (ก่อนคูณ accelMultiplier)")]
	public float kphPerPress = 0.0833f;
	public float zoneAccelMultiplier = 2f;
	[Tooltip("ไม่กดนานกว่านี้ (วินาที) ถึงเริ่มลดความเร็ว")]
	public float idleDelay = 0.2f;
	[Tooltip("kph/s ที่ลดเมื่อปล่อยปุ่ม")]
	public float coastDecelKph = 6f;
	[Tooltip("kph/s เพิ่มเติมต่อทุก 1 kph (แรงต้านลม)")]
	public float dragDecelPerKph = 0.05f;
	[Tooltip("kph/s ตอนกดเบรค S (ในโซน)")]
	public float brakeDecelKph = 140f;
	[Tooltip("kph/s ที่ลดเมื่อความเร็วเกินเพดานเกียร์ (เช่นหลังลดเกียร์)")]
	public float overRevDecelKph = 60f;
	public float shiftCooldown = 0.12f;
	[Tooltip("นอกโซน: ปล่อยปุ่มเร่งนานกว่านี้ (วินาที) Space จะกลายเป็นลดเกียร์")]
	public float downshiftIdleTime = 0.35f;

	[Header("Reverse (ในโซน: หยุดนิ่ง + กด S ค้าง)")]
	public float reverseAccelKph = 12f;
	public float maxReverseKph = 25f;
	public float reverseDecelKph = 40f;

	[Header("Shift light thresholds (ความเร็ว / เพดานเกียร์)")]
	[Range(0, 1)] public float stage1Ratio = 0.55f;   // จุดแรกเริ่มติด
	[Range(0, 1)] public float stage2Ratio = 0.75f;
	[Range(0, 1)] public float stage3Ratio = 0.90f;
	[Range(0, 1)] public float stage4Ratio = 0.95f;
	[Range(0, 1)] public float shiftNowRatio = 0.985f; // ! SHIFT !

	[Header("Model")]
	[Tooltip("หมุนโมเดลรถเพื่อให้หัวรถหันไปทางที่วิ่ง (ถ้าโมเดลหันหลังให้ใช้ 180)")]
	public float modelYawOffset = 180f;

	[Header("Grip")]
	[Tooltip("1 world unit = กี่เมตร (ใช้ 1 ถ้าสเกลฉาก = เมตร)")]
	public float metersToWorld = 1f;

	[Tooltip("ตัวคูณความเร็วที่เห็นในฉาก (ไม่กระทบเลข KPH/RPM) - 100 kph ที่ดูเหมือน 60 = ประมาณ 1.7")]
	public float visualSpeedScale = 1.2f;
	[Tooltip("แรงเกาะด้านข้างสูงสุด (unit/s^2) - ยิ่งน้อยยิ่งหลุดโค้งง่าย")]
	public float lateralGrip = 28f;
	public float maxYawRateDeg = 140f;
	[Tooltip("kph/s สูงสุดที่เสียไปตอนหลุดโค้ง (understeer scrub)")]
	public float slideScrubKph = 25f;

	[Header("Auto path follow (ทำงานตลอด)")]
	public float lookAheadMin = 6f;
	public float lookAheadTime = 0.5f;
	public int searchWindow = 20;
	public float relockDistance = 25f;

	[Header("Ground (drive anywhere)")]
	public LayerMask groundMask = ~0;
	public float groundOffset = 0f;
	public float groundRayHeight = 20f;
	public float groundRayLength = 80f;
	public float groundSnapSpeed = 25f;
	public bool alignToGround = true;
	public float alignSpeed = 12f;

	// ---------- public state ----------
	public float SpeedKph => speed;
	public float ReverseKph => reverseSpeed;
	public bool IsReversing => reverseSpeed > 0.5f;
	public int Rpm => Mathf.RoundToInt(tapRpm);
	public bool InZone => zones.Count > 0;
	public int GearNumber => gearIndex + 1;
	public string GearLabel => IsReversing ? "R" : (SpeedKph < 0.5f ? "N" : GearNumber.ToString());
	public Vector3 HeadingForward => Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
	public Gear CurrentGear => gears[Mathf.Clamp(gearIndex, 0, gears.Length - 1)];
	public bool IsTopGear => gearIndex >= gears.Length - 1;
	/// <summary>ความเร็ว / เพดานของเกียร์ปัจจุบัน (0..1+)</summary>
	public float GearRatio => SpeedKph / Mathf.Max(1f, CurrentGear.maxSpeedKph);

	// ---------- สำหรับ AudioManager ----------
	public int GearIndex => gearIndex;                       // 0-based
	public bool Started => started;
	bool shiftLocked = true;
	public bool BrakeHeld { get; private set; }
	public float TimeSinceLastPress => Time.time - lastPressTime;
	public event System.Action Accelerated;                  // ยิงทุกครั้งที่กดเร่ง
	public event System.Action BrakeStarted;                 // กด S เริ่มเบรค (ในโซน, ไม่ได้เร่งพร้อมกัน)
	public event System.Action<int, int> GearChanged;        // (เกียร์เก่า, เกียร์ใหม่) 0-based

	/// <summary>0 = ไม่มี, 1..3 = กำลังจะถึงเพดาน, 4 = ! SHIFT ! (เกียร์สุดท้ายไม่มีขั้น 4)</summary>
	public int ShiftStage
	{
		get
		{
			if (SpeedKph < 1f) return 0;
			float f = GearRatio;
			if (f >= shiftNowRatio && !IsTopGear) return 5;
			if (f >= stage4Ratio) return 4;
			if (f >= stage3Ratio) return 3;
			if (f >= stage2Ratio) return 2;
			if (f >= stage1Ratio) return 1;
			return 0;
		}
	}

	// ---------- internals ----------
	static readonly KeyCode[] accelKeys = { KeyCode.J, KeyCode.K, KeyCode.L };
	static readonly KeyCode[] steerKeys = { KeyCode.A, KeyCode.S, KeyCode.D }; // นอกโซน = ปุ่มเร่ง

	readonly HashSet<CornerZone> zones = new HashSet<CornerZone>();
	readonly RaycastHit[] hits = new RaycastHit[8];
	readonly Queue<float> pressTimes = new Queue<float>();

	float speed;        // kph (เดินหน้า)
	float reverseSpeed; // kph (ถอยหลัง)
	float tapRpm;       // RPM ที่แสดง
	float rpmRef;       // RPM ล่าสุดตอนยังกดอยู่
	float speedRef;     // ความเร็วล่าสุดตอนยังกดอยู่
	int gearIndex;
	float heading;
	int nearest;
	float lastPressTime = -10f;
	float nextShiftTime;
	bool started;
	Vector3 groundNormal = Vector3.up;

	void Awake()
	{
		var rb = GetComponent<Rigidbody>();
		rb.isKinematic = true;
		rb.useGravity = false;
		if (GetComponentInChildren<Collider>() == null) gameObject.AddComponent<BoxCollider>();
		heading = transform.eulerAngles.y - modelYawOffset;
	}

	void OnDisable() { zones.Clear(); }

	void OnTriggerEnter(Collider other)
	{
		var z = other.GetComponentInParent<CornerZone>();
		if (z != null) zones.Add(z);
	}

	void OnTriggerExit(Collider other)
	{
		var z = other.GetComponentInParent<CornerZone>();
		if (z != null) zones.Remove(z);
	}

	void Update()
	{
		if (path == null || !path.IsClosed) { started = false; return; }
		if (!started) StartRun();

		float dt = Time.deltaTime;
		HandleInput(dt);
		Drive(dt);
		SnapToGround(dt);
	}

	void StartRun()
	{
		Vector3 d = path.StartDirection;
		heading = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
		Vector3 p = path.StartPoint;
		p.y = transform.position.y;
		transform.position = p;
		transform.rotation = Quaternion.Euler(0f, heading + modelYawOffset, 0f);
		speed = 0f;
		reverseSpeed = 0f;
		tapRpm = 0f;
		rpmRef = 0f;
		speedRef = 0f;
		pressTimes.Clear();
		gearIndex = 0;
		shiftLocked = true;
		nearest = 0;
		started = true;
	}

	// ---------------- input: กดย้ำ / เกียร์ / เบรค / ถอย ----------------
	void HandleInput(float dt)
	{
		bool inZone = InZone;

		int presses = 0;
		for (int i = 0; i < accelKeys.Length; i++)
			if (Input.GetKeyDown(accelKeys[i])) presses++;
		if (!inZone) // นอกโซน ASD ก็เป็นปุ่มเร่ง
			for (int i = 0; i < steerKeys.Length; i++)
				if (Input.GetKeyDown(steerKeys[i])) presses++;

		Gear g = CurrentGear;
		float cap = g.maxSpeedKph;

		if (presses > 0)
		{
			lastPressTime = Time.time;
			for (int i = 0; i < presses; i++) pressTimes.Enqueue(Time.time);
			float add = presses * kphPerPress * g.accelMultiplier * (inZone ? zoneAccelMultiplier : 1f);
			speed = Mathf.Min(speed + add, Mathf.Max(cap, speed)); // ชนเพดานเกียร์ = ต้องขึ้นเกียร์
			reverseSpeed = 0f;                                      // กดเร่ง = เลิกถอย
			shiftLocked = false;                                    // เร่งแล้ว = ขึ้นเกียร์ได้
			Accelerated?.Invoke();
		}

		if (Time.time - lastPressTime > idleDelay)
			speed -= (coastDecelKph + dragDecelPerKph * speed) * dt;

		BrakeHeld = inZone && Input.GetKey(KeyCode.S);
		if (presses == 0 && inZone && Input.GetKeyDown(KeyCode.S) && speed > 0.5f)
			BrakeStarted?.Invoke();
		if (BrakeHeld)
			speed -= brakeDecelKph * dt;

		if (speed > cap)
			speed = Mathf.Max(cap, speed - overRevDecelKph * dt);

		speed = Mathf.Max(0f, speed);

		// ถอยหลัง: อยู่ในโซน + หยุดนิ่ง + กด S ค้าง
		bool wantReverse = BrakeHeld && speed < 0.5f && presses == 0;
		if (wantReverse)
			reverseSpeed = Mathf.Min(maxReverseKph, reverseSpeed + reverseAccelKph * dt);
		else if (reverseSpeed > 0f)
			reverseSpeed = Mathf.Max(0f, reverseSpeed - reverseDecelKph * dt);

		UpdateTapRpm();

		// จอดนิ่ง (N) -> กลับเกียร์ 1 อัตโนมัติ
		if (speed < 0.5f && gearIndex > 0) SetGear(0);

		// Space:
		//  - ในโซน = ลดเกียร์
		//  - นอกโซน = ขึ้นเกียร์ (ขณะกำลังกดย้ำ) / ลดเกียร์ (เมื่อปล่อยปุ่มเร่งไปแล้ว)
		//  - ถ้าชนเพดานเกียร์ (! SHIFT !) จะขึ้นเกียร์เสมอ
		if (Input.GetKeyDown(KeyCode.Space) && Time.time >= nextShiftTime)
		{
			bool released = Time.time - lastPressTime > downshiftIdleTime;
			bool down = shiftLocked || ((inZone || released) && ShiftStage < 5);
			SetGear(down ? gearIndex - 1 : gearIndex + 1);
		}
	}

	// ตอนกด: RPM = ความถี่กด x 60 (ขึ้นทันที)
	// ตอนปล่อย: RPM ลดตามความเร็ว (สัดส่วนเดียวกัน) จนเป็น 0 พร้อม KPH = 0
	void UpdateTapRpm()
	{
		float now = Time.time;
		while (pressTimes.Count > 0 && now - pressTimes.Peek() > rpmWindow) pressTimes.Dequeue();

		int n = pressTimes.Count;
		bool pressing = n >= 2 && (now - lastPressTime) <= rpmReleaseGap;

		if (pressing)
		{
			float span = Mathf.Max(0.02f, lastPressTime - pressTimes.Peek());
			float rate = (n - 1) / span;                 // ครั้ง/วินาที
			tapRpm = rate * rpmPerTapPerSecond;
			rpmRef = tapRpm;
			speedRef = Mathf.Max(speed, 0.01f);
		}
		else
		{
			if (speed < 0.05f || speedRef < 0.01f) { tapRpm = 0f; return; }
			tapRpm = rpmRef * Mathf.Clamp01(speed / speedRef);
		}
	}

	void SetGear(int index)
	{
		index = Mathf.Clamp(index, 0, gears.Length - 1);
		if (index == gearIndex) return;
		int old = gearIndex;
		if (index < gearIndex) shiftLocked = true;
		gearIndex = index;
		nextShiftTime = Time.time + shiftCooldown;
		GearChanged?.Invoke(old, index);
	}

	// ---------------- เลี้ยวเอง + เคลื่อนที่ ----------------
	void Drive(float dt)
	{
		float s = Mathf.Max(0.1f, visualSpeedScale);

		// ถอยหลัง: ถอยตรงๆ ตามทิศหัวรถ ไม่ไล่เส้น
		if (reverseSpeed > 0.01f)
		{
			float vr = reverseSpeed / 3.6f * metersToWorld * s;
			transform.position -= HeadingForward * (vr * dt);
			return;
		}

		float v = speed / 3.6f * metersToWorld;   // ความเร็วตามเลข KPH (ใช้คำนวณ grip)
		float vw = v * s;                          // ความเร็วที่เห็นในฉาก

		float yaw = ComputeAutoYaw(vw);

		// grip คิดจากเลข KPH แล้วคูณ s ให้ตรงกับความเร็วที่เห็น -> หลุดโค้งที่ KPH เท่าเดิม
		float maxYaw = s * Mathf.Min(maxYawRateDeg * Mathf.Deg2Rad, lateralGrip / Mathf.Max(v, 0.1f));
		float ratio = Mathf.Abs(yaw) / Mathf.Max(0.0001f, maxYaw);
		if (ratio > 1f)
			speed = Mathf.Max(0f, speed - slideScrubKph * Mathf.Clamp01(ratio - 1f) * dt);
		yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);

		heading += yaw * Mathf.Rad2Deg * dt;
		transform.position += HeadingForward * (vw * dt);
	}

	// Pure pursuit: คืนค่า yaw rate (rad/s) ที่ต้องใช้เพื่อไล่เข้าเส้น
	float ComputeAutoYaw(float v)
	{
		var pts = path.Points;
		int n = pts.Count;
		if (n < 3) return 0f;

		Vector3 pos = transform.position;
		nearest = FindNearest(pos);

		float L = lookAheadMin + v * lookAheadTime;
		int steps = Mathf.Max(1, Mathf.CeilToInt(L / Mathf.Max(0.1f, path.ActualSpacing)));
		Vector3 target = pts[(nearest + steps) % n];

		Vector3 to = target - pos;
		to.y = 0f;
		float dist = to.magnitude;
		if (dist < 0.01f) return 0f;

		float alpha = Vector3.SignedAngle(HeadingForward, to, Vector3.up); // + = ขวา
		alpha = Mathf.Clamp(alpha, -90f, 90f) * Mathf.Deg2Rad;
		float curvature = 2f * Mathf.Sin(alpha) / dist;
		return v * curvature;
	}

	int FindNearest(Vector3 p)
	{
		var pts = path.Points;
		int n = pts.Count;
		int best = nearest;
		float bestD = float.MaxValue;

		for (int o = -searchWindow; o <= searchWindow; o++)
		{
			int i = ((nearest + o) % n + n) % n;
			float d = FlatSqr(pts[i], p);
			if (d < bestD) { bestD = d; best = i; }
		}
		if (bestD <= relockDistance * relockDistance) return best;

		// หลุดไกลเกิน -> ค้นหาใหม่ทั้งเส้น
		bestD = float.MaxValue;
		for (int i = 0; i < n; i++)
		{
			float d = FlatSqr(pts[i], p);
			if (d < bestD) { bestD = d; best = i; }
		}
		return best;
	}

	static float FlatSqr(Vector3 a, Vector3 b)
	{
		float dx = a.x - b.x, dz = a.z - b.z;
		return dx * dx + dz * dz;
	}

	// ---------------- เกาะพื้นได้ทุกที่ ----------------
	void SnapToGround(float dt)
	{
		Vector3 origin = transform.position + Vector3.up * groundRayHeight;
		int c = Physics.RaycastNonAlloc(origin, Vector3.down, hits, groundRayHeight + groundRayLength,
										groundMask, QueryTriggerInteraction.Ignore);
		float best = float.MaxValue;
		bool found = false;
		float y = 0f;
		Vector3 normal = Vector3.up;
		for (int i = 0; i < c; i++)
		{
			if (hits[i].collider.transform.root == transform.root) continue; // ข้ามตัวรถเอง
			if (hits[i].distance < best)
			{
				best = hits[i].distance;
				y = hits[i].point.y;
				normal = hits[i].normal;
				found = true;
			}
		}

		if (found)
		{
			Vector3 pos = transform.position;
			pos.y = Mathf.Lerp(pos.y, y + groundOffset, 1f - Mathf.Exp(-groundSnapSpeed * dt));
			transform.position = pos;
			groundNormal = Vector3.Slerp(groundNormal, normal, 1f - Mathf.Exp(-alignSpeed * dt));
		}

		Quaternion yawRot = Quaternion.Euler(0f, heading + modelYawOffset, 0f);
		if (alignToGround)
			transform.rotation = Quaternion.FromToRotation(Vector3.up, groundNormal) * yawRot;
		else
			transform.rotation = yawRot;
	}
}