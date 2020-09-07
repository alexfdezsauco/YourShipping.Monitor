namespace YourShipping.Monitor.Server.Services.HostedServices
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Serilog;

    using YourShipping.Monitor.Server.Services.Attributes;

    public class TimedHostedServiceBase : IHostedService, IDisposable
    {
        private readonly IHostApplicationLifetime applicationLifetime;

        private readonly IServiceProvider serviceProvider;

        private readonly object syncObj = new object();

        private bool isRunning;

        private Timer timer;

        public TimedHostedServiceBase(IServiceProvider serviceProvider)
        {
            this.applicationLifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();
            this.serviceProvider = serviceProvider;
        }

        public void Dispose()
        {
            this.timer?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return this.StartAsync(cancellationToken, true);
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            Log.Information("Timed Hosted Service is stopping.");

            this.timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        private void DoWork(CancellationToken cancellationToken)
        {
            Monitor.Enter(this.syncObj);

            if (!this.isRunning)
            {
                this.isRunning = true;

                Monitor.Exit(this.syncObj);

                try
                {
                    var executeMethod = this.GetType()
                        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .FirstOrDefault(info => info.GetCustomAttribute<ExecuteAttribute>() != null);

                    if (executeMethod != null)
                    {
                        var parameters = this.ResolveParameters(executeMethod, cancellationToken);

                        var startTime = DateTime.Now;
                        Log.Information("Executing hosted service '{Type}'", this.GetType());
                        var result = executeMethod.Invoke(this, parameters);
                        if (result is Task task)
                        {
                            task.GetAwaiter().GetResult();
                        }

                        var elapsedTime = DateTime.Now.Subtract(startTime);
                        Log.Information("Executed hosted service '{Type}' in '{Time}'", this.GetType(), elapsedTime.ToString(@"hh\h\:mm\m\:ss\s"));
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error executing hosted service '{Type}'", this.GetType());
                }
                finally
                {
                    lock (this.syncObj)
                    {
                        this.isRunning = false;
                    }
                }
            }
            else
            {
                Monitor.Exit(this.syncObj);

                // Log.Information(
                //    "Skipped execution of hosted service '{Type}' because is already running",
                //    this.GetType());
            }
        }

        private object[] ResolveParameters(MethodInfo executeMethodInfo, CancellationToken cancellationToken)
        {
            var objects = new List<object>();
            var parameterInfos = executeMethodInfo.GetParameters();
            var serviceScope = this.serviceProvider.CreateScope();

            foreach (var parameterInfo in parameterInfos)
            {
                if (parameterInfo.ParameterType == typeof(CancellationToken))
                {
                    objects.Add(cancellationToken);
                }
                else
                {
                    object service;

                    try
                    {
                        service = this.serviceProvider.GetService(parameterInfo.ParameterType);
                    }
                    catch (InvalidOperationException)
                    {
                        service = serviceScope.ServiceProvider.GetService(parameterInfo.ParameterType);
                    }

                    objects.Add(service);
                }
            }

            return objects.ToArray();
        }

        private Task StartAsync(CancellationToken cancellationToken, bool sync)
        {
            if (sync)
            {
                this.applicationLifetime.ApplicationStarted.Register(() => this.StartAsync(cancellationToken, false));
            }
            else
            {
                Log.Information("Timed Hosted Service running.");

                this.timer = new Timer(
                    o => this.DoWork(cancellationToken),
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(5));
            }

            return Task.CompletedTask;
        }
    }
}