using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace StatusBot.Services;

/// <summary>
/// A small conservative rate limiter used to throttle Discord operations.
/// Implements a fixed-window limiter using a queue of timestamps. Thread-safe.
/// </summary>
public sealed class RateLimiter
{
    private readonly object _lock = new();
    private readonly Queue<DateTimeOffset> _timestamps = new();

    /// <summary>
    /// Maximum operations allowed within the configured <see cref="Window"/>.
    /// </summary>
    /// <summary>
    /// Maximum operations allowed within the configured <see cref="Window"/>.
    /// This value can be adjusted at runtime via <see cref="AdjustRate"/>.
    /// </summary>
    public int MaxOperations { get; private set; }

    /// <summary>
    /// Sliding window duration for limiting.
    /// </summary>
    /// <summary>
    /// Sliding window duration for limiting.
    /// This value can be adjusted at runtime via <see cref="AdjustRate"/>.
    /// </summary>
    public TimeSpan Window { get; private set; }

    /// <summary>
    /// Create a new RateLimiter.
    /// </summary>
    /// <param name="maxOps">Maximum operations allowed per window (min 1).</param>
    /// <param name="window">Window duration. Defaults to 5 seconds.</param>
    public RateLimiter(int maxOps = 5, TimeSpan? window = null)
    {
        MaxOperations = Math.Max(1, maxOps);
        Window = window ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Attempt to consume a single operation immediately.
    /// Returns true if allowed; otherwise false.
    /// </summary>
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

    /// <summary>
    /// How many additional operations can be consumed right now.
    /// </summary>
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

    /// <summary>
    /// Returns the time until the next operation slot becomes available.
    /// Returns TimeSpan.Zero if a slot is available now.
    /// </summary>
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

    // Diagnostics counters
    private long _totalConsumed;
    private long _totalBlocked;

    /// <summary>
    /// Total number of operations successfully consumed since construction or last ClearDiagnostics.
    /// </summary>
    public long TotalConsumed => Interlocked.Read(ref _totalConsumed);

    /// <summary>
    /// Total number of operations that were blocked since construction or last ClearDiagnostics.
    /// </summary>
    public long TotalBlocked => Interlocked.Read(ref _totalBlocked);

    /// <summary>
    /// Occurs when an operation is allowed (a token was consumed).
    /// Handlers should be non-blocking and lightweight.
    /// </summary>
    public event Action? OnAllowed;

    /// <summary>
    /// Occurs when an operation is blocked due to rate limiting.
    /// </summary>
    public event Action? OnBlocked;

    /// <summary>
    /// Clear diagnostic counters.
    /// </summary>
    public void ClearDiagnostics()
    {
        Interlocked.Exchange(ref _totalConsumed, 0);
        Interlocked.Exchange(ref _totalBlocked, 0);
    }

    /// <summary>
    /// Adjust the limiter parameters at runtime. Existing timestamps remain and will be evaluated against the new window/maxOps.
    /// </summary>
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

    /// <summary>
    /// Wait asynchronously until a slot is available or the timeout/cancellation occurs.
    /// Returns true if a slot was consumed.
    /// </summary>
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

    /// <summary>
    /// Convenience wrapper that attempts to acquire a token asynchronously within the provided timeout.
    /// Equivalent to calling <see cref="WaitAsync"/>; provided for clearer intent in callers.
    /// </summary>
    public Task<bool> TryConsumeAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        => WaitAsync(timeout, cancellationToken);

    /// <summary>
    /// Clear stored timestamps (useful for testing or resetting state).
    /// </summary>
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
