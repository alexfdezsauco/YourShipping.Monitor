namespace YourShipping.Monitor.Server.Services.Interfaces
{
    using System.Collections.Generic;

    public interface IMultipleEntityScrapper<TEntity>
    {
        IAsyncEnumerable<TEntity> GetAsync(string uri);
    }
}