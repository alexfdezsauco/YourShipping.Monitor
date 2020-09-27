namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Helpers;
    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    [ApiController]
    [Route("[controller]")]
    public class DepartmentsController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<Department>> Add(
            [FromServices] IRepository<Models.Department, int> departmentRepository,
            [FromServices] IEntityScraper<Models.Department> departmentScraper,
            [FromBody] Uri uri)
        {
            var absoluteUrl = uri.AbsoluteUri;
            var storedDepartment =
                departmentRepository.Find(department => department.Url == absoluteUrl).FirstOrDefault();
            if (storedDepartment == null)
            {
                var department = await departmentScraper.GetAsync(absoluteUrl);
                if (department != null)
                {
                    var dateTime = DateTime.Now;
                    department.Added = dateTime;
                    department.Updated = dateTime;
                    department.Read = dateTime;

                    var transaction = PolicyHelper.WaitAndRetry().Execute(
                        () => departmentRepository.BeginTransaction(IsolationLevel.Serializable));
                    departmentRepository.Add(department);
                    await departmentRepository.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return department.ToDataTransferObject(true);
                }
            }

            if (storedDepartment == null)
            {
                return this.NotFound();
            }

            return storedDepartment?.ToDataTransferObject();
        }

        [HttpDelete("{id}")]
        public async Task Delete([FromServices] IRepository<Models.Department, int> departmentRepository, int id)
        {
            var transaction = PolicyHelper.WaitAndRetry()
                .Execute(() => departmentRepository.BeginTransaction(IsolationLevel.Serializable));
            departmentRepository.Delete(department => department.Id == id);
            await departmentRepository.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Department>> Get(
            [FromServices] IRepository<Models.Department, int> departmentRepository,
            int id)
        {
            var department = departmentRepository.Find(d => d.Id == id).FirstOrDefault();
            if (department == null)
            {
                return this.NotFound();
            }

            return department?.ToDataTransferObject();
        }

        [HttpGet]
        public async Task<IEnumerable<Department>> Get(
            [FromServices] IRepository<Models.Department, int> departmentRepository)
        {
            var departments = new List<Department>();

            foreach (var storedDepartment in departmentRepository.All())
            {
                var hasChanged = storedDepartment.Read < storedDepartment.Updated;

                var transaction = PolicyHelper.WaitAndRetry().Execute(
                    () => departmentRepository.BeginTransaction(IsolationLevel.Serializable));

                storedDepartment.Read = DateTime.Now;
                await departmentRepository.SaveChangesAsync();
                await transaction.CommitAsync();
                departments.Add(storedDepartment.ToDataTransferObject(hasChanged));
            }

            return departments;
        }

        [HttpGet("[action]/{id}")]
        public async Task<IEnumerable<Product>> GetProducts(
            [FromServices] IRepository<Models.Department, int> departmentRepository,
            [FromServices] IRepository<Models.Product, int> productRepository,
            [FromServices] IMultiEntityScraper<Models.Product> productsScraper,
            int id)
        {
            var products = new List<Product>();

            var storedDepartment = departmentRepository.Find(department => department.Id == id).FirstOrDefault();
            if (storedDepartment != null)
            {
                var scrappedProducts = productsScraper.GetAsync(storedDepartment.Url);
                await foreach (var product in scrappedProducts)
                {
                    var productUrl = product.Url;
                    var stored = productRepository.Contains(p => p.Url == productUrl);
                    products.Add(product.ToDataTransferObject(stored: stored));
                }
            }

            return products;
        }
    }
}