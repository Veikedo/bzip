using System.Diagnostics;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace BZip.Tests
{
  public class UnitTest1
  {
    public UnitTest1(ITestOutputHelper output)
    {
      _output = output;
    }

    private readonly ITestOutputHelper _output;

    [Fact]
    public void Test1()
    {
      const string filePath = "C:/temp/test.exe";

      var sw = Stopwatch.StartNew();

      {
        using var incomingStream = new FileStream(
          filePath,
          FileMode.Open,
          FileAccess.Read,
          FileShare.Read,
          4096,
          FileOptions.SequentialScan
        );

        using var outgoingStream = new FileStream(
          filePath + ".bz",
          FileMode.Create,
          FileAccess.Write,
          FileShare.None
        );

        var compressor = new BZipCompressor(incomingStream, outgoingStream);
        compressor.Compress();
      }

      {
        using var incomingStream = new FileStream(
          filePath + ".bz",
          FileMode.Open,
          FileAccess.Read,
          FileShare.Read,
          4096,
          FileOptions.SequentialScan
        );

        using var outgoingStream = new FileStream(
          filePath + ".unzip.exe",
          FileMode.Create,
          FileAccess.Write,
          FileShare.None
        );

        var decompressor = new BZipDecompressor(incomingStream, outgoingStream);
        decompressor.Decompress();
      }

      _output.WriteLine(sw.Elapsed.ToString());
    }
  }
}