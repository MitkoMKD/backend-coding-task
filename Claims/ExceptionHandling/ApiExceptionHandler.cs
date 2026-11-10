using Claims.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Claims.ExceptionHandling;

public class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        if (exception is BusinessException business)
        {
            problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [business.Field] = [business.Message]
            })
            {
                Status = business.Error switch
                {
                    BusinessError.NotFound => StatusCodes.Status404NotFound,
                    BusinessError.Conflict => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest
                },
                Title = business.Error switch
                {
                    BusinessError.NotFound => "Resource not found.",
                    BusinessError.Conflict => "Operation conflicts with existing data.",
                    _ => "Validation failed."
                }
            };
        }
        else if (exception is DbUpdateConcurrencyException ||
            exception is DbUpdateException { InnerException: SqlException { Number: 547 } })
        {
            problem = new ProblemDetails { Status = 409, Title = "The resource changed or has related records. Retry after refreshing." };
        }
        else
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status.Value;
        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        }))
        {
            await httpContext.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        }
        return true;
    }
}