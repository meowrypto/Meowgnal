using System;
using System.Collections.Generic;

namespace Meowgnal.DataProviders;

// Thread-safe in-memory cache with TTL for fundamental data.
public sealed class FundamentalCache<T>
{
    private readonly object _lock = new();
    private readonly TimeSpan _ttl;
    private readonly Dictionary<string, (T Value, DateTime FetchedAt)> _cache = new();

    public FundamentalCache(TimeSpan ttl)
    {
        _ttl = ttl;
    }

    // Tries to get a cached value. Returns true if found and not expired.
    public bool TryGet(string key, out T? value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry) &&
                DateTime.UtcNow - entry.FetchedAt < _ttl)
            {
                value = entry.Value;
                return true;
            }
            value = default;
            return false;
        }
    }

    // Stores a value with the current timestamp.
    public void Set(string key, T value)
    {
        lock (_lock)
        {
            _cache[key] = (value, DateTime.UtcNow);
        }
    }

    // Removes all cached entries.
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }
}