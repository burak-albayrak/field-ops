import type {
  CompleteVisitRequest,
  StartVisitRequest,
  Visit,
  VisitListParams,
  VisitListResponse,
} from '../types/visit'
import { apiRequest } from './client'

export function getVisits(params: VisitListParams) {
  const searchParams = new URLSearchParams()

  if (params.employeeId !== undefined) {
    searchParams.set('employeeId', String(params.employeeId))
  }
  if (params.storeId !== undefined) {
    searchParams.set('storeId', String(params.storeId))
  }
  if (params.status !== undefined) {
    searchParams.set('status', params.status)
  }
  if (params.countryCode !== undefined) {
    searchParams.set('countryCode', params.countryCode)
  }
  if (params.startDate !== undefined) {
    searchParams.set('startDate', params.startDate)
  }
  if (params.endDate !== undefined) {
    searchParams.set('endDate', params.endDate)
  }
  if (params.page !== undefined) {
    searchParams.set('page', String(params.page))
  }
  if (params.pageSize !== undefined) {
    searchParams.set('pageSize', String(params.pageSize))
  }

  const query = searchParams.toString()
  const path = query ? `/api/visits?${query}` : '/api/visits'

  return apiRequest<VisitListResponse>(path)
}

export function getVisit(id: number) {
  return apiRequest<Visit>(`/api/visits/${id}`)
}

export function startVisit(id: number, request: StartVisitRequest) {
  return apiRequest<Visit>(`/api/visits/${id}/start`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })
}

export function completeVisit(id: number, request: CompleteVisitRequest) {
  return apiRequest<Visit>(`/api/visits/${id}/complete`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })
}
