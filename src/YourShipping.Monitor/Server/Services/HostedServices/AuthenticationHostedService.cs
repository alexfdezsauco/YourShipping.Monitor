namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using Dasync.Collections;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Services.Attributes;
    using YourShipping.Monitor.Shared;

    using Store = YourShipping.Monitor.Server.Models.Store;

    public sealed class AuthenticationHostedService : TimedHostedServiceBase
    {
        public AuthenticationHostedService(IServiceProvider serviceProvider, IConfiguration configuration)
            : base(
                serviceProvider,
                TimeSpan.FromSeconds(1),
                bool.TryParse(configuration["MaximizeParallelism"], out var maximizeParallelism) && maximizeParallelism)
        {
        }

        [Execute]
        public async Task ExecuteAsync(IRepository<Store, int> globalStoreRepository, IServiceProvider serviceProvider)
        {
            Log.Information("Running {Source} Monitor.", AlertSource.Stores);

            var storedStores = globalStoreRepository.Find(store => store.IsEnabled).ToList();
            await storedStores.ParallelForEachAsync(
                async storedStore =>
                    {
                        var serviceScope = serviceProvider.CreateScope();
                        var serviceScopeServiceProvider = serviceScope.ServiceProvider;
                        var cookiesAwareHttpClientFactory =
                            serviceScopeServiceProvider.GetService<ICookiesAwareHttpClientFactory>();
                        await cookiesAwareHttpClientFactory.BeginLoginAsync(storedStore.Url);
                    });
        }
    }
}