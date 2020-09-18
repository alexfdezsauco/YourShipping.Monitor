namespace YourShipping.Monitor.Server
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Authentication;

    using AngleSharp;

    using Catel.Caching;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using Telegram.Bot;

    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Hubs;
    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services;
    using YourShipping.Monitor.Server.Services.HostedServices;
    using YourShipping.Monitor.Server.Services.Interfaces;

    using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");

                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseDatabaseSeeder();

            app.UseHttpsRedirection();
            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(
                endpoints =>
                    {
                        endpoints.MapRazorPages();
                        endpoints.MapControllers();
                        endpoints.MapHub<MessagesHub>("/hubs/messages");
                        endpoints.MapFallbackToFile("index.html");
                    });

            var token = this.Configuration.GetSection("TelegramBot")?["Token"];
            if (!string.IsNullOrWhiteSpace(token) && token != "%TELEGRAM_BOT_TOKEN%")
            {
                serviceProvider.GetService<ITelegramCommander>().Start();
            }
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddRazorPages();

            services.AddSignalR();

            // TODO: Change the ServiceLifetime. The usage of ServiceLifetime.Transient is because multiple threads operations are running in the same dbcontext.
            services.AddDbContext<DbContext, ApplicationDbContext>(ServiceLifetime.Transient);
            services.AddOrcEntityFrameworkCore();
            services.AddDatabaseSeeder<ApplicationDbSeeder>();

            var token = this.Configuration.GetSection("TelegramBot")?["Token"];

            if (!string.IsNullOrWhiteSpace(token))
            {
                if (token == "%TELEGRAM_BOT_TOKEN%")
                {
                    Log.Warning(
                        "Telegram notification is disable. Replace %TELEGRAM_BOT_TOKEN% placeholder in the configuration file with a valid bot token.");
                }
                else
                {
                    Log.Information("Telegram notification is enable.");

                    services.AddTransient<ITelegramBotClient>(sp => new TelegramBotClient(token));
                    services.AddSingleton<ITelegramCommander, TelegramCommander>();
                }
            }
            else
            {
                Log.Warning(
                    "Telegram notification is disable. To enable it, add a TelegramBot section with a key Token.");
            }

            services.AddTransient(sp => new CookieContainer());

            services.AddTransient(sp => BrowsingContext.New(AngleSharp.Configuration.Default));

            services.AddTransient(
                sp =>
                    {
                        var cookieContainer = sp.GetService<CookieContainer>();

                        var handler = new HttpClientHandler
                                          {
                                              AutomaticDecompression =
                                                  DecompressionMethods.GZip | DecompressionMethods.Deflate
                                                                            | DecompressionMethods.Brotli,
                                              AllowAutoRedirect = true,
                                              //SslProtocols =
                                              //    SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12
                                              //    | SslProtocols.Tls13
                                          };

                        if (cookieContainer != null)
                        {
                            handler.CookieContainer = cookieContainer;
                        }

                        var httpTimeoutInSeconds = this.Configuration.GetSection("Http")?["TimeoutInSeconds"];
                        var httpClient = new HttpClient(handler)
                                             {
                                                 Timeout = int.TryParse(httpTimeoutInSeconds, out var timeoutInSeconds)
                                                               ? TimeSpan.FromSeconds(timeoutInSeconds)
                                                               : ScrappingConfiguration.HttpClientTimeout
                                             };

                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                            "user-agent",
                            ScrappingConfiguration.GetAgent());
                        var random = new Random();

                        /*
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                            "X-Forwarded-For",
                            $"192.168.{random.Next(1, 255)}.{random.Next(1, 255)}"); 
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                            "X-Real-IP",
                            $"192.168.{random.Next(1, 255)}.{random.Next(1, 255)}");
                        */

                        // httpClient.DefaultRequestHeaders.Host = $"192.168.43.{random.Next(1, 255)}";
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                            "accept-encoding",
                            "gzip, deflate, br");
                        httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

                        return httpClient;
                    });

            // services.AddHttpClient(
            // "json",
            // (sp, httpClient) =>
            // {
            // httpClient.Timeout = ScrappingConfiguration.HttpClientTimeout;
            // httpClient.DefaultRequestHeaders.CacheControl =
            // new CacheControlHeaderValue { NoCache = true };
            // httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            // "user-agent",
            // "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36");
            // });
            services.AddScoped<IStoreService, StoreService>();

            services.AddSingleton<ICacheStorage<string, Product>>(
                provider => new CacheStorage<string, Product>(storeNullValues: true));
            services.AddSingleton<ICacheStorage<string, Department>>(
                provider => new CacheStorage<string, Department>(storeNullValues: true));
            services.AddSingleton<ICacheStorage<string, Store>>(
                provider => new CacheStorage<string, Store>(storeNullValues: true));

            services.AddSingleton<ICookiesSynchronizationService, CookiesSynchronizationService>();
            services.AddSingleton<IOfficialStoreInfoService, OfficialStoreInfoService>();

            services.AddTransient<IEntityScrapper<Product>, ProductScrapper>();
            services.AddTransient<IEntityScrapper<Department>, DepartmentScrapper>();
            services.AddTransient<IEntityScrapper<Store>, StoreScrapper>();

            services.AddTransient<IMultiEntityScrapper<Product>, InspectDepartmentProductsScrapper>();
            services.AddTransient<IMultiEntityScrapper<Department>, InspectStoreDepartmentsScrapper>();

            services.AddSingleton<ImportStoresHostedService>();

            services.AddHostedService<DepartmentMonitorHostedService>();
            services.AddHostedService<ProductMonitorHostedService>();
            services.AddHostedService<StoreMonitorHostedService>();
            services.AddHostedService<CookieSerializationHostedService>();

            // services.AddHostedService<SyncUsersFromTelegramHostedService>();
        }
    }
}