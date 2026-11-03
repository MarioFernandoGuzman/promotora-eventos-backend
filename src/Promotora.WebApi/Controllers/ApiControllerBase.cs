using Microsoft.AspNetCore.Mvc;
using Promotora.Domain.Shared;
using Promotora.WebApi.Errors;

namespace Promotora.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var problem = ProblemDetailsMapper.Create(result.Errors, HttpContext);
        return new ObjectResult(problem) { StatusCode = problem.Status, ContentTypes = { "application/problem+json" } };
    }
}
