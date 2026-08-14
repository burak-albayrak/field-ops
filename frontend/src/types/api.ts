export type ApiProblem = {
  title: string
  status: number
  detail?: string
  code: string
  // ASP.NET Core validation responses map each invalid field to one or more messages.
  errors?: Record<string, string[]>
}
