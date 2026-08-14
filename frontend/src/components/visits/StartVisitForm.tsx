import { useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { useStartVisit } from '../../features/visits/mutations'

type StartVisitFormProps = {
  visitId: number
}

type Coordinates = {
  latitude: number
  longitude: number
}

function parseCoordinates(
  latitudeDraft: string,
  longitudeDraft: string,
): Coordinates | string {
  const trimmedLatitude = latitudeDraft.trim()
  const trimmedLongitude = longitudeDraft.trim()

  if (!trimmedLatitude) {
    return 'Latitude is required.'
  }

  if (!trimmedLongitude) {
    return 'Longitude is required.'
  }

  const latitude = Number(trimmedLatitude)
  const longitude = Number(trimmedLongitude)

  if (!Number.isFinite(latitude) || latitude < -90 || latitude > 90) {
    return 'Latitude must be a number between -90 and 90.'
  }

  if (!Number.isFinite(longitude) || longitude < -180 || longitude > 180) {
    return 'Longitude must be a number between -180 and 180.'
  }

  return { latitude, longitude }
}

function getStartErrorMessage(error: Error | null) {
  if (error instanceof ApiError) {
    if (error.problem?.code === 'visit_too_far_from_store') {
      return 'You are too far from the store. You must be within 200 metres.'
    }

    if (error.problem?.code === 'invalid_visit_status') {
      return 'This visit can no longer be started.'
    }
  }

  return 'Unable to start visit. Please try again.'
}

export function StartVisitForm({ visitId }: StartVisitFormProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [latitudeDraft, setLatitudeDraft] = useState('')
  const [longitudeDraft, setLongitudeDraft] = useState('')
  const [validationError, setValidationError] = useState<string | null>(null)
  const startMutation = useStartVisit(visitId)

  if (!isOpen) {
    return (
      <div className="visit-action">
        <button
          type="button"
          className="button-primary visit-action__open"
          onClick={() => setIsOpen(true)}
        >
          Start Visit
        </button>
      </div>
    )
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const coordinates = parseCoordinates(latitudeDraft, longitudeDraft)
    if (typeof coordinates === 'string') {
      setValidationError(coordinates)
      return
    }

    setValidationError(null)
    startMutation.mutate(coordinates)
  }

  function handleCancel() {
    setLatitudeDraft('')
    setLongitudeDraft('')
    setValidationError(null)
    startMutation.reset()
    setIsOpen(false)
  }

  const errorMessage = validationError ??
    (startMutation.isError ? getStartErrorMessage(startMutation.error) : null)

  return (
    <form className="start-visit-form" onSubmit={handleSubmit} noValidate>
      <div className="start-visit-form__heading">
        <h4>Start Visit</h4>
        <p>Enter your current coordinates to verify the store location.</p>
      </div>

      <div className="start-visit-form__fields">
        <label className="start-visit-form__field">
          Latitude
          <input
            type="number"
            min="-90"
            max="90"
            step="any"
            inputMode="decimal"
            value={latitudeDraft}
            disabled={startMutation.isPending}
            onChange={(event) => setLatitudeDraft(event.target.value)}
          />
        </label>

        <label className="start-visit-form__field">
          Longitude
          <input
            type="number"
            min="-180"
            max="180"
            step="any"
            inputMode="decimal"
            value={longitudeDraft}
            disabled={startMutation.isPending}
            onChange={(event) => setLongitudeDraft(event.target.value)}
          />
        </label>
      </div>

      {errorMessage ? (
        <p className="start-visit-form__error" role="alert">
          {errorMessage}
        </p>
      ) : null}

      <div className="start-visit-form__actions">
        <button
          type="button"
          className="button-secondary"
          disabled={startMutation.isPending}
          onClick={handleCancel}
        >
          Cancel
        </button>
        <button
          type="submit"
          className="button-primary"
          disabled={startMutation.isPending}
        >
          {startMutation.isPending ? 'Starting...' : 'Start Visit'}
        </button>
      </div>
    </form>
  )
}
