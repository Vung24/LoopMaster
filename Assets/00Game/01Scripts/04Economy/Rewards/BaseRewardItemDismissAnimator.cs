using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Snui
{
	public abstract class BaseRewardItemDismissAnimator
	{
        public abstract bool ShouldFlyToTarget();
        protected abstract Vector3 GetTargetPos(RewardCell cell);
        protected abstract void OnFlyToTarget(RewardCell cell);

        public bool IsHeartHudTarget(RewardType type) =>
            (type == RewardType.Heart || type == RewardType.InfinityHeart) &&
            Snui_HeartHud.Current != null && Snui_HeartHud.Current.isActiveAndEnabled;

        // Native FlyToTarget (0x14559d4): move 80 units away for .3s, then
        // fly to the target for .3s while shrinking to half the layout scale.
        public async UniTask FlyToTarget(List<RewardCell> rewardCells, CancellationToken token)
        {
            var flights = new List<UniTask>();
            int index = 0;
            foreach (var cell in rewardCells)
            {
                token.ThrowIfCancellationRequested();
                if (cell == null || cell.RewardItem?.Type == RewardType.Coin) continue;
                flights.Add(FlyCell(cell, token));
                await UniTask.Delay(System.TimeSpan.FromSeconds(Mathf.Max(0f, .1f - index++ * .02f)),
                    ignoreTimeScale: true, cancellationToken: token);
            }
            await UniTask.WhenAll(flights);
        }

        private async UniTask FlyCell(RewardCell cell, CancellationToken token)
        {
            Transform node = cell.transform;
            cell.SetBackLight(false);
            Vector3 start = node.localPosition;
            Vector3 target = GetTargetPos(cell);
            bool fly = ShouldFlyToTarget() && (target - start).sqrMagnitude > .01f;
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(cell.gameObject);
            if (fly)
            {
                sequence.Append(node.DOLocalMove(start + (start - target).normalized * 80f, .3f).SetEase(Ease.OutQuad));
                sequence.Append(node.DOLocalMove(target, .3f).SetEase(Ease.InQuad));
                sequence.Join(node.DOScale(node.localScale * .5f, .3f).SetEase(Ease.InOutSine));
            }
            else sequence.Append(node.DOScale(Vector3.zero, .2f));
            try
            {
                await UniTask.WaitUntil(() => !sequence.IsActive() || sequence.IsComplete(), cancellationToken: token);
                token.ThrowIfCancellationRequested();
                if (cell == null || !cell.isActiveAndEnabled) return;
                if (fly) OnFlyToTarget(cell);
                cell.PlayCollectSound();
                cell.gameObject.SetActive(false);
            }
            finally { sequence.Kill(); }
        }
	}
}
