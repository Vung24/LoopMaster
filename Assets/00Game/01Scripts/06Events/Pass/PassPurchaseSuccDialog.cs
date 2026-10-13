using Cysharp.Threading.Tasks;
using Snui;
using TMPro;
using UnityEngine;

public sealed class PassPurchaseSuccDialog : BasePopup
{
    public AudioClip SuccessSound;
    public TMP_Text tmp_title, tmp_desc2, tmp_btn_content;
    public Transform tf_ticket, tf_desc1, tf_reward_icon, tf_reward_heart, tf_reward_box, tf_btn_content;
    public static UniTask<PassPurchaseSuccDialog> Show(Transform parent) => EventPrefabUI.Show<PassPurchaseSuccDialog>(parent, "PassPurchaseSuccDialog");
    protected override void Awake()
    {
        base.Awake();
        PassUI.Bind(transform, "ContinueBtn", Close);
        Snui_Text.EnsureFontAssets(transform);
    }
    protected override void Start()
    {
        base.Start();
        PassUI.Sound(SuccessSound);
        PassUI.Text(transform, "Name", "You");
        PassUI.Text(transform, "Desc2", "Golden rewards unlocked for this season. Offline activation — no payment.");
        if (tf_reward_box != null) tf_reward_box.gameObject.SetActive(Managers.Pass.IsTeamEnable());
        if (tf_reward_heart != null) tf_reward_heart.gameObject.SetActive(Managers.Pass.IsHeartEnable());
        PassUI.Animate(tf_ticket ?? transform);
        foreach (var particle in GetComponentsInChildren<ParticleSystem>()) particle.Play();
    }
    protected override void OnClosed()
    {
        Managers.Pass?.NotifyPurchaseSuccessAnimationEnded();
        base.OnClosed();
    }
}
