import type { VisitListResponse } from '../../types/visit'
import { EmptyState } from '../common/EmptyState'
import { ErrorState } from '../common/ErrorState'
import { LoadingState } from '../common/LoadingState'
import { VisitListItem } from './VisitListItem'

type VisitListProps = {
  data: VisitListResponse | undefined
  isError: boolean
  isFetching: boolean
  isPending: boolean
  hasAppliedFilters: boolean
  selectedVisitId: number | null
  onRetry: () => void
  onVisitSelect: (visitId: number) => void
}

export function VisitList({
  data,
  isError,
  isFetching,
  isPending,
  hasAppliedFilters,
  selectedVisitId,
  onRetry,
  onVisitSelect,
}: VisitListProps) {
  if (isPending) {
    return <LoadingState />
  }

  if (isError && !data) {
    return (
      <ErrorState
        message="Unable to load visits. Please try again."
        isRetrying={isFetching}
        onRetry={onRetry}
      />
    )
  }

  if (!data || data.items.length === 0) {
    return hasAppliedFilters ? (
      <EmptyState
        title="No visits match the current filters"
        message="Try changing or clearing the filters."
      />
    ) : (
      <EmptyState />
    )
  }

  return (
    <div className="visit-list">
      <div className="visit-list__header" aria-hidden="true">
        <span>Visit</span>
        <span>Employee</span>
        <span>Store</span>
        <span>Planned</span>
        <span>Status</span>
      </div>
      <ul className="visit-list__items">
        {data.items.map((visit) => (
          <VisitListItem
            key={visit.id}
            visit={visit}
            isSelected={visit.id === selectedVisitId}
            onSelect={() => onVisitSelect(visit.id)}
          />
        ))}
      </ul>
    </div>
  )
}
