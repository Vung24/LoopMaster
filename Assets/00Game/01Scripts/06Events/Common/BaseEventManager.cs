using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Snui
{
	public abstract class BaseEventManager : BaseManager
	{
		public virtual void EventBeforeEnterHome()
		{
		}

		public virtual bool ShouldShowIcon()
		{
			return false;
		}

		public abstract UniTask<HomeEventBase> CreateIcon(Transform parent);
	}
}