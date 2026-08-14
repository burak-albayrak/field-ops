import { useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { useCompleteVisit } from '../../features/visits/mutations'
import type { CompleteVisitRequest } from '../../types/visit'

type CompleteVisitFormProps = {
  visitId: number
}

function getCompleteErrorMessage(error: Error | null) {
  if (error instanceof ApiError) {
    if (error.problem?.code === 'invalid_visit_status') {
      return 'This visit can no longer be completed.'
    }

    if (error.problem?.code === 'concurrency_conflict') {
      return 'The visit changed while you were completing it. Refresh and try again.'
    }
  }

  return 'Unable to complete visit. Please try again.'
}

export function CompleteVisitForm({ visitId }: CompleteVisitFormProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [notes, setNotes] = useState('')
  const completeMutation = useCompleteVisit(visitId)

  if (!isOpen) {
    return (
      <div className="visit-action">
        <button
          type="button"
          className="button-primary visit-action__open"
          onClick={() => setIsOpen(true)}
        >
          Complete Visit
        </button>
      </div>
    )
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const trimmedNotes = notes.trim()
    const request: CompleteVisitRequest = trimmedNotes
      ? { notes: trimmedNotes }
      : {}

    completeMutation.mutate(request)
  }

  function handleCancel() {
    setNotes('')
    completeMutation.reset()
    setIsOpen(false)
  }

  const errorMessage = completeMutation.isError
    ? getCompleteErrorMessage(completeMutation.error)
    : null

  return (
    <form className="complete-visit-form" onSubmit={handleSubmit}>
      <div className="complete-visit-form__heading">
        <h4>Complete Visit</h4>
        <p>Add an optional operational note before completing this visit.</p>
      </div>

      <label className="complete-visit-form__field">
        Notes (optional)
        <textarea
          rows={4}
          value={notes}
          disabled={completeMutation.isPending}
          onChange={(event) => setNotes(event.target.value)}
        />
      </label>

      {errorMessage ? (
        <p className="complete-visit-form__error" role="alert">
          {errorMessage}
        </p>
      ) : null}

      <div className="complete-visit-form__actions">
        <button
          type="button"
          className="button-secondary"
          disabled={completeMutation.isPending}
          onClick={handleCancel}
        >
          Cancel
        </button>
        <button
          type="submit"
          className="button-primary"
          disabled={completeMutation.isPending}
        >
          {completeMutation.isPending ? 'Completing...' : 'Complete Visit'}
        </button>
      </div>
    </form>
  )
}
