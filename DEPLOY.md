# Deploy Guide (Render + GitHub Pages)

## 1) Deploy API to Render (Docker)
1. In Render, create a **PostgreSQL** database.
2. Create a **Web Service** from this repo.
3. Runtime: Docker (uses root `Dockerfile`).
4. Ensure service uses port `8080` (container already exposes 8080 and uses `ASPNETCORE_URLS=http://+:8080`).
5. Set environment variables on the Web Service:
   - `GOOGLE_CLIENT_ID=308905289637-064rj6fgrqlcebmbh1gg1v2ub20gc93p.apps.googleusercontent.com`
   - `DATABASE_URL=<Render Postgres URL>`
   - `CORS_ORIGINS=https://<username>.github.io`

## 2) Deploy UI to GitHub Pages
1. Push to `main` (workflow: `.github/workflows/deploy-ui-pages.yml`).
2. In GitHub repo settings, open **Pages** and set Source to **GitHub Actions**.
3. After workflow succeeds, your Pages URL will be:
   - `https://<username>.github.io/<repo>/` (or root if configured that way).

## 3) Point UI to Render API
In browser console on your Pages site:
```js
localStorage.setItem("api_base_url", "https://YOUR_RENDER_URL_HERE")
```
Refresh the page.

## 4) Google OAuth settings
In Google Cloud Console (OAuth client):
- Authorized JavaScript origins:
  - `http://localhost:5173`
  - `https://<username>.github.io`

## 5) Auth note
The API validates **Google ID tokens** as Bearer tokens.
No Google client secret is required.
