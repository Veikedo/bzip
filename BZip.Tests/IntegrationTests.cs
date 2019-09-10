using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Xunit;
using Xunit.Abstractions;

namespace BZip.Tests
{
  public class IntegrationTests : IClassFixture<FileFixture>
  {
    public IntegrationTests(ITestOutputHelper output, FileFixture fixture)
    {
      _output = output;
      _fixture = fixture;
    }

    private readonly ITestOutputHelper _output;
    private readonly FileFixture _fixture;

    [Fact]
    public void Unzipped_file_has_the_same_hash_as_the_original()
    {
      var originalFileHash = _fixture.FileHash;
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
        using var outgoingStream = new FileStream(filePath + ".unzip", FileMode.Create);

        var decompressor = new BZipDecompressor(incomingStream, outgoingStream);
        decompressor.Decompress();
      }

      using var unzippedFile = new FileStream(filePath + ".unzip", FileMode.Open);

      using var md5 = MD5.Create();
      var actualHash = md5.ComputeHash(unzippedFile);

      Assert.Equal(originalFileHash, actualHash);

      _output.WriteLine(sw.Elapsed.ToString());
    }
  }
}