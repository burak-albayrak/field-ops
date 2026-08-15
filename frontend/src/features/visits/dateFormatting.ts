function padDatePart(value: number) {
  return String(value).padStart(2, '0')
}

export function formatPlannedDate(value: string) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)

  if (!match) {
    return value
  }

  const [, year, month, day] = match
  return `${day}.${month}.${year}`
}

export function formatUtcTimestamp(value: string | null) {
  if (value === null) {
    return '—'
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  const day = padDatePart(date.getUTCDate())
  const month = padDatePart(date.getUTCMonth() + 1)
  const year = date.getUTCFullYear()
  const hours = padDatePart(date.getUTCHours())
  const minutes = padDatePart(date.getUTCMinutes())

  return `${day}.${month}.${year}, ${hours}:${minutes} UTC`
}
