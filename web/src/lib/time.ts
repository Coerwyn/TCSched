// All times are shown on the store's clock, whatever time zone the browser is in.

/** "Fri, Jan 15, 2027, 6:00 PM" in the store's time zone. */
export function formatDateTime(utcIso: string, timeZone: string): string {
  return new Intl.DateTimeFormat(undefined, {
    timeZone, weekday: 'short', month: 'short', day: 'numeric', year: 'numeric',
    hour: 'numeric', minute: '2-digit',
  }).format(new Date(utcIso))
}

/** "6:00 PM" in the store's time zone. */
export function formatTime(utcIso: string, timeZone: string): string {
  return new Intl.DateTimeFormat(undefined, { timeZone, hour: 'numeric', minute: '2-digit' })
    .format(new Date(utcIso))
}

/**
 * The zone's name at an instant: "PDT"/"PST" (short), "Pacific Daylight Time" (long), or
 * "Pacific Time" (longGeneric, the same all year).
 */
export function zoneName(
  timeZone: string, at: Date = new Date(), style: 'short' | 'long' | 'longGeneric' = 'short',
): string {
  return new Intl.DateTimeFormat('en-US', { timeZone, timeZoneName: style })
    .formatToParts(at).find((p) => p.type === 'timeZoneName')?.value ?? timeZone
}

/**
 * "Fri, Jan 15, 2027, 6:00 PM – 10:00 PM PST". If a DST change falls inside the event, each
 * end gets its own zone: "Sat, Oct 31, 2026, 10:00 PM PDT – 1:00 AM PST".
 */
export function formatRange(startUtc: string, endUtc: string, timeZone: string): string {
  const startZone = zoneName(timeZone, new Date(startUtc))
  const endZone = zoneName(timeZone, new Date(endUtc))
  const start = formatDateTime(startUtc, timeZone)
  const end = formatTime(endUtc, timeZone)
  return startZone === endZone ? `${start} – ${end} ${endZone}` : `${start} ${startZone} – ${end} ${endZone}`
}

/**
 * The store-local wall time for a UTC instant, as "YYYY-MM-DDTHH:mm:ss" with no offset.
 * FullCalendar runs in UTC mode and is given these strings, so it draws events on the
 * store's clock without needing a time zone plugin.
 */
export function toStoreWallTime(utcIso: string, timeZone: string): string {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat('en-US', {
      timeZone, hourCycle: 'h23', year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit',
    }).formatToParts(new Date(utcIso)).map((p) => [p.type, p.value]),
  )
  return `${parts.year}-${parts.month}-${parts.day}T${parts.hour}:${parts.minute}:${parts.second}`
}

/** Today's date on the store's clock, "YYYY-MM-DD". */
export function storeToday(timeZone: string): string {
  return toStoreWallTime(new Date().toISOString(), timeZone).slice(0, 10)
}

/** The zone's UTC offset in minutes at an instant (e.g. -420 for PDT). */
function offsetMinutes(instantMs: number, timeZone: string): number {
  const wallAsUtc = Date.parse(`${toStoreWallTime(new Date(instantMs).toISOString(), timeZone)}Z`)
  return Math.round((wallAsUtc - instantMs) / 60_000)
}

/**
 * The instant for a store-local wall time "YYYY-MM-DDTHH:mm" (the same rules the server uses):
 * - normal times have exactly one match;
 * - a time that happens twice (clocks fall back) resolves to standard time, the later instant;
 * - a time that never happens (clocks spring forward) or bad input returns undefined.
 */
export function storeWallToDate(wall: string, timeZone: string): Date | undefined {
  const wallAsUtc = Date.parse(`${wall}:00Z`)
  if (Number.isNaN(wallAsUtc)) return undefined
  // The zone's offsets within a day either side cover both sides of any DST change.
  const day = 24 * 60 * 60_000
  const offsets = new Set([offsetMinutes(wallAsUtc - day, timeZone), offsetMinutes(wallAsUtc + day, timeZone)])
  const matches = [...offsets]
    .map((o) => wallAsUtc - o * 60_000)
    .filter((t) => toStoreWallTime(new Date(t).toISOString(), timeZone).slice(0, 16) === wall)
  return matches.length ? new Date(Math.max(...matches)) : undefined
}
