using UnityEngine;
using Cysharp.Threading.Tasks;
using Snui;

public class ChallengeBundleUnlockDialog : MonoBehaviour
{
    public static UniTask<ChallengeBundleUnlockDialog> Show(Transform parent) =>
        EventPrefabUI.Show<ChallengeBundleUnlockDialog>(parent, "ChallengeBundleUnlockDialog");
}
