using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LanguageLearning.Api.Authentication;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequireCsrfAttribute() : TypeFilterAttribute(typeof(CsrfValidationFilter));

public sealed class CsrfValidationFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            var problem = new ProblemDetails { Status = 400, Title = "Request verification failed." };
            problem.Extensions["code"] = "csrf_failed";
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            context.Result = new ObjectResult(problem) { StatusCode = 400 };
        }
    }
}
