# Requirements — TCG Event Calendar

A small web app for a game store organizer to schedule in-store trading card game events and let players register for them. Built under a ~3-hour timebox, so the priority is a working end-to-end flow over polish.

## Functional requirements

| ID | Requirement | Must-haves / acceptance |
|----|-------------|-------------------------|
| R1 | **Event creation** | Organizer creates an event with: name, game type, date + start time, player capacity. Capacity up to **30**. |
| R2 | **Game templates** | At least 3 TCGs, including **Magic: The Gathering**. Data-driven templates (no hard-coded strings or if/else on game names). Each template drives **at least 2 event properties** (e.g. formats, default duration, default/max capacity, min players). Adding a 4th game must not touch core event logic. |
| R3 | **Calendar view** | Organizer sees what's happening on a given day. Month grid or grouped-by-day agenda. |
| R4 | **ICS invite** | Event page offers a downloadable `.ics` with correct title, start/end and location, importable into Google Calendar / Outlook. Built with a library. |
| R5 | **Registration + QR** | Each event has a registration link; the event page shows a QR code of it (library). Scanning leads to a simple form (name is enough). **Capacity enforced server-side**; a full event rejects further registrations with a clear message. |

## Derived requirements

- **Concurrency:** the last-seat race must be safe (atomic check-and-claim, not read-then-write).
- **Duplicate registrations:** defined, sensible behavior.
- **Capacity is a first-class constraint** in the data model (Event / Template / Registration).
- **End time** is derived from the template's default duration, so the ICS has a correct end.
- **Location** for the ICS comes from store configuration.
- **Time zones:** ICS start/end must be correct when imported.

## Out of scope

Auth (one organizer; players are anonymous until they register), payments, email, recurring events, editing/cancelling events, admin dashboards, visual polish, deployment.

## Documentation

`README.md` covers:
- Running locally in one or two commands (`docker compose up`, or two dev commands with seeded demo data).
- A design write-up: where capacity lives and how it's enforced (including concurrent last-seat registration); how the template system works and what a 4th game (or a non-card game) needs; what was cut or faked and what's next.
- An AI usage note: tools used, what for, and an example of AI output that was rejected or fixed.

## Priority order

End-to-end flow first: **create event → see it on the calendar → scan/click QR → register → event fills up.** Then extras. An honest cut list beats a broken feature.

## Decisions (2026-10-21)

- **Frontend:** one Vue 3 + TypeScript SPA (Vite), two route areas: `/organizer/*` (create, calendar, event detail + QR + ICS) and `/e/:id` (public event view + register).
- **Backend:** C# / .NET 10 Minimal API, EF Core, SQLite.
- **Games:** Magic: The Gathering, Pokémon TCG, Disney Lorcana.
- **Phasing:** Phase 1 = the core end-to-end flow only. Extras come after.
- **Template admin section (Phase 2):** CRUD API plus `/admin/templates` pages, designed in DESIGN.md §15. Built only once Phase 1 is done; otherwise it goes in the README as "what's next". Phase 1 already copies template values onto each event and seeds insert-only, so adding the admin section later needs no rework.
- **Duplicates:** normalized name (trimmed, collapsed whitespace, case-insensitive) unique per event → 409. Name collisions between different people are a known limitation (README).

## Things to watch for

- Don't over-build: two separate apps, auth or admin dashboards cost time without serving the core flow.
- Keep a running log of AI output we reject or fix, for the README AI note.
- Test the last-seat race; it's the strongest evidence that capacity enforcement is correct.
