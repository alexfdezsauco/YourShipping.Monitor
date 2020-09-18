namespace YourShipping.Monitor.Server.Services
{
    using System.Threading.Tasks;

    using YourShipping.Monitor.Server.Models;

    public interface IOfficialStoreInfoService
    {
        Task<OfficialStoreInfo[]> GetAsync();
    }
}