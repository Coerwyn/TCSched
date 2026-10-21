import { ApiError } from '../api'

/** Player-facing message for an API error. Unknown codes fall back to the server's text. */
export function errorMessage(e: unknown): string {
  if (!(e instanceof ApiError)) return 'Something went wrong. Please try again.'
  switch (e.code) {
    case 'EVENT_FULL': return 'Sorry, this event is full.'
    case 'EVENT_STARTED': return 'Registration is closed: this event has already started.'
    case 'NOT_FOUND': return "We couldn't find that event. Check the link or QR code."
    case 'DUPLICATE_REGISTRATION':
    default: return e.message
  }
}

/** First validation message for a field, if any. */
export function fieldError(e: unknown, field: string): string | undefined {
  return e instanceof ApiError ? e.errors[field]?.[0] : undefined
}
