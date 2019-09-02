using System;
using System.Diagnostics;
using System.IO;

namespace BZip
{
  internal class Program
  {
    private static void Main(string[] args)
    {
      const string filePath = "D:/temp/Docker for Windows Installer.exe";

      var sw = Stopwatch.StartNew();

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

      Console.WriteLine(sw.Elapsed);
    }
  }
}