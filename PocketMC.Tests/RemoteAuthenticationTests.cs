using PocketMC.RemoteControl.Services;
using Xunit;

namespace PocketMC.Tests;

public class RemoteAuthenticationTests
{
    private readonly RemoteAuthenticationService _authService;

    public RemoteAuthenticationTests()
    {
        _authService = new RemoteAuthenticationService();
    }

    [Fact]
    public void HashPassword_OutputsCorrectFormat()
    {
        var password = "test_password_123";
        var hash = _authService.HashPassword(password);
        
        Assert.NotNull(hash);
        Assert.Contains(":", hash);
        var parts = hash.Split(':');
        Assert.Equal(2, parts.Length);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var password = "test_password_123";
        var hash = _authService.HashPassword(password);
        
        var result = _authService.VerifyPassword(password, hash);
        
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_IncorrectPassword_ReturnsFalse()
    {
        var password = "test_password_123";
        var hash = _authService.HashPassword(password);
        
        var result = _authService.VerifyPassword("wrong_password", hash);
        
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_MalformedHash_ReturnsFalse()
    {
        var result = _authService.VerifyPassword("password", "invalid_hash_format");
        Assert.False(result);
    }
}
