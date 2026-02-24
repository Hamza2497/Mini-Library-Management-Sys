using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Library.Api.Data;
using Library.Api.Models;
using Library.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController(LibraryDbContext dbContext, BookEnrichmentService enrichmentService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "MemberOrAbove")]
    public async Task<ActionResult<PagedBooksResponse>> GetBooks(
        [FromQuery] string? q,
        [FromQuery] bool? available,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Books.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var search = $"%{q.Trim()}%";
            if (dbContext.Database.IsNpgsql())
            {
                query = query.Where(b =>
                    EF.Functions.ILike(b.Title, search) ||
                    EF.Functions.ILike(b.Author, search));
            }
            else
            {
                query = query.Where(b =>
                    EF.Functions.Like(b.Title, search) ||
                    EF.Functions.Like(b.Author, search));
            }
        }

        if (available.HasValue)
        {
            if (available.Value)
            {
                query = query.Where(b => !b.Loans.Any(l => l.ReturnedAtUtc == null));
            }
            else
            {
                query = query.Where(b => b.Loans.Any(l => l.ReturnedAtUtc == null));
            }
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new
            {
                Book = b,
                IsAvailable = !b.Loans.Any(l => l.ReturnedAtUtc == null)
            })
            .ToListAsync();

        return Ok(new PagedBooksResponse(items.Select(x => ToResponse(x.Book, x.IsAvailable)).ToList(), page, pageSize, total));
    }

    [HttpPost]
    [Authorize(Policy = "LibrarianOrAdmin")]
    public async Task<ActionResult<BookListItemResponse>> CreateBook([FromBody] CreateBookRequest request)
    {
        var book = new Book
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Author = request.Author.Trim(),
            UpdatedAtUtc = DateTime.UtcNow
        };

        dbContext.Books.Add(book);
        await dbContext.SaveChangesAsync();

        var response = ToResponse(book, true);
        return CreatedAtAction(nameof(GetBooks), new { id = book.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "LibrarianOrAdmin")]
    public async Task<ActionResult<BookListItemResponse>> UpdateBook(Guid id, [FromBody] UpdateBookRequest request)
    {
        var book = await dbContext.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null)
        {
            return NotFound(new { message = "Book not found." });
        }

        book.Title = request.Title.Trim();
        book.Author = request.Author.Trim();
        book.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var isAvailable = !await dbContext.Loans.AnyAsync(l => l.BookId == id && l.ReturnedAtUtc == null);
        var response = ToResponse(book, isAvailable);
        return Ok(response);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteBook(Guid id)
    {
        var book = await dbContext.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null)
        {
            return NotFound(new { message = "Book not found." });
        }

        var hasActiveLoan = await dbContext.Loans.AnyAsync(l => l.BookId == id && l.ReturnedAtUtc == null);
        if (hasActiveLoan)
        {
            return Conflict(new { message = "Cannot delete while borrowed." });
        }

        dbContext.Books.Remove(book);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/checkout")]
    [Authorize(Policy = "MemberOrAbove")]
    public async Task<ActionResult<LoanResponse>> CheckoutBook(Guid id, [FromBody] CheckoutRequest request)
    {
        Console.WriteLine($"[CHECKOUT] Received ID: {id}");
        Console.WriteLine($"[CHECKOUT] ID formats - D: {id:D}, B: {id:B}, N: {id:N}");

        var bookCount = await dbContext.Books.CountAsync();
        Console.WriteLine($"[CHECKOUT] Total books in DB: {bookCount}");
        var bookIds = await dbContext.Books.Select(b => b.Id).ToListAsync();
        foreach (var bid in bookIds)
            Console.WriteLine($"[CHECKOUT]   DB has: {bid}");

        Console.WriteLine($"[CHECKOUT] About to query with ID parameter: '{id:D}'");
        var book = await dbContext.Books.FirstOrDefaultAsync(b => b.Id == id);

        if (book is null)
        {
            Console.WriteLine($"[CHECKOUT] NOT FOUND - Query returned null");
            Console.WriteLine($"[CHECKOUT] Trying direct SQL comparison...");
            var directCheck = await dbContext.Books.FromSqlInterpolated($"SELECT * FROM Books WHERE Id = {id:D}").FirstOrDefaultAsync();
            if (directCheck != null)
                Console.WriteLine($"[CHECKOUT] Direct SQL found it! Book: {directCheck.Title}");
            else
                Console.WriteLine($"[CHECKOUT] Direct SQL also didn't find it");
            return NotFound(new { message = "Book not found." });
        }
        Console.WriteLine($"[CHECKOUT] FOUND: {book.Title}");

        var activeLoan = await dbContext.Loans
            .FirstOrDefaultAsync(l => l.BookId == id && l.ReturnedAtUtc == null);

        if (activeLoan is not null)
        {
            return Conflict(new { message = "Book is already checked out." });
        }

        var borrowerUserId = request.BorrowerUserId?.Trim();
        if (string.IsNullOrWhiteSpace(borrowerUserId))
        {
            borrowerUserId = User.FindFirst("sub")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        if (string.IsNullOrWhiteSpace(borrowerUserId))
        {
            return BadRequest(new { message = "borrowerUserId is required." });
        }

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            BookId = id,
            BorrowerUserId = borrowerUserId,
            BorrowedAtUtc = DateTime.UtcNow,
            ReturnedAtUtc = null
        };

        dbContext.Loans.Add(loan);
        await dbContext.SaveChangesAsync();

        var response = new LoanResponse(
            loan.Id,
            loan.BookId,
            loan.BorrowerUserId,
            loan.BorrowedAtUtc,
            loan.ReturnedAtUtc);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("{id:guid}/checkin")]
    [Authorize(Policy = "LibrarianOrAdmin")]
    public async Task<ActionResult<LoanResponse>> CheckinBook(Guid id)
    {
        if (!await dbContext.Books.AnyAsync(b => b.Id == id))
        {
            return NotFound(new { message = "Book not found." });
        }

        var activeLoan = await dbContext.Loans
            .FirstOrDefaultAsync(l => l.BookId == id && l.ReturnedAtUtc == null);

        if (activeLoan is null)
        {
            return Conflict(new { message = "Book is not currently checked out." });
        }

        activeLoan.ReturnedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        var response = new LoanResponse(
            activeLoan.Id,
            activeLoan.BookId,
            activeLoan.BorrowerUserId,
            activeLoan.BorrowedAtUtc,
            activeLoan.ReturnedAtUtc);
        return Ok(response);
    }

    [HttpPost("{id:guid}/ai/enrich")]
    [Authorize(Policy = "LibrarianOrAdmin")]
    public async Task<ActionResult<BookListItemResponse>> EnrichBook(Guid id)
    {
        Console.WriteLine($"[ENRICH] Received ID: {id}");
        var bookCount = await dbContext.Books.CountAsync();
        Console.WriteLine($"[ENRICH] Total books in DB: {bookCount}");
        var bookIds = await dbContext.Books.Select(b => b.Id).ToListAsync();
        foreach (var bid in bookIds)
            Console.WriteLine($"[ENRICH]   DB has: {bid}");

        var book = await dbContext.Books.FirstOrDefaultAsync(b => b.Id == id);
        if (book is null)
        {
            Console.WriteLine($"[ENRICH] NOT FOUND");
            return NotFound(new { message = "Book not found." });
        }
        Console.WriteLine($"[ENRICH] FOUND");

        var enriched = await enrichmentService.EnrichAsync(book.Title, book.Author);
        book.Category = enriched.Category;
        book.Tags = string.Join(',', enriched.Tags);
        book.Description = enriched.Description;
        book.UpdatedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        var isAvailable = !await dbContext.Loans.AnyAsync(l => l.BookId == id && l.ReturnedAtUtc == null);
        return Ok(ToResponse(book, isAvailable));
    }

    private static IReadOnlyList<string> ParseTags(string? tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(5)
                .ToArray();
    }

    private static BookListItemResponse ToResponse(Book book, bool isAvailable)
    {
        return new BookListItemResponse(
            book.Id,
            book.Title,
            book.Author,
            isAvailable,
            book.Category,
            ParseTags(book.Tags),
            book.Description,
            book.UpdatedAtUtc);
    }
}

public class CreateBookRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Author { get; set; } = string.Empty;
}

public class CheckoutRequest
{
    public string? BorrowerUserId { get; set; }
}

public class UpdateBookRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Author { get; set; } = string.Empty;
}

public record BookListItemResponse(
    Guid Id,
    string Title,
    string Author,
    bool IsAvailable,
    string? Category,
    IReadOnlyList<string> Tags,
    string? Description,
    DateTime? UpdatedAtUtc);
public record PagedBooksResponse(IReadOnlyList<BookListItemResponse> Items, int Page, int PageSize, int Total);

public record LoanResponse(
    Guid Id,
    Guid BookId,
    string BorrowerUserId,
    DateTime BorrowedAtUtc,
    DateTime? ReturnedAtUtc);
