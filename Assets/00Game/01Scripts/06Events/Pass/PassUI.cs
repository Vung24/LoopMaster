using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Snui
{
    internal static class PassUI
    {
        public static T Node<T>(Transform root, string name) where T : Component
            => HierarchyReferenceCache.DescendantComponent<T>(root, name);
        public static void Active(Transform root, string name, bool active)
        {
            var node = HierarchyReferenceCache.Descendant(root, name);
            if (node != null) node.gameObject.SetActive(active);
        }
        public static void Text(Transform root, string name, string value)
        {
            var text = Node<TMP_Text>(root, name);
            if (text != null)
            {
                var label = text.GetComponent<Snui_Text>();
                if (label != null) label.SetText(value);
                else text.text = value;
            }
        }
        public static Button Bind(Transform root, string name, UnityEngine.Events.UnityAction action)
        {
            var button = Node<Button>(root, name);
            if (button != null) { button.onClick.RemoveListener(action); button.onClick.AddListener(action); }
            return button;
        }
        public static void Animate(Transform node)
        {
            if (node == null) return;
            node.DOKill(); node.localScale = Vector3.one * .85f;
            node.DOScale(Vector3.one, .25f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(node.gameObject);
        }
        public static void Spine(SkeletonGraphic graphic, string animation, bool loop)
        {
            if (graphic == null) return;
            graphic.freeze = false;
            graphic.UnscaledTime = true;
            graphic.Initialize(false);
            if (graphic.SkeletonData != null && graphic.SkeletonData.FindAnimation(animation) != null)
            {
                var current = graphic.AnimationState.GetCurrent(0);
                if (current != null && current.Animation.Name == animation && current.Loop == loop) return;
                graphic.AnimationState.SetAnimation(0, animation, loop);
                graphic.Update(0f);
                graphic.UpdateMesh();
            }
        }
        public static void NativeIcon(Image icon)
        {
            if (icon == null || icon.sprite == null) return;
            icon.type = Image.Type.Simple; icon.preserveAspect = true; icon.SetNativeSize();
        }
        public static void Sound(AudioClip sound)
        {
            if (sound != null) AudioManager.Current?.PlayOneShot(sound);
        }
        public static async UniTask PresentRewards(List<RewardItem> rewards, Transform owner)
        {
            if (rewards == null || rewards.Count == 0 || owner == null) return;
            var parent = owner.GetComponentInParent<Canvas>()?.rootCanvas.transform ?? owner;
            var merged = new List<RewardItem>();
            var totals = new Dictionary<RewardType, RewardItem>();
            foreach (var reward in rewards)
            {
                if (totals.TryGetValue(reward.Type, out var total)) total.Count = checked(total.Count + reward.Count);
                else { total = new RewardItem(reward.Type, reward.Count); totals.Add(reward.Type, total); merged.Add(total); }
            }
            await RewardDialog.ShowSpecialStreakRewards(merged, parent);
        }
        public static UniTask WaitForClose(Component owner)
            => UniTask.WaitUntil(() => owner == null || !owner.gameObject.activeInHierarchy);
    }
}
