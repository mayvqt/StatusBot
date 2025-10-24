using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StatusBot.Services;

/// <summary>Thread-safe fixed-window rate limiter</summary>
public sealed class RateLimiter
{
    private readonly object _lock = new();
    private readonly Queue<DateTimeOffset> _timestamps = new();

    /// <summary>Max operations per window</summary>
    public int MaxOperations { get; private set; }

    /// <summary>Time window for limiting</summary>
    public TimeSpan Window { get; private set; }

    /// <summary>Create rate limiter with max ops and window</summary>
    public RateLimiter(int maxOps = 5, TimeSpan? window = null)
    {
        MaxOperations = Math.Max(1, maxOps);
        Window = window ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>Try consume operation immediately</summary>
    public bool TryConsume()
    {
        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            CleanupStale(now);
            if (_timestamps.Count < MaxOperations)
            {
                _timestamps.Enqueue(now);
                Interlocked.Increment(ref _totalConsumed);
                OnAllowed?.Invoke();
                return true;
            }
            Interlocked.Increment(ref _totalBlocked);
            OnBlocked?.Invoke();
            return false;
        }
    }

    /// <summary>Available operation slots</summary>
    public int RemainingOperations
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            lock (_lock)
            {
                CleanupStale(now);
                return Math.Max(0, MaxOperations - _timestamps.Count);
            }
        }
    }

    /// <summary>Time until next slot available</summary>
    public TimeSpan NextRetryAfter
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            lock (_lock)
            {
                CleanupStale(now);
                if (_timestamps.Count < MaxOperations) return TimeSpan.Zero;
                var oldest = _timestamps.Peek();
                var until = (oldest + Window) - now;
                return until <= TimeSpan.Zero ? TimeSpan.Zero : until;
            }
        }
    }

    private long _totalConsumed;
    private long _totalBlocked;

    /// <summary>Operations consumed since reset</summary>
    public long TotalConsumed => Interlocked.Read(ref _totalConsumed);

    /// <summary>Operations blocked since reset</summary>
    public long TotalBlocked => Interlocked.Read(ref _totalBlocked);

    /// <summary>Operation allowed event</summary>
    public event Action? OnAllowed;

    /// <summary>Operation blocked event</summary>
    public event Action? OnBlocked;

    /// <summary>Reset diagnostic counters</summary>
    public void ClearDiagnostics()
    {
        Interlocked.Exchange(ref _totalConsumed, 0);
        Interlocked.Exchange(ref _totalBlocked, 0);
    }

    /// <summary>Update rate limit parameters</summary>
    public void AdjustRate(int maxOps, TimeSpan? window = null)
    {
        lock (_lock)
        {
            MaxOperations = Math.Max(1, maxOps);
            if (window.HasValue) Window = window.Value;
            // cleanup in case window shrank
            CleanupStale(DateTimeOffset.UtcNow);
        }
    }

    /// <summary>Wait for rate limit slot</summary>
    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTimeOffset.UtcNow;
            lock (_lock)
            {
                CleanupStale(now);
                if (_timestamps.Count < MaxOperations)
                {
                    _timestamps.Enqueue(now);
                    return true;
                }
            }

            var remaining = timeout - sw.Elapsed;
            if (remaining <= TimeSpan.Zero) return false;

            var wait = NextRetryAfter;
            if (wait <= TimeSpan.Zero) wait = TimeSpan.FromMilliseconds(10);
            if (wait > remaining) wait = remaining;

            try
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }

    /// <summary>Try consume with async wait</summary>
    public Task<bool> TryConsumeAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => WaitAsync(timeout, cancellationToken);

    /// <summary>Reset limiter state</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _timestamps.Clear();
        }
    }

    private void CleanupStale(DateTimeOffset now)
    {
        while (_timestamps.Count > 0 && (now - _timestamps.Peek()) > Window)
        {
            _timestamps.Dequeue();
        }
    }
}
