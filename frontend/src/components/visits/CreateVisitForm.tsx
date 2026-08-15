import { useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { useCreateVisit } from '../../features/visits/mutations'
import type { CreateVisitRequest, Visit } from '../../types/visit'

type CreateVisitFormProps = {
  onCancel: () => void
  onCreated: (visit: Visit) => void
  onPendingChange: (isPending: boolean) => void
}

function parsePositiveInteger(value: string, label: string) {
  const number = Number(value)

  if (!value.trim() || !Number.isInteger(number) || number <= 0) {
    return `${label} must be a positive whole number.`
  }

  return number
}

function getCreateErrorMessage(error: Error | null) {
  if (error instanceof ApiError) {
    switch (error.problem?.code) {
      case 'employee_not_found':
        return 'The employee could not be found. Check the Employee ID.'
      case 'store_not_found':
        return 'The store could not be found. Check the Store ID.'
      case 'duplicate_visit':
        return 'An active visit already exists for this employee, store, and planned date.'
      case 'validation_error':
        return 'The visit details are invalid. Review the fields and try again.'
    }
  }

  return 'Unable to create visit. Please try again.'
}

export function CreateVisitForm({
  onCancel,
  onCreated,
  onPendingChange,
}: CreateVisitFormProps) {
  const [employeeIdDraft, setEmployeeIdDraft] = useState('')
  const [storeIdDraft, setStoreIdDraft] = useState('')
  const [plannedDate, setPlannedDate] = useState('')
  const [validationError, setValidationError] = useState<string | null>(null)
  const createMutation = useCreateVisit()

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const employeeId = parsePositiveInteger(employeeIdDraft, 'Employee ID')
    if (typeof employeeId === 'string') {
      setValidationError(employeeId)
      return
    }

    const storeId = parsePositiveInteger(storeIdDraft, 'Store ID')
    if (typeof storeId === 'string') {
      setValidationError(storeId)
      return
    }

    if (!plannedDate) {
      setValidationError('Planned Date is required.')
      return
    }

    setValidationError(null)
    const request: CreateVisitRequest = {
      employeeId,
      storeId,
      plannedDate,
    }

    onPendingChange(true)
    createMutation.mutate(request, {
      onSuccess: (visit) => {
        setEmployeeIdDraft('')
        setStoreIdDraft('')
        setPlannedDate('')
        onCreated(visit)
      },
      onSettled: () => onPendingChange(false),
    })
  }

  function handleCancel() {
    setEmployeeIdDraft('')
    setStoreIdDraft('')
    setPlannedDate('')
    setValidationError(null)
    createMutation.reset()
    onCancel()
  }

  const errorMessage = validationError ??
    (createMutation.isError
      ? getCreateErrorMessage(createMutation.error)
      : null)

  return (
    <form
      id="create-visit-form"
      className="create-visit-form"
      aria-labelledby="create-visit-heading"
      onSubmit={handleSubmit}
      noValidate
    >
      <div className="create-visit-form__heading">
        <h3 id="create-visit-heading">Create Visit</h3>
        <p>Enter the existing employee and store IDs for the planned visit.</p>
      </div>

      <div className="create-visit-form__fields">
        <label className="create-visit-form__field">
          Employee ID
          <input
            type="number"
            min="1"
            step="1"
            inputMode="numeric"
            value={employeeIdDraft}
            disabled={createMutation.isPending}
            onChange={(event) => setEmployeeIdDraft(event.target.value)}
          />
        </label>

        <label className="create-visit-form__field">
          Store ID
          <input
            type="number"
            min="1"
            step="1"
            inputMode="numeric"
            value={storeIdDraft}
            disabled={createMutation.isPending}
            onChange={(event) => setStoreIdDraft(event.target.value)}
          />
        </label>

        <label className="create-visit-form__field">
          Planned Date
          <input
            type="date"
            value={plannedDate}
            disabled={createMutation.isPending}
            onChange={(event) => setPlannedDate(event.target.value)}
          />
        </label>
      </div>

      {errorMessage ? (
        <p className="create-visit-form__error" role="alert">
          {errorMessage}
        </p>
      ) : null}

      <div className="create-visit-form__actions">
        <button
          type="button"
          className="button-secondary"
          disabled={createMutation.isPending}
          onClick={handleCancel}
        >
          Cancel
        </button>
        <button
          type="submit"
          className="button-primary"
          disabled={createMutation.isPending}
        >
          {createMutation.isPending ? 'Creating...' : 'Create Visit'}
        </button>
      </div>
    </form>
  )
}
