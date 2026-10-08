using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SupportService.Services;

public class DbMetricsInterceptor : DbCommandInterceptor
{
    private readonly DbRequestMetrics _metrics;

    public DbMetricsInterceptor(DbRequestMetrics metrics)
    {
        _metrics = metrics;
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        _metrics.AddQuery(eventData.Duration);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        _metrics.AddQuery(eventData.Duration);

        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        _metrics.AddQuery(eventData.Duration);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        _metrics.AddQuery(eventData.Duration);

        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        _metrics.AddQuery(eventData.Duration);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        _metrics.AddQuery(eventData.Duration);

        return ValueTask.FromResult(result);
    }
}