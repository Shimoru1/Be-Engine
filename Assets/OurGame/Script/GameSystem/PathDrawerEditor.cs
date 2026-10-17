#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ต้องอยู่ในโฟลเดอร์ชื่อ "Editor" - เพิ่มปุ่มวาดเส้นแบบอิสระใน Scene view ให้ PathDrawer
/// </summary>
[CustomEditor(typeof(PathDrawer))]
public class PathDrawerEditor : Editor
{
    bool drawing;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var pd = (PathDrawer)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            drawing
                ? "กำลังวาด: กดซ้ายค้างแล้วลากใน Scene view จนปลายเส้นกลับมาชนจุดเริ่ม (วงกลมเขียว) เส้นจะปิดเอง\n(ถ้าต้องการหมุนมุมมอง ให้กด Alt ค้างไว้)"
                : "กด Start Drawing เพื่อวาดเส้นทางใหม่ (เส้นเดิมจะถูกล้าง)",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(drawing ? "Stop Drawing" : "Start Drawing"))
            {
                drawing = !drawing;
                if (drawing)
                {
                    Undo.RecordObject(pd, "Start path drawing");
                    pd.ClearPath();
                    EditorUtility.SetDirty(pd);
                }
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Close Now"))
            {
                Undo.RecordObject(pd, "Close path");
                pd.FinishPath();
                drawing = false;
                EditorUtility.SetDirty(pd);
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Clear"))
            {
                Undo.RecordObject(pd, "Clear path");
                pd.ClearPath();
                drawing = false;
                EditorUtility.SetDirty(pd);
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.LabelField("Raw points: " + pd.rawPoints.Count +
                                   "   Final points: " + pd.savedPoints.Count +
                                   "   Closed: " + pd.IsClosed);
    }

    void OnSceneGUI()
    {
        if (!drawing) return;
        var pd = (PathDrawer)target;
        Event e = Event.current;

        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
        if (e.alt) return;

        EventType t = e.GetTypeForControl(id);
        if (t == EventType.MouseDown && e.button == 0)
        {
            GUIUtility.hotControl = id;
            AddPoint(pd, e);
            e.Use();
        }
        else if (t == EventType.MouseDrag && e.button == 0 && GUIUtility.hotControl == id)
        {
            AddPoint(pd, e);
            e.Use();
        }
        else if (t == EventType.MouseUp && e.button == 0 && GUIUtility.hotControl == id)
        {
            GUIUtility.hotControl = 0;
            e.Use();
        }
        SceneView.RepaintAll();
    }

    void AddPoint(PathDrawer pd, Event e)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, pd.drawPlaneY, 0f));
        if (!plane.Raycast(ray, out float d)) return;
        Vector3 p = ray.GetPoint(d);

        var raw = pd.rawPoints;
        if (raw.Count > 0 && Vector3.Distance(raw[raw.Count - 1], p) < pd.minPointDistance) return;

        Undo.RecordObject(pd, "Draw path point");
        raw.Add(p);
        EditorUtility.SetDirty(pd);

        if (raw.Count >= pd.minPointsToClose && Vector3.Distance(p, raw[0]) <= pd.closeDistance)
        {
            pd.FinishPath();
            drawing = false;
            GUIUtility.hotControl = 0;
        }
    }
}
#endif
