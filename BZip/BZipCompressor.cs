using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace BZip
{
  public class BZipCompressor
  {
    private const int ChunkSize = 1 * 1024 * 1024;
    private readonly ProducerConsumer<StreamChunk> _chunksToWrite;
    private readonly ProducerConsumer<StreamChunk> _chunksToZip;
    private readonly Stream _incomingStream;
    private readonly Stream _outgoingStream;

    public BZipCompressor(Stream incomingStream, Stream outgoingStream)
    {
      _incomingStream = incomingStream ?? throw new ArgumentNullException(nameof(incomingStream));
      _outgoingStream = outgoingStream ?? throw new ArgumentNullException(nameof(outgoingStream));

      _chunksToZip = new ProducerConsumer<StreamChunk>(100);
      _chunksToWrite = new ProducerConsumer<StreamChunk>();
    }

    public void Compress()
    {
      var readerThread = new Thread(_ => SplitStreamByChunks());

      var archiverThreads = Enumerable
        .Range(0, 1)
        .Select(_ => new Thread(_ => ZipChunks()))
        .ToList();

      var writerThread = new Thread(_ => WriteZippedChunks());

      readerThread.Start();
      archiverThreads.ForEach(x => x.Start());
      writerThread.Start();

      readerThread.Join();
      archiverThreads.ForEach(x => x.Join());

      _chunksToWrite.CompleteAdding();

      writerThread.Join();
    }

    private void SplitStreamByChunks()
    {
      var chunkIndex = 0;

      while (true)
      {
        var memoryOwner = MemoryPool<byte>.Shared.Rent(ChunkSize);
        var bytesRead = _incomingStream.Read(memoryOwner.Memory.Span);

        if (bytesRead == 0)
        {
          memoryOwner.Dispose();
          break;
        }

        var chunk = new StreamChunk(chunkIndex, memoryOwner, bytesRead);
        _chunksToZip.TryAdd(chunk);

        chunkIndex++;
      }

      _chunksToZip.CompleteAdding();
    }

    private void ZipChunks()
    {
      while (_chunksToZip.TryTake(out var chunk))
      {
        var zippedChunk = ZipChunk(chunk);
        chunk.Dispose();

        _chunksToWrite.TryAdd(zippedChunk);
      }

      StreamChunk ZipChunk(StreamChunk chunk)
      {
        using var ms = new MemoryStream();
        using (var zipStream = new GZipStream(ms, CompressionLevel.Optimal, true))
        {
          zipStream.Write(chunk.Span);
        }

//        var array = ms.ToArray();
        
        var bytesRead = (int) ms.Position;
        var memoryOwner = MemoryPool<byte>.Shared.Rent(bytesRead);
        
        ms.Read(memoryOwner.Memory.Span);

        return new StreamChunk(chunk.Index, memoryOwner, bytesRead);
      }
    }

    private void WriteZippedChunks()
    {
      var sprinters = new OrderedList<StreamChunk>();

      var nextIndexToWrite = 0;

      while (_chunksToWrite.TryTake(out var chunk))
      {
        if (chunk.Index == nextIndexToWrite)
        {
          WriteChunkAndIncrementIndex(chunk);

          while (sprinters.TryPeek(out var sprinter) && sprinter.Index == nextIndexToWrite)
          {
            WriteChunkAndIncrementIndex(sprinter);
            sprinters.RemoveSmallest();
          }
        }
        else
        {
          sprinters.Add(chunk);
        }
      }

      void WriteChunkAndIncrementIndex(StreamChunk chunk)
      {
        var blockLength = BitConverter.GetBytes(chunk.Span.Length);

        _outgoingStream.Write(blockLength);
        _outgoingStream.Write(chunk.Span);

        nextIndexToWrite++;
      }
    }
  }
}