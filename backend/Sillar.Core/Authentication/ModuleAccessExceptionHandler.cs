using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Sillar.Core.Contracts;

namespace Sillar.Core.Authentication;

internal sealed class ModuleAccessExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not ModuleAccessDeniedException denied) return false;
        await Results.Problem(statusCode: denied.StatusCode, title: denied.Message).ExecuteAsync(context);
        return true;
    }
}
