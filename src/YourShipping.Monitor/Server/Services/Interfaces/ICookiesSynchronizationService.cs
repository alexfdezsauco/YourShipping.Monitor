namespace YourShipping.Monitor.Server.Services
{
    using System.Net;

    public interface ICookiesSynchronizationService
    {
        void SyncCookies(CookieContainer cookieContainer);

        void InvalidateCookies();

        CookieCollection GetCollection();
    }
}