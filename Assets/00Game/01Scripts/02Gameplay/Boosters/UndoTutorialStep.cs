using System.Threading;
using Cysharp.Threading.Tasks;

public sealed class UndoTutorialStep : IBoosterTutorialStep
{
    public async UniTask ExecuteAsync(BoosterTutorialContext context, CancellationToken token)
    {
        if (await context.Guide.GuideFirstMove(token))
            await context.Guide.GuideToolUse(token);
    }
}
