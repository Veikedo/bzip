using System.IO;
using BenchmarkDotNet.Attributes;

namespace BZip.Benchmarks
{
  [MemoryDiagnoser]
  [RankColumn]
  public class CompressionBenchmarks
  {
    [Params(1, 5, 10, 30, 100)] public int ChunksBoundedCapacity;

    [Benchmark]
    public void Compressor()
    {
      using var incomingStream = File.OpenRead("D:/temp/1gb.bin");
      using var outgoingStream = File.OpenWrite("D:/temp/1gb.bz");

      var options = new BZipArchiverOptions(
        chunksToProcessBoundedCapacity: ChunksBoundedCapacity,
        chunksToWriteBoundedCapacity: ChunksBoundedCapacity);

      var compressor = new BZipCompressor(incomingStream, outgoingStream, options);
      compressor.Compress();
    }
  }
}