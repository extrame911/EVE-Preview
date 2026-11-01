using EvePreview.Core;

namespace EvePreview.Core.Tests;

public sealed class ProcessNameNormalizerTests
{
    [Theory]
    [InlineData("  ExeFile.EXE  ", "exefile")]
    [InlineData(@"C:\\Games\\EVE\\ExeFile.exe", "exefile")]
    [InlineData("exefile", "exefile")]
    public void NormalizeRemovesPathExtensionAndCase(string value, string expected)
    {
        Assert.Equal(expected, ProcessNameNormalizer.Normalize(value));
    }
}
