using System.Threading;
using Cysharp.Threading.Tasks;

public interface IBoosterTutorialStep
{
    UniTask ExecuteAsync(BoosterTutorialContext context, CancellationToken token);
}
