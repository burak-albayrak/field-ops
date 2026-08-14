type LoadingStateProps = {
  message?: string
}

export function LoadingState({
  message = 'Loading visits...',
}: LoadingStateProps) {
  return (
    <div className="state-panel" role="status" aria-live="polite">
      <span className="loading-indicator" aria-hidden="true" />
      <p>{message}</p>
    </div>
  )
}
