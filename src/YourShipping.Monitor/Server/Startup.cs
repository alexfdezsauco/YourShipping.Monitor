namespace YourShipping.Monitor.Server
{
    using AngleSharp;

    using Catel.Caching;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Orc.EntityFrameworkCore;

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
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddRazorPages();

            services.AddSignalR();

            services.AddDbContext<DbContext, ApplicationDbContext>();
            services.AddOrcEntityFrameworkCore();
            services.AddDatabaseSeeder<ApplicationDbSeeder>();

            services.AddTransient(sp => BrowsingContext.New(AngleSharp.Configuration.Default));

            services.AddScoped<IStoreService, StoreService>();

            services.AddSingleton<ICacheStorage<string, Product>>(provider => new CacheStorage<string, Product>(storeNullValues: true));
            services.AddSingleton<ICacheStorage<string, Department>>(provider => new CacheStorage<string, Department>(storeNullValues: true));
            services.AddSingleton<ICacheStorage<string, Store>>(provider => new CacheStorage<string, Store>(storeNullValues: true));

            services.AddScoped<IEntityScrapper<Product>, ProductScrapper>();
            services.AddScoped<IEntityScrapper<Department>, DepartmentScrapper>();
            services.AddScoped<IEntityScrapper<Store>, StoreScrapper>();

            services.AddScoped<IMultiEntityScrapper<Product>, InspectDepartmentProductsScrapper>();
            services.AddScoped<IMultiEntityScrapper<Department>, InspectStoreDepartmentsScrapper>();

            services.AddSingleton<ImportStoresHostedService>();

            services.AddHostedService<DepartmentMonitorHostedService>();
            services.AddHostedService<ProductMonitorHostedService>();
            services.AddHostedService<StoreMonitorHostedService>();
        }
    }
}