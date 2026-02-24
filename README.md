# Mini Library Management System

## Live Demo
- UI: `https://hamza2497.github.io/Mini-Library-Management-Sys/`
- API Swagger: `https://mini-library-api-po6c.onrender.com/swagger/index.html`

## Features
- Book CRUD: create, read, update, delete.
- Checkout/checkin workflow with active-loan rule.
- Search/filter/pagination on books list.
- Google SSO using Google ID token (JWT Bearer).
- Role-based authorization: Admin, Librarian, Member.
- AI Book Enrichment: Gemini API-powered category, tags, and description generation.

## Roles & Permissions
- `Admin`: full access (add/edit/delete, enrich, operational actions).
- `Librarian`: manage books, enrich, checkin/checkout operations (no admin-only delete if restricted).
- `Member`: browse/search and member-level actions.
- Role source: `RoleMappings` in `Library.Api/appsettings.Development.json`.
- Current mapped admin email: `hamzahassaf08@gmail.com`.

## Database Support
The API is **database-aware** and automatically uses the appropriate database:
- **Local Development**: SQLite (`library.db`) - no external dependencies
- **Production**: PostgreSQL - set via `DATABASE_URL` environment variable

The code intelligently detects the database type and configures UUID handling accordingly.

## Local Setup
- API:
  - `cd Library.Api`
  - Set Gemini API key: `export GEMINI_API_KEY=your_key`
  - `ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5099`
- UI:
  - `cd ui`
  - `python3 -m http.server 5173`
  - Open `http://localhost:5173`

## Deployment
- API: Render (Docker + PostgreSQL).
- UI: GitHub Pages via `.github/workflows/deploy-ui-pages.yml`.
- Required environment variables (Render):
  - `GOOGLE_CLIENT_ID` - Google OAuth client ID
  - `DATABASE_URL` - PostgreSQL connection string (auto-detected)
  - `GEMINI_API_KEY` - Gemini API key for book enrichment
  - `CORS_ORIGINS=https://hamza2497.github.io` - CORS allowed origins
  - `ASPNETCORE_ENVIRONMENT=Development` - for Swagger visibility

## Troubleshooting
- `401 Unauthorized`:
  - Re-sign in to refresh Google ID token.
  - Verify `GOOGLE_CLIENT_ID` matches token `aud`.
- `403 Forbidden`:
  - User authenticated but missing role mapping.
  - Check `RoleMappings` (email exactness/casing).
- CORS errors:
  - Ensure `CORS_ORIGINS` includes `https://hamza2497.github.io`.
  - Confirm UI `localStorage.api_base_url` points to active Render API URL.
- Token/popup issues:
  - Confirm Google OAuth Authorized JavaScript origins include:
    - `http://localhost:5173`
    - `https://hamza2497.github.io`
