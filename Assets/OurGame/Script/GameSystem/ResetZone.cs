using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ResetZone : MonoBehaviour
{
	[Tooltip("วาร์ปซ้ำภายในกี่วินาทีไม่นับ")]
	public float cooldown = 0.5f;

	float lastTime = -10f;

	void Reset()
	{
		var c = GetComponent<Collider>();
		c.isTrigger = true;
	}

	void Awake()
	{
		GetComponent<Collider>().isTrigger = true;
	}

	void OnTriggerEnter(Collider other)
	{
		var car = other.GetComponentInParent<CarController>();
		if (car == null) return;
		if (Time.time - lastTime < cooldown) return;
		lastTime = Time.time;
		car.RespawnAtLastSafe();
	}

	void OnDrawGizmos()
	{
		var c = GetComponent<Collider>();
		if (c == null) return;
		Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.4f);
		Gizmos.matrix = transform.localToWorldMatrix;
		if (c is BoxCollider b) Gizmos.DrawCube(b.center, b.size);
		else if (c is SphereCollider s) Gizmos.DrawSphere(s.center, s.radius);
	}
}