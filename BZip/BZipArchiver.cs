using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading;
using Nerdbank.Streams;

namespace BZip
{
  /// <summary>
  ///   Encapsulates common logic to zip/unzip files
  /// </summary>
  internal sealed class BZipArchiver : IDisposable
  {
    private readonly ProducerConsumer<StreamChunk> _chunksToProcess;
    private readonly ProducerConsumer<StreamChunk> _chunksToWrite;
    private readonly Stream _incomingStream;
    private readonly BZipArchiverOptions _options;
    private readonly Stream _outgoingStream;
    private readonly IBZipProcessor _zipProcessor;

    public BZipArchiver(
      Stream incomingStream,
      Stream outgoingStream,
      IBZipProcessor zipProcessor,
      BZipArchiverOptions options)
    {
      _incomingStream = incomingStream;
      _outgoingStream = outgoingStream;
      _zipProcessor = zipProcessor;
      _options = options;

      _chunksToProcess = new ProducerConsumer<StreamChunk>(options.ChunksToProcessBoundedCapacity);
      _chunksToWrite = new ProducerConsumer<StreamChunk>(options.ChunksToWriteBoundedCapacity);
    }

    public void Dispose()
    {
      _chunksToProcess.Dispose();
      _chunksToWrite.Dispose();
      _incomingStream.Dispose();
      _outgoingStream.Dispose();
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
        throw new InvalidOperationException("Could not process entry", error);
      }

      void Workflow()
      {
        var readerThread = new Thread(_ => CatchExceptions(SplitIncomingStreamByChunks));

        var processThreads = Enumerable
          .Range(0, _options.MaxDegreeOfParallelism)
          .Select(_ => new Thread(__ => CatchExceptions(ProcessChunks)))
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

      while (_zipProcessor.TryReadNextChunk(_incomingStream, out var sequence))
      {
        var chunk = new StreamChunk(chunkIndex, sequence);
        _chunksToProcess.TryAdd(chunk);

        chunkIndex++;
      }

      _chunksToProcess.CompleteAdding();
    }

    private void ProcessChunks()
    {
      while (_chunksToProcess.TryTake(out var chunk))
      {
        var sequence = new Sequence<byte>(ArrayPool<byte>.Shared);
        using (chunk)
        {
          using var buffer = sequence.AsStream();
          _zipProcessor.ProcessChunk(chunk, buffer);
        }

        var processedChunk = new StreamChunk(chunk.Index, sequence);

        _chunksToWrite.TryAdd(processedChunk);
      }
    }

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
        using (chunk)
        {
          _zipProcessor.WriteChunkLength(chunk, _outgoingStream);
          chunk.Stream.CopyTo(_outgoingStream);
        }

        nextIndexToWrite++;
      }
    }
  }
}