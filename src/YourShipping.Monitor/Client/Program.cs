namespace YourShipping.Monitor.Client
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Blorc.PatternFly.Services.Extensions;
    using Blorc.Services;

    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.AspNetCore.SignalR.Client;
    using Microsoft.Extensions.DependencyInjection;

    using Toolbelt.Blazor.Extensions.DependencyInjection;

    using YourShipping.Monitor.Client.Services;
    using YourShipping.Monitor.Client.Services.Interfaces;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("app");

            builder.Services.AddTransient(
                sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }.EnableIntercept(sp));

            builder.Services.AddLoadingBar();
            builder.Services.AddTransient<HubConnectionBuilder>();

            builder.Services.AddBlorcCore();
            builder.Services.AddBlorcPatternFly();
            builder.Services.AddSingleton<IApplicationState, ApplicationState>();

            await builder.Build()
                .MapComponentServices(options => options.MapBlorcPatternFly())
                .UseLoadingBar()
                .RunAsync();
        }
    }
}