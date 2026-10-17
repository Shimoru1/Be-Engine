using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// เครื่องมือตัดช่วงไฟล์เสียง (Editor only): Tools > Audio Editor
///  - ดูคลื่นเสียง, ลากกำหนดช่วง Start/End, ซูมด้วยล้อเมาส์
///  - ฟังตัวอย่างหลังตัด (รวม fade/gain), snap ที่ zero crossing กันเสียงแตก
///  - บันทึกเป็นไฟล์ใหม่ หรือเขียนทับไฟล์เดิม (WAV) โดยคง GUID เดิม reference ในซีนไม่หลุด
/// หมายเหตุ: Load Type ของคลิปต้องเป็น Decompress On Load ถึงจะอ่านข้อมูลเสียงได้
/// </summary>
public class AudioEditorWindow : EditorWindow
{
	AudioClip clip;
	float[] samples;               // interleaved
	int channels, frequency, totalFrames;
	string error;

	float startSec, endSec;
	float viewStart, viewEnd;
	float fadeInMs, fadeOutMs;
	float gain = 1f;
	bool snapZero = true;
	string suffix = "_trim";

	int dragMode;                  // 0 = ไม่ลาก, 1 = Start, 2 = End, 3 = เลือกช่วงใหม่
	float dragAnchor, prevStart, prevEnd;

	AudioClip previewClip;
	bool playing;
	double playStartTime;
	float playFromSec, playDur;

	float Length => frequency > 0 ? (float)totalFrames / frequency : 0f;

	[MenuItem("Tools/Audio Editor")]
	static void Open()
	{
		var w = GetWindow<AudioEditorWindow>("Audio Editor");
		w.minSize = new Vector2(480, 520);
	}

	void OnEnable() { EditorApplication.update += OnEditorUpdate; }

	void OnDisable()
	{
		EditorApplication.update -= OnEditorUpdate;
		StopPreview();
	}

	void OnEditorUpdate()
	{
		if (!playing) return;
		if (EditorApplication.timeSinceStartup - playStartTime > playDur + 0.1) playing = false;
		Repaint();
	}

	// ---------------- โหลดคลิป ----------------
	void LoadClip(AudioClip c)
	{
		StopPreview();
		clip = c;
		samples = null;
		error = null;
		if (c == null) return;
		try
		{
			c.LoadAudioData();
			channels = c.channels;
			frequency = c.frequency;
			totalFrames = c.samples;
			samples = new float[totalFrames * channels];
			if (!c.GetData(samples, 0))
			{
				samples = null;
				error = "อ่านข้อมูลเสียงไม่ได้ - ตั้ง Load Type ของคลิปเป็น Decompress On Load ใน Import Settings แล้วกด Retry";
				return;
			}
		}
		catch (Exception ex)
		{
			samples = null;
			error = ex.Message;
			return;
		}
		startSec = 0f;
		endSec = Length;
		viewStart = 0f;
		viewEnd = Length;
	}

	// ---------------- GUI ----------------
	void OnGUI()
	{
		EditorGUILayout.Space(4);
		EditorGUI.BeginChangeCheck();
		var c = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", clip, typeof(AudioClip), false);
		if (EditorGUI.EndChangeCheck()) LoadClip(c);

		using (new EditorGUI.DisabledScope(!(Selection.activeObject is AudioClip)))
			if (GUILayout.Button("Use Clip Selected in Project"))
				LoadClip((AudioClip)Selection.activeObject);

		if (clip == null)
		{
			EditorGUILayout.HelpBox("ลากไฟล์เสียงมาใส่ หรือเลือกไฟล์ใน Project แล้วกดปุ่มด้านบน", MessageType.Info);
			return;
		}
		if (samples == null)
		{
			EditorGUILayout.HelpBox(error ?? "โหลดไม่สำเร็จ", MessageType.Error);
			if (GUILayout.Button("Retry")) LoadClip(clip);
			return;
		}

		EditorGUILayout.LabelField($"{channels} ch   |   {frequency} Hz   |   ความยาว {Length:0.000} s", EditorStyles.miniLabel);

		Rect wave = GUILayoutUtility.GetRect(10f, 200f, GUILayout.ExpandWidth(true));
		DrawWaveform(wave);
		EditorGUILayout.LabelField("ลากที่เส้นขาวเพื่อปรับ Start/End, ลากในที่ว่างเพื่อเลือกช่วงใหม่, ล้อเมาส์ = ซูม", EditorStyles.miniLabel);

		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button("Zoom to Selection"))
			{
				viewStart = startSec;
				viewEnd = Mathf.Max(endSec, startSec + 0.01f);
			}
			if (GUILayout.Button("Show All")) { viewStart = 0f; viewEnd = Length; }
		}

		EditorGUILayout.Space(6);
		EditorGUI.BeginChangeCheck();
		float s = EditorGUILayout.FloatField("Start (s)", startSec);
		float e = EditorGUILayout.FloatField("End (s)", endSec);
		EditorGUILayout.MinMaxSlider(ref s, ref e, 0f, Length);
		if (EditorGUI.EndChangeCheck())
		{
			startSec = Mathf.Clamp(s, 0f, Length);
			endSec = Mathf.Min(Length, Mathf.Max(e, startSec + 0.001f));
		}
		EditorGUILayout.LabelField($"ความยาวหลังตัด: {endSec - startSec:0.000} s", EditorStyles.miniLabel);

		EditorGUILayout.Space(6);
		fadeInMs = Mathf.Max(0f, EditorGUILayout.FloatField("Fade In (ms)", fadeInMs));
		fadeOutMs = Mathf.Max(0f, EditorGUILayout.FloatField("Fade Out (ms)", fadeOutMs));
		gain = EditorGUILayout.Slider("Gain", gain, 0f, 2f);
		snapZero = EditorGUILayout.Toggle("Snap ที่ Zero Crossing (ตอนปล่อยเมาส์)", snapZero);
		if (GUILayout.Button("Snap Start/End ที่ Zero Crossing ตอนนี้")) SnapSelection();

		EditorGUILayout.Space(6);
		using (new EditorGUILayout.HorizontalScope())
		{
			if (GUILayout.Button("▶ Play Selection", GUILayout.Height(26))) PlaySelection();
			if (GUILayout.Button("▶ Play Full", GUILayout.Height(26))) PlayFull();
			if (GUILayout.Button("■ Stop", GUILayout.Height(26))) StopPreview();
		}

		EditorGUILayout.Space(6);
		string src = AssetDatabase.GetAssetPath(clip);
		bool isAsset = !string.IsNullOrEmpty(src);
		bool isWav = isAsset && src.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);

		suffix = EditorGUILayout.TextField("ต่อท้ายชื่อไฟล์ใหม่", suffix);
		using (new EditorGUI.DisabledScope(!isAsset))
		{
			if (GUILayout.Button("Save as New WAV", GUILayout.Height(28))) SaveAsNew(src);
		}
		using (new EditorGUI.DisabledScope(!isWav))
		{
			if (GUILayout.Button("Overwrite Original (WAV only)")) Overwrite(src);
		}
		if (isAsset && !isWav)
			EditorGUILayout.HelpBox("ไฟล์ต้นฉบับไม่ใช่ .wav จึงเขียนทับไม่ได้ ใช้ Save as New WAV แทน", MessageType.None);
	}

	// ---------------- คลื่นเสียง + ลากเลือกช่วง ----------------
	float TimeToX(Rect r, float t) { return r.x + (t - viewStart) / Mathf.Max(0.0001f, viewEnd - viewStart) * r.width; }
	float XToTime(Rect r, float x) { return viewStart + (x - r.x) / r.width * (viewEnd - viewStart); }

	void DrawWaveform(Rect rect)
	{
		var ev = Event.current;
		int id = GUIUtility.GetControlID(FocusType.Passive);

		if (ev.type == EventType.Repaint)
		{
			EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));

			float span = viewEnd - viewStart;
			float mid = rect.center.y;
			float half = rect.height * 0.5f - 2f;
			int w = Mathf.Max(1, (int)rect.width);
			var waveColor = new Color(0.35f, 0.75f, 1f);

			for (int x = 0; x < w; x++)
			{
				int a = Mathf.Clamp(Mathf.FloorToInt((viewStart + span * x / w) * frequency), 0, totalFrames - 1);
				int b = Mathf.Clamp(Mathf.FloorToInt((viewStart + span * (x + 1) / w) * frequency), a + 1, totalFrames);
				int step = Mathf.Max(1, (b - a) / 64);
				float mn = 0f, mx = 0f;
				for (int f = a; f < b; f += step)
				{
					int i = f * channels;
					for (int ch = 0; ch < channels; ch++)
					{
						float v = samples[i + ch];
						if (v < mn) mn = v;
						if (v > mx) mx = v;
					}
				}
				float top = mid - Mathf.Clamp(mx, -1f, 1f) * half;
				float bot = mid - Mathf.Clamp(mn, -1f, 1f) * half;
				EditorGUI.DrawRect(new Rect(rect.x + x, top, 1f, Mathf.Max(1f, bot - top)), waveColor);
			}

			// ส่วนนอกช่วงที่เลือก = มืด
			float sx = TimeToX(rect, startSec), ex = TimeToX(rect, endSec);
			var dim = new Color(0f, 0f, 0f, 0.55f);
			float cl = Mathf.Clamp(sx, rect.xMin, rect.xMax);
			float cr = Mathf.Clamp(ex, rect.xMin, rect.xMax);
			EditorGUI.DrawRect(Rect.MinMaxRect(rect.xMin, rect.yMin, cl, rect.yMax), dim);
			EditorGUI.DrawRect(Rect.MinMaxRect(cr, rect.yMin, rect.xMax, rect.yMax), dim);

			// เส้น Start / End
			if (sx >= rect.xMin && sx <= rect.xMax)
			{
				EditorGUI.DrawRect(new Rect(sx - 1f, rect.y, 2f, rect.height), Color.white);
				EditorGUIUtility.AddCursorRect(new Rect(sx - 5f, rect.y, 10f, rect.height), MouseCursor.ResizeHorizontal);
			}
			if (ex >= rect.xMin && ex <= rect.xMax)
			{
				EditorGUI.DrawRect(new Rect(ex - 1f, rect.y, 2f, rect.height), Color.white);
				EditorGUIUtility.AddCursorRect(new Rect(ex - 5f, rect.y, 10f, rect.height), MouseCursor.ResizeHorizontal);
			}

			// playhead
			if (playing)
			{
				float t = playFromSec + (float)(EditorApplication.timeSinceStartup - playStartTime);
				float px = TimeToX(rect, t);
				if (px >= rect.xMin && px <= rect.xMax)
					EditorGUI.DrawRect(new Rect(px, rect.y, 1f, rect.height), new Color(1f, 0.85f, 0.2f));
			}

			// เวลาขอบซ้าย/ขวาของมุมมอง
			GUI.Label(new Rect(rect.x + 4f, rect.yMax - 18f, 120f, 16f), viewStart.ToString("0.000") + " s", EditorStyles.miniLabel);
			var rs = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
			GUI.Label(new Rect(rect.xMax - 124f, rect.yMax - 18f, 120f, 16f), viewEnd.ToString("0.000") + " s", rs);
		}

		// ซูมด้วยล้อเมาส์
		if (ev.type == EventType.ScrollWheel && rect.Contains(ev.mousePosition))
		{
			float tm = XToTime(rect, ev.mousePosition.x);
			float z = 1f + ev.delta.y * 0.05f;
			float ns = tm - (tm - viewStart) * z;
			float ne = tm + (viewEnd - tm) * z;
			viewStart = Mathf.Max(0f, ns);
			viewEnd = Mathf.Min(Length, ne);
			if (viewEnd - viewStart < 0.005f) viewEnd = Mathf.Min(Length, viewStart + 0.005f);
			ev.Use();
			Repaint();
		}

		float minLen = 0.001f;
		switch (ev.GetTypeForControl(id))
		{
			case EventType.MouseDown:
				if (ev.button == 0 && rect.Contains(ev.mousePosition))
				{
					GUIUtility.hotControl = id;
					prevStart = startSec;
					prevEnd = endSec;
					float t = XToTime(rect, ev.mousePosition.x);
					float tol = 8f / rect.width * (viewEnd - viewStart);
					if (Mathf.Abs(t - startSec) <= tol) dragMode = 1;
					else if (Mathf.Abs(t - endSec) <= tol) dragMode = 2;
					else { dragMode = 3; dragAnchor = Mathf.Clamp(t, 0f, Length); }
					ev.Use();
				}
				break;

			case EventType.MouseDrag:
				if (GUIUtility.hotControl == id)
				{
					float t = Mathf.Clamp(XToTime(rect, ev.mousePosition.x), 0f, Length);
					if (dragMode == 1) startSec = Mathf.Min(t, endSec - minLen);
					else if (dragMode == 2) endSec = Mathf.Max(t, startSec + minLen);
					else if (dragMode == 3)
					{
						startSec = Mathf.Min(dragAnchor, t);
						endSec = Mathf.Max(dragAnchor, t);
					}
					ev.Use();
					Repaint();
				}
				break;

			case EventType.MouseUp:
				if (GUIUtility.hotControl == id)
				{
					GUIUtility.hotControl = 0;
					if (endSec - startSec < minLen) { startSec = prevStart; endSec = prevEnd; } // แค่คลิก = ไม่เปลี่ยน
					else if (snapZero) SnapSelection();
					dragMode = 0;
					ev.Use();
					Repaint();
				}
				break;
		}
	}

	// ---------------- Zero crossing ----------------
	float Mono(int f)
	{
		float s = 0f;
		int i = f * channels;
		for (int ch = 0; ch < channels; ch++) s += samples[i + ch];
		return s;
	}

	bool IsCross(int f)
	{
		if (f <= 0 || f >= totalFrames) return false;
		return Mono(f - 1) * Mono(f) <= 0f;
	}

	int SnapFrame(int frame)
	{
		if (frame <= 0 || frame >= totalFrames) return frame;
		int range = Mathf.RoundToInt(frequency * 0.02f); // ค้นหา +-20 ms
		for (int k = 0; k <= range; k++)
		{
			if (IsCross(frame + k)) return frame + k;
			if (IsCross(frame - k)) return frame - k;
		}
		return frame;
	}

	void SnapSelection()
	{
		int a = SnapFrame(Mathf.RoundToInt(startSec * frequency));
		int b = SnapFrame(Mathf.RoundToInt(endSec * frequency));
		if (b <= a) return;
		startSec = (float)a / frequency;
		endSec = (float)b / frequency;
	}

	// ---------------- ประมวลผลช่วงที่เลือก ----------------
	float[] BuildTrimmed(out int frames)
	{
		int f0 = Mathf.Clamp(Mathf.RoundToInt(startSec * frequency), 0, totalFrames);
		int f1 = Mathf.Clamp(Mathf.RoundToInt(endSec * frequency), f0, totalFrames);
		frames = f1 - f0;
		var o = new float[frames * channels];
		Array.Copy(samples, f0 * channels, o, 0, o.Length);

		int fi = Mathf.Min(frames, Mathf.RoundToInt(fadeInMs * 0.001f * frequency));
		int fo = Mathf.Min(frames, Mathf.RoundToInt(fadeOutMs * 0.001f * frequency));
		for (int i = 0; i < fi; i++)
		{
			float g = i / (float)fi;
			for (int ch = 0; ch < channels; ch++) o[i * channels + ch] *= g;
		}
		for (int i = 0; i < fo; i++)
		{
			int idx = frames - 1 - i;
			float g = i / (float)fo;
			for (int ch = 0; ch < channels; ch++) o[idx * channels + ch] *= g;
		}
		if (!Mathf.Approximately(gain, 1f))
			for (int i = 0; i < o.Length; i++) o[i] = Mathf.Clamp(o[i] * gain, -1f, 1f);
		return o;
	}

	// ---------------- ฟังตัวอย่าง ----------------
	static readonly Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

	static void PlayPreviewClip(AudioClip c)
	{
		StopAllPreview();
		if (audioUtil == null) return;
		var args = new[] { typeof(AudioClip), typeof(int), typeof(bool) };
		var m = audioUtil.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, args, null)
			 ?? audioUtil.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public, null, args, null);
		if (m != null) m.Invoke(null, new object[] { c, 0, false });
		else Debug.LogWarning("AudioEditor: เวอร์ชัน Unity นี้ไม่มีฟังก์ชัน preview ที่ใช้อยู่");
	}

	static void StopAllPreview()
	{
		if (audioUtil == null) return;
		var m = audioUtil.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)
			 ?? audioUtil.GetMethod("StopAllClips", BindingFlags.Static | BindingFlags.Public);
		if (m != null) m.Invoke(null, null);
	}

	void StartPlayhead(float fromSec, float dur)
	{
		playing = true;
		playStartTime = EditorApplication.timeSinceStartup;
		playFromSec = fromSec;
		playDur = dur;
	}

	void PlaySelection()
	{
		StopPreview();
		var data = BuildTrimmed(out int frames);
		if (frames < 1) return;
		previewClip = AudioClip.Create("AudioEditorPreview", frames, channels, frequency, false);
		previewClip.SetData(data, 0);
		PlayPreviewClip(previewClip);
		StartPlayhead(startSec, (float)frames / frequency);
	}

	void PlayFull()
	{
		StopPreview();
		PlayPreviewClip(clip);
		StartPlayhead(0f, Length);
	}

	void StopPreview()
	{
		StopAllPreview();
		playing = false;
		if (previewClip != null) DestroyImmediate(previewClip);
		previewClip = null;
	}

	// ---------------- บันทึก ----------------
	static string ToFullPath(string assetPath)
	{
		return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath));
	}

	void SaveAsNew(string src)
	{
		var data = BuildTrimmed(out int frames);
		if (frames < 1) return;

		string dir = Path.GetDirectoryName(src);
		string name = Path.GetFileNameWithoutExtension(src) + suffix;
		string dst = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, name + ".wav").Replace('\\', '/'));

		WriteWav(ToFullPath(dst), data, channels, frequency);
		AssetDatabase.ImportAsset(dst);

		// คัดลอก import settings จากไฟล์เดิม
		var so = AssetImporter.GetAtPath(src) as AudioImporter;
		var no = AssetImporter.GetAtPath(dst) as AudioImporter;
		if (so != null && no != null)
		{
			no.forceToMono = so.forceToMono;
			no.defaultSampleSettings = so.defaultSampleSettings;
			no.loadInBackground = so.loadInBackground;
			no.SaveAndReimport();
		}

		var saved = AssetDatabase.LoadAssetAtPath<AudioClip>(dst);
		EditorGUIUtility.PingObject(saved);
		Debug.Log("AudioEditor: บันทึกแล้ว " + dst);
	}

	void Overwrite(string src)
	{
		if (!EditorUtility.DisplayDialog("เขียนทับไฟล์เดิม",
			"จะเขียนทับ " + src + "\nGUID เดิมยังอยู่ reference ในซีนไม่หลุด แต่ย้อนกลับไม่ได้ (ไฟล์จะถูกบันทึกเป็น WAV 16-bit)\nแนะนำให้สำรองไฟล์ก่อน",
			"Overwrite", "Cancel")) return;

		var data = BuildTrimmed(out int frames);
		if (frames < 1) return;

		StopPreview();
		WriteWav(ToFullPath(src), data, channels, frequency);
		AssetDatabase.ImportAsset(src, ImportAssetOptions.ForceUpdate);
		LoadClip(AssetDatabase.LoadAssetAtPath<AudioClip>(src));
		Debug.Log("AudioEditor: เขียนทับแล้ว " + src);
	}

	static void WriteWav(string path, float[] data, int channels, int freq)
	{
		int dataBytes = data.Length * 2;
		using (var fs = new FileStream(path, FileMode.Create))
		using (var w = new BinaryWriter(fs))
		{
			w.Write(Encoding.ASCII.GetBytes("RIFF"));
			w.Write(36 + dataBytes);
			w.Write(Encoding.ASCII.GetBytes("WAVE"));
			w.Write(Encoding.ASCII.GetBytes("fmt "));
			w.Write(16);
			w.Write((short)1);                       // PCM
			w.Write((short)channels);
			w.Write(freq);
			w.Write(freq * channels * 2);
			w.Write((short)(channels * 2));
			w.Write((short)16);
			w.Write(Encoding.ASCII.GetBytes("data"));
			w.Write(dataBytes);
			for (int i = 0; i < data.Length; i++)
			{
				int v = Mathf.RoundToInt(Mathf.Clamp(data[i], -1f, 1f) * 32767f);
				w.Write((short)Mathf.Clamp(v, -32768, 32767));
			}
		}
	}
}