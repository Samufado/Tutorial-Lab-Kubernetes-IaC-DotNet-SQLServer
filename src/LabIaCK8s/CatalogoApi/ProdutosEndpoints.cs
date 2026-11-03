using CatalogoApi.Data;
using CatalogoApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CatalogoApi;

public static class ProdutosEndpoints
{
    private static bool Invalido(ProdutoEntrada entrada) =>
        string.IsNullOrWhiteSpace(entrada.Nome) || entrada.Nome.Length > 80 ||
        entrada.Preco < 0 || entrada.Preco > 99999999.99m;

    public static void MapProdutos(this WebApplication app)
    {
        app.MapGet("/produtos", async (AppDbContext db) =>
            await db.Produtos.AsNoTracking().OrderBy(p => p.Id).ToListAsync());

        app.MapGet("/produtos/{id:int}", async (int id, AppDbContext db) =>
        {
            var produto = await db.Produtos.FindAsync(id);
            return produto is null ? Results.NotFound() : Results.Ok(produto);
        });

        app.MapPost("/produtos", async (ProdutoEntrada entrada, AppDbContext db) =>
        {
            if (Invalido(entrada))
                return Results.BadRequest("Informe nome (1 a 80 caracteres) e preco valido.");
            var produto = new Produto { Nome = entrada.Nome.Trim(), Preco = entrada.Preco };
            db.Produtos.Add(produto);
            await db.SaveChangesAsync();
            return Results.Created($"/produtos/{produto.Id}", produto);
        });

        app.MapPut("/produtos/{id:int}", async (int id, ProdutoEntrada entrada, AppDbContext db) =>
        {
            if (Invalido(entrada))
                return Results.BadRequest("Nome ou preco invalido.");
            var produto = await db.Produtos.FindAsync(id);
            if (produto is null) return Results.NotFound();
            produto.Nome = entrada.Nome.Trim();
            produto.Preco = entrada.Preco;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        app.MapDelete("/produtos/{id:int}", async (int id, AppDbContext db) =>
        {
            var produto = await db.Produtos.FindAsync(id);
            if (produto is null) return Results.NotFound();
            db.Produtos.Remove(produto);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
