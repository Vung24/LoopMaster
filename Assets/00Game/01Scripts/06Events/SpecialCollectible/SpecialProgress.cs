using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui.SpecialCol
{
	public class SpecialProgress : MonoBehaviour
	{
		public virtual UniTask ShowUnlockAnimation() => UniTask.CompletedTask;
	}
}