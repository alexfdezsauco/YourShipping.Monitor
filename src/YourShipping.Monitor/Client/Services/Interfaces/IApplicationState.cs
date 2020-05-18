namespace YourShipping.Monitor.Client.Services.Interfaces
{
    using System;

    using YourShipping.Monitor.Shared;

    public interface IApplicationState
    {
        event EventHandler SourceChanged;

        bool HasAlertsFrom(AlertSource alertSource);
    }
}