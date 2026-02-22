using Library.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Data;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Loan>()
            .HasOne(l => l.Book)
            .WithMany(b => b.Loans)
            .HasForeignKey(l => l.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Loan>()
            .HasIndex(l => l.BookId);

        modelBuilder.Entity<Loan>()
            .HasIndex(l => l.BookId)
            .HasFilter("\"ReturnedAtUtc\" IS NULL")
            .IsUnique();
    }
}
