using Library.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Library.Api.Data;

public class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();
    public DbSet<Loan> Loans => Set<Loan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Value converter to convert Guid to/from string for SQLite TEXT columns
        var guidConverter = new ValueConverter<Guid, string>(
            g => g.ToString("D"),
            s => string.IsNullOrEmpty(s) ? Guid.Empty : Guid.Parse(s));

        // Configure Book Guid columns to use TEXT with value converter
        modelBuilder.Entity<Book>()
            .Property(b => b.Id)
            .HasColumnType("TEXT")
            .HasConversion(guidConverter);

        modelBuilder.Entity<Loan>()
            .Property(l => l.Id)
            .HasColumnType("TEXT")
            .HasConversion(guidConverter);

        modelBuilder.Entity<Loan>()
            .Property(l => l.BookId)
            .HasColumnType("TEXT")
            .HasConversion(guidConverter);

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
