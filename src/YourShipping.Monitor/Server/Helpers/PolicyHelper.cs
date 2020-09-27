namespace YourShipping.Monitor.Server.Helpers
{
    using System;

    using Microsoft.EntityFrameworkCore.Storage;

    using Polly;
    using Polly.Retry;

    using Serilog;

    public static class PolicyHelper
    {
        public static RetryPolicy<IDbContextTransaction> WaitAndRetry()
        {
            return Policy<IDbContextTransaction>.Handle<Exception>().WaitAndRetryForever(
                retryAttempt => TimeSpan.FromSeconds(5),
                (delegateResult, timespan) => Log.Warning(
                    delegateResult.Exception,
                    "Error opening transaction. Will retry in {timespan}.",
                    timespan));
        }

        public static RetryPolicy<IDbContextTransaction> WaitAndRetry2()
        {
            return Policy<IDbContextTransaction>.Handle<Exception>().WaitAndRetry(
                5,
                retryAttempt => TimeSpan.FromSeconds(5),
                (delegateResult, timespan) => Log.Warning(
                    delegateResult.Exception,
                    "Error opening transaction. Will retry in {timespan}.",
                    timespan));
        }
    }
}