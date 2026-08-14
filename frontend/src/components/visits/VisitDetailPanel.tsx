import { ApiError } from '../../api/client'
import { useVisit } from '../../features/visits/queries'
import { ErrorState } from '../common/ErrorState'
import { LoadingState } from '../common/LoadingState'
import { StatusBadge } from '../common/StatusBadge'
import { CompleteVisitForm } from './CompleteVisitForm'
import { StartVisitForm } from './StartVisitForm'

type VisitDetailPanelProps = {
  visitId: number | null
  onBack: () => void
}

const utcDateTimeFormatter = new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'UTC',
})

function formatTimestamp(value: string | null) {
  if (value === null) {
    return '—'
  }

  return `${utcDateTimeFormatter.format(new Date(value))} UTC`
}

function isVisitNotFound(error: Error | null) {
  return (
    error instanceof ApiError && error.problem?.code === 'visit_not_found'
  )
}

function getDetailErrorMessage(error: Error | null) {
  if (isVisitNotFound(error)) {
    return 'This visit could not be found.'
  }

  return 'Unable to load visit. Please try again.'
}

export function VisitDetailPanel({
  visitId,
  onBack,
}: VisitDetailPanelProps) {
  const visitQuery = useVisit(visitId)

  if (visitId === null) {
    return (
      <div className="detail-panel detail-panel--empty">
        <h3>Select a visit</h3>
        <p>Choose a visit from the list to see its details.</p>
      </div>
    )
  }

  if (visitQuery.isPending) {
    return (
      <div className="detail-panel">
        <button type="button" className="detail-back" onClick={onBack}>
          ← Back to visits
        </button>
        <LoadingState message="Loading visit..." />
      </div>
    )
  }

  if (
    visitQuery.isError &&
    (!visitQuery.data || isVisitNotFound(visitQuery.error))
  ) {
    return (
      <div className="detail-panel">
        <button type="button" className="detail-back" onClick={onBack}>
          ← Back to visits
        </button>
        <ErrorState
          title="Unable to load visit"
          message={getDetailErrorMessage(visitQuery.error)}
          isRetrying={visitQuery.isFetching}
          onRetry={() => void visitQuery.refetch()}
        />
      </div>
    )
  }

  const visit = visitQuery.data
  const startLocation =
    visit.startLatitude !== null && visit.startLongitude !== null
      ? `${visit.startLatitude}, ${visit.startLongitude}`
      : '—'
  const notes = visit.notes?.trim() ? visit.notes : '—'

  return (
    <div className="detail-panel">
      <button type="button" className="detail-back" onClick={onBack}>
        ← Back to visits
      </button>

      <div className="detail-panel__heading">
        <div>
          <p className="detail-panel__eyebrow">Visit</p>
          <h3>Visit #{visit.id}</h3>
        </div>
        <StatusBadge status={visit.status} />
      </div>

      {visit.status === 'Planned' ? (
        <StartVisitForm key={visit.id} visitId={visit.id} />
      ) : null}

      {visit.status === 'InProgress' ? (
        <CompleteVisitForm key={visit.id} visitId={visit.id} />
      ) : null}

      <dl className="detail-list">
        <div>
          <dt>Employee</dt>
          <dd>
            {visit.employee.name}
            <span>{visit.employee.email}</span>
          </dd>
        </div>
        <div>
          <dt>Store</dt>
          <dd>{visit.store.name}</dd>
        </div>
        <div>
          <dt>Country</dt>
          <dd>{visit.store.countryCode}</dd>
        </div>
        <div>
          <dt>Planned Date</dt>
          <dd>
            <time dateTime={visit.plannedDate}>{visit.plannedDate}</time>
          </dd>
        </div>
        <div>
          <dt>Created At</dt>
          <dd>
            <time dateTime={visit.createdAt}>
              {formatTimestamp(visit.createdAt)}
            </time>
          </dd>
        </div>
        <div>
          <dt>Started At</dt>
          <dd>{formatTimestamp(visit.startedAt)}</dd>
        </div>
        <div>
          <dt>Completed At</dt>
          <dd>{formatTimestamp(visit.completedAt)}</dd>
        </div>
        <div>
          <dt>Start Location</dt>
          <dd>{startLocation}</dd>
        </div>
        <div className="detail-list__notes">
          <dt>Notes</dt>
          <dd>{notes}</dd>
        </div>
      </dl>
    </div>
  )
}
