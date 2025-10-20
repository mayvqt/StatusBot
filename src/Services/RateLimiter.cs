using System.Diagnostics;

namespace ServiceStatusBot.Services;

/// <summary>
/// A simple conservative rate limiter used to throttle Discord operations.
/// This is a lightweight fixed-window limiter (queue of timestamps). It is not a
/// full-featured token-bucket, but it is sufficient to prevent short bursts of
/// rapid send/modify operations against the Discord API.
/// </summary>
public class RateLimiter
{
    private readonly object _lock = new();
    private readonly int _maxOps;
    private readonly TimeSpan _window;
    private readonly Queue<DateTime> _timestamps = new();

    public RateLimiter(int maxOps = 5, TimeSpan? window = null)
    {
        _maxOps = maxOps;
        _window = window ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Attempt to consume a single operation token. Returns true if the operation is permitted
    /// under the configured <see cref="_maxOps"/> per <see cref="_window"/> window.
    /// </summary>
    public bool TryConsume()
    {
        lock (_lock)
        {
            // use UTC for timestamps in the limiter to avoid daylight savings / local timezone issues
            var now = DateTime.UtcNow;
            while (_timestamps.Count > 0 && (now - _timestamps.Peek()) > _window)
            {
                _timestamps.Dequeue();
            }

            if (_timestamps.Count < _maxOps)
            {
                _timestamps.Enqueue(now);
                return true;
            }

            return false;
        }
    }
}
