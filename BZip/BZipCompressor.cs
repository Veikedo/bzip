using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Nerdbank.Streams;

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
      Exception error = null;
      var errorRaised = new ManualResetEventSlim();
      var completed = new ManualResetEventSlim();

      var workflow = new Thread(Workflow);
      workflow.Start();

      WaitHandle.WaitAny(new[] {errorRaised.WaitHandle, completed.WaitHandle});
      if (errorRaised.IsSet)
      {
        throw new Exception("Could not compress", error);
      }

      void Workflow()
      {
        var readerThread = new Thread(_ => CatchExceptions(SplitStreamByChunks));

        var zipThreads = Enumerable.Range(0, 1)
          .Select(_ => new Thread(_ => CatchExceptions(ZipChunks)))
          .ToList();

        var writerThread = new Thread(_ => CatchExceptions(WriteZippedChunks));

        readerThread.Start();
        zipThreads.ForEach(x => x.Start());
        writerThread.Start();

        readerThread.Join();
        zipThreads.ForEach(x => x.Join());

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

    private void SplitStreamByChunks()
    {
      var chunkIndex = 0;

      while (true)
      {
        var sequence = new Sequence<byte>(ArrayPool<byte>.Shared);

        var buffer = sequence.GetSpan(ChunkSize);
        var bytesRead = _incomingStream.Read(buffer);

        sequence.Advance(bytesRead);

        if (bytesRead == 0)
        {
          sequence.Dispose();
          break;
        }

        var chunk = new StreamChunk(chunkIndex, sequence);
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
        var sequence = new Sequence<byte>(ArrayPool<byte>.Shared);

        using var buffer = sequence.AsStream();
        using (var zipStream = new GZipStream(buffer, CompressionLevel.Optimal))
        {
          chunk.Stream.CopyTo(zipStream);
        }

        return new StreamChunk(chunk.Index, sequence);
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
        var blockLength = BitConverter.GetBytes(chunk.Stream.Length);

        _outgoingStream.Write(blockLength);
        chunk.Stream.CopyTo(_outgoingStream);

        nextIndexToWrite++;
        
        chunk.Dispose();
      }
    }
  }
}