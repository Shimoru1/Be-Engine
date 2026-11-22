using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CarSelectUI : MonoBehaviour
{
	[Serializable]
	public class CarOption
	{
		[Tooltip("ว่าง = ใช้ชื่อ prefab")]
		public string displayName;
		[Tooltip("Prefab รถที่มี CarController ที่ root")]
		public CarController prefab;
	}

	public static CarController SpawnedCar { get; private set; }
	public static event Action<CarController> PlayerCarSpawned;

	[Header("Hide while selecting")]
	public List<GameObject> hideWhileSelecting = new List<GameObject>();

	[Header("Cars")]
	public List<CarOption> cars = new List<CarOption>();
	[Tooltip("เส้นทางในซีน (ว่าง = หา PathDrawer ในซีนให้เอง)")]
	public PathDrawer path;
	[Tooltip("จุดเกิดของรถ (ลูกศรสีน้ำเงินของ Transform = ทิศที่รถวิ่ง) ว่าง = ใช้ตำแหน่งรถที่วางไว้ในซีน")]
	public Transform spawnPoint;
	public bool autoSelectFirst = true;
	bool hasPlacedPose;
	Vector3 placedPos;
	float placedHeading;

	[Header("Music (ID จาก Music List ของ AudioManager)")]
	public string selectMusicId;
	public string raceMusicId;

	[Header("Engine preview")]
	public float enginePreviewSeconds = 3f;

	[Header("Layout")]
	public int columns = 3;
	public Vector2 cellSize = new Vector2(220f, 165f);
	public float cellSpacing = 16f;
	public float panelPadding = 20f;
	public float screenMargin = 40f;
	public float bottomBarHeight = 130f;
	public float scrollbarWidth = 16f;

	[Header("Colors")]
	public Color dimColor = new Color(0.05f, 0.05f, 0.07f, 0.96f);
	public Color panelColor = new Color(0.12f, 0.12f, 0.15f, 1f);
	public Color cellColor = new Color(0.25f, 0.25f, 0.3f, 1f);
	public Color cellSelectedColor = new Color(1f, 0.75f, 0.1f, 1f);
	public Color previewFrameColor = new Color(0.07f, 0.07f, 0.09f, 1f);
	public Color barColor = new Color(0.18f, 0.18f, 0.22f, 1f);
	public Color engineButtonColor = new Color(0.25f, 0.45f, 0.8f, 1f);
	public Color startButtonColor = new Color(0.2f, 0.7f, 0.3f, 1f);

	[Header("Preview render")]
	public int previewWidth = 800;
	public int previewHeight = 600;
	[Tooltip("มุมกล้องรอบรถ (องศา) 0 = มองตรงจากหน้ารถ")]
	public float previewYaw = 35f;
	public float previewPitch = 15f;
	public float previewFov = 30f;
	[Tooltip("เว้นขอบรอบรถ (มาก = รถเล็กลง)")]
	public float previewPadding = 1.15f;
	public Vector3 previewStagePosition = new Vector3(5000f, 5000f, 5000f);

	[Header("Car name text")]
	[Tooltip("ลาก TMP Text ที่วางเองใน Canvas มาใส่ได้ จะจัดตำแหน่งในซีนด้วยมือ (ว่าง = สร้างให้ในแถบล่าง)")]
	public TMP_Text nameTextOverride;
	[Tooltip("ตำแหน่งของชื่อรถในแถบล่าง (px) วัดจากกึ่งกลางซ้ายของแถบ (ใช้เมื่อไม่ได้ลาก Override)")]
	public Vector2 nameOffset = new Vector2(28f, 0f);
	public Vector2 nameBoxSize = new Vector2(600f, 100f);
	public float nameFontSize = 52f;
	public TextAlignmentOptions nameAlignment = TextAlignmentOptions.MidlineLeft;

	// ---------- internals ----------
	readonly List<RenderTexture> previews = new List<RenderTexture>();
	readonly List<Image> cellFrames = new List<Image>();

	GameObject selectionRoot;
	TMP_Text nameText;
	RawImage bigPreview;
	Button engineButton;
	Button startButton;
	TMP_Text countdownText;

	int selected = -1;
	bool confirmed;
	Coroutine hideGoRoutine;

	// =========================================================
	// Lifecycle
	// =========================================================
	void Start()
	{
		if (path == null) path = FindFirstObjectByType<PathDrawer>();

		foreach (var c in FindObjectsByType<CarController>(FindObjectsSortMode.None))
		{
			if (!hasPlacedPose)
			{
				hasPlacedPose = true;
				placedPos = c.transform.position;
				placedHeading = c.transform.eulerAngles.y - c.modelYawOffset;
			}
			c.gameObject.SetActive(false);
		}

		RenderAllPreviews();
		foreach (var go in hideWhileSelecting)
			if (go != null) go.SetActive(false);
		BuildUI();

		if (autoSelectFirst && cars.Count > 0) Select(0);
		else RefreshSelection();

		var gm = GameManager.Instance;
		if (gm != null)
		{
			gm.CountdownTick += OnCountdownTick;
			gm.GameStarted += OnGameStarted;
		}
		else Debug.LogWarning("[CarSelectUI] ไม่พบ GameManager ในซีน");

		StartCoroutine(InitRoutine());
	}

	void Update()
	{
		if (nameTextOverride == null) ApplyNameLayout();
	}

	void ApplyNameLayout()
	{
		if (nameText == null) return;
		var rt = nameText.rectTransform;
		rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
		rt.pivot = new Vector2(0f, 0.5f);
		rt.sizeDelta = nameBoxSize;
		rt.anchoredPosition = nameOffset;
		nameText.fontSize = nameFontSize;
		nameText.alignment = nameAlignment;
	}

	IEnumerator InitRoutine()
	{
		yield return null;
		yield return null;

		UnlockMouse();

		if (AudioManager.Instance != null && !string.IsNullOrEmpty(selectMusicId))
			AudioManager.Instance.PlayMusic(selectMusicId);
	}

	void OnDestroy()
	{
		var gm = GameManager.Instance;
		if (gm != null)
		{
			gm.CountdownTick -= OnCountdownTick;
			gm.GameStarted -= OnGameStarted;
		}

		foreach (var rt in previews)
			if (rt != null) { rt.Release(); Destroy(rt); }
		previews.Clear();

		if (SpawnedCar != null && SpawnedCar.gameObject == null) SpawnedCar = null;
	}

	// =========================================================
	// Preview render (Prefab -> RenderTexture)
	// =========================================================
	void RenderAllPreviews()
	{
		var hidden = new GameObject("PreviewHidden");
		hidden.SetActive(false);

		var stage = new GameObject("PreviewStage");
		stage.transform.position = previewStagePosition;

		var camGO = new GameObject("PreviewCamera");
		camGO.transform.SetParent(stage.transform, false);
		var cam = camGO.AddComponent<Camera>();
		cam.enabled = false;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		cam.fieldOfView = previewFov;

		var lightGO = new GameObject("PreviewLight");
		lightGO.transform.SetParent(stage.transform, false);
		lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
		var light = lightGO.AddComponent<Light>();
		light.type = LightType.Directional;
		light.intensity = 1.1f;

		foreach (var opt in cars)
		{
			RenderTexture rt = null;
			if (opt != null && opt.prefab != null)
				rt = RenderOne(opt.prefab, hidden.transform, stage.transform, cam);
			previews.Add(rt);
		}

		light.enabled = false;
		stage.SetActive(false);
		Destroy(stage);
		Destroy(hidden);
	}

	RenderTexture RenderOne(CarController prefab, Transform hidden, Transform stage, Camera cam)
	{
		float yawOffset = prefab.modelYawOffset;

		var inst = Instantiate(prefab.gameObject, hidden);
		StripForPreview(inst);
		foreach (var t in inst.GetComponentsInChildren<Transform>(true))
			t.gameObject.tag = "Untagged";

		inst.transform.SetParent(stage, false);
		inst.transform.localPosition = Vector3.zero;
		inst.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f); // หัวรถหันไป +Z

		bool has = false;
		Bounds b = new Bounds();
		foreach (var r in inst.GetComponentsInChildren<Renderer>())
		{
			if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
			if (!has) { b = r.bounds; has = true; }
			else b.Encapsulate(r.bounds);
		}
		if (!has)
		{
			inst.SetActive(false);
			Destroy(inst);
			return null;
		}

		float radius = Mathf.Max(0.01f, b.extents.magnitude);
		float dist = radius / Mathf.Sin(previewFov * 0.5f * Mathf.Deg2Rad) * previewPadding;
		Vector3 dir = Quaternion.Euler(-previewPitch, previewYaw, 0f) * Vector3.forward;

		cam.transform.position = b.center + dir * dist;
		cam.transform.LookAt(b.center);
		cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
		cam.farClipPlane = dist + radius * 2f;

		var rt = new RenderTexture(previewWidth, previewHeight, 24, RenderTextureFormat.ARGB32);
		rt.antiAliasing = 4;
		rt.Create();

		cam.targetTexture = rt;
		cam.Render();
		cam.targetTexture = null;

		inst.SetActive(false);
		Destroy(inst);
		return rt;
	}

	static void StripForPreview(GameObject go)
	{
		foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null) DestroyImmediate(m);
		foreach (var c in go.GetComponentsInChildren<Collider>(true)) if (c != null) DestroyImmediate(c);
		foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) DestroyImmediate(rb);
		foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) if (a != null) DestroyImmediate(a);
		foreach (var l in go.GetComponentsInChildren<AudioListener>(true)) if (l != null) DestroyImmediate(l);
		foreach (var c in go.GetComponentsInChildren<Camera>(true)) if (c != null) DestroyImmediate(c);
	}

	// =========================================================
	// Build UI
	// =========================================================
	void BuildUI()
	{
		if (FindFirstObjectByType<EventSystem>() == null)
		{
			var es = new GameObject("EventSystem");
			es.AddComponent<EventSystem>();
			es.AddComponent<StandaloneInputModule>();
		}

		var canvasGO = new GameObject("CarSelectCanvas");
		canvasGO.transform.SetParent(transform, false);
		var canvas = canvasGO.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 100;
		var scaler = canvasGO.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;
		canvasGO.AddComponent<GraphicRaycaster>();

		var rootImg = NewImage("SelectionRoot", canvasGO.transform, dimColor);
		FullStretch(rootImg.rectTransform);
		selectionRoot = rootImg.gameObject;

		float gridWidth = columns * cellSize.x + (columns - 1) * cellSpacing;
		float leftWidth = panelPadding * 2f + gridWidth + scrollbarWidth + 6f;

		var left = NewImage("LeftPanel", selectionRoot.transform, panelColor).rectTransform;
		left.anchorMin = new Vector2(0f, 0f);
		left.anchorMax = new Vector2(0f, 1f);
		left.pivot = new Vector2(0f, 0.5f);
		left.sizeDelta = new Vector2(leftWidth, -2f * screenMargin);
		left.anchoredPosition = new Vector2(screenMargin, 0f);

		BuildCarList(left);

		var right = NewImage("RightPanel", selectionRoot.transform, panelColor).rectTransform;
		right.anchorMin = Vector2.zero;
		right.anchorMax = Vector2.one;
		right.offsetMin = new Vector2(screenMargin * 2f + leftWidth, screenMargin);
		right.offsetMax = new Vector2(-screenMargin, -screenMargin);

		BuildRightPanel(right);

		countdownText = NewText("Countdown", canvasGO.transform, "", 280f, TextAlignmentOptions.Center, Color.white);
		FullStretch(countdownText.rectTransform);
		countdownText.fontStyle = FontStyles.Bold;
		countdownText.gameObject.SetActive(false);
	}

	void BuildCarList(RectTransform leftPanel)
	{
		var scrollRT = NewRect("Scroll", leftPanel);
		scrollRT.anchorMin = Vector2.zero;
		scrollRT.anchorMax = Vector2.one;
		scrollRT.offsetMin = new Vector2(panelPadding, panelPadding);
		scrollRT.offsetMax = new Vector2(-panelPadding, -panelPadding);
		var scroll = scrollRT.gameObject.AddComponent<ScrollRect>();

		var viewport = NewRect("Viewport", scrollRT);
		viewport.anchorMin = Vector2.zero;
		viewport.anchorMax = Vector2.one;
		viewport.offsetMin = Vector2.zero;
		viewport.offsetMax = new Vector2(-scrollbarWidth - 6f, 0f);
		var vpImg = viewport.gameObject.AddComponent<Image>();
		vpImg.color = new Color(0f, 0f, 0f, 0f);
		viewport.gameObject.AddComponent<RectMask2D>();

		var content = NewRect("Content", viewport);
		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0f, 1f);
		content.anchoredPosition = Vector2.zero;
		content.sizeDelta = Vector2.zero;

		var grid = content.gameObject.AddComponent<GridLayoutGroup>();
		grid.cellSize = cellSize;
		grid.spacing = new Vector2(cellSpacing, cellSpacing);
		grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
		grid.startAxis = GridLayoutGroup.Axis.Horizontal;
		grid.childAlignment = TextAnchor.UpperLeft;
		grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		grid.constraintCount = columns;

		var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		// ---- Scrollbar ----
		var track = NewRect("Scrollbar", scrollRT);
		track.anchorMin = new Vector2(1f, 0f);
		track.anchorMax = new Vector2(1f, 1f);
		track.pivot = new Vector2(1f, 0.5f);
		track.sizeDelta = new Vector2(scrollbarWidth, 0f);
		track.anchoredPosition = Vector2.zero;
		var trackImg = track.gameObject.AddComponent<Image>();
		trackImg.color = new Color(0f, 0f, 0f, 0.35f);

		var handle = NewRect("Handle", track);
		handle.sizeDelta = Vector2.zero;
		var handleImg = handle.gameObject.AddComponent<Image>();
		handleImg.color = new Color(0.6f, 0.6f, 0.65f, 1f);

		var sb = track.gameObject.AddComponent<Scrollbar>();
		sb.handleRect = handle;
		sb.targetGraphic = handleImg;
		sb.direction = Scrollbar.Direction.BottomToTop;

		scroll.viewport = viewport;
		scroll.content = content;
		scroll.horizontal = false;
		scroll.vertical = true;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.scrollSensitivity = 30f;
		scroll.verticalScrollbar = sb;
		scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

		// ---- Car Slot ----
		for (int i = 0; i < cars.Count; i++)
		{
			int idx = i;
			var cell = NewImage("Car_" + i, content, cellColor);
			var btn = cell.gameObject.AddComponent<Button>();
			btn.targetGraphic = cell;
			btn.onClick.AddListener(() => Select(idx));

			var rawRT = NewRect("Preview", cell.transform);
			rawRT.anchorMin = Vector2.zero;
			rawRT.anchorMax = Vector2.one;
			rawRT.offsetMin = new Vector2(6f, 6f);
			rawRT.offsetMax = new Vector2(-6f, -6f);
			var raw = rawRT.gameObject.AddComponent<RawImage>();
			raw.raycastTarget = false;
			var tex = i < previews.Count ? previews[i] : null;
			raw.texture = tex;
			raw.color = tex != null ? Color.white : Color.clear;

			cellFrames.Add(cell);
		}
	}

	void BuildRightPanel(RectTransform right)
	{
		var area = NewImage("PreviewArea", right, previewFrameColor).rectTransform;
		area.anchorMin = Vector2.zero;
		area.anchorMax = Vector2.one;
		area.offsetMin = new Vector2(panelPadding, panelPadding * 2f + bottomBarHeight);
		area.offsetMax = new Vector2(-panelPadding, -panelPadding);

		var bigRT = NewRect("BigPreview", area);
		FullStretch(bigRT);
		bigPreview = bigRT.gameObject.AddComponent<RawImage>();
		bigPreview.raycastTarget = false;
		var arf = bigRT.gameObject.AddComponent<AspectRatioFitter>();
		arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		arf.aspectRatio = (float)previewWidth / Mathf.Max(1, previewHeight);

		var bar = NewImage("BottomBar", right, barColor).rectTransform;
		bar.anchorMin = new Vector2(0f, 0f);
		bar.anchorMax = new Vector2(1f, 0f);
		bar.pivot = new Vector2(0.5f, 0f);
		bar.sizeDelta = new Vector2(-2f * panelPadding, bottomBarHeight);
		bar.anchoredPosition = new Vector2(0f, panelPadding);

		if (nameTextOverride != null)
		{
			nameText = nameTextOverride;
			nameText.gameObject.SetActive(true);
		}
		else
		{
			nameText = NewText("CarName", bar, "", nameFontSize, nameAlignment, Color.white);
			nameText.fontStyle = FontStyles.Bold;
			nameText.enableWordWrapping = false;
			nameText.overflowMode = TextOverflowModes.Overflow;
			ApplyNameLayout();
		}

		float btnH = bottomBarHeight - 40f;

		startButton = NewButton("StartButton", bar, "START", startButtonColor);
		var sRT = (RectTransform)startButton.transform;
		sRT.anchorMin = sRT.anchorMax = new Vector2(1f, 0.5f);
		sRT.pivot = new Vector2(1f, 0.5f);
		sRT.sizeDelta = new Vector2(260f, btnH);
		sRT.anchoredPosition = new Vector2(-20f, 0f);
		startButton.onClick.AddListener(OnStartClicked);

		engineButton = NewButton("EngineSoundButton", bar, "ENGINE SOUND", engineButtonColor);
		var eRT = (RectTransform)engineButton.transform;
		eRT.anchorMin = eRT.anchorMax = new Vector2(1f, 0.5f);
		eRT.pivot = new Vector2(1f, 0.5f);
		eRT.sizeDelta = new Vector2(320f, btnH);
		eRT.anchoredPosition = new Vector2(-20f - 260f - 16f, 0f);
		engineButton.onClick.AddListener(OnEngineClicked);
	}

	// =========================================================
	// Selection
	// =========================================================
	void Select(int i)
	{
		if (confirmed || i < 0 || i >= cars.Count) return;
		selected = i;

		if (AudioManager.Instance != null) AudioManager.Instance.StopEnginePreview();
		RefreshSelection();
	}

	void RefreshSelection()
	{
		for (int i = 0; i < cellFrames.Count; i++)
			cellFrames[i].color = (i == selected) ? cellSelectedColor : cellColor;

		bool has = selected >= 0 && selected < cars.Count && cars[selected].prefab != null;

		if (bigPreview != null)
		{
			var tex = (has && selected < previews.Count) ? previews[selected] : null;
			bigPreview.texture = tex;
			bigPreview.color = tex != null ? Color.white : Color.clear;
		}
		if (nameText != null) nameText.text = has ? GetName(selected) : "เลือกรถ";
		if (startButton != null) startButton.interactable = has;
		if (engineButton != null) engineButton.interactable = has && HasEnginePreview(selected);
	}

	string GetName(int i)
	{
		var o = cars[i];
		if (!string.IsNullOrWhiteSpace(o.displayName)) return o.displayName;
		return o.prefab != null ? o.prefab.name : "Car " + (i + 1);
	}

	bool HasEnginePreview(int i)
	{
		var set = cars[i].prefab != null ? cars[i].prefab.soundSet : null;
		return set != null && set.TryGetPreview(out _, out _, out _);
	}

	void OnEngineClicked()
	{
		if (selected < 0 || AudioManager.Instance == null) return;
		var set = cars[selected].prefab != null ? cars[selected].prefab.soundSet : null;
		if (set == null) return;

		if (set.TryGetPreview(out var clip, out var pMin, out var pMax))
			AudioManager.Instance.PlayEnginePreview(clip, pMin, pMax, enginePreviewSeconds);
	}

	void OnStartClicked()
	{
		if (confirmed || selected < 0) return;
		var prefab = cars[selected].prefab;
		if (prefab == null) return;

		confirmed = true;

		var am = AudioManager.Instance;
		if (am != null)
		{
			am.StopEnginePreview();
			am.StopMusic();          
		}

		SpawnCar(prefab);
		selectionRoot.SetActive(false);
		foreach (var go in hideWhileSelecting)
			if (go != null) go.SetActive(true);
		if (nameTextOverride != null) nameTextOverride.gameObject.SetActive(false);

		if (PauseMenu.Instance != null) PauseMenu.Instance.LockMouse();
		else { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

		if (GameManager.Instance != null) GameManager.Instance.BeginCountdown();
	}

	void SpawnCar(CarController prefab)
	{
		bool custom = false;
		Vector3 pos = Vector3.zero;
		float heading = 0f;

		if (spawnPoint != null)
		{
			custom = true;
			pos = spawnPoint.position;
			heading = spawnPoint.eulerAngles.y;
		}
		else if (hasPlacedPose)
		{
			custom = true;
			pos = placedPos;
			heading = placedHeading;
		}
		else if (path != null && path.IsClosed) pos = path.StartPoint;

		var rot = Quaternion.Euler(0f, heading + prefab.modelYawOffset, 0f);
		var car = Instantiate(prefab, pos, rot);
		car.startAtPathStart = !custom;
		car.gameObject.tag = "Player";
		car.path = path;
		SpawnedCar = car;

		var hud = FindFirstObjectByType<RaceHUD>();
		if (hud != null)
		{
			hud.car = car;
			if (hud.path == null) hud.path = path;
		}

		if (AudioManager.Instance != null) AudioManager.Instance.RefreshCar();

		PlayerCarSpawned?.Invoke(car);
	}

	// =========================================================
	// Countdown / music
	// =========================================================
	void OnCountdownTick(int n)
	{
		if (countdownText == null) return;

		if (hideGoRoutine != null) { StopCoroutine(hideGoRoutine); hideGoRoutine = null; }
		countdownText.gameObject.SetActive(true);

		if (n > 0) countdownText.text = n.ToString();
		else
		{
			countdownText.text = "GO!";
			hideGoRoutine = StartCoroutine(HideCountdownAfter(0.8f));
		}
	}

	IEnumerator HideCountdownAfter(float sec)
	{
		yield return new WaitForSecondsRealtime(sec);
		if (countdownText != null) countdownText.gameObject.SetActive(false);
		hideGoRoutine = null;
	}

	void OnGameStarted()
	{
		if (AudioManager.Instance != null && !string.IsNullOrEmpty(raceMusicId))
			AudioManager.Instance.PlayMusic(raceMusicId);
	}

	void UnlockMouse()
	{
		if (PauseMenu.Instance != null) PauseMenu.Instance.UnlockMouse();
		else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
	}

	// =========================================================
	// UI helpers
	// =========================================================
	static RectTransform NewRect(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		var rt = (RectTransform)go.transform;
		rt.SetParent(parent, false);
		return rt;
	}

	static void FullStretch(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	static Image NewImage(string name, Transform parent, Color color)
	{
		var rt = NewRect(name, parent);
		var img = rt.gameObject.AddComponent<Image>();
		img.color = color;
		return img;
	}

	static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, TextAlignmentOptions align, Color color)
	{
		var rt = NewRect(name, parent);
		var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
		t.text = text;
		t.fontSize = size;
		t.alignment = align;
		t.color = color;
		t.raycastTarget = false;
		return t;
	}

	static Button NewButton(string name, Transform parent, string label, Color color)
	{
		var img = NewImage(name, parent, color);
		var btn = img.gameObject.AddComponent<Button>();
		btn.targetGraphic = img;

		var txt = NewText("Label", img.transform, label, 34f, TextAlignmentOptions.Center, Color.white);
		txt.fontStyle = FontStyles.Bold;
		FullStretch(txt.rectTransform);
		return btn;
	}
}