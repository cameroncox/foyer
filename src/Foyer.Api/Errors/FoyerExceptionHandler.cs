using Foyer.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace Foyer.Api.Errors;

/// <summary>Turns Core rule failures into problem details: 404 not found, 403 forbidden, 409 rule broken, 400 bad input.</summary>
internal sealed class FoyerExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not FoyerException foyer)
        {
            return false;
        }

        context.Response.StatusCode = foyer switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ForbiddenException => StatusCodes.Status403Forbidden,
            RuleViolationException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Status = context.Response.StatusCode, Detail = foyer.Message },
        });
    }
}
