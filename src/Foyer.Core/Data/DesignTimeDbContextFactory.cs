using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Foyer.Core.Data;

/// <summary>Lets <c>dotnet ef</c> build the context from this project alone.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FoyerDbContext>
{
    public FoyerDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FoyerDbContext>()
            .UseSqlite("Data Source=foyer-design.db")
            .Options);
}
