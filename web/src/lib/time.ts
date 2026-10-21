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

/** The zone's name at an instant: "CDT" (short) or "Central Daylight Time" (long). */
export function zoneName(timeZone: string, at: Date = new Date(), style: 'short' | 'long' = 'short'): string {
  return new Intl.DateTimeFormat('en-US', { timeZone, timeZoneName: style })
    .formatToParts(at).find((p) => p.type === 'timeZoneName')?.value ?? timeZone
}

/** "Fri, Jan 15, 2027, 6:00 PM – 10:00 PM CST" */
export function formatRange(startUtc: string, endUtc: string, timeZone: string): string {
  const zone = new Intl.DateTimeFormat(undefined, { timeZone, timeZoneName: 'short' })
    .formatToParts(new Date(startUtc)).find((p) => p.type === 'timeZoneName')?.value ?? ''
  return `${formatDateTime(startUtc, timeZone)} – ${formatTime(endUtc, timeZone)} ${zone}`.trim()
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

/** Adds minutes to a "YYYY-MM-DDTHH:mm" wall time and formats the result as "6:00 PM" (no zone math). */
export function wallTimePlus(wall: string, minutes: number): string {
  const d = new Date(`${wall}:00Z`)
  d.setUTCMinutes(d.getUTCMinutes() + minutes)
  return new Intl.DateTimeFormat(undefined, { timeZone: 'UTC', hour: 'numeric', minute: '2-digit' }).format(d)
}
