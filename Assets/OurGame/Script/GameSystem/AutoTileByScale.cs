using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(Renderer))]
public class AutoTileByScale : MonoBehaviour
{
	public enum Axis { X, Y, Z }

	[Tooltip("จำนวนรอบของ texture ต่อ 1 world unit (0.25 = ซ้ำทุก 4 หน่วย)")]
	public float tilesPerUnit = 0.25f;
	[Tooltip("แกน local ของโมเดลที่ตรงกับแนวนอนของ texture (U)")]
	public Axis uAxis = Axis.X;
	[Tooltip("แกน local ของโมเดลที่ตรงกับแนวตั้งของ texture (V)")]
	public Axis vAxis = Axis.Z;

	Renderer rend;
	MeshFilter mf;
	MaterialPropertyBlock mpb;

	static float Pick(Vector3 s, Axis a) { return a == Axis.X ? s.x : (a == Axis.Y ? s.y : s.z); }

	void Update()
	{
		if (rend == null) { rend = GetComponent<Renderer>(); mf = GetComponent<MeshFilter>(); }
		if (mf.sharedMesh == null) return;
		if (mpb == null) mpb = new MaterialPropertyBlock();

		Vector3 size = Vector3.Scale(mf.sharedMesh.bounds.size, transform.lossyScale);
		var st = new Vector4(Pick(size, uAxis) * tilesPerUnit, Pick(size, vAxis) * tilesPerUnit, 0f, 0f);

		rend.GetPropertyBlock(mpb);
		mpb.SetVector("_BaseMap_ST", st);   // URP Lit
		mpb.SetVector("_MainTex_ST", st);   // Built-in
		rend.SetPropertyBlock(mpb);
	}
}