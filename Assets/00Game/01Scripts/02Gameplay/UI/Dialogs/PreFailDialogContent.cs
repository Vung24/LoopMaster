using UnityEngine;

public class PreFailDialogContent : FailDialogContent
{
    [SerializeField] private Transform _addCapacity;
    [SerializeField] private Transform _freeCoinTip;

    protected override void Awake()
    {
        base.Awake();
        if (_addCapacity == null)
            _addCapacity = HierarchyReferenceCache.Path(transform, "TipsBg", "Content", "AddPath");
        if (_addCapacity != null) _addCapacity.gameObject.SetActive(true);
        if (_freeCoinTip != null) _freeCoinTip.gameObject.SetActive(false);
        ReviveRoute = "pre_fail";
    }
    public void Init(bool isFreeRevive) => Configure(isFreeRevive);
}
