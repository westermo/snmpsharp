using System.Security.Cryptography;
using System.Text;

namespace SnmpSharpNet.Tests;

public class Sha224Tests
{
    [Test]
    [Arguments("", "d14a028c2a3a2bc9476102bb288234c415a2b01f828ea62ac5b3e42f")]
    [Arguments("The quick brown fox jumps over the lazy dog",
        "730e109bd7a8a32b1cb9d9a09aa2325d2430587ddbc0c38bad911525")]
    [Arguments("The quick brown fox jumps over the lazy dog.",
        "619cba8e8e05826e9b8c519c0a5c68f4fb653e8a3d8aa04bb2c8cd4c")]
    [Arguments("hello world", "2f05477fc24bb4faefd86517156dafdecec45b8ad3cf2522a563582b")]
    [Arguments("GeeksForGeeks", "c9e0629b317667db73eaa051cf05f4c5896f5c0c7c2cec4589919d02")]
    public async Task Sha224Test(string input, string output)
    {
        var hash = SHA224.HashData(Encoding.UTF8.GetBytes(input));
        await Assert.That(Convert.ToHexStringLower(hash))
            .IsEqualTo(output);
    }

    [Test]
    [Arguments("", "d14a028c2a3a2bc9476102bb288234c415a2b01f828ea62ac5b3e42f")]
    [Arguments("The quick brown fox jumps over the lazy dog",
        "730e109bd7a8a32b1cb9d9a09aa2325d2430587ddbc0c38bad911525")]
    [Arguments("The quick brown fox jumps over the lazy dog.",
        "619cba8e8e05826e9b8c519c0a5c68f4fb653e8a3d8aa04bb2c8cd4c")]
    [Arguments("hello world", "2f05477fc24bb4faefd86517156dafdecec45b8ad3cf2522a563582b")]
    [Arguments("GeeksForGeeks", "c9e0629b317667db73eaa051cf05f4c5896f5c0c7c2cec4589919d02")]
    public async Task Sha224TestInstance(string input, string output)
    {
        using var sha = SHA224.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        await Assert.That(Convert.ToHexStringLower(hash))
            .IsEqualTo(output);
    }

    [Test]
    [Arguments("", "d14a028c2a3a2bc9476102bb288234c415a2b01f828ea62ac5b3e42f")]
    [Arguments("The quick brown fox jumps over the lazy dog",
        "730e109bd7a8a32b1cb9d9a09aa2325d2430587ddbc0c38bad911525")]
    [Arguments("The quick brown fox jumps over the lazy dog.",
        "619cba8e8e05826e9b8c519c0a5c68f4fb653e8a3d8aa04bb2c8cd4c")]
    [Arguments("hello world", "2f05477fc24bb4faefd86517156dafdecec45b8ad3cf2522a563582b")]
    [Arguments("GeeksForGeeks", "c9e0629b317667db73eaa051cf05f4c5896f5c0c7c2cec4589919d02")]
    public async Task Sha224TestInstanceInParts(string input, string output)
    {
        using var sha = SHA224.Create();
        var first = input[..(input.Length / 3)];
        var second = input[(input.Length / 3)..(input.Length * 2 / 3)];
        var third = input[(input.Length * 2 / 3)..];
        sha.TransformBlock(Encoding.UTF8.GetBytes(first), 0, first.Length, null, 0);
        sha.TransformBlock(Encoding.UTF8.GetBytes(second), 0, second.Length, null, 0);
        sha.TransformFinalBlock(Encoding.UTF8.GetBytes(third), 0, third.Length);
        var hash = sha.Hash!;
        await Assert.That(Convert.ToHexStringLower(hash))
            .IsEqualTo(output);
    }
}