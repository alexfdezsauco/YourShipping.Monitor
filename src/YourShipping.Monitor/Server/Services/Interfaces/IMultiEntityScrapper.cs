namespace YourShipping.Monitor.Server.Services.Interfaces
{
    using System.Collections.Generic;

    public interface IMultiEntityScrapper<TEntity>
    {
        IAsyncEnumerable<TEntity> GetAsync(string url);
    }
}