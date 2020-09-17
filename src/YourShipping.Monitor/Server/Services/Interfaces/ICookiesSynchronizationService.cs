namespace YourShipping.Monitor.Server.Services
{
    using System.Net.Http;
    using System.Threading.Tasks;

    public interface ICookiesSynchronizationService
    {
        Task<HttpClient> CreateHttpClientAsync(string url);

        void InvalidateCookies(string url);

        Task SyncCookiesAsync(HttpClient cookieCollection, string url);

        Task SerializeAsync();
    }
}