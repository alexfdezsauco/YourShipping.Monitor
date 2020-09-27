namespace YourShipping.Monitor.Server.Services.Interfaces
{
    using System.Collections.Generic;

    public interface IMultiEntityScraper<TEntity>
    {
        IAsyncEnumerable<TEntity> GetAsync(string url, bool force = false);
    }
}