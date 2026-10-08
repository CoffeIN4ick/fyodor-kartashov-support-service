using SupportService.Services;

namespace SupportService.Tests;

public class TicketRulesTests
{
    [Fact]
    public void CalculateReactionTimeMinutes_ReturnsCorrectValue()
    {
        var createdAt =
            new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);

        var firstResponseAt =
            new DateTime(2026, 10, 8, 10, 15, 0, DateTimeKind.Utc);

        var result =
            TicketRules.CalculateReactionTimeMinutes(
                createdAt,
                firstResponseAt);

        Assert.Equal(15, result);
    }

    [Fact]
    public void IsSlaViolated_ReturnsFalse_WhenResponseIsWithinLimit()
    {
        var createdAt =
            new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);

        var responseAt =
            new DateTime(2026, 10, 8, 10, 20, 0, DateTimeKind.Utc);

        var result =
            TicketRules.IsSlaViolated(
                createdAt,
                responseAt,
                30);

        Assert.False(result);
    }

    [Fact]
    public void IsSlaViolated_ReturnsTrue_WhenResponseExceedsLimit()
    {
        var createdAt =
            new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);

        var responseAt =
            new DateTime(2026, 10, 8, 10, 45, 0, DateTimeKind.Utc);

        var result =
            TicketRules.IsSlaViolated(
                createdAt,
                responseAt,
                30);

        Assert.True(result);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("in_progress")]
    [InlineData("closed")]
    public void IsStatusAllowed_ReturnsTrue_ForAllowedStatuses(
        string status)
    {
        var result =
            TicketRules.IsStatusAllowed(status);

        Assert.True(result);
    }

    [Fact]
    public void IsStatusAllowed_ReturnsFalse_ForUnknownStatus()
    {
        var result =
            TicketRules.IsStatusAllowed("deleted");

        Assert.False(result);
    }
}