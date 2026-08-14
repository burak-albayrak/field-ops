type ErrorStateProps = {
  title?: string
  message: string
  isRetrying: boolean
  onRetry: () => void
}

export function ErrorState({
  title = 'Unable to load visits',
  message,
  isRetrying,
  onRetry,
}: ErrorStateProps) {
  return (
    <div className="state-panel" role="alert">
      <h3>{title}</h3>
      <p>{message}</p>
      <button type="button" onClick={onRetry} disabled={isRetrying}>
        {isRetrying ? 'Retrying...' : 'Retry'}
      </button>
    </div>
  )
}
