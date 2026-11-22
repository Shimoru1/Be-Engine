using UnityEngine;
using UnityEngine.UI;

public class RaceHUD : MonoBehaviour
{
	[Header("References")]
	public CarController car;
	public PathDrawer path;

	[Header("UI elements (สร้างด้วย Build HUD แล้วย้ายได้เอง)")]
	public Text rpmText;
	public Text kphText;
	public Text gearText;
	public Text hintText;
	public Image zoneLeft;
	public Image zoneRight;
	public Image shiftBar;
	public Text shiftText;
	[Tooltip("เรียงจากนอกสุด -> ใกล้กลางสุด")]
	public Image[] dotsLeft;
	public Image[] dotsRight;
	public Sprite dotSprite;
	public Font font;

	[Header("Behaviour")]
	public float blinkHz = 8f;
	[Range(1, 4)] public int dotsPerStage = 2;
	public Color zoneOutColor = new Color(0.2f, 1f, 0.3f, 0.85f);
	public Color zoneInColor = new Color(1f, 0.9f, 0.1f, 0.95f);

	[Header("Zone bars (ปรับได้ตอน Play ก็เห็นผลทันที)")]
	[Tooltip("ถ้าปิด จะไม่ยุ่งกับตำแหน่ง/ขนาดแถบ ย้ายด้วยมือได้อิสระ")]
	public bool controlZoneLayout = true;
	[Tooltip("ระยะห่างจากขอบจอ (px) - เพิ่ม = แถบเข้าใกล้กลางจอ")]
	public float zoneEdgeGap = 0f;
	[Tooltip("ตำแหน่งขึ้น/ลง (px) - บวก = ขึ้น, ลบ = ลง")]
	public float zoneY = 0f;
	public Vector2 zoneSize = new Vector2(30f, 460f);

	[Header("Power bars (ความแรงการกดรัว)")]
	public bool showPowerBars = true;
	[Tooltip("ช่วงเวลา (วินาที) ที่ใช้นับความถี่การกด สั้น = ไว, ยาว = นิ่ง")]
	public float powerWindow = 0.4f;
	[Tooltip("ความถี่การกด (ครั้ง/วินาที) ที่ถือว่า Power = 100%")]
	public float maxTapsPerSecond = 37f;
	[Tooltip("ความถี่การกด (ครั้ง/วินาที) ที่ถือว่า Power = 100% ตอนอยู่ในโซนโค้ง")]
	public float maxTapsPerSecondInZone = 20f;
	[Tooltip("ความเร็วที่หลอดไล่ตามค่า (ยิ่งมากยิ่งไว)")]
	public float powerSmooth = 12f;
	[Tooltip("ระยะห่างจากขอบจอ (px)")]
	public float powerEdgeGap = 60f;
	[Tooltip("ตำแหน่งขึ้น/ลง (px) บวก = ขึ้น")]
	public float powerY = 0f;
	public Vector2 powerSize = new Vector2(28f, 460f);
	public Color powerBackColor = new Color(0f, 0f, 0f, 0.5f);
	public Color powerLowColor = new Color(0.2f, 0.8f, 1f, 1f);
	public Color powerHighColor = new Color(1f, 0.2f, 0.2f, 1f);
	[Tooltip("ติ๊ก = สีไล่จาก Low ไป High ตามระดับ / ไม่ติ๊ก = ใช้ Low ตลอด")]
	public bool powerGradient = true;
	public Text powerText;

	Image powerBackL, powerBackR, powerFillL, powerFillR;
	float powerValue;
	readonly System.Collections.Generic.Queue<float> tapTimes = new System.Collections.Generic.Queue<float>();

	[Header("Shift lights layout (ใช้ตอนกด Build HUD)")]
	public float dotSize = 40f;
	public float dotSpacing = 46f;
	[Tooltip("ตำแหน่ง x ของจุดในสุด (ใกล้กลางจอ) - ใส่ dotSpacing/2 เพื่อให้เขียว 2 ดวงติดกัน")]
	public float dotsInnerX = 23f;
	public float dotsY = 420f;
	public Color shiftBarColor = new Color(0.1f, 0.6f, 1f, 1f);
	[Tooltip("สีไฟทุกดวงตอน SHIFT (กระพริบ)")]
	public Color shiftDotColor = new Color(0.1f, 0.6f, 1f, 1f);
	[Tooltip("สีต่อข้าง เรียงจากนอกสุด -> กลาง (จำนวนช่อง = จำนวนจุดต่อข้าง)")]
	public Color[] dotColors =
	{
		new Color(1f, 0.15f, 0.15f),
		new Color(1f, 0.15f, 0.15f),
		new Color(1f, 0.55f, 0.1f),  
		new Color(1f, 0.55f, 0.1f),
		new Color(1f, 0.95f, 0.2f), 
		new Color(1f, 0.95f, 0.2f),
		new Color(0.2f, 1f, 0.3f),  
	};

	static readonly KeyCode[] zoneKeys = { KeyCode.J, KeyCode.K, KeyCode.L };
	static readonly KeyCode[] outZoneKeys = { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.J, KeyCode.K, KeyCode.L };

	void Awake()
	{
		if (!HasAllRefs()) BuildUI();
		if (dotSprite == null) dotSprite = MakeCircle();
		ApplyDotSprite(dotsLeft);
		ApplyDotSprite(dotsRight);
		if (font == null) font = GetFont();
		foreach (var t in new[] { rpmText, kphText, gearText, hintText, shiftText })
			if (t != null && t.font == null) t.font = font;
		ApplyZoneLayout();
		BuildPowerBars();
	}

	void Start()
	{
		if (car == null)
		{
			var go = GameObject.FindGameObjectWithTag("Player");
			if (go != null) car = go.GetComponent<CarController>();
		}
	}

	void OnValidate() { ApplyZoneLayout(); ApplyPowerLayout(); }

	void Update()
	{
		ApplyZoneLayout();
		ApplyPowerLayout();

		if (hintText != null)
			hintText.text = (path == null || !path.IsClosed) ? "Path not set - select PathDrawer and press Start Drawing in the Scene view" : "";

		if (car == null)
		{
			var go = GameObject.FindGameObjectWithTag("Player");
			if (go != null) car = go.GetComponent<CarController>();
		}

		if (car == null) return;

		if (rpmText != null) rpmText.text = car.Rpm + " RPM";
		if (kphText != null) kphText.text = Mathf.RoundToInt(car.IsReversing ? car.ReverseKph : car.SpeedKph) + " KPH";
		if (gearText != null) gearText.text = car.GearLabel;

		bool top = car.IsTopGear || car.IsReversing;
		int stage = car.ShiftStage;
		bool shift = !top && stage >= 5;
		bool blinkOn = Mathf.FloorToInt(Time.unscaledTime * blinkHz * 2f) % 2 == 0;

		if (shiftBar != null) shiftBar.gameObject.SetActive(false);

		int perSide = dotsLeft != null ? dotsLeft.Length : 0;
		if (shift)
		{
			int lit = blinkOn ? perSide : 0;
			SetDots(dotsLeft, lit);
			SetDots(dotsRight, lit);
			TintDots(dotsLeft, shiftDotColor);
			TintDots(dotsRight, shiftDotColor);
		}
		else
		{
			int lit = top ? 0 : Mathf.Min(stage * dotsPerStage, perSide);
			SetDots(dotsLeft, lit);
			SetDots(dotsRight, lit);
			RestoreDotColors(dotsLeft);
			RestoreDotColors(dotsRight);
		}

		UpdatePowerBars();

		Color zc = car.InZone ? zoneInColor : zoneOutColor;
		if (zoneLeft != null) zoneLeft.color = zc;
		if (zoneRight != null) zoneRight.color = zc;
	}

	static void SetDots(Image[] dots, int lit)
	{
		if (dots == null) return;
		for (int i = 0; i < dots.Length; i++)
			if (dots[i] != null) dots[i].gameObject.SetActive(i < lit);
	}

	static void TintDots(Image[] dots, Color c)
	{
		if (dots == null) return;
		for (int i = 0; i < dots.Length; i++)
			if (dots[i] != null) dots[i].color = c;
	}

	void RestoreDotColors(Image[] dots)
	{
		if (dots == null || dotColors == null) return;
		for (int i = 0; i < dots.Length && i < dotColors.Length; i++)
			if (dots[i] != null) dots[i].color = dotColors[i];
	}

	void ApplyDotSprite(Image[] dots)
	{
		if (dots == null) return;
		foreach (var d in dots) if (d != null && d.sprite == null) d.sprite = dotSprite;
	}

	void ApplyZoneLayout()
	{
		if (!controlZoneLayout) return;
		SetZone(zoneLeft, 0f, 1f);
		SetZone(zoneRight, 1f, -1f);
	}

	void SetZone(Image img, float anchorX, float dir)
	{
		if (img == null) return;
		var rt = img.rectTransform;
		rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 0.5f);
		rt.pivot = new Vector2(anchorX, 0.5f);
		rt.sizeDelta = zoneSize;
		rt.anchoredPosition = new Vector2(dir * zoneEdgeGap, zoneY);
	}

	// ---------------- Power bars ----------------
	void BuildPowerBars()
	{
		if (!showPowerBars || powerBackL != null) return;

		Transform root = null;
		var canvas = GetComponentInChildren<Canvas>(true);
		if (canvas != null) root = canvas.transform;
		else if (zoneLeft != null) root = zoneLeft.transform.parent;
		if (root == null) return;

		MakePowerBar("PowerL", root, 0f, out powerBackL, out powerFillL);
		MakePowerBar("PowerR", root, 1f, out powerBackR, out powerFillR);
		ApplyPowerLayout();
		SetPowerFill(0f);
	}

	void MakePowerBar(string name, Transform root, float anchorX, out Image back, out Image fill)
	{
		back = MakeImage(name + "Back", root, new Vector2(anchorX, 0.5f), new Vector2(anchorX, 0.5f), Vector2.zero, powerSize, powerBackColor, null);

		fill = MakeImage(name + "Fill", back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero, powerLowColor, null);
		var rt = fill.rectTransform;
		rt.anchorMin = new Vector2(0f, 0f);
		rt.anchorMax = new Vector2(1f, 0f);
		rt.pivot = new Vector2(0.5f, 0f);
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	void ApplyPowerLayout()
	{
		if (powerBackL == null) return;
		bool on = showPowerBars;
		powerBackL.gameObject.SetActive(on);
		powerBackR.gameObject.SetActive(on);
		if (!on) return;

		PlacePower(powerBackL, 0f, 1f);
		PlacePower(powerBackR, 1f, -1f);
		powerBackL.color = powerBackColor;
		powerBackR.color = powerBackColor;
	}

	void PlacePower(Image back, float anchorX, float dir)
	{
		var rt = back.rectTransform;
		rt.anchorMin = rt.anchorMax = new Vector2(anchorX, 0.5f);
		rt.pivot = new Vector2(anchorX, 0.5f);
		rt.sizeDelta = powerSize;
		rt.anchoredPosition = new Vector2(dir * powerEdgeGap, powerY);
	}

	void CountTaps()
	{
		var keys = (car != null && car.InZone) ? zoneKeys : outZoneKeys;
		for (int i = 0; i < keys.Length; i++)
			if (Input.GetKeyDown(keys[i])) tapTimes.Enqueue(Time.unscaledTime);
	}

	void UpdatePowerBars()
	{
		if (!showPowerBars || powerFillL == null) return;

		CountTaps();

		float now = Time.unscaledTime;
		while (tapTimes.Count > 0 && now - tapTimes.Peek() > powerWindow) tapTimes.Dequeue();

		float rate = tapTimes.Count / Mathf.Max(0.05f, powerWindow);
		float maxTaps = (car != null && car.InZone) ? maxTapsPerSecondInZone : maxTapsPerSecond;
		float target = Mathf.Clamp01(rate / Mathf.Max(1f, maxTaps));

		powerValue = Mathf.Lerp(powerValue, target, 1f - Mathf.Exp(-powerSmooth * Time.unscaledDeltaTime));
		if (Mathf.Abs(powerValue - target) < 0.002f) powerValue = target;
		SetPowerFill(powerValue);
	}

	void SetPowerFill(float v)
	{
		if (powerFillL == null) return;
		Color c = powerGradient ? Color.Lerp(powerLowColor, powerHighColor, v) : powerLowColor;

		SetFill(powerFillL, v, c);
		SetFill(powerFillR, v, c);

		if (powerText != null) powerText.text = Mathf.RoundToInt(v * 100f) + "%";
	}

	static void SetFill(Image fill, float v, Color c)
	{
		fill.color = c;
		var rt = fill.rectTransform;
		rt.anchorMin = new Vector2(0f, 0f);
		rt.anchorMax = new Vector2(1f, Mathf.Clamp01(v));
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	bool HasAllRefs()
	{
		int n = dotColors != null ? dotColors.Length : 0;
		return rpmText && kphText && gearText && hintText && zoneLeft && zoneRight && shiftBar && dotsLeft != null && dotsLeft.Length == n && n > 0 && dotsRight != null && dotsRight.Length == n;
	}

	[ContextMenu("Build HUD (editable)")]
	void BuildHudInEditor()
	{
#if UNITY_EDITOR
		var old = transform.Find("HUD Canvas");
		if (old != null) UnityEditor.Undo.DestroyObjectImmediate(old.gameObject);
		BuildUI();
		UnityEditor.EditorUtility.SetDirty(this);
		if (!Application.isPlaying)
			UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
	}

	// ---------------- UI builder ----------------
	void BuildUI()
	{
		font = GetFont();

		if (Application.isPlaying)
		{
			var old = transform.Find("HUD Canvas");
			if (old != null) Destroy(old.gameObject);
		}

		var canvasGO = new GameObject("HUD Canvas");
		canvasGO.transform.SetParent(transform, false);
		var canvas = canvasGO.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		var scaler = canvasGO.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		canvasGO.AddComponent<GraphicRaycaster>();
#if UNITY_EDITOR
		UnityEditor.Undo.RegisterCreatedObjectUndo(canvasGO, "Build HUD");
#endif
		Transform root = canvasGO.transform;
		Vector2 bottom = new Vector2(0.5f, 0f);
		Vector2 mid = new Vector2(0.5f, 0.5f);

		zoneLeft = MakeImage("ZoneLeft", root, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, zoneSize, zoneOutColor, null);
		zoneRight = MakeImage("ZoneRight", root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, zoneSize, zoneOutColor, null);
		ApplyZoneLayout();

		int total = Mathf.Max(1, dotColors.Length);
		dotsLeft = new Image[total];
		dotsRight = new Image[total];
		for (int i = 0; i < total; i++)
		{
			float x = dotsInnerX + (total - 1 - i) * dotSpacing;
			Color c = dotColors[i];
			dotsLeft[i] = MakeImage("DotL" + i, root, bottom, mid, new Vector2(-x, dotsY), new Vector2(dotSize, dotSize), c, dotSprite);
			dotsRight[i] = MakeImage("DotR" + i, root, bottom, mid, new Vector2(x, dotsY), new Vector2(dotSize, dotSize), c, dotSprite);
		}

		shiftBar = MakeImage("ShiftBar", root, bottom, mid, new Vector2(0f, dotsY), new Vector2(520f, 80f), shiftBarColor, null);
		shiftText = MakeText("ShiftText", shiftBar.transform, mid, Vector2.zero, new Vector2(520f, 80f), 52, TextAnchor.MiddleCenter, Color.white);
		shiftText.fontStyle = FontStyle.Bold;
		shiftText.text = "! SHIFT !";

		gearText = MakeText("Gear", root, bottom, new Vector2(0f, 300f), new Vector2(300f, 160f), 150, TextAnchor.MiddleCenter, Color.white);
		gearText.fontStyle = FontStyle.Bold;
		gearText.text = "N";
		rpmText = MakeText("RPM", root, bottom, new Vector2(0f, 190f), new Vector2(600f, 80f), 60, TextAnchor.MiddleCenter, Color.white);
		rpmText.text = "0 RPM";
		kphText = MakeText("KPH", root, bottom, new Vector2(0f, 125f), new Vector2(600f, 60f), 44, TextAnchor.MiddleCenter, new Color(0.8f, 0.9f, 1f));
		kphText.text = "0 KPH";
		hintText = MakeText("Hint", root, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(1500f, 60f), 34, TextAnchor.MiddleCenter, Color.white);
	}

	Font GetFont()
	{
		if (font != null) return font;
		Font f = null;
		try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
		catch { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
		if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 32);
		return f;
	}

	RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
	{
		var go = new GameObject(name, typeof(RectTransform));
		var rt = go.GetComponent<RectTransform>();
		rt.SetParent(parent, false);
		rt.anchorMin = rt.anchorMax = anchor;
		rt.pivot = pivot;
		rt.anchoredPosition = pos;
		rt.sizeDelta = size;
		return rt;
	}

	Image MakeImage(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, Sprite sprite)
	{
		var rt = MakeRect(name, parent, anchor, pivot, pos, size);
		var img = rt.gameObject.AddComponent<Image>();
		img.sprite = sprite;
		img.color = color;
		img.raycastTarget = false;
		return img;
	}

	Text MakeText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color)
	{
		var rt = MakeRect(name, parent, anchor, new Vector2(0.5f, 0.5f), pos, size);
		var t = rt.gameObject.AddComponent<Text>();
		t.font = font;
		t.fontSize = fontSize;
		t.alignment = align;
		t.color = color;
		t.raycastTarget = false;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		t.verticalOverflow = VerticalWrapMode.Overflow;
		return t;
	}

	static Sprite MakeCircle()
	{
		const int s = 64;
		var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
		var px = new Color32[s * s];
		float r = s / 2f;
		for (int y = 0; y < s; y++)
			for (int x = 0; x < s; x++)
			{
				float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
				float a = Mathf.Clamp01(r - d);
				px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
			}
		tex.SetPixels32(px);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
	}
}