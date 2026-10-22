using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class ShuffleTutorialStep : IBoosterTutorialStep
{
    public async UniTask ExecuteAsync(BoosterTutorialContext context, CancellationToken token)
    {
        await context.Guide.GuideToolUse(token);
        if (context.Controller.IsSelectingShuffle)
            await context.Guide.GuideShuffleTarget(token);
    }
}
