using System;
using System.Collections.Generic;
using System.Threading;

namespace BZip
{
  internal class ProducerConsumer<T>
  {
    private const int UnboundedCapacity = -1;
    private readonly int _boundedCapacity;
    private readonly SemaphoreSlim _bulkhead;
    private readonly Queue<T> _queue = new Queue<T>();
    private readonly object _sync = new object();
    private bool _isDead;

    public ProducerConsumer(int boundedCapacity)
    {
      if (boundedCapacity <= 0 && boundedCapacity != UnboundedCapacity)
      {
        throw new ArgumentOutOfRangeException(nameof(boundedCapacity));
      }

      _boundedCapacity = boundedCapacity;

      if (boundedCapacity != UnboundedCapacity)
      {
        _bulkhead = new SemaphoreSlim(boundedCapacity, boundedCapacity);
      }
    }

    public ProducerConsumer() : this(UnboundedCapacity)
    {
    }

    public bool TryAdd(T value)
    {
      if (value == null)
      {
        throw new ArgumentNullException(nameof(value));
      }

      _bulkhead?.Wait();

      lock (_sync)
      {
        if (_isDead)
        {
          return false;
        }

        _queue.Enqueue(value);
        Monitor.Pulse(_sync);

        return true;
      }
    }

    public bool TryTake(out T value)
    {
      lock (_sync)
      {
        while (_queue.Count == 0 && !_isDead)
        {
          Monitor.Wait(_sync);
        }

        if (_queue.Count == 0)
        {
          value = default;
          return false;
        }

        value = _queue.Dequeue();
        if (!_isDead)
        {
          _bulkhead?.Release();
        }

        return true;
      }
    }

    public void CompleteAdding()
    {
      lock (_sync)
      {
        _isDead = true;

        if (_bulkhead != null)
        {
          var currentlyAcquired = _boundedCapacity - _bulkhead.CurrentCount;
          if (currentlyAcquired > 0)
          {
            _bulkhead.Release(currentlyAcquired);
          }
        }

        Monitor.PulseAll(_sync);
      }
    }
  }
}