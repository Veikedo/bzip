using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using Nerdbank.Streams;

namespace BZip
{
  /// <summary>
  ///   A facade for unzipping files
  /// </summary>
  public sealed class BZipDecompressor : IDisposable
  {
    private readonly Stream _incomingStream;
    private readonly BZipArchiverOptions _options;
    private readonly Stream _outgoingStream;

    public BZipDecompressor(Stream incomingStream, Stream outgoingStream, BZipArchiverOptions? options = default)
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

    public void Decompress()
    {
      var processor = new Processor(_options.ChunkSize);
      var archiver = new BZipArchiver(_incomingStream, _outgoingStream, processor, _options);

      try
      {
        archiver.Process();
      }
      catch (Exception e)
      {
        throw new InvalidOperationException("Could not decompress stream", e);
      }
    }

    private sealed class Processor : IBZipProcessor
    {
      private readonly int _chunkSize;

      public Processor(int chunkSize)
      {
        _chunkSize = chunkSize;
      }

      public bool TryReadNextChunk(Stream stream, [MaybeNullWhen(false)] out Sequence<byte>? chunk)
      {
        Span<byte> chunkLengthBuffer = stackalloc byte[sizeof(int)];
        var chunkLengthBytesRead = stream.Read(chunkLengthBuffer);

        if (chunkLengthBytesRead == 0)
        {
          chunk = null;
          return false;
        }

        var chunkLength = BitConverter.ToInt32(chunkLengthBuffer);
        var bufferSize = chunkLength > _chunkSize ? chunkLength : _chunkSize;

        chunk = new Sequence<byte>(ArrayPool<byte>.Shared);
        try
        {
          var buffer = chunk.GetSpan(bufferSize).Slice(0, chunkLength);
          var bytesRead = stream.Read(buffer);
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

      public void ProcessChunk(StreamChunk chunk, Stream buffer)
      {
        using var zipStream = new GZipStream(chunk.Stream, CompressionMode.Decompress);
        zipStream.CopyTo(buffer);
      }

      public void WriteChunkLength(StreamChunk chunk, Stream stream)
      {
      }
    }
  }
}