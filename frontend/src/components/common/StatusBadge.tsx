import type { VisitStatus } from '../../types/visit'

type StatusBadgeProps = {
  status: VisitStatus
}

const statusLabels: Record<VisitStatus, string> = {
  Planned: 'Planned',
  InProgress: 'In Progress',
  Completed: 'Completed',
  Cancelled: 'Cancelled',
}

export function StatusBadge({ status }: StatusBadgeProps) {
  return (
    <span className={`status-badge status-badge--${status.toLowerCase()}`}>
      {statusLabels[status]}
    </span>
  )
}
