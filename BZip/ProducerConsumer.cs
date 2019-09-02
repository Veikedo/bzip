using System;
using System.Collections.Generic;
using System.Threading;

namespace BZip
{
  public class ProducerConsumer<T>
  {
    private readonly Queue<T> _queue = new Queue<T>();
    private readonly object _sync = new object();
    private bool _isDead;

    public bool TryAdd(T task)
    {
      if (task == null)
      {
        throw new ArgumentNullException(nameof(task));
      }

      lock (_sync)
      {
        if (_isDead)
        {
          return false;
        }

        _queue.Enqueue(task);
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

        return true;
      }
    }

    public void Stop()
    {
      lock (_sync)
      {
        _isDead = true;
        Monitor.PulseAll(_sync);
      }
    }
  }
}