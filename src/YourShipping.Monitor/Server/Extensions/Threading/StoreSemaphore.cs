using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace YourShipping.Monitor.Server.Extensions.Threading
{
    public class StoreSemaphore
    {
        private readonly string _id;

        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        private readonly TimeSpan _timeBetweenCallsInSeconds;

        private DateTime _lastDateTime;

        public StoreSemaphore(string id, TimeSpan timeBetweenCallsInSeconds)
        {
            _id = id;
            _timeBetweenCallsInSeconds = timeBetweenCallsInSeconds;
        }

        public async Task WaitAsync()
        {
            await _semaphoreSlim.WaitAsync();

            if (_lastDateTime != default)
            {
                var elapsedTime = DateTime.Now.Subtract(_lastDateTime);
                if (elapsedTime < _timeBetweenCallsInSeconds)
                {
                    var timeToSleep = _timeBetweenCallsInSeconds.Subtract(elapsedTime);
                    Log.Information("Requests too fast to {StoreSlug}. Will wait {Time}.", _id, timeToSleep);
                    await Task.Delay(timeToSleep);
                }
            }
        }

        public void Release()
        {
            _lastDateTime = DateTime.Now;
            _semaphoreSlim.Release();
        }
    }
}