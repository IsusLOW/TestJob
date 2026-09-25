using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace TestJob.Api;

[ApiController]
[Route("api/process")]
public sealed class ProcessController : ControllerBase
{
    private readonly IValidator<ProcessRequest> _validator;
    private readonly IProcessingService _service;

    public ProcessController(IValidator<ProcessRequest> validator, IProcessingService service)
    {
        _validator = validator;
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<ProcessResponse>> Process(
        [FromBody] ProcessRequest? request, CancellationToken ct)
    {
        if (request is null)
        {
            return Ok(new ProcessResponse
            {
                IsError = 1,
                ErrorCode = ErrorCodes.MissingParam,
                ErrorMessage = "Пустое тело запроса.",
            });
        }

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var first = validation.Errors[0];
            return Ok(new ProcessResponse
            {
                IsError = 1,
                ErrorCode = string.IsNullOrEmpty(first.ErrorCode)
                    ? ErrorCodes.MissingParam
                    : first.ErrorCode,
                ErrorMessage = first.ErrorMessage,
            });
        }

        ProcessResponse response = await _service.ProcessAsync(request, ct);
        return Ok(response);
    }
}
