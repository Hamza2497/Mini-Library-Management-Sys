# Demo Script (60–90s)

## Quick Flow
- Open UI: `https://hamza2497.github.io/Mini-Library-Management-Sys/`
- Click **Sign in** with Google.
- In **Library**, click **Search** to load books.
- Go to **Dashboard**, add one book (title + author).
- Back to **Library**, find that book.
- Click **Checkout**, then **Checkin**.
- Click **Edit** and update title/author, save.
- (Admin) Click **Delete** on a test book.
- Click **Enrich** on a book.
- Open **Details** and confirm `Category`, `Tags`, `Description`.

## Swagger Proof
- Open Swagger: `https://mini-library-api-po6c.onrender.com/swagger/index.html`
- Click **Authorize**, paste `Bearer <Google ID token>`.
- Execute:
  - `GET /api/me` (identity/roles)
  - `GET /api/books` (data load)
  - `POST /api/books/{id}/ai/enrich` (enrichment endpoint)
