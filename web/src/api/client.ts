import type { ProblemDetails } from './types'

const BASE = '/api'

export class ApiError extends Error {
  status: number
  code: string | undefined
  title: string
  detail: string | undefined

  constructor(status: number, code: string | undefined, title: string, detail: string | undefined) {
    super(detail || title)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.title = title
    this.detail = detail
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const res = await fetch(BASE + path, {
    method,
    headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!res.ok) {
    let problem: ProblemDetails | undefined
    try {
      problem = (await res.json()) as ProblemDetails
    } catch {
      // non-JSON error body
    }
    throw new ApiError(
      problem?.status ?? res.status,
      problem?.code,
      problem?.title ?? res.statusText ?? 'Request failed',
      problem?.detail,
    )
  }
  if (res.status === 204) return undefined as T
  return (await res.json()) as T
}

export const apiGet = <T>(path: string) => request<T>('GET', path)
export const apiPost = <T>(path: string, body?: unknown) => request<T>('POST', path, body)
