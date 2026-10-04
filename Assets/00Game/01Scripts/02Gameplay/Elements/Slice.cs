using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

public class Slice : MonoBehaviour
{
	private static readonly Vector3 RoadScale = new Vector3(0.68f, 0.6f, 1.2f);
	private const float JumpArcHeight = GameplayFeedbackRules.SliceJumpHeight;

	public UnityEvent OnSliceDismiss = new UnityEvent();

	private SliceInfo _info;
	[SerializeField] private MeshRenderer _meshRenderer;
	private MaterialPropertyBlock _colorPropBlock;
	private Tween _positionTween;
	private Tween _rotationTween;
	private Tween _scaleTween;
	private bool _isJumping;
	[SerializeField] private Texture _longBoxSignTex;

	public SliceInfo Info => _info;
	public int CurPathIndex => _info == null ? -1 : _info.PathIndex;

	public static Vector3 GetRoadPosition(int pathIndex)
	{
		return GameData.Current == null ? Vector3.zero : GameData.Current.GetPosByPathIndex(pathIndex);
	}
	public void Init(SliceInfo info, Vector3 pos, Quaternion rot)
	{
		StopMotion();
		if (_info != null && _info.SliceSc == this) _info.SliceSc = null;
		_info = info;
		if (_info == null) return;
		_info.SliceSc = this;
		transform.SetPositionAndRotation(pos, rot);
		transform.localScale = Vector3.one;

		Material material = GameAssetsConfig.GetSliceMatByType(_info.Type);
		if (_meshRenderer != null && material != null) _meshRenderer.material = material;
		if (_meshRenderer != null)
		{
			if (_colorPropBlock == null) _colorPropBlock = new MaterialPropertyBlock();
			_meshRenderer.GetPropertyBlock(_colorPropBlock);
			Color color = GameAssetsConfig.GetCubeColor(_info.Type);
			_colorPropBlock.SetColor("_BaseColor", color);
			_colorPropBlock.SetColor("_Color", color);
			_meshRenderer.SetPropertyBlock(_colorPropBlock);
		}
		if (_meshRenderer != null && CubeData.IsLongBoxColor(_info.Type) && _longBoxSignTex != null)
			_meshRenderer.material.SetTexture("_BaseMap", _longBoxSignTex);
	}

	public UniTask JumpToTransfer(int targetPathIndex, float delayTime, float jumpHeight = JumpArcHeight)
	{
		if (_info == null || GameData.Current == null) return default;
		// A negative index keeps the conveyor tick from moving this slice again
		// while its jump from the box is still in progress.
		_info.PathIndex = -1;
		_info.IsMoved = false;
		Vector3 target = GetRoadPosition(targetPathIndex);
		Vector3 next = GetRoadPosition(targetPathIndex + 1);
		float duration = Mathf.Max(0.01f, delayTime);
		Vector3 direction = next - target;
		Quaternion rotation = direction.sqrMagnitude > 0.0001f
			? Quaternion.LookRotation(direction, Vector3.up) : transform.rotation;
		// Apply the road thickness as the slice lands on the conveyor.
		StartAnimation(target, rotation, RoadScale, duration, () =>
		{
			if (_info == null) return;
			_info.PathIndex = WrapPathIndex(targetPathIndex);
			_info.IsMoved = false;
		}, jumpHeight, Ease.InQuad);
		return default;
	}

	public UniTask JumpToBox(Box box, float delayTime, System.Action landed = null, float jumpHeight = JumpArcHeight)
	{
		if (_info == null || box == null) return default;
		if (GameData.Current != null) GameData.Current.RemoveTransferSlice(_info);
		_info.PathIndex = -1;
		float duration = Mathf.Max(0.01f, delayTime);
		StartAnimation(box.GetTopSlicePos(), box.transform.rotation, Vector3.one,
			duration, () =>
			{
				landed?.Invoke();
				if (box != null && _info != null) box.PlayCubeAddEffect(GameAssetsConfig.GetCubeColor(_info.Type));
				Dismiss();
			}, jumpHeight, Ease.OutQuad);
		return default;
	}

	public void MoveNextPath(float interval)
	{
		if (_info == null || GameData.Current == null) return;
		int nextPathIndex = _info.PathIndex + 1;
		if (GameData.Current.PathPoints.Count > 0 &&
			nextPathIndex >= GameData.Current.PathPoints.Count)
		{
			_info.PathIndex = 0;
			GameData.Current.CircleTransferSlice(_info);
		}
		else
		{
			_info.PathIndex = WrapPathIndex(nextPathIndex);
		}
		_info.IsMoved = true;

		Vector3 target = GetRoadPosition(_info.PathIndex);
		float duration = Mathf.Max(0.01f, interval);
		if (!GameData.Current.IsPathCircle &&
			GameData.Current.PathSectionIndices.Contains(_info.PathIndex))
		{
			StopMotion();
			Vector3 next = GetRoadPosition(_info.PathIndex + 1);
			Vector3 nextDirection = next - target;
			Quaternion nextRotation = nextDirection.sqrMagnitude > 0.0001f
				? Quaternion.LookRotation(nextDirection, Vector3.up) : transform.rotation;
			transform.SetPositionAndRotation(target, nextRotation);
			return;
		}

		Vector3 moveDirection = target - transform.position;
		Quaternion rotation = moveDirection.sqrMagnitude > 0.0001f
			? Quaternion.LookRotation(moveDirection, Vector3.up) : transform.rotation;
		StartAnimation(target, rotation, transform.localScale, duration, null);
	}

	private void StartAnimation(Vector3 targetPosition, Quaternion targetRotation,
		Vector3 targetScale, float duration, Action completed, float jumpHeight = 0f,
		Ease rotationEase = Ease.OutQuad)
	{
		StopMotion();
		_rotationTween = transform.DORotate(targetRotation.eulerAngles, duration)
			.SetEase(rotationEase).SetTarget(gameObject);
		if (jumpHeight > 0f)
		{
			_isJumping = true;
			_scaleTween = transform.DOScale(targetScale, duration)
				.SetEase(Ease.OutQuad).SetTarget(gameObject);
			_positionTween = transform.DOJump(targetPosition, jumpHeight, 1, duration)
				.SetEase(Ease.OutQuad).SetTarget(gameObject);
			SetJumpTimeScale(GameController.Current == null ? 1f : GameController.Current.SliceJumpTimeScale);
		}
		else
		{
			_positionTween = transform.DOMove(targetPosition, duration)
				.SetEase(Ease.Linear).SetTarget(gameObject);
		}
		_positionTween.OnComplete(() =>
		{
			_positionTween = null;
			_isJumping = false;
			completed?.Invoke();
		});
	}

	public void SetJumpTimeScale(float timeScale)
	{
		if (!_isJumping) return;
		timeScale = Mathf.Max(0.01f, timeScale);
		if (_positionTween != null) _positionTween.timeScale = timeScale;
		if (_rotationTween != null) _rotationTween.timeScale = timeScale;
		if (_scaleTween != null) _scaleTween.timeScale = timeScale;
	}

	private void StopMotion()
	{
		_positionTween?.Kill();
		_rotationTween?.Kill();
		_scaleTween?.Kill();
		_positionTween = null;
		_rotationTween = null;
		_scaleTween = null;
		_isJumping = false;
	}

	private void Dismiss()
	{
		gameObject.SetActive(false);
		OnSliceDismiss?.Invoke();
	}

	public void DismissForUndo()
	{
		StopMotion();
		Dismiss();
	}

	private static int WrapPathIndex(int index)
	{
		int count = GameData.Current == null ? 0 : GameData.Current.PathPoints.Count;
		return count <= 0 ? index : ((index % count) + count) % count;
	}

	private void OnDisable()
	{
		StopMotion();
	}
}
