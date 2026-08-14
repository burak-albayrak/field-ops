using FieldOps.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FieldOps.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (problemDetails, code) = exception switch
        {
            VisitNotFoundException visitNotFound =>
                (CreateProblemDetails(StatusCodes.Status404NotFound, "Visit not found", visitNotFound.Message), "visit_not_found"),
            EmployeeNotFoundException employeeNotFound =>
                (CreateProblemDetails(StatusCodes.Status404NotFound, "Employee not found", employeeNotFound.Message), "employee_not_found"),
            StoreNotFoundException storeNotFound =>
                (CreateProblemDetails(StatusCodes.Status404NotFound, "Store not found", storeNotFound.Message), "store_not_found"),
            DuplicateVisitException duplicateVisit =>
                (CreateProblemDetails(StatusCodes.Status409Conflict, "Duplicate visit", duplicateVisit.Message), "duplicate_visit"),
            ApplicationValidationException validation =>
                (CreateValidationProblemDetails(validation), "validation_error"),
            _ => HandleUnexpectedException(exception)
        };

        problemDetails.Extensions["code"] = code;
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static ProblemDetails CreateProblemDetails(int status, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }

    private static ValidationProblemDetails CreateValidationProblemDetails(ApplicationValidationException exception)
    {
        return new ValidationProblemDetails(exception.Errors.ToDictionary(pair => pair.Key, pair => pair.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = exception.Message
        };
    }

    private (ProblemDetails ProblemDetails, string Code) HandleUnexpectedException(Exception exception)
    {
        // Beklenmeyen ayrıntılar logda kalır; HTTP yanıtı stack trace veya altyapı bilgisini sızdırmaz.
        _logger.LogError(exception, "Unhandled exception while processing HTTP request.");

        return (CreateProblemDetails(
            StatusCodes.Status500InternalServerError,
            "An unexpected error occurred",
            "The request could not be completed."), "internal_error");
    }
}
