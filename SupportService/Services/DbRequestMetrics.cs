namespace SupportService.Services;

public class DbRequestMetrics
{
    public int QueryCount { get; private set; }

    public double TotalMilliseconds { get; private set; }

    public void AddQuery(TimeSpan duration)
    {
        QueryCount++;
        TotalMilliseconds += duration.TotalMilliseconds;
    }

    public void Reset()
    {
        QueryCount = 0;
        TotalMilliseconds = 0;
    }
}