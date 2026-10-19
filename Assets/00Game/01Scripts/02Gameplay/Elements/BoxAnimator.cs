using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class BoxAnimator
{
	private const string DoneClipName = "BoxSingleDone";
	private const string ClickClipName = "Skeleton|cube_click";
	private const string StaticClipName = "Skeleton|cube_static";

	private readonly Box _box;
	private readonly Animation _anim;
	private readonly Transform _boxCard;
	private readonly AudioSource _audioSource;

	private Cube _finishCube;
	private bool _onPlayingAnim;
	private bool _finishAnimationPending;
	private float? _finishPresentationTime;
	private readonly List<Cube> _addAnimationCubes = new List<Cube>();

	public BoxAnimator(Box box, Animation anim, Transform boxCard, AudioSource audioSource)
	{
		_box = box;
		_anim = anim;
		_boxCard = boxCard;
		_audioSource = audioSource;
	}

	public Cube FinishCube => _finishCube;

	public bool IsFinishAnimationPlaying
	{
		get
		{
			if (_box == null || !_box.isActiveAndEnabled) return false;
			if (_box.IsSurpriseTransformationPlaying || _finishAnimationPending ||
				(_finishCube != null && _finishCube.IsFinishAnimationPlaying)) return true;
			for (int i = 0; i < _box.Cubes.Count; i++)
			{
				Cube cube = _box.Cubes[i];
				if (cube != null && cube.IsFinishAnimationPlaying) return true;
			}
			return _anim != null && _anim.IsPlaying(DoneClipName);
		}
	}

	public void SetFinishPresentationTime(float? time)
	{
		_finishPresentationTime = time;
	}

	public void Reset()
	{
		_onPlayingAnim = false;
		_addAnimationCubes.Clear();
		_finishAnimationPending = false;
		_finishPresentationTime = null;
		_finishCube = null;
	}

	public void OnDisable()
	{
		_onPlayingAnim = false;
		_addAnimationCubes.Clear();
		_finishAnimationPending = false;
		_finishPresentationTime = null;
	}

	public void PlayPopInAudio()
	{
		GameLoader loader = _box != null ? _box.GameLoader : null;
		if (loader == null || GameAudioPlayer.Current == null) return;
		int combo = GameController.Current == null ? 0 : GameController.Current.ComboInCount;
		GameAudioPlayer.Current.PlaySlicePopIn(_audioSource, loader.SlicePopIn,
			GameplayFeedbackRules.GetPopInPitch(combo));
	}

	public void PlayJumpAudio()
	{
		GameLoader loader = _box != null ? _box.GameLoader : null;
		if (loader == null || GameAudioPlayer.Current == null) return;
		GameAudioPlayer.Current.PlaySlicePopOut(_audioSource, loader.SlicePopOut, loader.SlicePopOutPitch);
	}

	public void PlayClickedAnim(bool isPopup, IReadOnlyList<Cube> cubes)
	{
		if (_onPlayingAnim) return;
		GameVibrationHelper.PlayBoxTap(isPopup);
		string clip = isPopup ? ClickClipName : StaticClipName;
		if (_anim != null && _anim.GetClip(clip) != null) _anim.Play(clip);
		if (cubes != null)
		{
			for (int i = 0; i < cubes.Count; i++)
			{
				Cube cube = cubes[i];
				if (cube != null) cube.PlayAnim(clip);
			}
		}
	}

	public async UniTask PlayAddCubeAnim(IReadOnlyList<Cube> cubes, int version)
	{
		if (_onPlayingAnim || _box == null || !_box.isActiveAndEnabled) return;
		_onPlayingAnim = true;
		try
		{
			_addAnimationCubes.Clear();
			if (cubes != null)
			{
				for (int i = 0; i < cubes.Count; i++)
				{
					Cube cube = cubes[i];
					if (cube != null && cube.Info != null && cube.Info.Size == GameData.SliceCountPerGroup)
						_addAnimationCubes.Add(cube);
				}
			}
			for (int i = _addAnimationCubes.Count - 1; i >= 0; i--)
			{
				if (_addAnimationCubes[i] != null) _addAnimationCubes[i].PlayAnim("Skeleton|cube_one");
				if (!await _box.WaitForRoutine(0.02f, version)) return;
			}
			await _box.WaitForRoutine(2f, version);
		}
		finally
		{
			if (_box != null && version == _box.RoutineVersion)
			{
				_onPlayingAnim = false;
				_addAnimationCubes.Clear();
			}
		}
	}

	public void PlayShuffleLayout(IReadOnlyList<Cube> cubes, float duration, int version, Action onComplete)
	{
		if (_box == null || _box.Info == null) return;
		_box.Info.State = BoxState.OnUnlocking;
		if (cubes != null)
		{
			for (int i = 0; i < cubes.Count; i++)
			{
				Cube cube = cubes[i];
				if (cube != null && cube.Info != null) cube.PlayShuffleMove(duration);
			}
		}
		FinishShuffleLayout(Mathf.Max(0.01f, duration), version, onComplete).Forget();
	}

	private async UniTask FinishShuffleLayout(float duration, int version, Action onComplete)
	{
		if (_box == null || !await _box.WaitForRoutine(duration, version)) return;
		if (_box.Info != null) _box.Info.State = BoxState.Free;
		onComplete?.Invoke();
	}

	public void PlayCubeAddEffect(Color color, Vector3 targetPos)
	{
		GameLoader loader = _box != null ? _box.GameLoader : null;
		if (loader == null || loader.BoxNormalEffect == null || _box == null || _box.Info == null) return;

		PooledParticleEffect pooled = loader.GetPooledGameObject<PooledParticleEffect>(
			"BoxNormalEffect", loader.BoxNormalEffect, loader.transform);
		if (pooled != null)
		{
			pooled.Play(targetPos, _box.transform.rotation, color);
		}
		else
		{
			GameObject effect = UnityEngine.Object.Instantiate(loader.BoxNormalEffect, _box.transform);
			effect.transform.position = targetPos;
			GameplayEffects.PlaySlicePopIn(effect, color);
		}
	}

	public async UniTask PlayFullAnim(float interval, int combo, int finished, int version)
	{
		_finishAnimationPending = true;
		try
		{
			float delay = _finishPresentationTime.HasValue
				? Mathf.Max(0f, _finishPresentationTime.Value - Time.time)
				: BoxFinishRules.FinishDelay;
			if (delay > 0f && !await _box.WaitForRoutine(delay, version)) return;
			if (_box == null || !_box.IsCurrentRoutine(version) || _box.Info == null) return;

			CreateFinishCube(_box.Info.GetTopType());

			for (int i = 0; i < _box.Cubes.Count; i++)
			{
				Cube cube = _box.Cubes[i];
				if (cube != null) cube.PlayDismissAnim();
			}

			PlayBoxFinishEffect(combo, interval, finished);

			GameLoader loader = _box != null ? _box.GameLoader : null;
			if (loader != null && GameAudioPlayer.Current != null)
				GameAudioPlayer.Current.PlayBoxAudio(_audioSource, loader.MatchClip);
		}
		finally
		{
			if (_box != null && version == _box.RoutineVersion)
				_finishAnimationPending = false;
		}
	}

	public void PlayBoxFinishEffect(int combo, float interval, int finished)
	{
		GameLoader loader = _box != null ? _box.GameLoader : null;
		if (loader == null || _box == null || _box.Info == null) return;

		if (loader.BoxFinishEffect != null)
		{
			GameObject effect = UnityEngine.Object.Instantiate(loader.BoxFinishEffect, _box.transform);
			if (_box.Info.BoxType == BoxType.Long) effect.transform.localPosition = new Vector3(0f, 0.9f, -0.8f);
			Transform text = HierarchyReferenceCache.Child(effect.transform, "Text");
			bool show = BoxFinishRules.ShouldShowCombo(interval, finished) &&
				(GameAssetsConfig.Instance == null || GameAssetsConfig.Instance.EnableBoxComboEffects);
			bool emoji = BoxFinishRules.UseEmoji(interval, finished);
			if (text != null)
			{
				text.gameObject.SetActive(show && !emoji);
				SpriteRenderer square = text.GetComponentInChildren<SpriteRenderer>(true);
				if (square != null)
				{
					square.sprite = GameAssetsConfig.GetBoxFinishSprite(combo);
					if (show && !emoji) GameplayEffects.BringComboToFront(square);
					Camera view = GameController.Current != null ? GameController.Current.MainCamera : Camera.main;
					if (view != null) square.transform.rotation = view.transform.rotation;
					square.gameObject.SetActive(square.sprite != null);
				}
			}
			GameplayEffects.PlayParticles(effect);
			if (show && emoji && loader.BoxComboEffect != null)
			{
				GameObject comboEffect = UnityEngine.Object.Instantiate(loader.BoxComboEffect, _box.transform);
				comboEffect.transform.localPosition = new Vector3(0f, 2f, 0f);
				GameplayEffects.PlayCombo(comboEffect, BoxFinishRules.GetEmojiVariant(combo));
			}
		}
	}

	public void CreateFinishCube(int type)
	{
		GameLoader loader = _box != null ? _box.GameLoader : null;
		if (_finishCube != null || loader == null || _box == null || _box.Info == null) return;

		GameObject prefab = _box.Info.BoxType == BoxType.Long
			? loader.FinishLongCube
			: loader.FinishCube;
		if (prefab == null) return;

		Transform parent = _boxCard != null ? _boxCard : _box.transform;
		GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
		_finishCube = instance.GetComponent<Cube>();
		if (_finishCube == null)
		{
			UnityEngine.Object.Destroy(instance);
			return;
		}

		_finishCube.transform.SetPositionAndRotation(_box.GetFinishCubePos(), _box.transform.rotation);
		_finishCube.InitFinish(type);
		_finishCube.PlayAnim("Skeleton|cube_all");
		if (_box.Info.BoxType == BoxType.Single && _anim != null && _anim.GetClip(DoneClipName) != null)
			_anim.Play(DoneClipName);
	}
}
