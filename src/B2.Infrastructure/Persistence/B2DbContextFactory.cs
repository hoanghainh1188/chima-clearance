using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace B2.Infrastructure.Persistence;

public sealed class B2DbContextFactory : IDesignTimeDbContextFactory<B2DbContext>
{
    public B2DbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<B2DbContext>()
            .UseSqlite("Data Source=design.db")
            .Options;
        return new B2DbContext(options);
    }
}
