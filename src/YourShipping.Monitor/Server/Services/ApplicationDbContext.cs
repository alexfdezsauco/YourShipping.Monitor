namespace YourShipping.Monitor.Server.Services
{
    using System.IO;
    using System.Reflection;

    using Microsoft.EntityFrameworkCore;

    using YourShipping.Monitor.Server.Models;

    public class ApplicationDbContext : DbContext
    {
        public DbSet<Department> Departments { get; set; }

        public DbSet<Product> Products { get; set; }

        public DbSet<Store> Stores { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(
                "Filename=your-shipping.db",
                options => { options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName); });
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity => { entity.HasIndex(e => e.Url).IsUnique(); });
            modelBuilder.Entity<Department>(entity => { entity.HasIndex(e => e.Url).IsUnique(); });
            modelBuilder.Entity<Store>(entity => { entity.HasIndex(e => e.Url).IsUnique(); });
            base.OnModelCreating(modelBuilder);
        }
    }
}