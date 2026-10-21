// DTO types mirroring the API (src/TCSched.Api/Endpoints). Dates are ISO strings in UTC.

/** RFC 7807 problem details, plus the API's `code` extension and validation `errors`. */
export interface ProblemDetails {
  type?: string
  title?: string
  detail?: string
  status?: number
  code?: string
  errors?: Record<string, string[]>
}

export interface Store {
  name: string
  address: string
  /** IANA time zone, e.g. "America/Chicago". All times are shown in this zone. */
  timeZone: string
  /** Game pre-selected on the create form; falls back to the first template if unset/unknown. */
  defaultGameTemplateId?: string | null
}

export interface GameTemplate {
  id: string
  name: string
  formats: string[]
  defaultFormat: string
  defaultDurationMinutes: number
  defaultCapacity: number
  maxCapacity: number
  minPlayers: number
}

export interface EventDto {
  id: string
  name: string
  gameTemplateId: string
  gameName: string
  format: string
  startsAtUtc: string
  endsAtUtc: string
  durationMinutes: number
  capacity: number
  registeredCount: number
  seatsLeft: number
  minPlayers: number
  isFull: boolean
}

export interface CreateEventRequest {
  name: string
  gameTemplateId: string
  format?: string
  /** Store-local wall time, "YYYY-MM-DDTHH:mm" (no offset). */
  startsAtLocal: string
  capacity?: number
}

export interface RegistrationResult {
  registrationId: string
  eventId: string
  displayName: string
  registeredCount: number
  capacity: number
  seatsLeft: number
}

export interface Registration {
  id: string
  displayName: string
  registeredAtUtc: string
}
