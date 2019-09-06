using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using Nerdbank.Streams;

namespace BZip
{
  internal class BZipDecompressor : BZipProcessor
  {
    public BZipDecompressor(Stream incomingStream, Stream outgoingStream) : base(incomingStream, outgoingStream)
    {
    }

    protected override bool TryGetNextChunk([MaybeNullWhen(false)] out Sequence<byte>? chunk)
    {
      Span<byte> chunkLengthBuffer = stackalloc byte[sizeof(int)];
      var chunkLengthBytesRead = _incomingStream.Read(chunkLengthBuffer);

      if (chunkLengthBytesRead == 0)
      {
        chunk = null;
        return false;
      }

      var chunkLength = BitConverter.ToInt32(chunkLengthBuffer);
      var bufferSize = chunkLength > ChunkSize ? chunkLength : ChunkSize;

      chunk = new Sequence<byte>(ArrayPool<byte>.Shared);
      try
      {
        var buffer = chunk.GetSpan(bufferSize).Slice(0, chunkLength);
        var bytesRead = _incomingStream.Read(buffer);
        chunk.Advance(bytesRead);

        if (bytesRead < chunkLength)
        {
          throw new InvalidOperationException("Archive entry is corrupted");
        }
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
      using var zipStream = new GZipStream(chunk.Stream, CompressionMode.Decompress);
      zipStream.CopyTo(buffer);
    }
    
    protected override void WriteBlockLength(StreamChunk chunk)
    {
    }
  }
}