using FieldOps.Api.Models.Visits;
using FieldOps.Application.Visits;
using FieldOps.Application.Visits.Models;
using Microsoft.AspNetCore.Mvc;

namespace FieldOps.Api.Controllers;

[ApiController]
[Route("api/visits")]
public class VisitsController : ControllerBase
{
    private readonly IVisitService _visitService;

    public VisitsController(IVisitService visitService)
    {
        _visitService = visitService;
    }

    [HttpGet]
    public async Task<ActionResult<VisitListResult>> List(
        [FromQuery] VisitListQuery query,
        CancellationToken cancellationToken)
    {
        var filter = new VisitListFilter
        {
            EmployeeId = query.EmployeeId,
            StoreId = query.StoreId,
            Status = query.Status,
            CountryCode = query.CountryCode,
            StartDate = query.StartDate,
            EndDate = query.EndDate
        };
        var result = await _visitService.ListAsync(
            filter,
            query.Page,
            query.PageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var visit = await _visitService.GetByIdAsync(id, cancellationToken);

        return Ok(visit);
    }

    [HttpPost]
    public async Task<ActionResult<VisitDetailDto>> Create(
        CreateVisitRequest request,
        CancellationToken cancellationToken)
    {
        var input = new CreateVisitInput(
            request.EmployeeId,
            request.StoreId,
            request.PlannedDate!.Value);
        var created = await _visitService.CreateAsync(input, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("{id:long}/start")]
    public async Task<ActionResult<VisitDetailDto>> Start(
        long id,
        StartVisitRequest request,
        CancellationToken cancellationToken)
    {
        var input = new StartVisitInput(request.Latitude!.Value, request.Longitude!.Value);
        var started = await _visitService.StartAsync(id, input, cancellationToken);

        return Ok(started);
    }

    [HttpPost("{id:long}/complete")]
    public async Task<ActionResult<VisitDetailDto>> Complete(
        long id,
        CompleteVisitRequest request,
        CancellationToken cancellationToken)
    {
        var input = new CompleteVisitInput(request.Notes);
        var completed = await _visitService.CompleteAsync(id, input, cancellationToken);

        return Ok(completed);
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<VisitDetailDto>> Cancel(
        long id,
        CancelVisitRequest request,
        CancellationToken cancellationToken)
    {
        var input = new CancelVisitInput(request.Version!.Value);
        var cancelled = await _visitService.CancelAsync(id, input, cancellationToken);

        return Ok(cancelled);
    }
}
