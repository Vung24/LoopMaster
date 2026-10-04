using UnityEngine;

public interface IHeartHud
{
	void SetTop(int order = 1);
	void RevertToOriginLayer();
	Vector3 GetIconPos();
	void PlayShakeAnimation();
	void SetAutoUpdate(bool isAuto);
	void PlaySparks();
}
