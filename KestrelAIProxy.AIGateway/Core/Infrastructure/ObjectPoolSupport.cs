using System.Text;

using Microsoft.Extensions.ObjectPool;

namespace KestrelAIProxy.AIGateway.Core.Infrastructure;

/// <summary>
/// Object pool policy for StringBuilder instances
/// </summary>
public sealed class StringBuilderPooledObjectPolicy : IPooledObjectPolicy<StringBuilder>
{
    private const int MaxCapacity = 1024;

    public StringBuilder Create() => new StringBuilder();

    public bool Return(StringBuilder obj)
    {
        if (obj.Capacity > MaxCapacity)
        {
            // Don't pool very large StringBuilders
            return false;
        }

        obj.Clear();
        return true;
    }
}

/// <summary>
/// Generic pooled object policy that implements reset pattern
/// </summary>
public sealed class DefaultPooledObjectPolicy<T> : IPooledObjectPolicy<T> where T : class, new()
{
    public T Create() => new T();

    public bool Return(T obj)
    {
        // Reset the object if it implements a reset pattern
        if (obj is IPoolResettable resettable)
        {
            resettable.Reset();
        }
        return true;
    }
}

/// <summary>
/// Interface for objects that can be reset for pooling
/// </summary>
public interface IPoolResettable
{
    void Reset();
}

/// <summary>
/// High-performance object pool implementation
/// </summary>
public sealed class FastObjectPool<T>(IPooledObjectPolicy<T> policy, int maxCapacity = 64) : ObjectPool<T>
    where T : class
{
    private readonly IPooledObjectPolicy<T> _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    private readonly T[] _items = new T[maxCapacity];
    private int _count;

    public override T Get()
    {
        var items = _items;

        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (item != null && Interlocked.CompareExchange(ref items[i], null!, item) == item)
            {
                Interlocked.Decrement(ref _count);
                return item;
            }
        }

        return _policy.Create();
    }

    public override void Return(T obj)
    {
        if (!_policy.Return(obj) || _count >= maxCapacity)
        {
            return;
        }

        var items = _items;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null && Interlocked.CompareExchange(ref items[i], obj, null) == null)
            {
                Interlocked.Increment(ref _count);
                return;
            }
        }
    }
}