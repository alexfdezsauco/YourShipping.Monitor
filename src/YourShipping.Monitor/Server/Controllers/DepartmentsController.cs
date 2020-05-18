namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    using Orc.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Models;
    using YourShipping.Monitor.Server.Services.Interfaces;

    [ApiController]
    [Route("[controller]")]
    public class DepartmentsController : ControllerBase
    {
        private readonly ILogger<ProductsController> logger;

        public DepartmentsController(ILogger<ProductsController> logger)
        {
            this.logger = logger;
        }

        [HttpPost]
        public async Task<Department> Add(
            [FromServices] IRepository<Department, int> departmentRepository,
            [FromServices] IEntityScrapper<Department> departmentScrapper,
            [FromBody] Uri uri)
        {
            var absoluteUrl = uri.AbsoluteUri;
            var storedDepartment =
                departmentRepository.Find(department => department.Url == absoluteUrl).FirstOrDefault();
            if (storedDepartment == null)
            {
                var department = await departmentScrapper.GetAsync(absoluteUrl);
                if (department != null)
                {
                    var dateTime = DateTime.Now;

                    department.Added = dateTime;
                    department.Updated = dateTime;
                    department.Read = dateTime;

                    departmentRepository.Add(department);
                    await departmentRepository.SaveChangesAsync();

                    return department;
                }
            }

            return null;
        }

        [HttpDelete("{id}")]
        public async Task Delete([FromServices] IRepository<Department, int> departmentRepository, int id)
        {
            departmentRepository.Delete(department => department.Id == id);
            await departmentRepository.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<IEnumerable<Shared.Department>> Get(
            [FromServices] IRepository<Department, int> departmentRepository,
            [FromServices] IEntityScrapper<Department> entityScrapper)
        {
            var departments = new List<Shared.Department>();
            foreach (var storedDepartment in departmentRepository.All())
            {
                var dateTime = DateTime.Now;
                Department department;
                var hasChanged = storedDepartment.Read < storedDepartment.Updated;
                if (hasChanged)
                {
                    department = storedDepartment;
                }
                else
                {
                    department = await entityScrapper.GetAsync(storedDepartment.Url);
                    if (department != null)
                    {
                        department.Id = storedDepartment.Id;
                        hasChanged = storedDepartment.Sha256 != department.Sha256;
                        if (hasChanged)
                        {
                            department.Updated = dateTime;
                        }
                    }
                }

                if (department != null)
                {
                    department.Read = dateTime;
                    department = departmentRepository.TryAddOrUpdate(department, nameof(Product.Added));

                    departments.Add(
                        new Shared.Department
                            {
                                Id = department.Id,
                                Url = department.Url,
                                Store = department.Store,
                                Name = department.Name,
                                HasChanged = hasChanged,
                                ProductsCount = department.ProductsCount
                            });
                }
            }

            await departmentRepository.SaveChangesAsync();

            return departments;
        }
    }
}