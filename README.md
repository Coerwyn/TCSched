# TCSched — TCG Event Calendar

Schedule in-store trading card game events and let players register by scanning a QR code.

> Work in progress. Design: [docs/DESIGN.md](docs/DESIGN.md) · Requirements: [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md)

## Run locally (dev)

Requires the .NET 10 SDK and Node 20+.

```bash
dotnet run --project src/TCSched.Api
```

```bash
cd web && npm install && npm run dev
```

The API listens on http://localhost:5080. Open the app at http://localhost:5173 (Vite forwards `/api` requests to the API).

## Tests

```bash
dotnet test
```
