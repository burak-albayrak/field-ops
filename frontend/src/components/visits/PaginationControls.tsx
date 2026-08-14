type PaginationControlsProps = {
  page: number
  hasNextPage: boolean
  isFetching: boolean
  onPrevious: () => void
  onNext: () => void
}

export function PaginationControls({
  page,
  hasNextPage,
  isFetching,
  onPrevious,
  onNext,
}: PaginationControlsProps) {
  return (
    <nav className="pagination" aria-label="Visit list pagination">
      <button
        type="button"
        className="button-secondary"
        onClick={onPrevious}
        disabled={page <= 1 || isFetching}
      >
        Previous
      </button>
      <span className="pagination__page" aria-live="polite">
        Page {page}
      </span>
      <button
        type="button"
        className="button-secondary"
        onClick={onNext}
        disabled={!hasNextPage || isFetching}
      >
        Next
      </button>
    </nav>
  )
}
