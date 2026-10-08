using TMPro;
using UnityEngine;

namespace Snui
{
	public class EventScoreItem : MonoBehaviour
	{
		[SerializeField]
		private TMP_Text _scoreText;

		private void Awake()
		{
			if (_scoreText == null)
			{
				_scoreText = GetComponentInChildren<TMP_Text>(true);
			}
		}

		public void SetScore(int score)
		{
			if (_scoreText == null)
			{
				_scoreText = GetComponentInChildren<TMP_Text>(true);
			}
			if (_scoreText != null)
			{
				_scoreText.text = score.ToString();
			}
		}
	}
}