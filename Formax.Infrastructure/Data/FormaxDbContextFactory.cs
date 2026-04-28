using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Formax.Infrastructure.Data
{
    public class FormaxDbContextFactory
        : IDesignTimeDbContextFactory<FormaxDbContext>
    {
        public FormaxDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<FormaxDbContext>();
            optionsBuilder.UseSqlServer(
                configuration.GetConnectionString("FormaxDB"));

            return new FormaxDbContext(optionsBuilder.Options);
        }
    }
}
