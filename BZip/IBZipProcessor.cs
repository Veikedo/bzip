using System.Diagnostics.CodeAnalysis;
using System.IO;
using Nerdbank.Streams;

namespace BZip
{
  /// <summary>
  ///   Encapsulates specific logic used to zip or unzip files
  /// </summary>
  internal interface IBZipProcessor
  {
    bool TryReadNextChunk(Stream stream, [NotNullWhen(true)] out Sequence<byte>? chunk);
    void ProcessChunk(StreamChunk chunk, Stream buffer);
    void WriteChunkLength(StreamChunk chunk, Stream stream);
  }
}