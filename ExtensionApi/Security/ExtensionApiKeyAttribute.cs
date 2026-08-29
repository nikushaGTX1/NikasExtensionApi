using System.Security.Cryptography;
using System.Text;
using ExtensionApi.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ExtensionApi.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExtensionApiKeyAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var configured = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<SecurityOptions>>().Value.ExtensionApiKey;

        if (string.IsNullOrWhiteSpace(configured))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "AI service access key is not configured."
            }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
            return Task.CompletedTask;
        }

        var supplied = context.HttpContext.Request.Headers["X-Extension-Key"].ToString();
        if (!SecureEquals(configured, supplied))
        {
            context.Result = new UnauthorizedObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "A valid X-Extension-Key header is required."
            });
        }

        return Task.CompletedTask;
    }

    private static bool SecureEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
