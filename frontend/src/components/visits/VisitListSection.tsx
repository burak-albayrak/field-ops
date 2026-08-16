import { useState } from 'react'
import { useVisits } from '../../features/visits/queries'
import type { Visit, VisitListParams } from '../../types/visit'
import { CreateVisitForm } from './CreateVisitForm'
import { PaginationControls } from './PaginationControls'
import { VisitDetailPanel } from './VisitDetailPanel'
import { VisitFilters } from './VisitFilters'
import type { AppliedVisitFilters } from './VisitFilters'
import { VisitList } from './VisitList'

const pageSize = 10

export function VisitListSection() {
  const [appliedFilters, setAppliedFilters] =
    useState<AppliedVisitFilters>({})
  const [page, setPage] = useState(1)
  const [selectedVisitId, setSelectedVisitId] = useState<number | null>(null)
  const [isCreateOpen, setIsCreateOpen] = useState(false)
  const [isCreateRendered, setIsCreateRendered] = useState(false)
  const [isCreatePending, setIsCreatePending] = useState(false)
  const [filterResetKey, setFilterResetKey] = useState(0)

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

  function handleCreateToggle() {
    if (isCreateOpen) {
      setIsCreateOpen(false)
      return
    }

    setSelectedVisitId(null)
    setIsCreateRendered(true)
    setIsCreateOpen(true)
  }

  function handleVisitSelect(visitId: number) {
    if (isCreatePending) {
      return
    }

    setIsCreateOpen(false)
    setSelectedVisitId(visitId)
  }

  function handleVisitCreated(visit: Visit) {
    setAppliedFilters({})
    setPage(1)
    setFilterResetKey((currentKey) => currentKey + 1)
    setIsCreateOpen(false)
    setSelectedVisitId(visit.id)
  }

  const workspaceClassName = selectedVisitId
    ? 'visit-workspace visit-workspace--detail'
    : 'visit-workspace visit-workspace--list'

  return (
    <section className="visit-section" aria-labelledby="visits-heading">
      <div className="visit-section__heading">
        <div>
          <h2 id="visits-heading">Visits</h2>
        </div>
        <button
          type="button"
          className="button-primary visit-section__create-button"
          aria-expanded={isCreateOpen}
          aria-controls="create-visit-form"
          disabled={isCreatePending}
          onClick={handleCreateToggle}
        >
          {isCreateOpen ? 'Close Create Form' : 'Create Visit'}
        </button>
      </div>

      {isCreateRendered ? (
        <div
          className={`create-visit-transition create-visit-transition--${isCreateOpen ? 'enter' : 'exit'}`}
          onAnimationEnd={() => {
            if (!isCreateOpen) {
              setIsCreateRendered(false)
            }
          }}
        >
          <div className="create-visit-transition__content">
            <CreateVisitForm
              onCancel={() => setIsCreateOpen(false)}
              onCreated={handleVisitCreated}
              onPendingChange={setIsCreatePending}
            />
          </div>
        </div>
      ) : null}

      <div className={workspaceClassName}>
        <VisitFilters
          key={filterResetKey}
          onApply={handleApplyFilters}
          onClear={handleClearFilters}
        />

        <div className="visit-workspace__list">
          <VisitList
            data={visitsQuery.data}
            isError={visitsQuery.isError}
            isFetching={visitsQuery.isFetching}
            isPending={visitsQuery.isPending}
            hasAppliedFilters={hasAppliedFilters}
            selectedVisitId={selectedVisitId}
            onRetry={() => void visitsQuery.refetch()}
            onVisitSelect={handleVisitSelect}
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
