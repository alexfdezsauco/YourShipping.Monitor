namespace YourShipping.Monitor.Server
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Hosting;

    using Serilog;

    public class Program
    {
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
        }

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.File("log.txt", rollingInterval: RollingInterval.Day).WriteTo
                .Console().CreateLogger();

            CreateHostBuilder(args).Build().Run();
        }
    }
}