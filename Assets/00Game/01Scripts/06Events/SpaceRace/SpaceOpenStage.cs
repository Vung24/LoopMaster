using System;
using Spine.Unity;
using TMPro;
using UnityEngine;

namespace Snui.SpaceRace
{
	public class SpaceOpenStage : MonoBehaviour
	{
		[SerializeField] private int _stageIndex;
		[SerializeField] private GameObject _staticNode;
		[SerializeField] private GameObject _selectNode;
		[SerializeField] private TMP_Text _stageText;
		[SerializeField] private TMP_Text _timeText;
		[SerializeField] private SkeletonGraphic _planetSpine;
		[SerializeField] private GameObject _completeMark;

		public int StageIndex => _stageIndex;

		private void Awake()
		{
			if (_staticNode == null)
			{
				var t = HierarchyReferenceCache.Child(transform, "Static");
				if (t != null) _staticNode = t.gameObject;
			}
			if (_selectNode == null)
			{
				var t = HierarchyReferenceCache.Child(transform, "Select");
				if (t != null) _selectNode = t.gameObject;
			}
		}

		private void Start()
		{
			Init();
		}

		public void Init()
		{
			if (Managers.SpaceRace == null) return;

			int currentStage = Managers.SpaceRace.Stage;
			bool isCurrent = _stageIndex == currentStage;

			var activeRoot = isCurrent ? _selectNode : _staticNode;
			if (activeRoot != null)
			{
				if (_stageText == null)
				{
					_stageText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(activeRoot.transform, "Stage");
				}
				if (_timeText == null)
				{
					_timeText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(activeRoot.transform, "Time");
				}
				if (_planetSpine == null)
				{
					_planetSpine = HierarchyReferenceCache.DescendantComponent<SkeletonGraphic>(activeRoot.transform, "Planet");
				}
			}

			if (_completeMark == null)
			{
				var t = HierarchyReferenceCache.Path(transform, "Static", "Planet", "Complete");
				if (t != null) _completeMark = t.gameObject;
			}

			int stageDisplayNum = Managers.SpaceRace.WinCount < 3
				? _stageIndex + 1
				: Managers.SpaceRace.WinCount + _stageIndex - 1;

			if (_stageText != null)
			{
				_stageText.text = $"Stage {stageDisplayNum}";
			}

			int durationIdx = Math.Min(stageDisplayNum - 1, 2);
			if (durationIdx < 0) durationIdx = 0;
			long duration = Managers.SpaceRace.GetStageDuration(durationIdx);
			if (_timeText != null)
			{
				_timeText.text = TimeFormatter.FormatTimeLeft(duration);
			}

			if (_selectNode != null) _selectNode.SetActive(isCurrent);
			if (_staticNode != null) _staticNode.SetActive(!isCurrent);

			if (_planetSpine != null && _planetSpine.AnimationState != null)
			{
				string anim = isCurrent ? GetCurrentStageAnimName() : GetStageStopAnimName();
				_planetSpine.AnimationState.SetAnimation(0, anim, isCurrent);
			}

			if (_completeMark != null)
			{
				_completeMark.SetActive(_stageIndex < currentStage);
			}
		}

		public string GetCurrentStageAnimName()
		{
			return $"planet{_stageIndex + 1}_loop";
		}

		public string GetStageStopAnimName()
		{
			return $"planet{_stageIndex + 1}_stop";
		}
	}
}