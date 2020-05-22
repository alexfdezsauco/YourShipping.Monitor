
namespace YourShipping.Monitor.Server.Services.Interfaces
{
    using System;
    using System.Threading.Tasks;

    using YourShipping.Monitor.Shared;

    public interface IStoreService
    {
        Task<Store> AddAsync(Uri uri);

        Task ImportAsync();
    }
}