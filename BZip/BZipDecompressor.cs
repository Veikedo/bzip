using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace BZip
{
  public class BZipDecompressor
  {
    private const int ChunkSize = 1 * 1024 * 1024;
    private readonly ProducerConsumer<StreamChunk> _chunksToUnzip;
    private readonly ProducerConsumer<StreamChunk> _chunksToWrite;
    private readonly Stream _incomingStream;
    private readonly Stream _outgoingStream;

    public BZipDecompressor(Stream incomingStream, Stream outgoingStream)
    {
      _incomingStream = incomingStream ?? throw new ArgumentNullException(nameof(incomingStream));
      _outgoingStream = outgoingStream ?? throw new ArgumentNullException(nameof(outgoingStream));

      _chunksToUnzip = new ProducerConsumer<StreamChunk>(5);
      _chunksToWrite = new ProducerConsumer<StreamChunk>();
    }

    public void Decompress()
    {
      Exception error = null;
      var errorRaised = new ManualResetEventSlim();
      var completed = new ManualResetEventSlim();

      var workflow = new Thread(Workflow);
      workflow.Start();

      WaitHandle.WaitAny(new[] {errorRaised.WaitHandle, completed.WaitHandle});
      if (errorRaised.IsSet)
      {
        throw error;
      }

      void Workflow()
      {
        var readerThread = new Thread(_ => CatchExceptions(ReadStreamByChunks));

        var unzipThreads = Enumerable.Range(0, Environment.ProcessorCount)
          .Select(_ => new Thread(_ => CatchExceptions(UnzipChunks)))
          .ToList();

        var writerThread = new Thread(_ => CatchExceptions(WriteUnzippedChunks));

        readerThread.Start();
        unzipThreads.ForEach(x => x.Start());
        writerThread.Start();

        readerThread.Join();
        unzipThreads.ForEach(x => x.Join());

        _chunksToWrite.CompleteAdding();

        writerThread.Join();
        completed.Set();
      }

      void CatchExceptions(Action action)
      {
        try
        {
          action();
        }
        catch (Exception e)
        {
          error = e;
          errorRaised.Set();
        }
      }
    }

    private void ReadStreamByChunks()
    {
      var chunkIndex = 0;
      Span<byte> chunkLengthBuffer = stackalloc byte[sizeof(int)];

      while (true)
      {
        var chunkLengthBytesRead = _incomingStream.Read(chunkLengthBuffer);
        if (chunkLengthBytesRead == 0)
        {
          break;
        }

        var chunkLength = BitConverter.ToInt32(chunkLengthBuffer);
        var minBufferSize = chunkLength > ChunkSize ? chunkLength : ChunkSize;
        var memoryOwner = MemoryPool<byte>.Shared.Rent(minBufferSize);

        var bytesRead = _incomingStream.Read(memoryOwner.Memory.Span.Slice(0, chunkLength));

        if (bytesRead == 0)
        {
          // TODO Add error handling
          throw new InvalidOperationException("Invalid format");
        }

        var chunk = new StreamChunk(chunkIndex, memoryOwner, bytesRead);
        _chunksToUnzip.TryAdd(chunk);

        chunkIndex++;
      }

      _chunksToUnzip.CompleteAdding();
    }

    private void UnzipChunks()
    {
      while (_chunksToUnzip.TryTake(out var chunk))
      {
        var unzippedChunk = UnzipChunk(chunk);
        chunk.Dispose();

        _chunksToWrite.TryAdd(unzippedChunk);
      }

      StreamChunk UnzipChunk(StreamChunk chunk)
      {
        var memoryOwner = MemoryPool<byte>.Shared.Rent(ChunkSize * 2);

        using var ms = new MemoryStream(chunk.Span.ToArray());
        using var gZipStream = new GZipStream(ms, CompressionMode.Decompress);

        var bytesRead = gZipStream.Read(memoryOwner.Memory.Span);
        gZipStream.Flush();

        return new StreamChunk(chunk.Index, memoryOwner, bytesRead);
      }
    }

    private void WriteUnzippedChunks()
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
        _outgoingStream.Write(chunk.Span);
        nextIndexToWrite++;
      }
    }
  }
}