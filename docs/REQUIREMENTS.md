# Requirements Breakdown — TCG Event Calendar

**Timebox: 3 hours.** Judgment about where to spend time matters more than polish.

## Functional requirements

| ID | Requirement | Must-haves / acceptance |
|----|-------------|-------------------------|
| R1 | **Event creation** | Organizer creates event with: name, game type, date + start time, player capacity. Capacity up to **30**. |
| R2 | **Game templates** | ≥3 TCGs, one must be **Magic: The Gathering**. Data-driven templates (not hard-coded strings / if-else on names). Each template drives **≥2 event properties** (e.g. formats, default duration, default/max capacity, min players). Adding a 4th game must not touch core event logic. |
| R3 | **Calendar view** | Organizer sees events on a given day. Month grid **or** grouped-by-day agenda. Library OK. |
| R4 | **ICS invite** | Event page offers downloadable `.ics` with correct title, start/end, location. Importable into Google Calendar / Outlook. **Use a library.** |
| R5 | **Registration + QR** | Each event has a registration link; event page shows a QR code of it (**library**). Scan → simple form (name is enough). **Capacity enforced server-side**; full event → clear rejection message. |

## Derived requirements

- **Concurrency:** last-seat race must be safe (atomic check-and-insert, not read-then-write).
- **Duplicate registrations:** must have a defined, sensible behavior.
- **Capacity is a first-class constraint** in the data model (Event / Template / Registration entities).
- **End time** must be derived (template default duration) so the ICS has a correct end.
- **Location** is needed for the ICS — needs a source (store config or event field).
- **Time zones:** ICS start/end must be correct when imported.

## Explicitly out of scope

Auth, payments, email, recurring events, editing/cancelling events, admin dashboards. Visual polish and deployment are out of scope.

## Deliverables

1. Public git repo (GitHub etc.) — commit incrementally.
2. `README.md` with:
   - Run locally in 1–2 commands (`docker compose up` or seed script is a plus).
   - **Design write-up (~1 page):**
     - How capacity is determined/enforced, where it lives, what happens on concurrent last-seat registration.
     - How the template system works; what a 4th game (or non-card game) requires.
     - What was cut/faked and what's next.
   - **AI usage note:** tools used, for what, plus one example of AI output rejected/fixed.

## Priority order

End-to-end flow first: **create event → see on calendar → scan/click QR → register → event fills up.** Then polish. Ship an honest cut list over a broken feature.

## Decisions (2026-10-21)

- **Frontend:** one Vue 3 + TypeScript SPA (Vite), two route areas: `/organizer/*` (create, calendar, event detail + QR + ICS) and `/e/:id` (public event view + register).
- **Backend:** C# / .NET 10 Minimal API, EF Core, SQLite.
- **Games:** Magic: The Gathering, Pokémon TCG, Disney Lorcana.
- **Phasing:** Phase 1 = the core end-to-end flow only. Extras come after.
- **Template admin section (Phase 2):** CRUD API plus `/admin/templates` pages, designed in DESIGN.md §15. Built only once Phase 1 is done; otherwise it goes in the README as "what's next" (draft text in DESIGN.md §16). Phase 1 already copies template values onto each event and seeds insert-only, so adding the admin section later needs no rework.
- **Duplicates:** normalized name (trimmed, collapsed whitespace, case-insensitive) unique per event → 409. Name collisions between different people are a known limitation (README).

## Things to watch for

- Don't over-build: two separate apps, auth, or admin dashboards cost time without serving the core flow.
- Keep a running log of AI output we reject/fix — needed for the README AI note.
- Write a test for the last-seat race; it's the strongest evidence that capacity enforcement is correct.
