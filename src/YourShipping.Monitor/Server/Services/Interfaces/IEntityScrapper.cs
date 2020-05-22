
namespace YourShipping.Monitor.Server.Services.Interfaces
{
    using System.Threading.Tasks;

    public interface IEntityScrapper<TEntity>
    {
        Task<TEntity> GetAsync(string url);
    }
}