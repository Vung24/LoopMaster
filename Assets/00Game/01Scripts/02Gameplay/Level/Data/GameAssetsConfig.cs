using UnityEngine;

[CreateAssetMenu(fileName = "GameAssetsConfig", menuName = "ScriptableObjects/SpawnGameAssetsConfig")]
public class GameAssetsConfig : ScriptableObject
{
	private static GameAssetsConfig _instance;
	private static readonly Color[] DefaultCubeColors =
	{
		new Color(1f, 0.83265305f, 0.020408154f, 1f),
		new Color(0.47941464f, 0.84f, 0.16799998f, 1f),
		new Color(1f, 0.31f, 0.6707843f, 1f),
		new Color(0.1273585f, 0.8030578f, 1f, 1f),
		new Color(0.98f, 0.88984f, 0.84672004f, 1f),
		new Color(0.041475415f, 0.4901641f, 0.92f, 1f),
		new Color(0.7304521f, 0.255f, 1f, 1f),
		new Color(0.936f, 0.18321703f, 0.18321703f, 1f),
		new Color(1f, 0.5058824f, 0.05098039f, 1f),
		new Color(0.27310407f, 0.808f, 0.6095166f, 1f),
		new Color(0.11557377f, 0.47f, 0.30434427f, 1f),
		new Color(0.509434f, 0.2780895f, 0.1321645f, 1f),
		new Color(0.7075472f, 0.6164661f, 0.5506853f, 1f),
		new Color(0.22352941f, 0.23529412f, 0.2509804f, 1f)
	};

	public Material[] CubeMats;
	public GameObject BoxFinishEffectPrefab;
	public GameObject BoxNormalEffectPrefab;
	public GameObject ToolShuffleMaskPrefab;
	public bool EnableBoxComboEffects = true;
	public AudioClip SlicePopIn;
	public AudioClip SlicePopOut;
	public AudioClip Match;
	public AudioClip Match2;
    [Tooltip("Offline equivalent of the native GameBoxEffect A/B group B: use Match2.")]
    public bool UseAlternateMatchAudio;
	public AudioClip ToolUndoAudio;
	public AudioClip ToolShuffleAudio;
	public AudioClip ToolPathAudio;
	public AudioClip StartShakeAudio;
	public AudioClip Fail;
	public AudioClip FailShow;
	public AudioClip OnlyEnterRockBlast;
	public AudioClip ColorLockOpen;
	public AudioClip AccelerateAudio;
	public AudioClip DecelerateAudio;
	public AudioClip AccelerateShortAudio;
	public AudioClip ButtonClickAudio;
	public AudioClip HomeBgm;
	public AudioClip GameBgm;
	public AudioClip[] CommonAudioClips;
	public Sprite SpeedImage;
	public Sprite UnSpeedImage;
	public Material[] LongBoxCubeMats;
	public Sprite LongBoxCubeSprite;
	public Material[] LongBoxSliceMats;
	public Material[] SliceMats;
	public Material[] SliceSurpriseMats;
	public Material[] ColorLockMats;
	public Material[] SurpriseCubeMats;
	public Sprite[] CubeSprites;
	public Sprite CubeFinishSprite;
	public Material[] GroupColorLockMats;
	public Color[] LongBoxCubeColors;
	public Color[] CubeColors;
	public Color[] SurpriseCubeColors;
	public Material SecretMat;
	public Sprite SecretSprite;
	public Sprite SupriseSprite;
	public Sprite[] BoxFinishEffectSprites;

	public static GameAssetsConfig Instance
	{
		get
		{
			if (_instance == null) _instance = Resources.Load<GameAssetsConfig>("GameAssetsConfig");
			return _instance;
		}
	}

	public static Sprite GetBoxFinishSprite(int combo)
	{
		Sprite[] sprites = Instance?.BoxFinishEffectSprites;
		return GetAt(sprites, BoxFinishRules.GetSpriteIndex(combo, sprites == null ? 0 : sprites.Length));
	}

	public static Material GetMatByType(int type)
	{
		GameAssetsConfig config = Instance;
		if (config == null) return null;
		if (CubeData.IsLongBoxColor(type)) return GetAt(config.LongBoxCubeMats, type - 101);
		if (CubeData.IsSurpriseColor(type)) return GetAt(config.SurpriseCubeMats, type - 201);
		return GetAt(config.CubeMats, GetColorIndex(type));
	}

	public static Material GetSliceMatByType(int type)
	{
		GameAssetsConfig config = Instance;
		if (config == null) return null;
		if (CubeData.IsLongBoxColor(type)) return GetAt(config.LongBoxSliceMats, type - 101);
		if (CubeData.IsSurpriseColor(type)) return GetAt(config.SliceSurpriseMats, type - 201);
		return GetAt(config.SliceMats, GetColorIndex(type));
	}

	public static Material GetLockMatByType(int type) => GetAt(Instance?.ColorLockMats, GetColorIndex(type));
	public static Material GetGroupLockMatByType(int type) => GetAt(Instance?.GroupColorLockMats, GetColorIndex(type));

	public static Sprite GetCubeSpriteByType(int type)
	{
		GameAssetsConfig config = Instance;
		if (config == null) return null;
		return CubeData.IsLongBoxColor(type) ? config.LongBoxCubeSprite : GetAt(config.CubeSprites, GetColorIndex(type));
	}

	public static Material GetSecretMat() => Instance?.SecretMat;
	public static Sprite GetCubeSecretSprite() => Instance?.SecretSprite;
	public static Sprite GetCubeSurpriseSprite() => Instance?.SupriseSprite;
	public static Sprite GetCubeFinishSprite() => Instance?.CubeFinishSprite;

	public static Color GetCubeColor(int type)
	{
		GameAssetsConfig config = Instance;
		if (CubeData.IsLongBoxColor(type))
			return GetAt(config?.LongBoxCubeColors, type - 101, GetDefaultCubeColor(type - 101));
		if (CubeData.IsSurpriseColor(type))
			return GetAt(config?.SurpriseCubeColors, type - 201, GetDefaultCubeColor(type - 201));
		int index = GetColorIndex(type);
		return GetAt(config?.CubeColors, index, GetDefaultCubeColor(index));
	}

	private static int GetColorIndex(int type) => Mathf.Max(0, type - 1);

	private static T GetAt<T>(T[] values, int index) where T : Object =>
		values != null && index >= 0 && index < values.Length ? values[index] : null;

	private static Color GetAt(Color[] values, int index, Color fallback) =>
		values != null && index >= 0 && index < values.Length ? values[index] : fallback;

	private static Color GetDefaultCubeColor(int index) =>
		index >= 0 && index < DefaultCubeColors.Length ? DefaultCubeColors[index] : Color.white;
}
