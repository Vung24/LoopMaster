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
	private bool _scaleJumpWithConveyor;
	private readonly RoadMotion _roadMotion = new RoadMotion(true);

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

		PrepareVisual(_info.Type);
	}

	internal void PrepareVisual(int type)
	{
		Material material = GameAssetsConfig.GetSliceMatByType(type);
		if (_meshRenderer != null && material != null) _meshRenderer.sharedMaterial = material;
		if (_meshRenderer != null)
		{
			if (_colorPropBlock == null) _colorPropBlock = new MaterialPropertyBlock();
			_colorPropBlock.Clear();
			Color color = GameAssetsConfig.GetCubeColor(type);
			_colorPropBlock.SetColor("_BaseColor", color);
			_colorPropBlock.SetColor("_Color", color);
			if (CubeData.IsLongBoxColor(type) && _longBoxSignTex != null)
				_colorPropBlock.SetTexture("_BaseMap", _longBoxSignTex);
			_meshRenderer.SetPropertyBlock(_colorPropBlock);
		}
	}

	public UniTask JumpToTransfer(int targetPathIndex, float delayTime, float jumpHeight = JumpArcHeight,
		Action landed = null, bool commitLogicOnComplete = true)
	{
		if (_info == null || GameData.Current == null) return default;
		_info.PathIndex = -1;
		_info.IsMoved = false;
		Vector3 target = GetRoadPosition(targetPathIndex);
		Vector3 next = GetRoadPosition(targetPathIndex + 1);
		float duration = Mathf.Max(0.01f, delayTime);
		Vector3 direction = next - target;
		Quaternion rotation = direction.sqrMagnitude > 0.0001f
			? Quaternion.LookRotation(direction, Vector3.up) : transform.rotation;
		StartAnimation(target, rotation, RoadScale, duration, () =>
		{
			if (_info == null) return;
			if (commitLogicOnComplete)
			{
				_info.PathIndex = WrapPathIndex(targetPathIndex);
				_info.IsMoved = false;
			}
			landed?.Invoke();
		}, jumpHeight, Ease.InQuad, false);
		return default;
	}

	public UniTask JumpToBox(Box box, float delayTime, Action landed = null, float jumpHeight = JumpArcHeight)
	{
		if (_info == null || box == null) return default;
		if (GameData.Current != null) GameData.Current.RemoveTransferSlice(_info);
		_info.PathIndex = -1;
		float duration = Mathf.Max(0.01f, delayTime);
		StartAnimation(box.GetTopSlicePos(), box.transform.rotation, Vector3.one,
			duration, () =>
			{
				landed?.Invoke();
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
		float duration = Mathf.Max(0.000001f, interval);
		if (!GameData.Current.IsPathCircle &&
			GameData.Current.IsPathSectionIndex(_info.PathIndex))
		{
			StopMotion();
			Vector3 next = GetRoadPosition(_info.PathIndex + 1);
			Vector3 nextDirection = next - target;
			Quaternion nextRotation = nextDirection.sqrMagnitude > 0.0001f
				? Quaternion.LookRotation(nextDirection, Vector3.up) : transform.rotation;
			transform.SetPositionAndRotation(target, nextRotation);
			return;
		}

		if (!_roadMotion.IsMoving)
		{
			StopMotion();
		}
		Vector3 anchor = transform.position;
		Quaternion rotation = transform.rotation;
		if (_roadMotion.IsMoving)
		{
			anchor = _roadMotion.TailPosition;
			rotation = _roadMotion.TailRotation;
		}
		Vector3 moveDirection = target - anchor;
		if (moveDirection.sqrMagnitude > 0.0001f)
			rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
		_roadMotion.Enqueue(target, rotation, duration);
	}

	internal void AdvanceRoadVisual(float delta)
	{
		if (!isActiveAndEnabled || !_roadMotion.Advance(delta)) return;
		transform.SetPositionAndRotation(_roadMotion.Position, _roadMotion.Rotation);
	}

	private void StartAnimation(Vector3 targetPosition, Quaternion targetRotation,
		Vector3 targetScale, float duration, Action completed, float jumpHeight = 0f,
		Ease rotationEase = Ease.OutQuad, bool scaleJumpWithConveyor = true)
	{
		StopMotion();
		_rotationTween = transform.DORotate(targetRotation.eulerAngles, duration)
			.SetEase(rotationEase).SetTarget(gameObject).SetLink(gameObject);
		if (jumpHeight > 0f)
		{
			_isJumping = true;
			_scaleJumpWithConveyor = scaleJumpWithConveyor;
			_scaleTween = transform.DOScale(targetScale, duration)
				.SetEase(Ease.OutQuad).SetTarget(gameObject).SetLink(gameObject);
			_positionTween = transform.DOJump(targetPosition, jumpHeight, 1, duration)
				.SetEase(Ease.OutQuad).SetTarget(gameObject).SetLink(gameObject);
			SetJumpTimeScale(GameController.Current == null ? 1f : GameController.Current.SliceJumpTimeScale);
		}
		else
		{
			_positionTween = transform.DOMove(targetPosition, duration)
				.SetEase(Ease.Linear).SetTarget(gameObject).SetLink(gameObject);
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
		if (!_isJumping || !_scaleJumpWithConveyor) return;
		timeScale = Mathf.Max(0.01f, timeScale);
		if (_positionTween != null) _positionTween.timeScale = timeScale;
		if (_rotationTween != null) _rotationTween.timeScale = timeScale;
		if (_scaleTween != null) _scaleTween.timeScale = timeScale;
	}

	private void StopMotion()
	{
		_roadMotion.Reset(transform.position, transform.rotation);
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

	public void ResetForPool()
	{
		StopMotion();
		if (_info != null && _info.SliceSc == this) _info.SliceSc = null;
		_info = null;
		_scaleJumpWithConveyor = false;
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
