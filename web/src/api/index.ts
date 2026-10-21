import { apiGet, apiPost } from './client'
import type {
  CreateEventRequest, EventDto, GameTemplate, Registration, RegistrationResult, Store,
} from './types'

export { ApiError } from './client'
export type * from './types'

let storePromise: Promise<Store> | undefined

export const api = {
  /** The store rarely changes, so it's fetched once per page load. */
  store: () => (storePromise ??= apiGet<Store>('/store').catch((e) => { storePromise = undefined; throw e })),
  templates: () => apiGet<GameTemplate[]>('/templates'),
  events: (fromIso: string, toIso: string) =>
    apiGet<EventDto[]>(`/events?from=${encodeURIComponent(fromIso)}&to=${encodeURIComponent(toIso)}`),
  event: (id: string) => apiGet<EventDto>(`/events/${encodeURIComponent(id)}`),
  createEvent: (req: CreateEventRequest) => apiPost<EventDto>('/events', req),
  registrations: (id: string) => apiGet<Registration[]>(`/events/${encodeURIComponent(id)}/registrations`),
  register: (id: string, name: string) =>
    apiPost<RegistrationResult>(`/events/${encodeURIComponent(id)}/registrations`, { name }),
  icsUrl: (id: string) => `/api/events/${encodeURIComponent(id)}/calendar.ics`,
}
