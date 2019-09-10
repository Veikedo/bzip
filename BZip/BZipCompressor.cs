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
  public sealed class BZipCompressor : IDisposable
  {
    private readonly Stream _incomingStream;
    private readonly BZipArchiverOptions _options;
    private readonly Stream _outgoingStream;

    public BZipCompressor(Stream incomingStream, Stream outgoingStream, BZipArchiverOptions? options = default)
    {
      _incomingStream = incomingStream;
      _outgoingStream = outgoingStream;
      _options = options ?? new BZipArchiverOptions();
    }

    public void Dispose()
    {
      _incomingStream.Dispose();
      _outgoingStream.Dispose();
    }

    public void Compress()
    {
      var processor = new Processor(_options.ChunkSize, _options.CompressionLevel);
      var archiver = new BZipArchiver(_incomingStream, _outgoingStream, processor, _options);

      try
      {
        archiver.Process();
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("Could not compress stream", e);
      }
    }

    private sealed class Processor : IBZipProcessor
    {
      private readonly int _chunkSize;
      private readonly CompressionLevel _compressionLevel;

      public Processor(int chunkSize, CompressionLevel compressionLevel)
      {
        _chunkSize = chunkSize;
        _compressionLevel = compressionLevel;
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
        using var zipStream = new GZipStream(buffer, _compressionLevel);
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