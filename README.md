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

## Game templates

Games (Magic: The Gathering, Pokémon TCG, Disney Lorcana) are data, not code. They live in the `GameTemplates` table and are seeded from [src/TCSched.Api/Templates/templates.json](src/TCSched.Api/Templates/templates.json).

**Seeding guard:** at startup the app validates `templates.json` (it won't start if the file is invalid) and then **inserts only templates whose `id` isn't already in the database**. Existing templates are never updated or overwritten, even if the JSON for them changed.

- **Add a game:** add a new entry with a new `id` to `templates.json` and restart. No code change.
- **Change an existing game:** editing its JSON entry has no effect on a database that already has it. Change it in the database, or delete `tcsched.db` to start fresh. Existing events are unaffected either way, because each event keeps its own copy of format, duration, capacity and min players.
- **Known quirk:** a template deleted from the database comes back on the next restart if it's still in `templates.json`.

### Future: explicit template sync

An opt-in command so JSON changes can be applied on purpose, never by accident at startup:

```bash
dotnet run --project src/TCSched.Api -- templates sync --dry-run
```

- Shows a diff (added / changed / unchanged / only in DB) before writing anything; without `--dry-run` it applies the changes in one transaction.
- Upserts templates from `templates.json` using the same validator as startup.
- Never deletes templates that events use. Templates that exist only in the DB are reported, not removed.
- Startup seeding stays insert-only.

## Tests

```bash
dotnet test
```
