using UnityEngine;
using Cysharp.Threading.Tasks;
using Snui;

public class ChallengeBundleQuitDialog : MonoBehaviour
{
    public static UniTask<ChallengeBundleQuitDialog> Show(Transform parent) =>
        EventPrefabUI.Show<ChallengeBundleQuitDialog>(parent, "ChallengeBundleQuitDialog");
}
