using UnityEngine;

[RequireComponent(typeof(Camera))]
public class TopDownCamera : MonoBehaviour
{
	public Transform target;

	[Header("View")]
	[Tooltip("90 = มองตรงลงมา (Top Down), ยิ่งน้อยยิ่งมองจากข้างหลังรถ")]
	[Range(10f, 90f)] public float pitch = 50f;
	[Tooltip("ระยะกล้องห่างจากจุดโฟกัส")]
	public float distance = 35f;
	[Tooltip("ยกจุดโฟกัสขึ้นจากตัวรถ")]
	public Vector3 focusOffset = new Vector3(0f, 1f, 0f);

	[Header("Speed effect")]
	public float maxSpeedForEffect = 320f;
	[Tooltip("ระยะที่เพิ่มขึ้นเมื่อรถเร็วสุด (ซูมออก)")]
	public float speedExtraDistance = 15f;
	[Tooltip("องศา pitch ที่เปลี่ยนเมื่อรถเร็วสุด (บวก = ก้มลงมากขึ้น)")]
	public float speedPitchChange = 0f;
	public float distanceSmoothTime = 0.6f;
	public float pitchSmoothTime = 0.4f;

	[Header("Follow")]
	public float positionSmoothTime = 0.25f;
	[Tooltip("โฟกัสนำหน้ารถไปข้างหน้ากี่วินาที (ใกล้เคียง positionSmoothTime = รถอยู่กลางจอตอนวิ่งเร็ว)")]
	public float lookAheadSeconds = 0.25f;

	[Header("Rotation")]
	[Tooltip("เปิด = กล้องหมุนตามทิศรถ (มองจากข้างหลัง), ปิด = ล็อกทิศ")]
	public bool rotateWithCar = true;
	public float rotationSmoothTime = 0.4f;
	public float fixedYaw = 0f;

	CarController car;
	Vector3 focusPos, focusVel;
	float curDistance, distVel;
	float curPitch, pitchVel;
	float curYaw, yawVel;
	bool initialized;

	void OnEnable()
	{
		CarSelectUI.PlayerCarSpawned += OnPlayerCarSpawned;

		if (CarSelectUI.SpawnedCar != null) OnPlayerCarSpawned(CarSelectUI.SpawnedCar);
	}

	void OnDisable()
	{
		CarSelectUI.PlayerCarSpawned -= OnPlayerCarSpawned;
	}

	void OnPlayerCarSpawned(CarController c)
	{
		if (c == null) return;
		SetTarget(c.transform);
	}

	public void SetTarget(Transform t)
	{
		target = t;
		car = t != null ? t.GetComponent<CarController>() : null;
		initialized = false; 
		focusVel = Vector3.zero;
		distVel = pitchVel = yawVel = 0f;
	}

	void LateUpdate()
	{
		if (target == null)
		{
			var go = GameObject.FindGameObjectWithTag("Player");
			if (go == null) return;    
			SetTarget(go.transform);
		}
		if (car == null) car = target.GetComponent<CarController>();

		float speedKph = car != null ? car.SpeedKph : 0f;
		float speedT = Mathf.Clamp01(speedKph / Mathf.Max(1f, maxSpeedForEffect));
		Vector3 flatFwd = car != null ? car.HeadingForward : Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;

		float targetYaw = rotateWithCar ? Mathf.Atan2(flatFwd.x, flatFwd.z) * Mathf.Rad2Deg : fixedYaw;
		float targetPitch = Mathf.Clamp(pitch + speedPitchChange * speedT, 10f, 90f);
		float targetDist = distance + speedExtraDistance * speedT;
		Vector3 targetFocus = target.position + focusOffset + flatFwd * (speedKph / 3.6f * lookAheadSeconds);

		if (!initialized)
		{
			focusPos = targetFocus;
			curDistance = targetDist;
			curPitch = targetPitch;
			curYaw = targetYaw;
			initialized = true;
		}

		focusPos = Vector3.SmoothDamp(focusPos, targetFocus, ref focusVel, positionSmoothTime);
		curDistance = Mathf.SmoothDamp(curDistance, targetDist, ref distVel, distanceSmoothTime);
		curPitch = Mathf.SmoothDamp(curPitch, targetPitch, ref pitchVel, pitchSmoothTime);
		curYaw = Mathf.SmoothDampAngle(curYaw, targetYaw, ref yawVel, rotationSmoothTime);

		Quaternion rot = Quaternion.Euler(curPitch, curYaw, 0f);
		transform.rotation = rot;
		transform.position = focusPos - rot * Vector3.forward * curDistance;
	}
}