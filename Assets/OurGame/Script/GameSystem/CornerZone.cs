using UnityEngine;

/// <summary>
/// โซนโค้ง (วางเองในซีน) - ใส่กับ GameObject ที่มี BoxCollider แล้วปรับขนาดให้คลุมโค้ง
/// ในโซน: S = เบรค, JKL = เร่ง, Space = ลดเกียร์
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class CornerZone : MonoBehaviour
{
    public Color gizmoColor = new Color(1f, 0.9f, 0f, 0.3f);

    void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
    void Awake() { GetComponent<BoxCollider>().isTrigger = true; }

    void OnDrawGizmos()
    {
        var bc = GetComponent<BoxCollider>();
        if (bc == null) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(bc.center, bc.size);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(bc.center, bc.size);
    }
}
