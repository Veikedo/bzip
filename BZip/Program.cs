using System;
using System.Diagnostics;
using System.IO;
using CommandLine;

namespace BZip
{
  internal class Program
  {
    private static int Main(string[] args)
    {
      return Parser
        .Default
        .ParseArguments<CommandLineOptions>(args)
        .MapResult(o =>
          {
            try
            {
              return RunApp(o);
            }
            catch (Exception exception)
            {
              var handled = exception.HandleInner(e =>
              {
                switch (e)
                {
                  case OutOfMemoryException _:
                    Console.WriteLine("You don't have enough memory");
                    return true;

                  case IOException io:
                    Console.WriteLine($"There is a problem with your disk {io.Message}");
                    return true;

                  case PlatformNotSupportedException _:
                    Console.WriteLine("Your OS is not supported");
                    return true;

                  default:
                    return false;
                }
              });

              if (!handled)
              {
                Console.WriteLine(exception.Message);
              }
            }

            return 1;
          },
          _ => 1);
    }

    private static int RunApp(CommandLineOptions o)
    {
      using var incomingStream = o.Input.OpenRead();
      using var outgoingStream = o.Output.OpenWrite();

      var sw = Stopwatch.StartNew();
      if (o.Compress)
      {
        var compressor = new BZipCompressor(incomingStream, outgoingStream);
        compressor.Compress();
      }
      else if (o.Decompress)
      {
        var compressor = new BZipDecompressor(incomingStream, outgoingStream);
        compressor.Decompress();
      }

      Console.WriteLine($"File processed in {sw.Elapsed.ToString()}");

      return 0;
    }
  }
}