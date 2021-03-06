﻿namespace YourShipping.Monitor.Server
{
    using System;
    using System.IO;
    using System.Net;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;

    using Serilog;

    using YourShipping.Monitor.Server.Helpers;

    public class Program
    {
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseStartup<Startup>(); });
        }

        public static void Main(string[] args)
        {
            if (!Directory.Exists("data"))
            {
                Directory.CreateDirectory("data");
            }

            if (!Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }

            Log.Logger = new LoggerConfiguration().WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day).WriteTo
                .Console().CreateLogger();

            CreateHostBuilder(args).ConfigureAppConfiguration(
                builder =>
                    {
                        var environmentPath =
#if !DEBUG
                        $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json";
#else
                        $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json";
#endif
                        builder.SetBasePath(Directory.GetCurrentDirectory());
                        builder.AddJsonFile("appsettings.json", true, true);

                        Log.Information("Adding configuration file '{EnvironmentPath}'", environmentPath);
                        builder.AddJsonFile(environmentPath, true, true);

                        Log.Information("Adding configuration from environment variables");
                        builder.AddEnvironmentVariables();
                    }).Build().Run();
        }
    }
}