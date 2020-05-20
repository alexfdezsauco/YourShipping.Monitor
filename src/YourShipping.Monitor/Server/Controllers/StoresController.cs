namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    [ApiController]
    [Route("[controller]")]
    public class StoresController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<Store>> Add([FromServices] IStoreService storeService, 
                                                   [FromBody] Uri uri)
        {
            return await storeService.AddAsync(uri);
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
                var dateTime = DateTime.Now;
                Models.Store store;
                var hasChanged = storedStore.Read < storedStore.Updated;
                if (hasChanged)
                {
                    store = storedStore;
                }
                else
                {
                    store = await entityScrapper.GetAsync(storedStore.Url);
                    if (store != null)
                    {
                        store.Id = storedStore.Id;
                        hasChanged = storedStore.Sha256 != store.Sha256;
                        if (hasChanged)
                        {
                            store.Updated = dateTime;
                        }
                    }
                    else
                    {
                        store = storedStore;
                        store.Updated = dateTime;
                    }
                }

                store.Read = dateTime;
                store = storeRepository.TryAddOrUpdate(store, nameof(Models.Store.Added));
                stores.Add(store.ToDataTransferObject(hasChanged));
            }

            await storeRepository.SaveChangesAsync();

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