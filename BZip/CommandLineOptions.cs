using System.IO;
using CommandLine;

namespace BZip
{
  internal sealed class CommandLineOptions
  {
    public CommandLineOptions(FileInfo input, FileInfo output, bool compress, bool decompress)
    {
      Input = input;
      Output = output;
      Compress = compress;
      Decompress = decompress;
    }

    [Option(Required = true)] public FileInfo Input { get; }

    [Option(Required = true)] public FileInfo Output { get; }

    [Option] public bool Compress { get; }

    [Option] public bool Decompress { get; }
  }
}