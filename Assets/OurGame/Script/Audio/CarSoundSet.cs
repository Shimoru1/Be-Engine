using UnityEngine;

[CreateAssetMenu(fileName = "CarSoundSet", menuName = "Audio/Car Sound Set")]
public class CarSoundSet : ScriptableObject
{
	[Tooltip("ช่อง 0 = เกียร์ 1 ... ช่อง 5 = เกียร์ 6")]
	public AudioManager.GearSound[] gearSounds = new AudioManager.GearSound[6];

	[Header("Brake")]
	public AudioClip brakeClip;
	[Range(0f, 1f)] public float brakeVolume = 1f;

	[Header("Select screen (ปุ่ม Engine Sound)")]
	[Tooltip("ว่าง = ใช้คลิปเครื่องของเกียร์แรกที่มีคลิป")]
	public AudioClip previewClip;
	public float previewPitchMin = 0.8f;
	public float previewPitchMax = 1.6f;

	public bool TryGetPreview(out AudioClip clip, out float pMin, out float pMax)
	{
		if (previewClip != null)
		{
			clip = previewClip; pMin = previewPitchMin; pMax = previewPitchMax;
			return true;
		}

		if (gearSounds != null)
		{
			foreach (var gs in gearSounds)
			{
				if (gs != null && gs.engineClip != null)
				{
					clip = gs.engineClip; pMin = gs.enginePitchMin; pMax = gs.enginePitchMax;
					return true;
				}
			}
		}

		clip = null; pMin = pMax = 1f;
		return false;
	}
}