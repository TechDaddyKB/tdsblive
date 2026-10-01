using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExtensionSuite.Data;

public sealed class FoundationDesignFactory : IDesignTimeDbContextFactory<FoundationDbContext>
{
    public FoundationDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<FoundationDbContext>().UseSqlite("Data Source=:memory:").Options);
}
