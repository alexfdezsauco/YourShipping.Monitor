namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Extensions;
    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    [ApiController]
    [Route("[controller]")]
    public class StoresController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<Store>> Add([FromServices] IStoreService storeService, [FromBody] Uri uri)
        {
            return await storeService.AddAsync(uri);
        }

        [HttpDelete("{id}")]
        public async Task Delete([FromServices] IRepository<Models.Store, int> storeRepository, int id)
        {
            var transaction = storeRepository.BeginTransaction(IsolationLevel.Serializable);
            storeRepository.Delete(store => store.Id == id);
            await storeRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }       
        
        [HttpPost("[action]/{id}")]
        public async Task TurnOffScan([FromServices] IRepository<Models.Store, int> storeRepository, int id)
        {
            Log.Information("Turning Off");
            // storeRepository.Delete(store => store.Id == id);
            // await storeRepository.SaveChangesAsync();
        }

        [HttpPost("[action]/{id}")]
        public async Task TurnOnScan([FromServices] IRepository<Models.Store, int> storeRepository, int id)
        {
            Log.Information("Turning On");

            // storeRepository.Delete(store => store.Id == id);
            // await storeRepository.SaveChangesAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Store>> Get(
            [FromServices] IRepository<Models.Store, int> storeRepository,
            int id)
        {
            var store = storeRepository.Find(d => d.Id == id).FirstOrDefault();
            if (store == null)
            {
                return this.NotFound();
            }

            return store?.ToDataTransferObject();
        }

        [HttpGet]
        public async Task<IEnumerable<Store>> Get(
            [FromServices] IRepository<Models.Store, int> storeRepository,
            [FromServices] IEntityScrapper<Models.Store> entityScrapper)
        {
            var stores = new List<Store>();

            foreach (var storedStore in storeRepository.All())
            {
                bool hasChanged = storedStore.Read < storedStore.Updated;
                storedStore.Read = DateTime.Now;
                var transaction = storeRepository.BeginTransaction(IsolationLevel.Serializable);
                stores.Add(storedStore.ToDataTransferObject(hasChanged));
                await storeRepository.SaveChangesAsync();
                await transaction.CommitAsync();
            }


            return stores;
        }

        [HttpGet("[action]/{id}")]
        public async Task<IEnumerable<Department>> GetDepartments(
            [FromServices] IRepository<Models.Store, int> storeRepository,
            [FromServices] IRepository<Models.Department, int> productRepository,
            [FromServices] IMultiEntityScrapper<Models.Department> departmentScrapper,
            int id)
        {
            var departments = new List<Department>();

            var storedDepartment = storeRepository.Find(department => department.Id == id).FirstOrDefault();
            if (storedDepartment != null)
            {
                var scrappedDepartments = departmentScrapper.GetAsync(storedDepartment.Url);
                await foreach (var department in scrappedDepartments)
                {
                    var productUrl = department.Url;
                    var stored = productRepository.Contains(p => p.Url == productUrl);
                    departments.Add(department.ToDataTransferObject(stored: stored));
                }
            }

            return departments;
        }
    }
}