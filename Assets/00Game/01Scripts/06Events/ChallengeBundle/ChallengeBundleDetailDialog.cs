using UnityEngine;
using Cysharp.Threading.Tasks;
using Snui;

public class ChallengeBundleDetailDialog : MonoBehaviour
{
    public static UniTask<ChallengeBundleDetailDialog> Show(Transform parent) =>
        EventPrefabUI.Show<ChallengeBundleDetailDialog>(parent, "ChallengeBundleDetailDialog");
}
