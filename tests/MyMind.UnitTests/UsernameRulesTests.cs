using MyMind.Domain.Users;

namespace MyMind.UnitTests;

public class UsernameRulesTests
{
    [Theory]
    [InlineData("sarvar")]
    [InlineData("dev_2026")]
    [InlineData("abc")]
    [InlineData("a23456789012345678901234567890")]
    public void IsValidFormat_AcceptsLowercaseLettersDigitsUnderscore(string username) =>
        Assert.True(UsernameRules.IsValidFormat(username));

    [Theory]
    [InlineData("ab")]
    [InlineData("a234567890123456789012345678901")]
    [InlineData("Sarvar")]
    [InlineData("sar-var")]
    [InlineData("sar.var")]
    [InlineData("o'lmas")]
    [InlineData("")]
    public void IsValidFormat_RejectsOtherInput(string username) =>
        Assert.False(UsernameRules.IsValidFormat(username));

    [Theory]
    [InlineData("api")]
    [InlineData("ME")]
    [InlineData(" admin ")]
    public void IsReserved_MatchesRouteNamesCaseInsensitively(string username) =>
        Assert.True(UsernameRules.IsReserved(username));

    [Fact]
    public void IsReserved_AllowsOrdinaryNames() =>
        Assert.False(UsernameRules.IsReserved("sarvar"));
}
