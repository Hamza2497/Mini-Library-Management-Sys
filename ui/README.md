# UI Quick Start

## Run UI
```bash
cd ui
python3 -m http.server 5173
open http://localhost:5173
```

## Run API
```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project ../Library.Api/Library.Api.csproj --no-launch-profile --urls http://localhost:5099
```

The UI uses a Google ID token and sends it as `Authorization: Bearer <token>`.
The API validates Google ID tokens.

## API Base URL Override
Set API base URL without rebuild:
```js
localStorage.setItem("api_base_url", "http://localhost:5099")
```
For Render:
```js
localStorage.setItem("api_base_url", "https://YOUR_RENDER_URL_HERE")
```
