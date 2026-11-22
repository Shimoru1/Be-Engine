using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class FinishLine : MonoBehaviour
{
	public static event Action<CarController> CarCrossed;

	[Tooltip("ติ๊ก = นับเฉพาะตอนรถวิ่งผ่านในทิศที่ลูกศรสีน้ำเงิน (แกน Z) ของ object ชี้ไป")]
	public bool checkDirection = true;

	[Tooltip("ผ่านซ้ำภายในกี่วินาทีไม่นับ")]
	public float cooldown = 1f;

	float lastTime = -10f;

	void Reset()
	{
		var bc = GetComponent<BoxCollider>();
		bc.isTrigger = true;
		bc.size = new Vector3(20f, 4f, 1f);
	}

	void Awake()
	{
		var bc = GetComponent<BoxCollider>();
		bc.isTrigger = true;
	}

	void OnTriggerEnter(Collider other)
	{
		var car = other.GetComponentInParent<CarController>();
		if (car == null) return;
		if (Time.time - lastTime < cooldown) return;

		if (checkDirection && Vector3.Dot(car.HeadingForward, transform.forward) <= 0f) return;

		lastTime = Time.time;
		CarCrossed?.Invoke(car);
	}

	void OnDrawGizmos()
	{
		var bc = GetComponent<BoxCollider>();
		if (bc == null) return;
		Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.5f);
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.DrawCube(bc.center, bc.size);
		Gizmos.color = Color.white;
		Gizmos.DrawRay(bc.center, Vector3.forward * (bc.size.z + 3f));
	}
}