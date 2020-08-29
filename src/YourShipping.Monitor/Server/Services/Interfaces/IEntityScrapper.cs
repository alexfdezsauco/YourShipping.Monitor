
namespace YourShipping.Monitor.Server.Services.Interfaces
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IEntityScrapper<TEntity>
    {
        Task<TEntity> GetAsync(string url, bool force = false, params object[] parameters);
    }
}