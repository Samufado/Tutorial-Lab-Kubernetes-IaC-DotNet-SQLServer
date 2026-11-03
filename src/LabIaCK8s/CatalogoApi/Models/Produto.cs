using System.ComponentModel.DataAnnotations;

namespace CatalogoApi.Models;

public class Produto
{
    public int Id { get; set; }
    [Required, MaxLength(80)]
    public string Nome { get; set; } = "";
    public decimal Preco { get; set; }
}

public record ProdutoEntrada(string Nome, decimal Preco);
