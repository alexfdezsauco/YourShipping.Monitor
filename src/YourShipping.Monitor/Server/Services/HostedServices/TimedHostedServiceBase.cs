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

        private readonly MethodInfo executeMethod;

        private readonly ParameterInfo[] executeMethodParameters;

        private readonly bool maximizeParallelism;

        private readonly TimeSpan period;

        private readonly IServiceProvider serviceProvider;

        private readonly object syncObj = new object();

        private bool isRunning;

        private Timer timer;

        public TimedHostedServiceBase(
            IServiceProvider serviceProvider,
            TimeSpan period,
            bool maximizeParallelism = false)
        {
            this.applicationLifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();
            this.serviceProvider = serviceProvider;
            this.period = period;
            this.maximizeParallelism = maximizeParallelism;
            this.executeMethod = this.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(info => info.GetCustomAttribute<ExecuteAttribute>() != null);
            this.executeMethodParameters = this.executeMethod.GetParameters();
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
            if (this.maximizeParallelism)
            {
                this.Execute(cancellationToken);
            }
            else
            {
                this.ExecuteIfNotRunning(cancellationToken);
            }
        }

        private void Execute(CancellationToken cancellationToken)
        {
            if (this.executeMethod != null)
            {
                var parameters = this.ResolveParameters(cancellationToken);

                var startTime = DateTime.Now;
                Log.Information("Executing hosted service '{Type}'", this.GetType());
                var result = this.executeMethod.Invoke(this, parameters);
                if (result is Task task)
                {
                    task.ConfigureAwait(false).GetAwaiter().GetResult();
                }

                var elapsedTime = DateTime.Now.Subtract(startTime);
                Log.Information("Executed hosted service '{Type}' in '{Time}'", this.GetType(), elapsedTime);
            }
        }

        private void ExecuteIfNotRunning(CancellationToken cancellationToken)
        {
            Monitor.Enter(this.syncObj);

            if (!this.isRunning)
            {
                this.isRunning = true;

                Monitor.Exit(this.syncObj);

                try
                {
                    this.Execute(cancellationToken);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error executing hosted service '{Type}'", this.GetType());
                }
                finally
                {
                    this.isRunning = false;
                }
            }
            else
            {
                Monitor.Exit(this.syncObj);
            }
        }

        private object[] ResolveParameters(CancellationToken cancellationToken)
        {
            var objects = new List<object>();
            var serviceScope = this.serviceProvider.CreateScope();

            foreach (var parameterInfo in this.executeMethodParameters)
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

                this.timer = new Timer(o => this.DoWork(cancellationToken), null, TimeSpan.Zero, this.period);
            }

            return Task.CompletedTask;
        }
    }
}