# TCSched: TCG Event Calendar

A small web app for a game store organizer to schedule trading card game events and for players to register by scanning a QR code.

**Stack:** Vue 3 + TypeScript (Vite) single-page app · ASP.NET Core (.NET 10) minimal API · EF Core + SQLite · Ical.Net (`.ics`) · `qrcode` · FullCalendar

## Run it

### Docker (one command)

```shell
docker compose up --build
```

Open http://localhost:8080. Some demo events for the coming week are added on first start, including one with a single seat left so you can watch it fill.

### Without Docker

Requires the .NET 10 SDK and Node 20.19+ (or 22.12+). Use two terminals, both from the repo root. The commands work in any shell (PowerShell, cmd, bash, zsh).

Terminal 1, the API:

```shell
dotnet run --project src/TCSched.Api
```

Terminal 2, the web app (run the install once, then start the dev server):

```shell
npm --prefix web install
npm --prefix web run dev
```

Open http://localhost:5173. The API runs on :5080, and Vite forwards `/api` requests to it. Demo events are added in Development mode.

### Scanning the QR code with a phone

The QR code encodes the address you opened the organizer page with, and a phone can't reach `localhost`. Open the organizer page using your machine's LAN address instead, e.g. `http://192.168.1.20:8080` (Docker) or `http://192.168.1.20:5173` (dev; Vite already listens on the LAN). Phone and computer must be on the same network, and your firewall must allow the port.

### Tests

```shell
dotnet test
```

69 integration tests run the real API against a temporary SQLite file. They cover validation, per-game limits, time zones and DST, ICS output, duplicates, full events, and concurrent last-seat races.

## Try the flow

1. **Calendar** (`/organizer`): month grid or agenda list. Each event shows its seat count, and full events are marked. Click a day to schedule an event on it.
2. **New event:** pick a game. Its template fills in the formats, default capacity, allowed range and duration.
3. **Event page:** seats bar, live player list, QR code, copy link, **Add to calendar (.ics)**.
4. **Scan or open the link** (`/e/{id}`): enter a name and register. Try the same name again (duplicate), then fill the event and try once more (full).

All times are entered and shown on the **store's clock** (`Store:TimeZone` in `appsettings.json`, default `America/Los_Angeles`), whatever time zone the browser is in. The create form shows the store's zone and current time, and warns when it differs from your computer's. The game selected by default is store configuration too (`Store:DefaultGameTemplateId`, default `mtg`), so no game id is hard-coded in the UI.

## Design write-up

### Capacity: where it lives and how it's enforced

The **template** sets a default and an upper limit per game (e.g. Lorcana max 24). The organizer picks a capacity within `[minPlayers, min(template max, 30)]`, and the **event** stores its own copy. The **database** has the final word: `CHECK (RegisteredCount BETWEEN 0 AND Capacity)`, `CHECK (Capacity BETWEEN 1 AND 30)`, and a unique index on `(EventId, NormalizedName)`.

Registering is one transaction with two statements:

```sql
UPDATE Events SET RegisteredCount = RegisteredCount + 1
WHERE Id = @id AND RegisteredCount < Capacity AND StartsAtUtc > @now;   -- 0 rows = refused
INSERT INTO Registrations (...);   -- unique violation = duplicate → rollback releases the seat
```

The check and the increment happen in **one statement**, so there's no gap between reading the count and writing it, and two requests can't both see "1 seat left". SQLite lets one writer in at a time, and the next writer waits. The same SQL is also correct on Postgres or SQL Server under row locking, so it doesn't depend on SQLite. When a claim is refused, a follow-up read only picks the message: `404`, `409 DUPLICATE_REGISTRATION` (checked first, since "you're already in" is more useful than "full" to someone scanning again), `409 EVENT_STARTED`, or `409 EVENT_FULL`.

**Concurrent last seat:** the race tests release 50–100 requests at once at 4-, 5- and 30-seat events. Exactly `capacity` succeed, and the stored count matches the rows. To check that the tests can catch the bug, I temporarily swapped in a naive "read count → check → save" version. It let **7** players into 4 seats, **18** into 5, and **54** into 30. It also lost updates, so the counter went *down* and the CHECK constraint never fired. That's why the claim is a single statement and doesn't rely on the constraint alone.

**Duplicates:** names are normalized (Unicode-normalized, trimmed, whitespace collapsed, case-insensitive), so `" alex  smith"` equals `"ALEX SMITH"`. Known limitation: two different people with the same name collide, and the error message suggests adding an initial.

### Template system

A game is a row of **data** (`templates.json` → `GameTemplates` table): `formats`, `defaultFormat`, `defaultDurationMinutes`, `defaultCapacity`, `maxCapacity`, `minPlayers`. Event creation, validation, the create form and the calendar colors all read these properties. Nothing branches on a game's id or name. The file is validated at startup (the app won't start with an invalid template), and seeding only inserts missing ids, so existing templates are never overwritten. When an event is created it **copies** format, duration, capacity and min players from its template, so changing a template later never changes an existing event.

- **Add a 4th card game:** add one JSON entry with a new `id` and restart. No code change. A test does exactly this with a Flesh and Blood template and runs it end to end.
- **A non-card game** that fits the same properties (e.g. a board game night) is the same JSON entry. Different rules, such as tables of 4 or team registration, would need new optional template properties and a capacity rule chosen by data (e.g. `capacityMode: "individual" | "tables"`) inside the seat-claim step. That's the one place core logic would grow, and it would grow by mode, not by game.

More detail: [Game templates](#game-templates) below and [docs/DESIGN.md](docs/DESIGN.md).

### What I cut or faked, and what's next

**Faked or simplified:** one store, configured in `appsettings.json`; no auth, so `/organizer` is open (a single trusted organizer is assumed); demo data is seeded; registration needs only a name. The UI is plain on purpose.

**Testing I faked:**
- **QR scanning:** never done with a real phone camera. Instead I checked that the QR image encodes the registration URL and opened that URL in a separate browser tab, the way a phone would after scanning. Reaching the app from a phone over the LAN is documented above but not verified.
- **Docker deployment:** `docker compose up` was not run, because Docker wasn't available on the build machine. Instead I ran the same Release publish output with the built SPA in `wwwroot`, in Production mode on port 8080 with the container's environment variables. Every SPA route, static assets, the API, the JSON 404 for unknown API paths and demo seeding all worked. The image build itself (Node and .NET stages, the non-root user, the `/data` volume) has not been exercised.

**Cut:** editing or cancelling events, unregistering, waitlists, check-in, rate limiting and bot protection on the public registration endpoint, frontend unit and e2e tests (the API has 69 integration tests, and the UI flow was checked by hand in a browser), OpenAPI-generated TS types, and an accessibility pass.

**Next, in order:**
1. **Admin section for game templates.** An `/admin/templates` list page and one create/edit form, backed by `POST/PUT/DELETE /api/templates`, reusing the startup validator. The groundwork is already there: events copy their template values, so edits never change scheduled events; seeding is insert-only, so admin edits survive restarts; deleting would be refused (`409 TEMPLATE_IN_USE`) while events use a template, with archiving after that. Designed in [docs/DESIGN.md §15](docs/DESIGN.md).
2. **`templates sync --dry-run`** command (see [below](#future-explicit-template-sync)).
3. Rate limiting on `POST /registrations`, a waitlist, and player self-cancel with a token.
4. Playwright e2e tests for the create → scan → register → full flow.

## Game templates

Games (Magic: The Gathering, Pokémon TCG, Disney Lorcana) are data, not code. They live in the `GameTemplates` table and are seeded from [src/TCSched.Api/Templates/templates.json](src/TCSched.Api/Templates/templates.json).

**Seeding guard:** at startup the app validates `templates.json` (it won't start if the file is invalid) and then **inserts only templates whose `id` isn't already in the database**. Existing templates are never updated or overwritten, even if the JSON for them changed.

- **Add a game:** add a new entry with a new `id` to `templates.json` and restart. No code change.
- **Change an existing game:** editing its JSON entry has no effect on a database that already has it. Change it in the database, or delete `tcsched.db` (or the Docker volume) to start fresh. Existing events are unaffected either way, because each event keeps its own copy of format, duration, capacity and min players.
- **Known quirk:** a template deleted from the database comes back on the next restart if it's still in `templates.json`.

### Future: explicit template sync

An opt-in command so JSON changes can be applied on purpose, never by accident at startup:

```shell
dotnet run --project src/TCSched.Api -- templates sync --dry-run
```

- Shows a diff (added / changed / unchanged / only in DB) before writing anything; without `--dry-run` it applies the changes in one transaction.
- Upserts templates from `templates.json` using the same validator as startup.
- Never deletes templates that events use. Templates that exist only in the DB are reported, not removed.
- Startup seeding stays insert-only.

## API

| Method | Path | Notes |
|---|---|---|
| GET | `/api/store` | Store name, address, time zone |
| GET | `/api/templates` | Game templates |
| GET | `/api/events?from=&to=` | Events starting in range |
| POST | `/api/events` | `{ name, gameTemplateId, format?, startsAtLocal: "2026-11-07T18:00", capacity? }` |
| GET | `/api/events/{id}` | |
| GET | `/api/events/{id}/registrations` | Organizer's player list |
| POST | `/api/events/{id}/registrations` | `{ name }` → `201`, or `409 EVENT_FULL` / `DUPLICATE_REGISTRATION` / `EVENT_STARTED` |
| GET | `/api/events/{id}/calendar.ics` | Download (UTC start/end, store location, registration link) |

Errors are RFC 7807 problem details with a machine-readable `code`.

## Project layout

```
src/TCSched.Api/     Minimal API: Domain/, Data/ (EF Core, migrations, seeding), Endpoints/, Templates/templates.json
tests/TCSched.Api.Tests/   xUnit integration tests (WebApplicationFactory + temp SQLite file)
web/                 Vue 3 SPA: views/organizer/*, views/player/*, api/, lib/, components/
docs/                REQUIREMENTS.md, DESIGN.md (design/dev doc with decisions and AI log)
```

## AI usage

I used **Claude Code** (Claude Opus) as a pair programmer throughout: breaking down the requirements, writing the design doc, the API and tests, the Vue pages, and checking the full flow in a browser. A **Sonnet subagent** scaffolded the Vite/Vue project in parallel while the .NET side was set up. I made the scope and design calls: one app with organizer and player sections instead of two apps, phasing the template admin section out of the core build, keeping EF Core, and insert-only template seeding. Every generated change was reviewed, built and tested before it was committed. A running log is in [docs/DESIGN.md §14](docs/DESIGN.md).

**Example I had to fix:** EF Core's generated migration for snapshotting `MinPlayers` onto events added the column with a default of `0`, then added a `CHECK (MinPlayers >= 1)`. On SQLite that table rebuild fails for any database that already has events. The tests didn't catch it because they always start from an empty database. I added a backfill from each event's template (clamped to its capacity) before the constraint, and checked it by upgrading a database that already had an event. Others: the scaffold mixed FullCalendar majors 6 and 7, and was pinned to 6; and a browser run showed that the `.ics` registration link pointed at the API's port instead of the app's, fixed with forwarded headers and a test.
