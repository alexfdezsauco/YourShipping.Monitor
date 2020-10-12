namespace YourShipping.Monitor.Server.Services
{
    using System.Net.Http;
    using System.Threading.Tasks;

    public interface ICookiesAwareHttpClientFactory
    {
        Task<HttpClient> CreateHttpClientAsync(string url);

        void InvalidateCookies(string url);

        Task SerializeAsync();

        Task SyncCookiesAsync(string url, HttpClient httpClient);
    }
}