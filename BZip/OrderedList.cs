using System.Collections.Generic;

namespace BZip
{
  internal class OrderedList<T>
  {
    private readonly IComparer<T> _comparer;
    private readonly List<T> _items;

    public OrderedList(IComparer<T> comparer)
    {
      _comparer = comparer ?? Comparer<T>.Default;
      _items = new List<T>();
    }

    public void Add(T item)
    {
      var firstLargestThanItem = _items.FindIndex(x => _comparer.Compare(x, item) > 0);
      var isLargest = firstLargestThanItem < 0;

      if (isLargest)
      {
        _items.Add(item);
      }
      else
      {
        _items.Insert(firstLargestThanItem, item);
      }
    }

    public bool TryPeek(out T item)
    {
      if (_items.Count == 0)
      {
        item = default;
        return false;
      }

      item = _items[0];
      return true;
    }

    public void RemoveSmallest()
    {
      if (_items.Count != 0)
      {
        _items.RemoveAt(0);
      }
    }
  }
}