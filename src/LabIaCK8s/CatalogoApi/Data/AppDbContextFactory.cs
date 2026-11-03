using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CatalogoApi.Data;

// Usada somente pelo comando que gera as migrations.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=Catalogo;" +
                "Integrated Security=true;TrustServerCertificate=true;")
            .Options;
        return new AppDbContext(options);
    }
}
