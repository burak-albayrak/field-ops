import { useState } from 'react'
import { ApiError } from '../../api/client'
import { useCancelVisit } from '../../features/visits/mutations'

type CancelVisitActionProps = {
  visitId: number
  version: number
}

function getCancelErrorMessage(error: Error | null) {
  if (error instanceof ApiError) {
    if (error.problem?.code === 'concurrency_conflict') {
      return 'The visit changed while you were cancelling it. Review the latest details and try again.'
    }

    if (error.problem?.code === 'invalid_visit_status') {
      return 'This visit can no longer be cancelled.'
    }

    if (error.problem?.code === 'visit_not_found') {
      return 'This visit could not be found.'
    }
  }

  return 'Unable to cancel visit. Please try again.'
}

export function CancelVisitAction({
  visitId,
  version,
}: CancelVisitActionProps) {
  const [isConfirming, setIsConfirming] = useState(false)
  const cancelMutation = useCancelVisit(visitId)

  if (!isConfirming) {
    return (
      <div className="visit-action">
        <button
          type="button"
          className="button-danger visit-action__open"
          onClick={() => setIsConfirming(true)}
        >
          Cancel Visit
        </button>
      </div>
    )
  }

  const errorMessage = cancelMutation.isError
    ? getCancelErrorMessage(cancelMutation.error)
    : null

  function handleKeepVisit() {
    cancelMutation.reset()
    setIsConfirming(false)
  }

  return (
    <div className="cancel-visit-action">
      <div className="cancel-visit-action__confirmation">
        <h4>Cancel this visit?</h4>
        <p>This action changes the visit to a terminal Cancelled status.</p>

        {errorMessage ? (
          <p className="cancel-visit-action__error" role="alert">
            {errorMessage}
          </p>
        ) : null}

        <div className="cancel-visit-action__actions">
          <button
            type="button"
            className="button-secondary"
            disabled={cancelMutation.isPending}
            onClick={handleKeepVisit}
          >
            Keep Visit
          </button>
          <button
            type="button"
            className="button-danger"
            disabled={cancelMutation.isPending}
            onClick={() => cancelMutation.mutate({ version })}
          >
            {cancelMutation.isPending ? 'Cancelling...' : 'Confirm Cancel'}
          </button>
        </div>
      </div>
    </div>
  )
}
