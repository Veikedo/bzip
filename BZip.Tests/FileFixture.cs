using System;
using System.Buffers;
using System.IO;
using System.Security.Cryptography;

namespace BZip.Tests
{
  public class FileFixture : IDisposable
  {
    public FileFixture()
    {
      FilePath = Path.GetTempFileName();
      const int fileSize = 10 * 1024 * 1024;
      FileHash = GenerateFileAndGetHash(fileSize, FilePath);
    }

    public string FilePath { get; }
    public byte[] FileHash { get; }

    public void Dispose()
    {
      File.Delete(FilePath);
    }

    private byte[] GenerateFileAndGetHash(int fileSize, string filePath)
    {
      var buffer = ArrayPool<byte>.Shared.Rent(fileSize);
      var random = new Random();

      random.NextBytes(buffer);
      File.WriteAllBytes(filePath, buffer);

      ArrayPool<byte>.Shared.Return(buffer);

      using var md5 = MD5.Create();
      return md5.ComputeHash(buffer);
    }
  }
}