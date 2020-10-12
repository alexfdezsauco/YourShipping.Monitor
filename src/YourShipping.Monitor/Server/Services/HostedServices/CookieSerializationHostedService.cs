namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Threading.Tasks;

    using YourShipping.Monitor.Server.Services.Attributes;

    public sealed class CookieSerializationHostedService : TimedHostedServiceBase
    {
        public CookieSerializationHostedService(IServiceProvider serviceProvider)
            : base(serviceProvider, TimeSpan.FromSeconds(60))
        {
        }

        [Execute]
        public async Task ExecuteAsync(ICookiesAwareHttpClientFactory cookiesAwareHttpClientFactory)
        {
            await cookiesAwareHttpClientFactory.SerializeAsync();
        }
    }
}