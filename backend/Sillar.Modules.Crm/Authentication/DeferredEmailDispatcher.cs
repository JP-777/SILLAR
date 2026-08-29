using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Core.Contracts.Email;

namespace Sillar.Modules.Crm.Authentication;

/// <summary>
/// Ejecuta correos diferidos en un ámbito DI nuevo, después de responder.
/// Nunca captura un IEmailSender scoped de la petición que ya terminó.
/// </summary>
internal sealed class DeferredEmailDispatcher(IServiceScopeFactory scopes)
{
    public void Schedule(HttpResponse response, OutgoingEmail message)
    {
        response.OnCompleted(async () =>
        {
            await using var scope = scopes.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

            await sender.SendAsync(
                message,
                CancellationToken.None);
        });
    }
}
