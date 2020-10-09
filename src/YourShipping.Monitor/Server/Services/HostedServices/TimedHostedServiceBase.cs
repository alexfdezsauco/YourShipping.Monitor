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

namespace YourShipping.Monitor.Server.Services.HostedServices
{
    public class TimedHostedServiceBase : IHostedService, IDisposable
    {
        private readonly IHostApplicationLifetime applicationLifetime;

        private readonly TimeSpan period;

        private readonly MethodInfo executeMethod;
        private readonly ParameterInfo[] executeMethodParameters;
        private readonly IServiceProvider serviceProvider;

        private readonly object syncObj = new object();


        private bool isRunning;

        private Timer timer;

        public TimedHostedServiceBase(IServiceProvider serviceProvider, TimeSpan period)
        {
            applicationLifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();
            this.serviceProvider = serviceProvider;
            this.period = period;
            this.executeMethod = GetType()
                        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .FirstOrDefault(info => info.GetCustomAttribute<ExecuteAttribute>() != null);
            this.executeMethodParameters = executeMethod.GetParameters();                        
        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return StartAsync(cancellationToken, true);
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            Log.Information("Timed Hosted Service is stopping.");

            timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        private void DoWork(CancellationToken cancellationToken)
        {
            System.Threading.Monitor.Enter(syncObj);

            if (!isRunning)
            {
                isRunning = true;

                System.Threading.Monitor.Exit(syncObj);

                try
                {
                    if (executeMethod != null)
                    {
                        var parameters = ResolveParameters(cancellationToken);

                        var startTime = DateTime.Now;
                        Log.Information("Executing hosted service '{Type}'", GetType());
                        var result = executeMethod.Invoke(this, parameters);
                        if (result is Task task)
                        {
                            task.ConfigureAwait(false).GetAwaiter().GetResult();
                        }

                        var elapsedTime = DateTime.Now.Subtract(startTime);
                        Log.Information("Executed hosted service '{Type}' in '{Time}'", GetType(),
                            elapsedTime);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error executing hosted service '{Type}'", GetType());
                }
                finally
                {
                    isRunning = false;
                }
            }
            else
            {
                System.Threading.Monitor.Exit(syncObj);
            }
        }

        private object[] ResolveParameters(CancellationToken cancellationToken)
        {
            var objects = new List<object>();
            var serviceScope = serviceProvider.CreateScope();

            foreach (var parameterInfo in executeMethodParameters)
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
                        service = serviceProvider.GetService(parameterInfo.ParameterType);
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
                applicationLifetime.ApplicationStarted.Register(() => StartAsync(cancellationToken, false));
            }
            else
            {
                Log.Information("Timed Hosted Service running.");

                timer = new Timer(
                    o => DoWork(cancellationToken),
                    null,
                    TimeSpan.Zero,
                    period);
            }

            return Task.CompletedTask;
        }
    }
}