using System;
using System.IO;

namespace BZip
{
  internal class SuperMemoryStream : Stream
  {
    private readonly Memory<byte> _memory;
    private int _position;

    public SuperMemoryStream(Memory<byte> memory)
    {
      _memory = memory;
    }

    public override bool CanRead { get; }
    public override bool CanSeek { get; }
    public override bool CanWrite { get; } = true;
    public override long Length { get; }

    public override long Position
    {
      get => _position;
      set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
      throw new NotImplementedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
      throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      var span = new ReadOnlySpan<byte>(buffer, offset, count);
      span.CopyTo(_memory.Span.Slice(_position));

      _position += count;
    }
  }
}