export type VisitStatus =
  | 'Planned'
  | 'InProgress'
  | 'Completed'
  | 'Cancelled'

export type EmployeeSummary = {
  id: number
  name: string
  email: string
  countryCode: string
}

export type StoreSummary = {
  id: number
  name: string
  countryCode: string
  latitude: number
  longitude: number
}

export type Visit = {
  id: number
  employee: EmployeeSummary
  store: StoreSummary
  // The API sends PlannedDate as YYYY-MM-DD. Parsing and formatting belong to the presentation layer.
  plannedDate: string
  status: VisitStatus
  startedAt: string | null
  completedAt: string | null
  startLatitude: number | null
  startLongitude: number | null
  notes: string | null
  createdAt: string
  version: number
}

export type VisitListItem = {
  id: number
  employeeId: number
  employeeName: string
  storeId: number
  storeName: string
  countryCode: string
  plannedDate: string
  status: VisitStatus
  startedAt: string | null
  completedAt: string | null
  version: number
}

export type VisitListResponse = {
  items: VisitListItem[]
  page: number
  pageSize: number
  hasNextPage: boolean
}

export type VisitListParams = {
  employeeId?: number
  storeId?: number
  status?: VisitStatus
  countryCode?: string
  startDate?: string
  endDate?: string
  page?: number
  pageSize?: number
}

export type CreateVisitRequest = {
  employeeId: number
  storeId: number
  plannedDate: string
}

export type StartVisitRequest = {
  latitude: number
  longitude: number
}

export type CompleteVisitRequest = {
  // The backend accepts an omitted notes property, an explicit null, or a string (including an empty string).
  notes?: string | null
}

export type CancelVisitRequest = {
  version: number
}
