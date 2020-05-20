namespace YourShipping.Monitor.Server.Controllers
{
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    using YourShipping.Monitor.Server.Services;

    [ApiController]
    [Route("[controller]")]
    public class HostedServiceController : ControllerBase
    {
        [HttpPost("[action]")]
        public async Task StartImportStores([FromServices] ImportStoresHostedService importStoresHostedService)
        {
            await importStoresHostedService.StartAsync(CancellationToken.None);
        }

        [HttpPost("[action]")]
        public async Task StopImportStores([FromServices] ImportStoresHostedService importStoresHostedService)
        {
            await importStoresHostedService.StartAsync(CancellationToken.None);
        }
    }
}