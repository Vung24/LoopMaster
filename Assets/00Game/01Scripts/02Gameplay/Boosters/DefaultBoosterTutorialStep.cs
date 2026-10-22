using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class DefaultBoosterTutorialStep : IBoosterTutorialStep
{
    public async UniTask ExecuteAsync(BoosterTutorialContext context, CancellationToken token)
    {
        await context.Guide.GuideToolUse(token);
    }
}
