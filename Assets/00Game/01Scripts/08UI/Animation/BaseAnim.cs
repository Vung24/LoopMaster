using Cysharp.Threading.Tasks;
using UnityEngine;

public class BaseAnim : MonoBehaviour
{
	public virtual UniTask PlayAnim()
	{
		return UniTask.CompletedTask;
	}
}
