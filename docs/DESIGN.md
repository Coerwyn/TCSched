# Design / Dev Doc — TCG Event Calendar

Status: draft · 2026-10-21 · Requirements: [REQUIREMENTS.md](REQUIREMENTS.md)

## 1. Goal

A small web app for one store organizer to schedule trading card game events, and for players to register by scanning a QR code. The aim is a working end-to-end flow — **create → calendar → QR → register → full** — within a 3-hour timebox. Capacity is enforced at the database level.

**Phasing:** Phase 1 (§2–§13) is the core end-to-end flow, and it ships first. Phase 2 (§15) covers extras, starting with an admin section for game templates, built only if time is left. Otherwise it's written up as "what's next" in the README (§16).

## 2. Architecture

```
Browser (Vue 3 SPA)
  /organizer/*  ── create, calendar, event detail (QR, ICS, registrants)
  /e/:id        ── public event view + registration form
  (/admin/*     ── Phase 2: game template management)
        │  JSON over /api
        ▼
ASP.NET Core (.NET 10 Minimal API)
  Templates (read-only) · Events · Registrations · ICS
        │  EF Core
        ▼
SQLite (file, Docker volume)
```

- **One SPA, two route areas.** There's no auth, so "organizer" vs "player" is just routing. Both areas share the API client and types.
- **One deployable.** In Docker, a multi-stage build compiles the Vue app into the API's `wwwroot`. ASP.NET serves `/api/*` and falls back to `index.html` for every other path. One container, one port.
- **Local dev:** `dotnet run` (API on :5080) plus `npm run dev` (Vite on :5173, which proxies `/api` to :5080).

### Repo layout

```
/src/TCSched.Api/            Minimal API, EF Core, domain
    Data/                    DbContext, migrations, seeders
    Domain/                  Event, GameTemplate, Registration, name normalizer, GameTemplateValidator
    Endpoints/               TemplateEndpoints, EventEndpoints, RegistrationEndpoints
    Templates/templates.json Game template definitions
/tests/TCSched.Api.Tests/    xUnit + WebApplicationFactory integration tests
/web/                        Vue 3 + TS + Vite
    src/api/                 typed fetch client + DTO types
    src/views/organizer/     CalendarView, CreateEventView, EventDetailView
    src/views/player/        PublicEventView (includes the registration form)
    src/components/          QrCode, SeatsBadge, ...
docker-compose.yml, Dockerfile, README.md
```

## 3. Data model

```
GameTemplate 1 ──< Event 1 ──< Registration
```

### GameTemplate (DB table, seeded from `templates.json`; read-only in Phase 1)

| Field | Type | Notes |
|---|---|---|
| Id | string (slug) | `mtg`, `pokemon`, `lorcana` — stable key |
| Name | string | Display name |
| Formats | string[] (JSON column) | e.g. MTG: Commander, Draft, Modern, Standard |
| DefaultFormat | string | Must be in Formats |
| DefaultDurationMinutes | int | Sets the event's end time (needed for the ICS) |
| DefaultCapacity | int | Pre-filled on the create form |
| MaxCapacity | int | ≤ 30 (global limit) |
| MinPlayers | int | Shown as "needs N more to fire" |

Seed values (initial — easy to tweak):

| Game | Formats | Duration | Default / Max cap | Min |
|---|---|---|---|---|
| Magic: The Gathering | Commander, Draft, Standard, Modern | 240 | 16 / 30 | 4 |
| Pokémon TCG | Standard, Expanded | 180 | 16 / 30 | 4 |
| Disney Lorcana | Core Constructed, Sealed | 180 | 12 / 24 | 4 |

**Template rules** live in one `GameTemplateValidator`: slug matches `^[a-z0-9-]{2,32}$`, Name is 1–60 chars, at least one format and no duplicate formats, DefaultFormat is one of Formats, 1 ≤ MinPlayers ≤ DefaultCapacity ≤ MaxCapacity ≤ 30, and duration is 15–720 minutes. Phase 1 uses it on the seed file; Phase 2's admin endpoints reuse it unchanged.

**Seeding:** at startup, each template in `templates.json` is inserted **only if its Id doesn't exist yet**, and an invalid file stops startup. Insert-only (never overwrite) costs nothing now and means Phase 2's UI edits would survive restarts. Decided 2026-10-21: we kept per-template insert-only rather than seeding only an empty table, so "add a JSON entry and restart" still adds a game. The tradeoff: a template deleted from the DB comes back while it's still in the JSON. Applying JSON changes to existing templates is left to a future explicit `templates sync` command (§12).

### Event

| Field | Type | Notes |
|---|---|---|
| Id | Guid | Used in the public link; not guessable |
| Name | string (1–100) | |
| GameTemplateId | FK | |
| Format | string | Validated against the template's formats when the event is created |
| StartsAtUtc | DateTime (UTC) | Converted from store-local input |
| DurationMinutes | int | Copied from the template at creation |
| Capacity | int | MinPlayers ≤ cap ≤ min(template.MaxCapacity, 30) |
| MinPlayers | int | Copied from the template at creation; drives "needs N more" |
| RegisteredCount | int | Running count of registrations; guarded by the conditional update (§4) |
| CreatedAtUtc | DateTime | |

DB constraints: `CHECK (Capacity BETWEEN 1 AND 30)`, `CHECK (RegisteredCount >= 0 AND RegisteredCount <= Capacity)` and `CHECK (MinPlayers >= 1 AND MinPlayers <= Capacity)`.

**Snapshot, not reference:** format, duration, capacity and min players are copied onto the event when it's created. The template only supplies defaults and limits at that moment. So a template can change (by editing the JSON now, or in the admin section later) without changing or invalidating events already scheduled.

### Registration

| Field | Type | Notes |
|---|---|---|
| Id | Guid | |
| EventId | FK | |
| DisplayName | string (1–60) | As entered |
| NormalizedName | string | Trimmed, collapsed whitespace, `ToUpperInvariant` |
| RegisteredAtUtc | DateTime | |

Unique index: `(EventId, NormalizedName)`.

## 4. Capacity enforcement (the core correctness piece)

**Where capacity lives:** the template sets the default and upper limit, each event stores its own `Capacity`, and the database's CHECK constraint is the final guard.

**Registration algorithm** (one transaction):

1. Validate the name (non-empty after normalization, ≤ 60 chars) → `400`.
2. Atomic seat claim:
   ```sql
   UPDATE Events SET RegisteredCount = RegisteredCount + 1
   WHERE Id = @id AND RegisteredCount < Capacity;
   ```
   In EF: `ExecuteUpdateAsync`. If 0 rows were affected, check whether the event exists: missing → `404`, otherwise → `409 EVENT_FULL`.
3. `INSERT` the Registration. A unique-index violation → roll back (which releases the seat) → `409 DUPLICATE_REGISTRATION`.
4. Commit → `201` with the registration and the new seat count.

**Why this is safe under concurrency:** the check and the increment happen in one statement, so two requests can't both see "1 seat left". There's no read-then-write window. SQLite only lets one writer in at a time, and Microsoft.Data.Sqlite's busy timeout makes the next writer wait instead of failing. The same SQL is also correct on Postgres or SQL Server under row locking, so we don't rely on SQLite's single writer. The CHECK constraint is a backstop in case a future code path skips the conditional update.

**When the claim is refused**, a follow-up read picks the clearest message: event missing → `404`; this name already registered → `DUPLICATE_REGISTRATION` (more useful than "full" for someone scanning again); event started → `EVENT_STARTED`; otherwise → `EVENT_FULL`. That read only chooses the message. The `UPDATE` already decided whether to accept the registration.

**Evidence** (`LastSeatRaceTests`): the tests send concurrent requests through a barrier, each against its own temp-file DB. They passed 8 out of 8 repeated runs. As a check, we temporarily swapped in a naive "read count → check in C# → save" version and ran the same tests:

| Scenario | Correct (conditional UPDATE) | Naive read-then-write |
|---|---|---|
| 4 seats, 50 concurrent | 4 registered | **7** registered |
| 5 seats, 50 concurrent | 5 | **18** |
| 30 seats, 100 concurrent | 30 | **54** |
| 4 seats, 20× same name + 3 others | 4 rows, counter 4 | 4 rows, **counter 1** (lost updates) |

The last row explains why the CHECK constraint alone isn't enough. With lost updates the counter goes *down*, so the constraint never fires and the real overfill goes unnoticed.

*Alternatives considered:* `COUNT(*)` then insert (race-prone); app-level locks (break when there's more than one instance); serializable isolation plus retry (works, but more complex than one conditional update).

**Duplicates:** same normalized name on the same event → 409. Known limitation: two different people with the same name collide; the second can add an initial ("Alex P."). This goes in the README.

## 5. Game template system

- Templates are rows in `GameTemplates`, seeded from `templates.json`. Phase 1 exposes them read-only (`GET /api/templates`).
- Core logic works only with template **properties**, never game names: create-event validation reads `Formats`, `MaxCapacity` and `MinPlayers`, and defaulting reads `DefaultCapacity` and `DefaultDurationMinutes`. There's no `switch`/`if` on game id anywhere. A test proves it by seeding a fake 4th template and creating an event with it.
- The frontend builds the create form from `GET /api/templates`: choosing a template fills the format dropdown, pre-fills capacity, sets the max on the capacity input, and shows the duration/end time.
- **Adding a 4th game (Phase 1):** add one JSON object to `templates.json` and restart. No code change. Phase 2 makes this a form in an admin section (§15).
- **A non-card game** (e.g. a board game with tables of 4): if it fits the same properties, it's still just data. Different rules, like `seatsPerTable` or team-based capacity, would need new optional template properties plus a capacity rule chosen by a template field (e.g. `capacityMode: "individual" | "tables"`). That's a deliberate extension point, cut for time.

## 6. API (Phase 1)

All errors use RFC 7807 `ProblemDetails` with a machine-readable `code` extension that the UI maps to messages.

| Method | Path | Result |
|---|---|---|
| GET | `/api/store` | `{ name, address, timeZone }` (from `appsettings`) |
| GET | `/api/templates` | `GameTemplateDto[]` |
| GET | `/api/events?from=&to=` | `EventSummaryDto[]` (incl. `registeredCount`, `capacity`, `endsAtUtc`) |
| POST | `/api/events` | `201 EventDto` · `400 VALIDATION` · `400 UNKNOWN_TEMPLATE` |
| GET | `/api/events/{id}` | `EventDto` · `404` |
| GET | `/api/events/{id}/registrations` | `RegistrationDto[]` (organizer view) |
| POST | `/api/events/{id}/registrations` | `201` · `400` · `404` · `409 EVENT_FULL` · `409 DUPLICATE_REGISTRATION` · `409 EVENT_STARTED` |
| GET | `/api/events/{id}/calendar.ics` | `text/calendar`, `Content-Disposition: attachment` |

**Create request:** `{ name, gameTemplateId, format?, startsAtLocal: "2026-11-07T18:00", capacity? }`. Missing `format` or `capacity` fall back to the template defaults.

**Registration cut-off:** registration is rejected once the event's start time has passed (`EVENT_STARTED`). It's a cheap check and avoids the obvious oddity.

## 7. Time zones & ICS

- The store's time zone is configured as IANA (e.g. `America/Chicago`) in `appsettings.json` / env.
- The organizer enters **store-local wall time** with no offset. The server converts it to UTC with `TimeZoneInfo` (IANA ids work on Windows via ICU in .NET 6+). That way the browser's own time zone doesn't matter.
- DST edges: a time skipped by "spring forward" (e.g. 02:30 on the change day) is rejected with a clear message. A time that happens twice ("fall back") resolves to standard time. An invalid `Store:TimeZone` stops startup.
- The API returns UTC; the UI formats it with `Intl.DateTimeFormat(…, { timeZone: store.timeZone })`.
- **ICS via Ical.Net:** `SUMMARY` = event name (plus game/format), `DTSTART`/`DTEND` in UTC (`Z`), `LOCATION` = store name + address, `DESCRIPTION` includes the registration URL, a stable `UID` = `{eventId}@tcsched`, `DTSTAMP`, `METHOD:PUBLISH`, `URL` = the registration link. Download filename: `{event-name-slug}-{local date}.ics`.
- The registration link inside the file uses `Store:PublicBaseUrl` if set, otherwise the request's own host. Through the Vite proxy that's `localhost:5173`, and in Docker the app's own port.
- Checked: the tests parse the file back with Ical.Net and assert the raw `DTSTART:…Z` / `DTEND:…Z` lines for both CST and CDT dates. A manual look at the raw bytes confirmed CRLF line endings, folding at 75 bytes, and escaped commas and newlines. **Still to do by hand:** import a downloaded file into Google Calendar and Outlook.

## 8. QR & registration link

- Link format: `{PublicBaseUrl}/e/{eventId}`.
- The QR code is generated client-side with the `qrcode` npm library and rendered on the organizer's event page, next to a "copy link" button.
- **Gotcha:** `localhost` URLs don't resolve from a phone. `PublicBaseUrl` defaults to `window.location.origin`, but can be overridden with `VITE_PUBLIC_BASE_URL` (e.g. `http://192.168.1.20:8080`) so a phone on the same Wi-Fi can scan it. Document this in the README.

## 9. Frontend (Phase 1)

| Route | View | Contents |
|---|---|---|
| `/` | → redirect `/organizer` | |
| `/organizer` | CalendarView | FullCalendar month grid (`@fullcalendar/vue3` + daygrid) plus a list view toggle; each event shows its seat count; click opens the detail page; "New event" button |
| `/organizer/events/new` | CreateEventView | Form driven by the template (§5); on success, go to the detail page |
| `/organizer/events/:id` | EventDetailView | Details, seats X / Y, "needs N more" indicator, registrant list, QR, copy link, download `.ics` |
| `/e/:id` | PublicEventView | Event info, seats left, name field → register; clear success / full / duplicate / started messages; form disabled when full |

- **Calendar time zone:** FullCalendar needs a Luxon or moment-timezone plugin for named time zones; without one it falls back to UTC. So the calendar runs in `timeZone: 'UTC'` and is given each event as a **store-local wall time** string with no offset, converted with `Intl.DateTimeFormat`. It draws the store's clock in any browser with no extra dependency. Each fetch asks for one extra day on both sides to cover the UTC offset.
- **Game colors** come from a hash of the template id into a palette, so a new game gets a color with no code change.
- The organizer's event page refreshes every 5 s, so the seat count rises live while players scan.
- **Dev proxy:** Vite forwards `/api` with `xfwd: true`, and the API trusts `X-Forwarded-Host`/`Proto` from loopback proxies. Without that, links the API builds (the URL inside the `.ics`) point at the API's own port (5080) instead of the address the player opened.
- Libraries: `vue-router`, `@fullcalendar/vue3`, `@fullcalendar/daygrid`, `@fullcalendar/list`, `@fullcalendar/interaction` (click a day to create), `qrcode`. Native `fetch`; no Pinia (no shared state worth a store).
- DTO types are written by hand in `src/api/types.ts`. OpenAPI codegen is listed under "next".
- Styling is minimal (a classless CSS such as Pico or plain CSS). Polish is out of scope.

## 10. Testing (Phase 1)

Backend integration tests (xUnit + `WebApplicationFactory`) run against a **temp-file SQLite DB**, not the EF in-memory provider. The in-memory provider has no transactions or constraints, so it would hide exactly the bugs we care about.

1. **Last-seat race:** capacity 4/5/30 with 50/50/100 concurrent distinct names → exactly `capacity` × 201, the rest `EVENT_FULL`, and in the DB `RegisteredCount == capacity == COUNT(Registrations)`. A second test sends 20 concurrent copies of one name plus 3 others at a 4-seat event → 4 registered, 19 duplicates. Results are in §4.
2. Full event → 409 `EVENT_FULL`; duplicate (`" alex  "` vs `"ALEX"`) → 409 `DUPLICATE_REGISTRATION`, and the seat count isn't consumed.
3. Create validation: capacity > template max, capacity > 30, unknown template, invalid format.
4. Extensibility: the test host seeds a fake 4th template → creating an event and registering for it work with no code change.
5. ICS: parse the response with Ical.Net → correct summary, UTC start/end, location.
6. Seed validation: an invalid template entry stops startup.

Frontend: `vue-tsc` type-check only. Component and e2e tests are cut (see §12).

## 11. Running & seeding

- `docker compose up --build` → app on `http://localhost:8080`. The SQLite file is kept in a named volume.
- Migrations are applied at startup (`Database.Migrate()`).
- `Seed:SampleEvents=true` (on by default in compose/dev) creates a few events this month, including one with capacity 4 and 3 registrations, so "fills up" can be demoed with one scan.
- Dev: `dotnet run --project src/TCSched.Api` and `cd web && npm i && npm run dev`.

## 12. Scope cuts (planned)

Out of scope from the start: auth, payments, email, recurring events, editing/cancelling, admin dashboards.

Our additional cuts (all will be listed in the README):
- Template admin section (designed in §15; built only if time is left)
- Explicit template sync command (`templates sync [--dry-run]`): an opt-in upsert from `templates.json` with a diff, and no deleting of templates in use. Startup seeding stays insert-only. Described in the README.
- Waitlist / unregister / check-in
- Rate limiting and bot protection on the public registration endpoint
- Multi-store / per-event location
- OpenAPI → TS client generation
- Frontend unit and e2e tests (Playwright would be next)
- Accessibility and styling pass

## 13. Build plan (3h) & commit plan — Phase 1 only

| Time | Work | Commit(s) |
|---|---|---|
| 0:00–0:15 | `git init`, scaffold API, tests, Vite app, docs | `chore: scaffold api, tests, web` · `docs: requirements + design` |
| 0:15–0:45 | Domain, DbContext, migration, templates.json + validation, templates/events endpoints | `feat(api): game templates from config` · `feat(api): create/list events` |
| 0:45–1:10 | Registration with atomic seat claim + tests (race, full, duplicate) | `feat(api): capacity-enforced registration` · `test: last-seat race` |
| 1:10–1:20 | ICS endpoint + test | `feat(api): ics download` |
| 1:20–2:20 | Vue: API client, calendar, create form, detail (QR/ICS), public register | one commit per view |
| 2:20–2:40 | Dockerfile, compose, seed; full end-to-end run incl. phone scan | `chore: docker compose + seed` |
| 2:40–3:00 | README: run steps, design write-up, AI note, cut list, "what's next" (§16) | `docs: readme` |

If time runs short, cut in this order: list-view toggle → registrant list → seed data → Docker (keep the two-command dev run).

Phase 2 (§15) starts only after Phase 1 is committed and the end-to-end flow has been checked, and it gets its own commits. That way the history shows exactly where the core submission ends.

## 14. AI usage log (fill in as we go — feeds README)

| When | Tool | Used for | Rejected / fixed |
|---|---|---|---|
| 2026-10-21 | Claude Code | Requirements breakdown, design doc draft | — |
| 2026-10-21 | Claude Code (Sonnet subagent) | Vue scaffold | `npm install` mixed FullCalendar majors (`@fullcalendar/vue3` 7.x pulled in its own core 7.x next to 6.x core/daygrid/list) → pinned all to 6.1.22. The first `ApiError` used constructor parameter properties, which `erasableSyntaxOnly` rejects (TS1294) → rewritten with explicit fields. |
| 2026-10-21 | Claude Code (EF tooling) | `EventMinPlayersSnapshot` migration | The generated migration added `MinPlayers` with default 0 and then a `MinPlayers >= 1` check. On SQLite that table rebuild fails for any DB that already has events. Added a backfill from the template (clamped to capacity) before the constraint, and checked by upgrading a DB that had an event in it. |
| 2026-10-21 | Claude Code | Vue pages + end-to-end browser test | The browser run found a bug in Claude's own code. The `.ics` registration link pointed at `localhost:5080` (the API), not `:5173`, because the Vite proxy rewrites `Host`. Fixed with forwarded headers plus a test. The unit tests had missed it because they talk to the API directly. |
| 2026-10-21 | Claude Code | Data access choice | Defaulted to EF Core. The user questioned it as heavy for the project; we weighed Dapper and kept EF (decision recorded). |
| | | | |

## 15. Phase 2 — Admin section: game templates

Built only after Phase 1 is done. Otherwise this section becomes the README's "what's next" entry (§16).

**Why:** today a 4th game means editing `templates.json` and restarting. An admin section lets the organizer add or tune a game from the browser. It's still data, not code, so it extends the template requirement without touching event logic.

**Route area:** a third area, `/admin/*`, kept apart from `/organizer/*` (day-to-day scheduling) and `/e/*` (public). No auth, same as the rest of the app. With auth, `/admin` would be the first area to lock down.

**API** (reuses `GameTemplateValidator`; errors use ProblemDetails codes):

| Method | Path | Result |
|---|---|---|
| GET | `/api/templates` | Phase 1 endpoint, plus an `eventCount` field |
| GET | `/api/templates/{id}` | `GameTemplateDto` · `404` |
| POST | `/api/templates` | `201` · `400 VALIDATION` · `409 TEMPLATE_EXISTS` |
| PUT | `/api/templates/{id}` | `200` · `400 VALIDATION` · `404` (Id can't change) |
| DELETE | `/api/templates/{id}` | `204` · `404` · `409 TEMPLATE_IN_USE` |

**Pages:**

| Route | View | Contents |
|---|---|---|
| `/admin/templates` | TemplateListView | Table: name, formats, default/max capacity, duration, # events; New / Edit / Delete (Delete disabled while in use) |
| `/admin/templates/new`, `/admin/templates/:id` | TemplateEditView | One form for create and edit; formats as a list you can add to and remove from, with a radio for the default; slug read-only when editing; shows server validation errors |

**Rules:**
- Edits never change existing events, because events keep their own copy of format, duration and capacity (§3). Phase 1 needs no changes for this.
- Seeding is already insert-only, so UI edits survive restarts.
- Delete is blocked while events use the template. Archiving (`IsArchived`, hidden from the create form) is the next step after that.
- Concurrent edits: last write wins (single organizer). Optimistic concurrency with a row version is a later step.

**Tests:** CRUD validation failures; duplicate slug → 409; delete while in use → 409; editing a template leaves existing events unchanged and they can still be registered for; a template added via `POST` can be used to create an event.

**Estimate:** about 30–40 minutes (API + tests ~15, pages ~20).

## 16. README plan

Sections, in order:

1. **Overview** — one paragraph and a screenshot or GIF of the end-to-end flow.
2. **Run locally** — `docker compose up --build`; the dev alternative; the `VITE_PUBLIC_BASE_URL` note for scanning from a phone; how to run the tests.
3. **Design write-up (~1 page)**
   - *Capacity:* where it lives (template default/max → event snapshot → DB CHECK), the atomic conditional update, what happens on the last-seat race, the test that proves it, and how duplicates are handled.
   - *Templates:* the data-driven properties, no branching on game names, adding a 4th game today (one JSON entry), and what a non-card game would need.
   - *Cuts & next:* the §12 list, honest about anything faked, the planned admin section (below), and the template sync command (already drafted in the README's "Game templates" section).
4. **What's next — admin section** (draft text; trim it if Phase 2 ships):

   > **Admin section for game templates.** Games are data in a `GameTemplates` table, seeded from `templates.json`. Today, adding a game means adding a JSON entry and restarting. The next step is an `/admin/templates` area with a list page and one create/edit form, backed by `POST/PUT/DELETE /api/templates` that reuse the same validator as the seed file. The groundwork is already in place: events keep their own copy of format, duration and capacity, so editing a template never changes events already scheduled; seeding is insert-only, so admin edits survive restarts; and delete would be refused (`409 TEMPLATE_IN_USE`) while events use a template, with archiving after that. Estimated at about 30–40 minutes; left out to keep the timebox on the core flow.

5. **AI usage note** — from §14, including one example of AI output we rejected or fixed.
