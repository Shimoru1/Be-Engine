using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LapTimerUI : MonoBehaviour
{
	[Header("เวลารอบปัจจุบัน")]
	[Tooltip("ลาก TMP Text ที่จัดเองมาใส่ได้ (ว่าง = สร้างให้)")]
	public TMP_Text textOverride;
	public Vector2 offset = new Vector2(0f, -40f);
	public float fontSize = 64f;
	public Color color = Color.white;

	[Header("เวลารอบที่เพิ่งจบ (สีเขียว)")]
	public TMP_Text resultTextOverride;
	public Vector2 resultOffset = new Vector2(0f, -130f);
	public float resultFontSize = 72f;
	public Color lapColor = new Color(0.2f, 1f, 0.3f, 1f);
	[Tooltip("โชว์เวลารอบกี่วินาทีแล้วหายไป")]
	public float resultShowSeconds = 3f;

	[Header("Display")]
	[Tooltip("ติ๊ก = ซ่อนตัวจับเวลาจนกว่าจะผ่านเส้นชัยครั้งแรก")]
	public bool hideUntilFirstCross = false;
	public int sortingOrder = 90;

	TMP_Text text, resultText;
	bool racing;          
	bool timing;          
	float lapTime;
	float resultTimer;

	void Start()
	{
		Canvas canvas = null;
		if (textOverride == null || resultTextOverride == null)
		{
			var c = new GameObject("LapTimerCanvas");
			c.transform.SetParent(transform, false);
			canvas = c.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = sortingOrder;
			var sc = c.AddComponent<CanvasScaler>();
			sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			sc.referenceResolution = new Vector2(1920f, 1080f);
			sc.matchWidthOrHeight = 0.5f;
		}

		text = textOverride != null ? textOverride : MakeText(canvas.transform, "LapTime", offset, fontSize, color);
		resultText = resultTextOverride != null ? resultTextOverride : MakeText(canvas.transform, "LapResult", resultOffset, resultFontSize, lapColor);
		resultText.color = lapColor;

		text.gameObject.SetActive(false);
		resultText.gameObject.SetActive(false);

		var gm = GameManager.Instance;
		if (gm != null) gm.GameStarted += OnGameStarted;
		FinishLine.CarCrossed += OnCarCrossed;
	}

	static TMP_Text MakeText(Transform parent, string name, Vector2 pos, float size, Color col)
	{
		var go = new GameObject(name, typeof(RectTransform));
		var rt = (RectTransform)go.transform;
		rt.SetParent(parent, false);
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.sizeDelta = new Vector2(900f, 100f);
		rt.anchoredPosition = pos;

		var t = go.AddComponent<TextMeshProUGUI>();
		t.fontSize = size;
		t.color = col;
		t.alignment = TextAlignmentOptions.Center;
		t.fontStyle = FontStyles.Bold;
		t.raycastTarget = false;
		return t;
	}

	void OnDestroy()
	{
		var gm = GameManager.Instance;
		if (gm != null) gm.GameStarted -= OnGameStarted;
		FinishLine.CarCrossed -= OnCarCrossed;
	}

	void OnGameStarted()
	{
		racing = true;
		timing = false;
		lapTime = 0f;
		if (text != null)
		{
			text.text = Format(0f);
			text.gameObject.SetActive(!hideUntilFirstCross);
		}
	}

	void OnCarCrossed(CarController car)
	{
		if (!racing) return;
		var gm = GameManager.Instance;
		if (gm != null && !gm.IsPlaying()) return;

		if (!timing)
		{
			timing = true;
			lapTime = 0f;
			if (text != null) text.gameObject.SetActive(true);
			return;
		}

		if (resultText != null)
		{
			resultText.text = Format(lapTime);
			resultText.gameObject.SetActive(true);
			resultTimer = resultShowSeconds;
		}
		lapTime = 0f;
	}

	void Update()
	{
		var gm = GameManager.Instance;
		if (!racing || text == null) return;

		if (timing && gm != null && gm.IsPlaying())
			lapTime += Time.deltaTime;

		text.text = Format(lapTime);

		if (resultTimer > 0f)
		{
			resultTimer -= Time.deltaTime;
			if (resultTimer <= 0f && resultText != null) resultText.gameObject.SetActive(false);
		}
	}

	static string Format(float seconds)
	{
		int total = Mathf.FloorToInt(seconds * 1000f);
		int ms = total % 1000;
		int s = (total / 1000) % 60;
		int m = (total / 60000) % 60;
		int h = total / 3600000;
		return $"{h:00}.{m:00}.{s:00}.{ms:000}";
	}
}