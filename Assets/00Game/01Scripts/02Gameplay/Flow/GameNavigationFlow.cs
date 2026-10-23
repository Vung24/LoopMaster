using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public interface IGameNavigationSession
{
    GameData Data { get; }
    GameEndState EndState { get; }
    int CurrentLevelNumber { get; }
    bool StartLevel(GameData data);
}

public interface IGameNavigationEffects
{
    bool IsSceneChanging { get; }
    bool ReturnHomeAfterWin { get; }
    bool IsLevelCompleted(int level);
    bool SettlePendingWins();
    void SynchronizeStreakLevel();
    void OnLevelAbandoned();
    UniTask<bool> EnterHomeAsync(bool withAnimation, bool showFailDialog, CancellationToken token);
    UniTask DismissLoadingAsync();
    void ReportError(Exception error);
}

// Owns transition and abandonment state. Dependencies expose only navigation capabilities.
public sealed class GameNavigationFlow : IDisposable
{
    private readonly IGameNavigationSession _session;
    private readonly GameLevelFlow _levels;
    private readonly IGameNavigationEffects _effects;
    private readonly CancellationToken _destroyToken;
    private GameData _abandonedData;
    private CancellationTokenSource _homeLifetime;
    private int _version;
    private bool _startingLevel;
    private bool _disposed;

    public GameNavigationFlow(IGameNavigationSession session, GameLevelFlow levels,
        IGameNavigationEffects effects, CancellationToken destroyToken)
    {
        _session = session;
        _levels = levels;
        _effects = effects;
        _destroyToken = destroyToken;
    }

    public bool IsTransitioning { get; private set; }
    public bool IsCurrentLevelAbandoned => _session.Data != null && _session.Data == _abandonedData;
    public bool HasNextLevel => _levels.HasNext(_session.CurrentLevelNumber);
    public bool CanReplayLevel => !_disposed && _session.Data != null && !IsTransitioning &&
        _session.EndState != GameEndState.Won && !_effects.IsLevelCompleted(_session.CurrentLevelNumber) &&
        (Managers.Heart == null || Managers.Heart.IsHeartEnough());
    public bool CanLoadNextLevel => !_disposed && _session.EndState == GameEndState.Won &&
        !IsTransitioning && HasNextLevel;
    public bool ShouldReturnHomeAfterWin => !CanLoadNextLevel || _effects.ReturnHomeAfterWin;

    public event Action<int> LevelReplayed;
    public event Action<int> LevelAdvanced;
    public event Action<int> NextLevelUnavailable;

    public void BeginLevel(GameData data)
    {
        if (!_startingLevel)
        {
            _version++;
            CancelHome();
            IsTransitioning = false;
        }
        _levels.Capture(data);
    }

    public bool LoadNextLevel()
    {
        if (_disposed || _session.EndState != GameEndState.Won || _session.Data == null ||
            IsTransitioning || _effects.IsSceneChanging) return false;
        int current = _session.CurrentLevelNumber;
        if (current < 1 || current == int.MaxValue) return false;
        int next = current + 1;
        bool loaded = false;
        bool unavailable = false;
        GameData currentData = _session.Data;
        int version = ++_version;
        IsTransitioning = true;
        try
        {
            if (!_effects.SettlePendingWins()) return false;
            if (!IsCurrentRequest(version, currentData)) return false;
            if (!_levels.HasNext(current)) unavailable = true;
            else if (_levels.CreateNext(current, out GameData data))
            {
                if (!IsCurrentRequest(version, currentData)) return false;
                _effects.SynchronizeStreakLevel();
                if (!IsCurrentRequest(version, currentData)) return false;
                loaded = StartPreparedLevel(data);
            }
        }
        catch (Exception error) { _effects.ReportError(error); }
        finally { if (version == _version) IsTransitioning = false; }
        if (unavailable) NextLevelUnavailable?.Invoke(next);
        if (loaded) LevelAdvanced?.Invoke(next);
        return loaded;
    }

    public bool ReplayLevel()
    {
        if (!CanReplayLevel || _effects.IsSceneChanging) return false;
        GameData current = _session.Data;
        int level = _session.CurrentLevelNumber;
        if (level < 1) level = Math.Max(1, current.GetPuzzleMainLevel());
        bool loaded = false;
        int version = ++_version;
        IsTransitioning = true;
        try
        {
            // Prepare before committing abandonment: failed reads leave the current game intact.
            if (!_levels.CreateReplay(current, level, out GameData data)) return false;
            if (!IsCurrentRequest(version, current)) return false;
            CommitAbandonedLevel();
            if (!IsCurrentRequest(version, current)) return false;
            loaded = StartPreparedLevel(data);
        }
        catch (Exception error) { _effects.ReportError(error); }
        finally { if (version == _version) IsTransitioning = false; }
        if (loaded) LevelReplayed?.Invoke(level);
        return loaded;
    }

    private bool StartPreparedLevel(GameData data)
    {
        _startingLevel = true;
        try { return _session.StartLevel(data); }
        finally { _startingLevel = false; }
    }

    private bool IsCurrentRequest(int version, GameData data) => !_disposed &&
        version == _version && _session.Data == data;

    public void CommitAbandonedLevel()
    {
        if (_disposed || _session.Data == null || IsCurrentLevelAbandoned ||
            _session.EndState == GameEndState.Won) return;
        _abandonedData = _session.Data;
        _effects.OnLevelAbandoned();
    }

    public bool ContinueAfterWin(bool rewardClaimed)
    {
        if (_disposed || _session.EndState != GameEndState.Won || !rewardClaimed || IsTransitioning ||
            _effects.IsSceneChanging) return false;
        return ShouldReturnHomeAfterWin ? ReturnHome() : LoadNextLevel();
    }

    public bool ReturnHome()
    {
        if (!BeginReturnHome(out CancellationTokenSource lifetime)) return false;
        ReturnHomeWithLoading(lifetime, _version).Forget();
        return true;
    }

    public UniTask<bool> ReturnHomeAsync(bool withAnimation = true)
    {
        return BeginReturnHome(out CancellationTokenSource lifetime)
            ? LoadHomeAsync(lifetime, _version, withAnimation) : UniTask.FromResult(false);
    }

    private bool BeginReturnHome(out CancellationTokenSource lifetime)
    {
        lifetime = null;
        if (_disposed || IsTransitioning || _effects.IsSceneChanging || _destroyToken.IsCancellationRequested) return false;
        int version = ++_version;
        IsTransitioning = true;
        try
        {
            // Pending wins stay saved until Home presents and settles them.
            _effects.SynchronizeStreakLevel();
            if (version != _version || _disposed) return false;
            CommitAbandonedLevel();
            if (version != _version || _disposed) return false;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(_destroyToken);
            _homeLifetime = lifetime;
            return true;
        }
        catch (Exception error)
        {
            if (version == _version) IsTransitioning = false;
            _effects.ReportError(error);
            return false;
        }
    }

    private async UniTask ReturnHomeWithLoading(CancellationTokenSource lifetime, int version)
    {
        if (!await LoadHomeAsync(lifetime, version, true) && version == _version && !_disposed)
        {
            try { await _effects.DismissLoadingAsync(); }
            catch (Exception error) { _effects.ReportError(error); }
        }
    }

    private async UniTask<bool> LoadHomeAsync(CancellationTokenSource lifetime, int version, bool withAnimation)
    {
        bool loaded = false;
        try
        {
            loaded = await _effects.EnterHomeAsync(withAnimation,
                _session.EndState != GameEndState.Won, lifetime.Token);
            return loaded;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return false; }
        catch (Exception error) { _effects.ReportError(error); return false; }
        finally
        {
            if (version == _version)
            {
                if (!loaded) IsTransitioning = false;
                if (_homeLifetime == lifetime) _homeLifetime = null;
            }
            lifetime.Dispose();
        }
    }

    private void CancelHome()
    {
        CancellationTokenSource lifetime = _homeLifetime;
        _homeLifetime = null;
        lifetime?.Cancel();
    }

    public void Dispose()
    {
        _disposed = true;
        _version++;
        CancelHome();
    }
}
