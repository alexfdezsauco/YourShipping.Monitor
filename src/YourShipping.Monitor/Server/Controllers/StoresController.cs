﻿namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Helpers;
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
            var transaction = PolicyHelper.WaitAndRetry()
                .Execute(() => storeRepository.BeginTransaction(IsolationLevel.Serializable));

            storeRepository.Delete(store => store.Id == id);
            await storeRepository.SaveChangesAsync();
            await transaction.CommitAsync();
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

        [HttpGet("[action]/{id}")]
        public async Task<IActionResult> Captcha([FromServices] IRepository<Models.Store, int> storeRepository, int id)
        {
            var store = storeRepository.Find(d => d.Id == id).FirstOrDefault();
            if (store == null)
            {
                return this.NotFound();
            }

            var storeSlug = UriHelper.GetStoreSlug(store.Url);
            var path = $"captchas/{storeSlug}.jpg";
            if (!System.IO.File.Exists(path))
            {
                return this.NotFound();
            }

            return this.File(System.IO.File.OpenRead(path), "image/jpeg");
        }

        [HttpGet("[action]/{id}/{captchaText}")]
        public async Task<IActionResult> ResolveCaptcha([FromServices] IRepository<Models.Store, int> storeRepository, int id, string captchaText)
        {
            var store = storeRepository.Find(d => d.Id == id).FirstOrDefault();
            if (store == null)
            {
                return this.NotFound();
            }

            var storeSlug = UriHelper.GetStoreSlug(store.Url);
            var path = $"captchas/{storeSlug}.jpg";
            if (!System.IO.File.Exists(path))
            {
                return this.NotFound();
            }

            var captchaSolutionFilePath = $"captchas/{storeSlug}.txt";
            await System.IO.File.WriteAllTextAsync(captchaSolutionFilePath, captchaText);

            return this.Accepted();
        }

        [HttpGet]
        public async Task<IEnumerable<Store>> Get([FromServices] IRepository<Models.Store, int> storeRepository)
        {
            var stores = new List<Store>();

            foreach (var storedStore in storeRepository.All())
            {
                var hasChanged = storedStore.Read < storedStore.Updated;

                var transaction = PolicyHelper.WaitAndRetry()
                    .Execute(() => storeRepository.BeginTransaction(IsolationLevel.Serializable));

                storedStore.Read = DateTime.Now;
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
            [FromServices] IMultiEntityScraper<Models.Department> departmentScraper,
            int id)
        {
            var departments = new List<Department>();

            var storedDepartment = storeRepository.Find(department => department.Id == id).FirstOrDefault();
            if (storedDepartment != null)
            {
                var scrappedDepartments = departmentScraper.GetAsync(storedDepartment.Url);
                await foreach (var department in scrappedDepartments)
                {
                    var productUrl = department.Url;
                    var stored = productRepository.Contains(p => p.Url == productUrl);
                    departments.Add(department.ToDataTransferObject(stored: stored));
                }
            }

            return departments;
        }

        [HttpGet("[action]")]
        public async Task<IEnumerable<Product>> Search(
            [FromServices] IRepository<Models.Store, int> storeRepository,
            [FromServices] IMultiEntityScraper<Models.Product> productsScraper,
            string keywords)
        {
            var keywordList = keywords.Split(',', ';').Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            var products = new List<Product>();

            foreach (var storedStore in storeRepository.All())
            {
                if (storedStore.IsEnabled)
                {
                    foreach (var keyword in keywordList)
                    {
                        var url = storedStore.Url.Replace(
                            "/Products?depPid=0",
                            $"/Search.aspx?keywords={keyword}&depPid=0");
                        await foreach (var product in productsScraper.GetAsync(url, true))
                        {
                            products.Add(product.ToDataTransferObject(false, false));
                        }
                    }
                }
            }

            return products;
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
    }
}