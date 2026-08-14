import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ApiError } from '../../api/client'
import { completeVisit, startVisit } from '../../api/visits'
import type {
  CompleteVisitRequest,
  StartVisitRequest,
} from '../../types/visit'
import { visitQueryKeys } from './queries'

export function useStartVisit(visitId: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: StartVisitRequest) => startVisit(visitId, request),
    retry: false,
    onSuccess: async (visit) => {
      queryClient.setQueryData(visitQueryKeys.detail(visitId), visit)

      await queryClient.invalidateQueries({
        queryKey: visitQueryKeys.lists(),
      })
    },
    onError: async (error) => {
      if (
        !(error instanceof ApiError) ||
        error.problem?.code !== 'invalid_visit_status'
      ) {
        return
      }

      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: visitQueryKeys.detail(visitId),
        }),
        queryClient.invalidateQueries({
          queryKey: visitQueryKeys.lists(),
        }),
      ])
    },
  })
}

export function useCompleteVisit(visitId: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (request: CompleteVisitRequest) =>
      completeVisit(visitId, request),
    retry: false,
    onSuccess: async (visit) => {
      queryClient.setQueryData(visitQueryKeys.detail(visitId), visit)

      await queryClient.invalidateQueries({
        queryKey: visitQueryKeys.lists(),
      })
    },
    onError: async (error) => {
      const errorCode = error instanceof ApiError ? error.problem?.code : null
      if (
        errorCode !== 'invalid_visit_status' &&
        errorCode !== 'concurrency_conflict'
      ) {
        return
      }

      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: visitQueryKeys.detail(visitId),
        }),
        queryClient.invalidateQueries({
          queryKey: visitQueryKeys.lists(),
        }),
      ])
    },
  })
}
