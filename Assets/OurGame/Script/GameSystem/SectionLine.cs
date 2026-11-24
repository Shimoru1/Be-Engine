using System;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SectionLine : MonoBehaviour
{
	public static event Action<int, CarController> CarPassed;

	[Tooltip("ลำดับเส้น 1, 2, 3 ... (ต้องผ่านตามลำดับ)")]
	public int sectionNumber = 1;
	public bool checkDirection = true;
	public float cooldown = 1f;

	float lastTime = -10f;

	void Reset()
	{
		var bc = GetComponent<BoxCollider>();
		bc.isTrigger = true;
		bc.size = new Vector3(20f, 4f, 1f);
	}

	void Awake() { GetComponent<BoxCollider>().isTrigger = true; }

	void OnTriggerEnter(Collider other)
	{
		var car = other.GetComponentInParent<CarController>();
		if (car == null || Time.time - lastTime < cooldown) return;
		if (checkDirection && Vector3.Dot(car.HeadingForward, transform.forward) <= 0f) return;
		lastTime = Time.time;
		CarPassed?.Invoke(sectionNumber, car);
	}

	void OnDrawGizmos()
	{
		var bc = GetComponent<BoxCollider>();
		if (bc == null) return;
		Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.5f);
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.DrawCube(bc.center, bc.size);
		Gizmos.color = Color.white;
		Gizmos.DrawRay(bc.center, Vector3.forward * (bc.size.z + 3f));
	}
}