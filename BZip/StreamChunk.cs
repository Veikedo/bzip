using System;
using System.IO;
using Nerdbank.Streams;

namespace BZip
{
  internal sealed class StreamChunk : IDisposable, IComparable<StreamChunk>
  {
    private readonly Sequence<byte> _sequence;

    public StreamChunk(int chunkIndex, Sequence<byte> sequence)
    {
      if (chunkIndex < 0)
      {
        throw new ArgumentOutOfRangeException(nameof(chunkIndex));
      }

      Index = chunkIndex;
      _sequence = sequence;
    }

    public int Index { get; }
    public Stream Stream => _sequence.AsReadOnlySequence.AsStream();

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
      _sequence.Dispose();
    }
  }
}