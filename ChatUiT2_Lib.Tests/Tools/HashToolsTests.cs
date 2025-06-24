using ChatUiT2_Lib.Tools;
using System.Collections.Generic;
using Xunit;

namespace ChatUiT2_Lib.Tests.Tools;

public class HashToolsTests
{
    [Theory]
    [MemberData(nameof(GetMd5HashTestData))]
    public void GetMd5Hash_ValidInput_ReturnsExpectedHash(string input, string expectedHash)
    {
        // Act
        string actualHash = HashTools.GetMd5Hash(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    public static IEnumerable<object[]> GetMd5HashTestData()
    {
        yield return new object[] { "test", "098f6bcd4621d373cade4e832627b4f6" }; // Precomputed MD5 hash for "test"
        yield return new object[] { "hello", "5d41402abc4b2a76b9719d911017c592" }; // Precomputed MD5 hash for "hello"
        yield return new object[] { "world", "7d793037a0760186574b0282f2f435e7" }; // Precomputed MD5 hash for "world"
    }

    [Theory]
    [MemberData(nameof(GetSha256HashTestData))]
    public void GetSha256Hash_ValidInput_ReturnsExpectedHash(string input, string expectedHash)
    {
        // Act
        string actualHash = HashTools.GetSha256Hash(input);

        // Assert
        Assert.Equal(expectedHash, actualHash);
    }

    public static IEnumerable<object[]> GetSha256HashTestData()
    {
        yield return new object[] { "test", "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08" }; // Precomputed SHA-256 hash for "test"
        yield return new object[] { "hello", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824" }; // Precomputed SHA-256 hash for "hello"
        yield return new object[] { "world", "486ea46224d1bb4fb680f34f7c9ad96a8f24ec88be73ea8e5a6c65260e9cb8a7" }; // Precomputed SHA-256 hash for "world"
    }
}
