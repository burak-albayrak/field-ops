import type { ApiProblem } from '../types/api'

export class ApiError extends Error {
  readonly status: number
  readonly problem?: ApiProblem

  constructor(status: number, problem?: ApiProblem) {
    super(
      problem?.detail ??
        problem?.title ??
        `The request failed with HTTP status ${status}.`,
    )
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

async function readApiProblem(response: Response) {
  const contentType = response.headers.get('content-type')

  if (!contentType?.includes('json')) {
    return undefined
  }

  try {
    return (await response.json()) as ApiProblem
  } catch {
    return undefined
  }
}

export async function apiRequest<T>(
  path: string,
  options: RequestInit = {},
): Promise<T> {
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json')

  const response = await fetch(path, {
    ...options,
    headers,
  })

  if (!response.ok) {
    throw new ApiError(response.status, await readApiProblem(response))
  }

  return (await response.json()) as T
}
