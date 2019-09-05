using System;
using System.Buffers;

namespace BZip
{
  internal class StreamChunk : IDisposable, IComparable<StreamChunk>
  {
    private readonly int _bytesRead;
    private readonly IMemoryOwner<byte> _memoryOwner;
    private readonly Memory<byte> _memory;

    public StreamChunk(int chunkIndex, IMemoryOwner<byte> memoryOwner, int bytesRead)
    {
      _memoryOwner = memoryOwner;
      _memory = memoryOwner.Memory.Slice(0, bytesRead);
      _bytesRead = bytesRead;
      Index = chunkIndex;
    }

    public int Index { get; }
    public Span<byte> Span => _memory.Span;

    public int CompareTo(StreamChunk other)
    {
      if (ReferenceEquals(this, other))
      {
        return 0;
      }

      if (ReferenceEquals(null, other))
      {
        return 1;
      }

      return Index.CompareTo(other.Index);
    }

    public void Dispose()
    {
      _memoryOwner.Dispose();
    }
  }
}