import type { ProblemDetails } from './types'

const BASE = '/api'

/** A non-2xx API response. `code` is the API's machine-readable error code (e.g. EVENT_FULL). */
export class ApiError extends Error {
  status: number
  code: string | undefined
  title: string
  detail: string | undefined
  /** Validation errors keyed by request field name (camelCase). */
  errors: Record<string, string[]>

  constructor(status: number, problem: ProblemDetails | undefined, fallbackTitle: string) {
    const title = problem?.title ?? fallbackTitle
    super(problem?.detail || title)
    this.name = 'ApiError'
    this.status = status
    this.code = problem?.code
    this.title = title
    this.detail = problem?.detail
    this.errors = problem?.errors ?? {}
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  let res: Response
  try {
    res = await fetch(BASE + path, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, { code: 'NETWORK' }, "Can't reach the server. Check your connection and try again.")
  }
  if (!res.ok) {
    let problem: ProblemDetails | undefined
    try {
      problem = (await res.json()) as ProblemDetails
    } catch {
      // non-JSON error body
    }
    throw new ApiError(res.status, problem, res.statusText || 'Request failed')
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

export const apiGet = <T>(path: string) => request<T>('GET', path)
export const apiPost = <T>(path: string, body?: unknown) => request<T>('POST', path, body)
