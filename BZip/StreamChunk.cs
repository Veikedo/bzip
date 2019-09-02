using System;
using System.Buffers;

namespace BZip
{
  internal class StreamChunk : IDisposable
  {
    private readonly int _bytesRead;
    private readonly IMemoryOwner<byte> _memoryOwner;
    public StreamChunk(int chunkIndex, IMemoryOwner<byte> memoryOwner, int bytesRead)
    {
      _memoryOwner = memoryOwner;
      _bytesRead = bytesRead;
      Index = chunkIndex;
    }

    public int Index { get; }
    public Span<byte> Span => _memoryOwner.Memory.Span.Slice(0, _bytesRead);

    public void Dispose()
    {
      _memoryOwner.Dispose();
    }
  }
}