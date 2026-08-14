import { useQuery } from '@tanstack/react-query'
import { getVisit, getVisits } from '../../api/visits'
import type { VisitListParams } from '../../types/visit'

export const visitQueryKeys = {
  lists: () => ['visits', 'list'] as const,
  list: (params: VisitListParams) => [...visitQueryKeys.lists(), params] as const,
  detail: (id: number | null) => ['visits', 'detail', id] as const,
}

export function useVisits(params: VisitListParams) {
  return useQuery({
    queryKey: visitQueryKeys.list(params),
    queryFn: () => getVisits(params),
  })
}

export function useVisit(id: number | null) {
  return useQuery({
    queryKey: visitQueryKeys.detail(id),
    queryFn: () => {
      if (id === null) {
        throw new Error('A visit id is required to load visit details.')
      }

      return getVisit(id)
    },
    enabled: id !== null,
  })
}
