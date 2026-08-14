type EmptyStateProps = {
  title?: string
  message?: string
}

export function EmptyState({
  title = 'No visits found',
  message = 'There are no visits to display.',
}: EmptyStateProps) {
  return (
    <div className="state-panel">
      <h3>{title}</h3>
      <p>{message}</p>
    </div>
  )
}
