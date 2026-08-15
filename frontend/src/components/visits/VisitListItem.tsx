import { StatusBadge } from '../common/StatusBadge'
import { formatPlannedDate } from '../../features/visits/dateFormatting'
import type { VisitListItem as VisitListItemType } from '../../types/visit'

type VisitListItemProps = {
  visit: VisitListItemType
  isSelected: boolean
  onSelect: () => void
}

export function VisitListItem({
  visit,
  isSelected,
  onSelect,
}: VisitListItemProps) {
  const itemClassName = isSelected
    ? 'visit-list__item visit-list__item--selected'
    : 'visit-list__item'

  return (
    <li className={itemClassName}>
      <button
        type="button"
        className="visit-row"
        aria-pressed={isSelected}
        onClick={onSelect}
      >
        <span className="visit-row__field">
          <span className="visit-row__label">Visit</span>
          <strong>#{visit.id}</strong>
        </span>
        <span className="visit-row__field">
          <span className="visit-row__label">Employee</span>
          <span>{visit.employeeName}</span>
          <span className="visit-row__meta">ID {visit.employeeId}</span>
        </span>
        <span className="visit-row__field">
          <span className="visit-row__label">Store</span>
          <span>{visit.storeName}</span>
          <span className="visit-row__meta">
            ID {visit.storeId} · {visit.countryCode}
          </span>
        </span>
        <span className="visit-row__field">
          <span className="visit-row__label">Planned</span>
          <time dateTime={visit.plannedDate}>
            {formatPlannedDate(visit.plannedDate)}
          </time>
        </span>
        <span className="visit-row__field visit-row__status">
          <span className="visit-row__label">Status</span>
          <StatusBadge status={visit.status} />
        </span>
      </button>
    </li>
  )
}
