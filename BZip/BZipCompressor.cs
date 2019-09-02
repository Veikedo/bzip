using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace BZip
{
  public class BZipCompressor
  {
    private const int ChunkSize = 1 * 1024 * 1024;
    private readonly ProducerConsumer<StreamChunk> _chunksToArchive;
    private readonly ProducerConsumer<StreamChunk> _chunksToWrite;
    private readonly Stream _incomingStream;
    private readonly Stream _outgoingStream;

    public BZipCompressor(Stream incomingStream, Stream outgoingStream)
    {
      _incomingStream = incomingStream ?? throw new ArgumentNullException(nameof(incomingStream));
      _outgoingStream = outgoingStream ?? throw new ArgumentNullException(nameof(outgoingStream));

      _chunksToArchive = new ProducerConsumer<StreamChunk>(100);
      _chunksToWrite = new ProducerConsumer<StreamChunk>();
    }

    public void Compress()
    {
      var readerThread = new Thread(_ => SplitStreamByChunks());

      var archiverThreads = Enumerable
        .Range(0, Environment.ProcessorCount)
        .Select(_ => new Thread(_ => ArchiveChunks()))
        .ToList();

      var writerThread = new Thread(_ => WriteArchivedChunks());

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
        _chunksToArchive.TryAdd(chunk);

        chunkIndex++;
      }

      _chunksToArchive.CompleteAdding();
    }

    private void ArchiveChunks()
    {
      while (_chunksToArchive.TryTake(out var chunk))
      {
        var archivedChunk = ArchiveChunk(chunk);
        chunk.Dispose();

        _chunksToWrite.TryAdd(archivedChunk);
      }

      StreamChunk ArchiveChunk(StreamChunk chunk)
      {
        var memoryOwner = MemoryPool<byte>.Shared.Rent(ChunkSize * 2);

        using var ms = new SuperMemoryStream(memoryOwner.Memory);
        using var gZipStream = new GZipStream(ms, CompressionLevel.Optimal);

        gZipStream.Write(chunk.Span);
        gZipStream.Flush();

        return new StreamChunk(chunk.Index, memoryOwner, (int) ms.Position);
      }
    }

    private void WriteArchivedChunks()
    {
      var comparer = Comparer<StreamChunk>.Create((x, y) => x.Index - y.Index);
      var sprinters = new OrderedList<StreamChunk>(comparer);

      var nextIndexToWrite = 0;

      while (_chunksToWrite.TryTake(out var chunk))
      {
        if (chunk.Index == nextIndexToWrite)
        {
          WriteBlockAndIncrementIndex(chunk);

          while (sprinters.TryPeek(out var sprinter) && sprinter.Index == nextIndexToWrite)
          {
            WriteBlockAndIncrementIndex(sprinter);
            sprinters.RemoveSmallest();
          }
        }
        else
        {
          sprinters.Add(chunk);
        }
      }

      void WriteBlockAndIncrementIndex(StreamChunk chunk)
      {
        var blockLength = BitConverter.GetBytes(chunk.Index);

        _outgoingStream.Write(blockLength);
        _outgoingStream.Write(chunk.Span);

        nextIndexToWrite++;
      }
    }
  }
}