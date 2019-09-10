using BenchmarkDotNet.Running;

namespace BZip.Benchmarks
{
  internal class Program
  {
    private static void Main(string[] args)
    {
      var summary = BenchmarkRunner.Run<CompressionBenchmarks>();
    }
  }
}