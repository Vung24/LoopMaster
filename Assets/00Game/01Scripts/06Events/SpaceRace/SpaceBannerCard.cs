using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Snui.Profile;
using Avatar = Snui.Profile.Avatar;

namespace Snui.SpaceRace
{
	public class SpaceBannerCard : MonoBehaviour
	{
		[SerializeField] private Sprite _rank1Sprite;
		[SerializeField] private Sprite _rankOtherSprite;
		[SerializeField] private Avatar _avatar;
		[SerializeField] private NameEffect _nameEffect;
		[SerializeField] private TMP_Text _scoreText;
		[SerializeField] private Image _rankImage;
		[SerializeField] private TMP_Text _rankText;
		[SerializeField] private BadgeTokenContainer _badgeTokenContainer;

		private void Awake()
		{
			if (_avatar == null)
			{
				_avatar = HierarchyReferenceCache.ChildComponent<Avatar>(transform, "Avatar");
			}
			if (_nameEffect == null)
			{
				_nameEffect = HierarchyReferenceCache.ChildComponent<NameEffect>(transform, "Name");
			}
			if (_rankImage == null)
			{
				_rankImage = HierarchyReferenceCache.ChildComponent<Image>(transform, "Rank");
			}
			if (_rankText == null)
			{
				_rankText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Rank");
			}
			if (_scoreText == null)
			{
				_scoreText = HierarchyReferenceCache.DescendantComponent<TMP_Text>(transform, "Record");
			}
			if (_badgeTokenContainer == null)
			{
				_badgeTokenContainer = GetComponentInChildren<BadgeTokenContainer>(true);
			}
		}

		private void Start()
		{
			if (Managers.SpaceRace != null && Managers.SpaceRace.Stage != 0)
			{
				if (transform is RectTransform rt)
				{
					var size = rt.sizeDelta;
					size.x = 156f;
					rt.sizeDelta = size;
				}
			}
		}

		public void InitSelfCard()
		{
			if (Managers.SpaceRace == null) return;

			int rank = Managers.SpaceRace.Rank;
			if (_rankImage != null)
			{
				_rankImage.sprite = rank == 1 ? _rank1Sprite : _rankOtherSprite;
			}
			if (_rankText != null)
			{
				_rankText.text = rank.ToString();
			}

			var userInfo = AccountManager.GetDisplayUserInfo();
			if (_avatar != null && userInfo != null)
			{
				_avatar.SetAvatar(userInfo);
			}
			if (_nameEffect != null && userInfo != null)
			{
				_nameEffect.SetPlayerInfo(userInfo);
			}
			if (_badgeTokenContainer != null && userInfo != null)
			{
				_badgeTokenContainer.SetToken(userInfo.Token);
			}
			if (_scoreText != null)
			{
				_scoreText.text = Managers.SpaceRace.TotalScore.ToString();
			}
		}

		public void InitOpponentCard(EventRaceOpponent opponent)
		{
			if (opponent == null) return;

			if (_rankImage != null)
			{
				_rankImage.sprite = opponent.Rank == 1 ? _rank1Sprite : _rankOtherSprite;
			}
			if (_rankText != null)
			{
				_rankText.text = opponent.Rank.ToString();
			}
			if (_avatar != null)
			{
				_avatar.SetAvatar(opponent);
			}
			if (_nameEffect != null)
			{
				_nameEffect.SetPlayerInfo(opponent);
			}
			if (_badgeTokenContainer != null)
			{
				_badgeTokenContainer.SetToken(opponent.Token);
			}
			if (_scoreText != null)
			{
				_scoreText.text = opponent.Score.ToString();
			}
		}
	}
}