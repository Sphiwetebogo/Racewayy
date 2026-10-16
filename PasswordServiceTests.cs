using RaceDay.Api.Services;

namespace RaceDay.Api.Tests;

public class PasswordServiceTests
{
    [Fact]
    public void Hash_DoesNotContainThePlainPassword()
    {
        var hash = PasswordService.Hash("Passw0rd123");
        Assert.DoesNotContain("Passw0rd123", hash);
    }

    [Fact]
    public void Hash_UsesADifferentSaltEachTime()
    {
        Assert.NotEqual(PasswordService.Hash("Passw0rd123"), PasswordService.Hash("Passw0rd123"));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordService.Hash("Passw0rd123");
        Assert.True(PasswordService.Verify("Passw0rd123", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordService.Hash("Passw0rd123");
        Assert.False(PasswordService.Verify("WrongPassword1", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hashedpassword123")]
    [InlineData("1.not-base64.not-base64")]
    public void Verify_MalformedHash_ReturnsFalse(string stored)
    {
        Assert.False(PasswordService.Verify("Passw0rd123", stored));
    }
}
