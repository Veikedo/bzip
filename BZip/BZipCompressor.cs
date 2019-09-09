using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using Nerdbank.Streams;

namespace BZip
{
  /// <summary>
  ///   A facade for zipping files
  /// </summary>
  public class BZipCompressor
  {
    private readonly int _chunkSize;
    private readonly Stream _incomingStream;
    private readonly Stream _outgoingStream;

    public BZipCompressor(Stream incomingStream, Stream outgoingStream, int chunkSize = 1 * 1024 * 1024)
    {
      if (chunkSize <= 0)
      {
        throw new ArgumentOutOfRangeException(nameof(chunkSize));
      }

      _incomingStream = incomingStream;
      _outgoingStream = outgoingStream;
      _chunkSize = chunkSize;
    }

    public void Compress()
    {
      var processor = new Processor(_chunkSize);
      var archiver = new BZipArchiver(_incomingStream, _outgoingStream, processor);

      try
      {
        archiver.Process();
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("Could not compress stream", e);
      }
    }

    private class Processor : IBZipProcessor
    {
      private readonly int _chunkSize;

      public Processor(int chunkSize)
      {
        _chunkSize = chunkSize;
      }

      public bool TryReadNextChunk(Stream stream, out Sequence<byte>? chunk)
      {
        chunk = new Sequence<byte>(ArrayPool<byte>.Shared);

        try
        {
          var buffer = chunk.GetSpan(_chunkSize);
          var bytesRead = stream.Read(buffer);
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

      public void ProcessChunk(StreamChunk chunk, Stream buffer)
      {
        using var zipStream = new GZipStream(buffer, CompressionLevel.Optimal);
        chunk.Stream.CopyTo(zipStream);
      }

      public void WriteChunkLength(StreamChunk chunk, Stream stream)
      {
        var blockLength = BitConverter.GetBytes((int) chunk.Stream.Length);
        stream.Write(blockLength);
      }
    }
  }
}