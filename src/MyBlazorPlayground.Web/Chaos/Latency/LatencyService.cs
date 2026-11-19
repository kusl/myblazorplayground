using MyBlazorPlayground.Web.Infrastructure;
using MyBlazorPlayground.Web.Chaos.Messages;
using Microsoft.Extensions.Options;

namespace MyBlazorPlayground.Web.Chaos.Latency;

internal sealed partial class LatencyService : IDisposable
{
    public const int MaxOffers = 50;
    public const int MaxLeaderOffers = 40;

    private readonly TimeProvider _time;
    private readonly ILatencyProbe _probe;
    private readonly ILogger<LatencyService> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _gate = new();
    private readonly HashSet<ILatencySubscriber> _subscribers = [];
    private readonly IJournal<BoardMessage> _journal;
    private readonly bool _serverProbe;
    private readonly TimeSpan _gap;
    private BoardMessage[] _messages = [];
    private LatencyLeaders _leaders = LatencyLeaders.Empty;
    private long _version;
    private Pending? _pending;
    private long? _lastStart;
    private Task _idle = Task.CompletedTask;

    public LatencyService(TimeProvider time, IOptions<ChaosOptions> options, ILatencyProbe probe, ILogger<LatencyService> logger, IJournal<BoardMessage>? journal = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(logger);
        var limits = options.Value;
        _time = time;
        _probe = probe;
        _logger = logger;
        _journal = journal ?? NoJournal<BoardMessage>.Instance;
        Capacity = limits.LatencyCapacity;
        RankCapacity = limits.LatencyRankCapacity;
        JackpotCapacity = limits.LatencyJackpotCapacity;
        Budget = TimeSpan.FromSeconds(limits.LatencyBudgetSeconds);
        BrowserWait = Budget + TimeSpan.FromSeconds(15);
        _gap = TimeSpan.FromSeconds(limits.LatencyGapSeconds);
        _serverProbe = limits.LatencyServerProbe;
        StartedAt = time.GetUtcNow();
    }

    public int Capacity { get; }

    public int RankCapacity { get; }

    public int JackpotCapacity { get; }

    public TimeSpan Gap => _gap;

    public TimeSpan Budget { get; }

    public TimeSpan BrowserWait { get; }

    public DateTimeOffset StartedAt { get; }

    public IReadOnlyList<BoardMessage> Messages => Volatile.Read(ref _messages);

    public LatencyLeaders Leaders => Volatile.Read(ref _leaders);

    public bool Running
    {
        get
        {
            lock (_gate)
            {
                return _pending is not null;
            }
        }
    }

    public Task WhenIdle()
    {
        lock (_gate)
        {
            return _idle;
        }
    }

    public LatencyStart Begin()
    {
        lock (_gate)
        {
            var now = _time.GetTimestamp();
            if (_pending is not null || (_lastStart is { } last && _time.GetElapsedTime(last, now) < _gap))
            {
                return LatencyStart.Busy;
            }

            _lastStart = now;
            var pending = new Pending(Uuid7.After(LatestId(), _time.GetUtcNow()));
            _pending = pending;
            Broadcast(LatencyUpdate.State(_version, true));
            _idle = RunAsync(pending);
            return LatencyStart.Started(pending.Id);
        }
    }

    public bool Finish(Guid id, IReadOnlyList<int>? browser)
    {
        if (browser is null || browser.Count != LatencyCatalog.Endpoints.Count)
        {
            return false;
        }

        foreach (var value in browser)
        {
            if (value < -1 || value > LatencyRun.MaxMilliseconds)
            {
                return false;
            }
        }

        lock (_gate)
        {
            return _pending is { } pending && pending.Id == id && pending.Browser.TrySetResult([.. browser.Select(value => value < 0 ? (int?)null : value)]);
        }
    }

    public int Offer(IEnumerable<BoardMessage> offered) => Restore(offered, MaxOffers, recent: true);

    public int OfferLeaders(IEnumerable<BoardMessage> offered) => Restore(offered, MaxLeaderOffers, recent: false);

    public int Load(IEnumerable<BoardMessage> recent, IEnumerable<BoardMessage> leaders)
    {
        ArgumentNullException.ThrowIfNull(recent);
        ArgumentNullException.ThrowIfNull(leaders);
        static bool Stored(BoardMessage message) => message is not null && Uuid7.IsValid(message.Id) && LatencyRun.IsValid(message.Text);
        var latest = recent.Where(Stored).ToList();
        lock (_gate)
        {
            _messages = MessageWindow.Merge(_messages, latest, Capacity)?.Next ?? _messages;
            _leaders = _leaders.Merge(latest.Concat(leaders.Where(Stored)), RankCapacity, JackpotCapacity) ?? _leaders;
            return _messages.Select(message => message.Id).Concat(_leaders.Boards(null).Runs.Select(run => run.Id)).Distinct().Count();
        }
    }

    public void Subscribe(ILatencySubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        lock (_gate)
        {
            if (subscriber.Deliver(LatencyUpdate.Snapshot(_version, _messages, _pending is not null, _leaders.Boards(null))))
            {
                _subscribers.Add(subscriber);
            }
        }
    }

    public void Unsubscribe(ILatencySubscriber subscriber)
    {
        lock (_gate)
        {
            _subscribers.Remove(subscriber);
        }
    }

    public bool IsSubscribed(ILatencySubscriber subscriber)
    {
        lock (_gate)
        {
            return _subscribers.Contains(subscriber);
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _stopping.Dispose();
    }

    private int Restore(IEnumerable<BoardMessage> offered, int limit, bool recent)
    {
        ArgumentNullException.ThrowIfNull(offered);
        var now = _time.GetUtcNow();
        var valid = offered
            .Take(limit)
            .Where(message => message is not null && MessageWindow.IsRestorable(message.Id, now, StartedAt) && LatencyRun.IsValid(message.Text))
            .ToList();
        if (valid.Count == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            return Merge(valid, recent);
        }
    }

    private Guid LatestId() => _messages.Length > 0 ? _messages[0].Id : Guid.Empty;

    private async Task RunAsync(Pending pending)
    {
        var endpoints = LatencyCatalog.Endpoints;
        var token = _stopping.Token;
        var server = new int?[endpoints.Count];
        IReadOnlyList<int?> browser = [];
        try
        {
            await Task.Yield();
            if (_serverProbe)
            {
                server = await _probe.MeasureAsync(endpoints, Budget, token).ConfigureAwait(false);
            }

            browser = await pending.Browser.Task.WaitAsync(BrowserWait, _time, token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            LogProbeFailed(_logger, exception);
        }

        lock (_gate)
        {
            _pending = null;
            var message = new BoardMessage(pending.Id, LatencyRun.Compose(endpoints, browser, server));
            if (Merge([message], recent: true) == 0)
            {
                Broadcast(LatencyUpdate.State(_version, false));
            }
        }
    }

    private int Merge(IReadOnlyCollection<BoardMessage> candidates, bool recent)
    {
        var change = recent ? MessageWindow.Merge(_messages, candidates, Capacity) : null;
        var leaders = _leaders.Merge(candidates, RankCapacity, JackpotCapacity);
        if (change is null && leaders is null)
        {
            return 0;
        }

        var previous = _version;
        _version++;
        if (change is not null)
        {
            Volatile.Write(ref _messages, change.Next);
        }

        LatencyBoards? boards = null;
        IReadOnlyList<Guid> entered = [];
        if (leaders is not null)
        {
            boards = leaders.Boards(_leaders);
            entered = leaders.Entered(_leaders);
            Volatile.Write(ref _leaders, leaders);
        }

        Broadcast(LatencyUpdate.Delta(_version, previous, change?.Added ?? [], change?.Removed ?? [], _pending is not null, boards));
        var stored = (change?.Added ?? []).Select(message => message.Id).Concat(entered).ToHashSet();
        _journal.Append([.. candidates.Where(message => stored.Contains(message.Id)).DistinctBy(message => message.Id)]);
        return stored.Count;
    }

    private void Broadcast(LatencyUpdate update)
    {
        List<ILatencySubscriber>? dropped = null;
        foreach (var subscriber in _subscribers)
        {
            if (!subscriber.Deliver(update))
            {
                (dropped ??= []).Add(subscriber);
            }
        }

        if (dropped is not null)
        {
            _subscribers.ExceptWith(dropped);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Server latency probe failed")]
    private static partial void LogProbeFailed(ILogger logger, Exception exception);

    private sealed class Pending(Guid id)
    {
        public Guid Id { get; } = id;

        public TaskCompletionSource<IReadOnlyList<int?>> Browser { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
