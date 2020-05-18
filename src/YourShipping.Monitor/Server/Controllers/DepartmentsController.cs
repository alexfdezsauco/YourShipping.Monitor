namespace YourShipping.Monitor.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Mvc;

    using Orc.EntityFrameworkCore;

    using Serilog;

    using YourShipping.Monitor.Server.Models.Extensions;
    using YourShipping.Monitor.Server.Services.Interfaces;
    using YourShipping.Monitor.Shared;

    using Product = YourShipping.Monitor.Server.Models.Product;

    [ApiController]
    [Route("[controller]")]
    public class DepartmentsController : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<Department>> Add(
            [FromServices] IRepository<Models.Department, int> departmentRepository,
            [FromServices] IEntityScrapper<Models.Department> departmentScrapper,
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
            departmentRepository.Delete(department => department.Id == id);
            await departmentRepository.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<IEnumerable<Department>> Get(
            [FromServices] IRepository<Models.Department, int> departmentRepository,
            [FromServices] IEntityScrapper<Models.Department> entityScrapper)
        {
            var departments = new List<Department>();
            foreach (var storedDepartment in departmentRepository.All())
            {
                var dateTime = DateTime.Now;
                Models.Department department;
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
                    else
                    {
                        department = storedDepartment;
                    }
                }

                if (department != null)
                {
                    department.Read = dateTime;
                    department = departmentRepository.TryAddOrUpdate(department, nameof(Product.Added));
                    departments.Add(department.ToDataTransferObject(hasChanged));
                }
            }

            await departmentRepository.SaveChangesAsync();

            return departments;
        }
    }
}