using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using AngleSharp;
using Catel.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Linq;
using Orc.EntityFrameworkCore;
using Serilog;
using Telegram.Bot;
using YourShipping.Monitor.Server.Extensions;
using YourShipping.Monitor.Server.Helpers;
using YourShipping.Monitor.Server.Hubs;
using YourShipping.Monitor.Server.Models;
using YourShipping.Monitor.Server.Services;
using YourShipping.Monitor.Server.Services.HostedServices;
using YourShipping.Monitor.Server.Services.Interfaces;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace YourShipping.Monitor.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
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

            var token = Configuration.GetSection("TelegramBot")?["Token"];
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

            var token = Configuration.GetSection("TelegramBot")?["Token"];

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

            HttpClientExtensions.Configure(Configuration);

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
                        AllowAutoRedirect = true
                    };

                    if (cookieContainer != null)
                    {
                        handler.CookieContainer = cookieContainer;
                    }

                    var httpTimeoutInSeconds = Configuration.GetSection("Http")?["TimeoutInSeconds"];
                    var httpClient = new HttpClient(handler)
                    {
                        Timeout = float.TryParse(httpTimeoutInSeconds, out var timeoutInSeconds)
                            ? TimeSpan.FromSeconds(timeoutInSeconds)
                            : ScraperConfigurations.HttpClientTimeout
                    };

                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                        "user-agent",
                        ScraperConfigurations.GetSupportedAgent());

                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                        "accept-encoding",
                        "gzip, deflate, br");
                    httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue {NoCache = true};

                    return httpClient;
                });


            services.AddScoped<IStoreService, StoreService>();

            services.AddSingleton<ICacheStorage<string, Product>>(
                provider => new CacheStorage<string, Product>(storeNullValues: true));
            services.AddSingleton<ICacheStorage<string, Department>>(
                provider => new CacheStorage<string, Department>(storeNullValues: true));
            services.AddSingleton<ICacheStorage<string, Store>>(
                provider => new CacheStorage<string, Store>(storeNullValues: true));

            services.AddSingleton<ICookiesSynchronizationService, CookiesSynchronizationService>();
            services.AddSingleton<IOfficialStoreInfoService, OfficialStoreInfoService>();

            services.AddTransient<IEntityScraper<Product>, ProductScraper>();
            services.AddTransient<IEntityScraper<Department>, DepartmentScraper>();
            services.AddTransient<IEntityScraper<Store>, StoreScraper>();

            services.AddTransient<IMultiEntityScraper<Product>, InspectDepartmentProductsScraper>();
            services.AddTransient<IMultiEntityScraper<Department>, InspectStoreDepartmentsScraper>();

            services.AddSingleton<ImportStoresHostedService>();

            services.AddHostedService<DepartmentMonitorHostedService>();
            services.AddHostedService<ProductMonitorHostedService>();
            services.AddHostedService<StoreMonitorHostedService>();
            services.AddHostedService<CookieSerializationHostedService>();

            // services.AddHostedService<SyncUsersFromTelegramHostedService>();
        }
    }
}