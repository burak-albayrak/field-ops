import { useState } from 'react'
import { useVisits } from '../../features/visits/queries'
import type { VisitListParams } from '../../types/visit'
import { PaginationControls } from './PaginationControls'
import { VisitDetailPanel } from './VisitDetailPanel'
import { VisitFilters } from './VisitFilters'
import type { AppliedVisitFilters } from './VisitFilters'
import { VisitList } from './VisitList'

const pageSize = 20

export function VisitListSection() {
  const [appliedFilters, setAppliedFilters] =
    useState<AppliedVisitFilters>({})
  const [page, setPage] = useState(1)
  const [selectedVisitId, setSelectedVisitId] = useState<number | null>(null)

  const queryParams: VisitListParams = {
    ...appliedFilters,
    page,
    pageSize,
  }
  const hasAppliedFilters = Object.keys(appliedFilters).length > 0
  const visitsQuery = useVisits(queryParams)

  function handleApplyFilters(filters: AppliedVisitFilters) {
    setAppliedFilters(filters)
    setPage(1)
    setSelectedVisitId(null)
  }

  function handleClearFilters() {
    setAppliedFilters({})
    setPage(1)
    setSelectedVisitId(null)
  }

  function handlePreviousPage() {
    setSelectedVisitId(null)
    setPage((currentPage) => Math.max(1, currentPage - 1))
  }

  function handleNextPage() {
    setSelectedVisitId(null)
    setPage((currentPage) => currentPage + 1)
  }

  const workspaceClassName = selectedVisitId
    ? 'visit-workspace visit-workspace--detail'
    : 'visit-workspace visit-workspace--list'

  return (
    <section className="visit-section" aria-labelledby="visits-heading">
      <div className="visit-section__heading">
        <div>
          <p className="visit-section__eyebrow">Workspace</p>
          <h2 id="visits-heading">Visits</h2>
        </div>
        <p>Current field activity across employees and stores.</p>
      </div>

      <div className={workspaceClassName}>
        <div className="visit-workspace__list">
          <VisitFilters
            onApply={handleApplyFilters}
            onClear={handleClearFilters}
          />

          <VisitList
            data={visitsQuery.data}
            isError={visitsQuery.isError}
            isFetching={visitsQuery.isFetching}
            isPending={visitsQuery.isPending}
            hasAppliedFilters={hasAppliedFilters}
            selectedVisitId={selectedVisitId}
            onRetry={() => void visitsQuery.refetch()}
            onVisitSelect={setSelectedVisitId}
          />

          <PaginationControls
            page={page}
            hasNextPage={visitsQuery.data?.hasNextPage ?? false}
            isFetching={visitsQuery.isFetching}
            onPrevious={handlePreviousPage}
            onNext={handleNextPage}
          />
        </div>

        <aside className="visit-workspace__detail" aria-label="Visit details">
          <VisitDetailPanel
            visitId={selectedVisitId}
            onBack={() => setSelectedVisitId(null)}
          />
        </aside>
      </div>
    </section>
  )
}
