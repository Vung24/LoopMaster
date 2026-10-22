using UnityEngine;
using DG.Tweening;
using Snui;

public class RewardItemDismissAnimator : BaseRewardItemDismissAnimator
{
    public override bool ShouldFlyToTarget() => HomeController.Current != null &&
        HomeController.Current.CurrentPageIndex == HomeController.HOME_PAGE_INDEX &&
        HomeController.Current.HomePage?.PlayButton != null;

    protected override Vector3 GetTargetPos(RewardCell cell)
    {
        if (IsHeartHudTarget(cell.RewardItem?.Type))
        {
            // Infinite hearts hide the normal icon, but its position remains the HUD destination.
            return cell.transform.parent.InverseTransformPoint(Snui_HeartHud.Current.GetIconPos());
        }
        Transform target = HomeController.Current?.HomePage?.PlayButton?.transform;
        // Both endpoints must use the cell parent's space, including SafeArea offsets.
        return target != null && target.gameObject.activeInHierarchy
            ? cell.transform.parent.InverseTransformPoint(target.position) : cell.transform.localPosition;
    }

    protected override void OnFlyToTarget(RewardCell cell)
    {
        if (IsHeartHudTarget(cell.RewardItem?.Type))
        {
            Snui_HeartHud.Current.PlayShakeAnimation();
            Snui_HeartHud.Current.PlaySparks();
            Snui_HeartHud.SetCurrentAutoUpdate(true);
            return;
        }
        var button = HomeController.Current?.HomePage?.PlayButton;
        if (button == null) return;
        var sparks = HierarchyReferenceCache.ChildComponent<ParticleSystem>(button.transform.parent, "BtnSparks");
        if (sparks != null) { sparks.gameObject.SetActive(true); sparks.Play(true); }
        Vector3 scale = button.transform.localScale;
        DOTween.Sequence().SetUpdate(true).SetLink(button.gameObject)
            .Append(button.transform.DOScale(scale * .9f, .06f).SetEase(Ease.InOutSine))
            .Append(button.transform.DOScale(scale * 1.12f, .1f).SetEase(Ease.OutSine))
            .Append(button.transform.DOScale(scale, .13f).SetEase(Ease.InOutSine));
    }
}
