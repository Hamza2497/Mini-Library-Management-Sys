using System.ComponentModel.DataAnnotations;

namespace Library.Api.Models;

public class Loan
{
    public Guid Id { get; set; }

    public Guid BookId { get; set; }

    [Required]
    public string BorrowerUserId { get; set; } = string.Empty;

    public DateTime BorrowedAtUtc { get; set; }

    public DateTime? ReturnedAtUtc { get; set; }

    public Book Book { get; set; } = null!;
}
