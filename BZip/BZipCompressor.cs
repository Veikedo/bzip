using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using Nerdbank.Streams;

namespace BZip
{
  internal class BZipCompressor : BZipProcessor
  {
    public BZipCompressor(Stream incomingStream, Stream outgoingStream) : base(incomingStream, outgoingStream)
    {
    }

    protected override bool TryGetNextChunk(out Sequence<byte> chunk)
    {
      chunk = new Sequence<byte>(ArrayPool<byte>.Shared);

      try
      {
        var buffer = chunk.GetSpan(ChunkSize);
        var bytesRead = _incomingStream.Read(buffer);
        if (bytesRead == 0)
        {
          chunk.Dispose();
          return false;
        }

        chunk.Advance(bytesRead);
      }
      catch
      {
        chunk.Dispose();
        throw;
      }

      return true;
    }

    protected override void ProcessChunk(Stream buffer, StreamChunk chunk)
    {
      using var zipStream = new GZipStream(buffer, CompressionLevel.Optimal);
      chunk.Stream.CopyTo(zipStream);
    }

    protected override void WriteBlockLength(StreamChunk chunk)
    {
      var blockLength = BitConverter.GetBytes((int) chunk.Stream.Length);
      _outgoingStream.Write(blockLength);
    }
  }
}