using System.ComponentModel.DataAnnotations;

namespace Library.Api.Models;

public class Book
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Author { get; set; } = string.Empty;

    public ICollection<Loan> Loans { get; set; } = new List<Loan>();
}
