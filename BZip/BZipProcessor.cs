using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading;
using Nerdbank.Streams;

namespace BZip
{
  internal abstract class BZipProcessor
  {
    protected const int ChunkSize = 1 * 1024 * 1024;
    private readonly ProducerConsumer<StreamChunk> _chunksToProcess;
    private readonly ProducerConsumer<StreamChunk> _chunksToWrite;
    protected readonly Stream _incomingStream;
    protected readonly Stream _outgoingStream;

    protected BZipProcessor(Stream incomingStream, Stream outgoingStream)
    {
      _incomingStream = incomingStream ?? throw new ArgumentNullException(nameof(incomingStream));
      _outgoingStream = outgoingStream ?? throw new ArgumentNullException(nameof(outgoingStream));

      _chunksToProcess = new ProducerConsumer<StreamChunk>(100);
      _chunksToWrite = new ProducerConsumer<StreamChunk>();
    }

    public void Process()
    {
      Exception? error = null;
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
        var readerThread = new Thread(_ => CatchExceptions(SplitIncomingStreamByChunks));

        var processThreads = Enumerable.Range(0, Environment.ProcessorCount)
          .Select(_ => new Thread(_ => CatchExceptions(ProcessChunks)))
          .ToList();

        var writerThread = new Thread(_ => CatchExceptions(WriteProcessedChunks));

        readerThread.Start();
        processThreads.ForEach(x => x.Start());
        writerThread.Start();

        readerThread.Join();
        processThreads.ForEach(x => x.Join());

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

    private void SplitIncomingStreamByChunks()
    {
      var chunkIndex = 0;

      while (TryGetNextChunk(out var sequence))
      {
        var chunk = new StreamChunk(chunkIndex, sequence);
        _chunksToProcess.TryAdd(chunk);

        chunkIndex++;
      }

      _chunksToProcess.CompleteAdding();
    }

    protected abstract bool TryGetNextChunk(out Sequence<byte> chunk);

    private void ProcessChunks()
    {
      while (_chunksToProcess.TryTake(out var chunk))
      {
        var sequence = new Sequence<byte>(ArrayPool<byte>.Shared);
        using (var buffer = sequence.AsStream())
        {
          ProcessChunk(buffer, chunk);
        }

        var processedChunk = new StreamChunk(chunk.Index, sequence);
        chunk.Dispose();

        _chunksToWrite.TryAdd(processedChunk);
      }
    }

    protected abstract void ProcessChunk(Stream buffer, StreamChunk chunk);

    private void WriteProcessedChunks()
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
        WriteBlockLength(chunk);

        chunk.Stream.CopyTo(_outgoingStream);
        chunk.Dispose();

        nextIndexToWrite++;
      }
    }

    protected abstract void WriteBlockLength(StreamChunk chunk);
  }
}