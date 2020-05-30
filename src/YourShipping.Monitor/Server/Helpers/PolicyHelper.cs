namespace YourShipping.Monitor.Server.Helpers
{
    using System;

    using Microsoft.EntityFrameworkCore.Storage;

    using Polly;
    using Polly.Retry;

    using Serilog;

    public static class PolicyHelper
    {
        public static RetryPolicy<IDbContextTransaction> WaitAndRetryForever()
        {
            return Policy<IDbContextTransaction>.Handle<Exception>().WaitAndRetryForever(
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (delegateResult, timespan) => Log.Warning(
                    delegateResult.Exception,
                    "Error opening transaction. Will retry in {timespan}.",
                    timespan));
        }
    }
}