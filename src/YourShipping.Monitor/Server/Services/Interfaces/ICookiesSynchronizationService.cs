namespace YourShipping.Monitor.Server.Services
{
    using System.Net;
    using System.Threading.Tasks;

    public interface ICookiesSynchronizationService
    {
        Task<CookieCollection> GetCookieCollectionAsync(string url);

        void InvalidateCookies(string url);

        Task SyncCookiesAsync(string url, CookieCollection cookieCollection);
    }
}