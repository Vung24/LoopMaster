public sealed class BoosterTutorialContext
{
    public BoosterTutorialGuide Guide { get; }
    public GameController Controller { get; }
    public string BoosterKey { get; }

    public BoosterTutorialContext(BoosterTutorialGuide guide, GameController controller, string boosterKey)
    {
        Guide = guide;
        Controller = controller;
        BoosterKey = boosterKey;
    }
}
