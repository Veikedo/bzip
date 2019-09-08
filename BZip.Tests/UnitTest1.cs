using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Xunit;
using Xunit.Abstractions;

namespace BZip.Tests
{
  public class UnitTest1 : IClassFixture<FileFixture>
  {
    public UnitTest1(ITestOutputHelper output, FileFixture fixture)
    {
      _output = output;
      _fixture = fixture;
    }

    private readonly ITestOutputHelper _output;
    private readonly FileFixture _fixture;

    [Fact]
    public void Test1()
    {
      var filePath = _fixture.FilePath;

      var sw = Stopwatch.StartNew();

      {
        using var incomingStream = new FileStream(filePath, FileMode.Open);
        using var outgoingStream = new FileStream(filePath + ".bz", FileMode.Create);

        var compressor = new BZipCompressor(incomingStream, outgoingStream);
        compressor.Compress();
      }

      {
        using var incomingStream = new FileStream(filePath + ".bz", FileMode.Open);
        using var outgoingStream = new FileStream(filePath + ".unzip.exe", FileMode.Create);

        var compressor = new BZipDecompressor(incomingStream, outgoingStream);
        compressor.Decompress();
      }

      using var unzippedFile = new FileStream(filePath + ".unzip.exe", FileMode.Open);

      using var md5 = MD5.Create();
      var actualHash = md5.ComputeHash(unzippedFile);

      Assert.Equal(_fixture.FileHash, actualHash);

      _output.WriteLine(sw.Elapsed.ToString());
    }
  }
}