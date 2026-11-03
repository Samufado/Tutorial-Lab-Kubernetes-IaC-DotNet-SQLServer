using CatalogoApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CatalogoApi;

public static class Migrator
{
    public static async Task RunAsync(IServiceProvider services)
    {
        for (var tentativa = 1; tentativa <= 24; tentativa++)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            try
            {
                await db.Database.MigrateAsync();
                Console.WriteLine("Migrations aplicadas.");
                return;
            }
            catch (Exception) when (tentativa < 24)
            {
                Console.WriteLine($"Banco ainda indisponivel: tentativa {tentativa}/24.");
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}
