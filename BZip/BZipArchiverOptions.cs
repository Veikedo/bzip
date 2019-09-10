using System;
using System.ComponentModel;
using System.IO.Compression;

namespace BZip
{
  public class BZipArchiverOptions
  {
    public const int Unbounded = -1;

    public BZipArchiverOptions(
      int chunkSize = 1 * 1024 * 1024,
      CompressionLevel compressionLevel = CompressionLevel.Optimal,
      int chunksToProcessBoundedCapacity = 30,
      int chunksToWriteBoundedCapacity = 30,
      int maxDegreeOfParallelism = Unbounded
    )
    {
      if (chunkSize <= 0)
      {
        throw new ArgumentOutOfRangeException(nameof(chunkSize));
      }

      if (!Enum.IsDefined(typeof(CompressionLevel), compressionLevel))
      {
        throw new InvalidEnumArgumentException(nameof(compressionLevel), (int) compressionLevel,
          typeof(CompressionLevel));
      }

      if (chunksToProcessBoundedCapacity <= 0)
      {
        throw new ArgumentOutOfRangeException(nameof(chunksToProcessBoundedCapacity));
      }

      if (chunksToWriteBoundedCapacity <= 0)
      {
        throw new ArgumentOutOfRangeException(nameof(chunksToWriteBoundedCapacity));
      }

      if (maxDegreeOfParallelism < Unbounded)
      {
        throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
      }

      ChunkSize = chunkSize;
      CompressionLevel = compressionLevel;
      MaxDegreeOfParallelism = maxDegreeOfParallelism == -1 ? Environment.ProcessorCount : maxDegreeOfParallelism;
      ChunksToProcessBoundedCapacity = chunksToProcessBoundedCapacity;
      ChunksToWriteBoundedCapacity = chunksToWriteBoundedCapacity;
    }

    public int ChunkSize { get; }
    public CompressionLevel CompressionLevel { get; }
    public int MaxDegreeOfParallelism { get; }
    public int ChunksToProcessBoundedCapacity { get; }
    public int ChunksToWriteBoundedCapacity { get; }
  }
}