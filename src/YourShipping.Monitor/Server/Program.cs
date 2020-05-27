namespace YourShipping.Monitor.Server
{
    using System.Threading;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;

    using Serilog;
    using Serilog.Core;
    using Serilog.Events;

    public class Program
    {
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
        }

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {ThreadId} {Level:u3}] {Message:lj}{NewLine}{Exception}").Enrich.WithThreadId().CreateLogger();

            CreateHostBuilder(args).Build().Run();
        }
    }

}