// DTO types mirroring the API — filled in as endpoints land.

/** RFC 7807 problem details, plus the API's `code` extension. */
export interface ProblemDetails {
  type?: string
  title?: string
  detail?: string
  status?: number
  code?: string
}
