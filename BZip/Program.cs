using System;
using System.Diagnostics;
using System.IO;

namespace BZip
{
  internal class Program
  {
    private static void Main(string[] args)
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
        compressor.Process();
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
        decompressor.Process();
      }

      Console.WriteLine(sw.Elapsed.ToString());
    }
  }
}