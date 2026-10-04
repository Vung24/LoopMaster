public class HeartManager : BaseHeartManager
{
	public override IHeartHud GetCurrentHeartHud() => null;
	public override bool IsMoreHeartDialogShowRvBtn() => false;
	public override int GetRefillHeartPrice() => 0;
}
