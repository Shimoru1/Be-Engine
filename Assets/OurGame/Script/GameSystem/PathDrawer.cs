using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// เส้นทางวิ่งที่ "วาดไว้ล่วงหน้าใน Scene view" (ไม่ได้วาดตอนเริ่มเกม)
/// วิธีใช้: เลือก GameObject นี้ > กดปุ่ม Start Drawing ใน Inspector > กดซ้ายค้างแล้วลากใน Scene view
/// ลากจนปลายเส้นกลับมาชนจุดเริ่ม เส้นจะปิด + smooth + resample ให้เอง และถูกบันทึกลงซีน
/// หมายเหตุ: เก็บเป็นพิกัด world จึงไม่ควรขยับ/หมุน GameObject นี้หลังวาดเสร็จ
/// </summary>
public class PathDrawer : MonoBehaviour
{
	[Header("Authoring (Scene view)")]
	public float drawPlaneY = 0f;          // ความสูงของระนาบที่ใช้วาด
	public float minPointDistance = 2f;    // ระยะห่างขั้นต่ำระหว่างจุดตอนลาก
	public float closeDistance = 8f;       // ปลายเส้นเข้าใกล้จุดเริ่มเท่านี้ = บรรจบ
	public int minPointsToClose = 25;      // ต้องมีจุดอย่างน้อยเท่านี้ก่อนถึงจะปิดเส้นได้
	[Range(0, 4)] public int smoothIterations = 2;
	public float resampleSpacing = 2f;

	[Header("Line visual")]
	public Color lineColor = new Color(1f, 0.9f, 0.2f, 0.9f);
	public bool showLineInGame = true;
	public float lineWidth = 1.2f;
	public float lineHeight = 0.1f;

	// ข้อมูลที่บันทึกในซีน (แก้ผ่านปุ่มใน Inspector)
	[HideInInspector] public List<Vector3> rawPoints = new List<Vector3>();
	[HideInInspector] public List<Vector3> savedPoints = new List<Vector3>();
	[HideInInspector] public float savedSpacing = 2f;
	[HideInInspector] public bool closed;

	public bool IsClosed => closed && savedPoints.Count >= 8;
	public IReadOnlyList<Vector3> Points => savedPoints;
	public float ActualSpacing => savedSpacing;
	public Vector3 StartPoint => savedPoints[0];
	public Vector3 StartDirection
	{
		get
		{
			Vector3 d = savedPoints[1] - savedPoints[0];
			d.y = 0f;
			return d.normalized;
		}
	}

	void Awake()
	{
		if (showLineInGame && IsClosed) BuildRuntimeLine();
	}

	void BuildRuntimeLine()
	{
		var go = new GameObject("PathLine");
		go.transform.SetParent(transform, false);
		var lr = go.AddComponent<LineRenderer>();
		lr.useWorldSpace = true;
		lr.loop = true;
		lr.widthMultiplier = lineWidth;
		lr.startColor = lr.endColor = lineColor;
		lr.material = new Material(Shader.Find("Sprites/Default"));
		lr.positionCount = savedPoints.Count;
		for (int i = 0; i < savedPoints.Count; i++)
			lr.SetPosition(i, savedPoints[i] + Vector3.up * lineHeight);
	}

	// ---------------- เรียกจาก Editor ----------------
	public void ClearPath()
	{
		rawPoints.Clear();
		savedPoints.Clear();
		closed = false;
	}

	public void FinishPath()
	{
		if (rawPoints.Count < 4) return;
		List<Vector3> p = new List<Vector3>(rawPoints);
		for (int i = 0; i < smoothIterations; i++) p = Chaikin(p);

		savedPoints = Resample(p, resampleSpacing, out float step);
		savedSpacing = step;
		closed = true;
	}

	// ---------------- Gizmos (เห็นเส้นใน Scene view เสมอ) ----------------
	void OnDrawGizmos()
	{
		Gizmos.color = lineColor;
		DrawPoly(savedPoints, true);
		Gizmos.color = new Color(1f, 1f, 1f, 0.8f);
		DrawPoly(rawPoints, false);

		Gizmos.color = Color.green;
		if (savedPoints.Count > 0) Gizmos.DrawSphere(savedPoints[0] + Vector3.up * lineHeight, 1.5f);
		else if (rawPoints.Count > 0) Gizmos.DrawSphere(rawPoints[0] + Vector3.up * lineHeight, 1.5f);
	}

	static void DrawPoly(List<Vector3> p, bool loop)
	{
		int n = p.Count;
		for (int i = 0; i < n - 1; i++) Gizmos.DrawLine(p[i], p[i + 1]);
		if (loop && n > 2) Gizmos.DrawLine(p[n - 1], p[0]);
	}

	// ทำมุมให้โค้งมนขึ้น (เส้นปิด)
	static List<Vector3> Chaikin(List<Vector3> src)
	{
		int n = src.Count;
		var res = new List<Vector3>(n * 2);
		for (int i = 0; i < n; i++)
		{
			Vector3 a = src[i], b = src[(i + 1) % n];
			res.Add(Vector3.Lerp(a, b, 0.25f));
			res.Add(Vector3.Lerp(a, b, 0.75f));
		}
		return res;
	}

	// แบ่งเส้นปิดให้จุดห่างเท่ากันตามความยาวเส้น
	static List<Vector3> Resample(List<Vector3> src, float spacing, out float step)
	{
		int n = src.Count;
		float total = 0f;
		for (int i = 0; i < n; i++) total += Vector3.Distance(src[i], src[(i + 1) % n]);
		if (total < 0.01f) { step = spacing; return new List<Vector3>(src); }

		int count = Mathf.Max(8, Mathf.RoundToInt(total / Mathf.Max(0.1f, spacing)));
		step = total / count;

		var res = new List<Vector3>(count) { src[0] };
		int seg = 0;
		float segPos = 0f;
		float segLen = Vector3.Distance(src[0], src[1 % n]);

		for (int k = 1; k < count; k++)
		{
			float need = step;
			while (need > 0f)
			{
				float remain = segLen - segPos;
				if (need <= remain) { segPos += need; need = 0f; }
				else
				{
					need -= remain;
					seg = (seg + 1) % n;
					segPos = 0f;
					segLen = Vector3.Distance(src[seg], src[(seg + 1) % n]);
				}
			}
			float t = segLen > 0f ? segPos / segLen : 0f;
			res.Add(Vector3.Lerp(src[seg], src[(seg + 1) % n], t));
		}
		return res;
	}
}