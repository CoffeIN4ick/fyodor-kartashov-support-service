namespace SupportService.Services;

public static class TicketRules
{
    public static readonly string[] AllowedStatuses =
    {
        "new",
        "in_progress",
        "closed"
    };

    public static bool IsStatusAllowed(string status)
    {
        return AllowedStatuses.Contains(status);
    }

    public static double CalculateReactionTimeMinutes(
        DateTime createdAt,
        DateTime firstEmployeeResponseAt)
    {
        return (firstEmployeeResponseAt - createdAt).TotalMinutes;
    }

    public static bool IsSlaViolated(
        DateTime createdAt,
        DateTime referenceTime,
        int reactionTimeMinutes)
    {
        return (referenceTime - createdAt).TotalMinutes >
               reactionTimeMinutes;
    }
}