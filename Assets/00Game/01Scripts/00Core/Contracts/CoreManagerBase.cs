using UnityEngine;

public class BaseManager : MonoBehaviour, IGameManager
{
	public ManagerStatus status { get; protected set; } = ManagerStatus.Shutdown;

	public virtual void Startup()
	{
		status = ManagerStatus.Started;
	}

	public virtual void ClearData()
	{
	}
}
