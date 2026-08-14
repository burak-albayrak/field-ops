import { useState } from 'react'
import type { FormEvent } from 'react'
import type { VisitListParams, VisitStatus } from '../../types/visit'

export type AppliedVisitFilters = Omit<
  VisitListParams,
  'page' | 'pageSize'
>

type VisitFiltersProps = {
  onApply: (filters: AppliedVisitFilters) => void
  onClear: () => void
}

type VisitFilterDraft = {
  employeeId: string
  storeId: string
  status: '' | VisitStatus
  countryCode: string
  startDate: string
  endDate: string
}

const emptyDraft: VisitFilterDraft = {
  employeeId: '',
  storeId: '',
  status: '',
  countryCode: '',
  startDate: '',
  endDate: '',
}

const statusOptions: Array<{ label: string; value: VisitStatus }> = [
  { label: 'Planned', value: 'Planned' },
  { label: 'In Progress', value: 'InProgress' },
  { label: 'Completed', value: 'Completed' },
  { label: 'Cancelled', value: 'Cancelled' },
]

const countryOptions = ['TR', 'DE', 'UK', 'AE']

export function VisitFilters({ onApply, onClear }: VisitFiltersProps) {
  const [draft, setDraft] = useState<VisitFilterDraft>(emptyDraft)
  const [validationError, setValidationError] = useState<string | null>(null)

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const employeeId = Number(draft.employeeId)
    if (
      draft.employeeId !== '' &&
      (!Number.isInteger(employeeId) || employeeId <= 0)
    ) {
      setValidationError('Employee ID must be a positive whole number.')
      return
    }

    const storeId = Number(draft.storeId)
    if (
      draft.storeId !== '' &&
      (!Number.isInteger(storeId) || storeId <= 0)
    ) {
      setValidationError('Store ID must be a positive whole number.')
      return
    }

    if (
      draft.startDate !== '' &&
      draft.endDate !== '' &&
      draft.startDate > draft.endDate
    ) {
      setValidationError('Start date cannot be after end date.')
      return
    }

    const filters: AppliedVisitFilters = {}

    if (draft.employeeId !== '') {
      filters.employeeId = employeeId
    }
    if (draft.storeId !== '') {
      filters.storeId = storeId
    }
    if (draft.status !== '') {
      filters.status = draft.status
    }
    if (draft.countryCode !== '') {
      filters.countryCode = draft.countryCode
    }
    if (draft.startDate !== '') {
      filters.startDate = draft.startDate
    }
    if (draft.endDate !== '') {
      filters.endDate = draft.endDate
    }

    setValidationError(null)
    onApply(filters)
  }

  function handleClear() {
    setDraft(emptyDraft)
    setValidationError(null)
    onClear()
  }

  return (
    <form className="visit-filters" onSubmit={handleSubmit} noValidate>
      <div className="visit-filters__grid">
        <label className="filter-field" htmlFor="employee-id">
          <span>Employee ID</span>
          <input
            id="employee-id"
            type="number"
            min="1"
            step="1"
            value={draft.employeeId}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                employeeId: event.target.value,
              }))
            }
          />
        </label>

        <label className="filter-field" htmlFor="store-id">
          <span>Store ID</span>
          <input
            id="store-id"
            type="number"
            min="1"
            step="1"
            value={draft.storeId}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                storeId: event.target.value,
              }))
            }
          />
        </label>

        <label className="filter-field" htmlFor="visit-status">
          <span>Status</span>
          <select
            id="visit-status"
            value={draft.status}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                status: event.target.value as '' | VisitStatus,
              }))
            }
          >
            <option value="">All statuses</option>
            {statusOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>

        <label className="filter-field" htmlFor="country-code">
          <span>Country</span>
          <select
            id="country-code"
            value={draft.countryCode}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                countryCode: event.target.value,
              }))
            }
          >
            <option value="">All countries</option>
            {countryOptions.map((countryCode) => (
              <option key={countryCode} value={countryCode}>
                {countryCode}
              </option>
            ))}
          </select>
        </label>

        <label className="filter-field" htmlFor="start-date">
          <span>Start Date</span>
          <input
            id="start-date"
            type="date"
            value={draft.startDate}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                startDate: event.target.value,
              }))
            }
          />
        </label>

        <label className="filter-field" htmlFor="end-date">
          <span>End Date</span>
          <input
            id="end-date"
            type="date"
            value={draft.endDate}
            onChange={(event) =>
              setDraft((current) => ({
                ...current,
                endDate: event.target.value,
              }))
            }
          />
        </label>
      </div>

      <div className="visit-filters__footer">
        {validationError ? (
          <p className="filter-error" role="alert">
            {validationError}
          </p>
        ) : null}
        <div className="visit-filters__actions">
          <button
            type="button"
            className="button-secondary"
            onClick={handleClear}
          >
            Clear
          </button>
          <button type="submit" className="button-primary">
            Apply filters
          </button>
        </div>
      </div>
    </form>
  )
}
