using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;

using static Sillar.Api.Tests.HostDePrueba;

namespace Sillar.Api.Tests;

/// <summary>
/// Evidencia de cierre de M04 que necesita observar el host real.
///
/// No usa WebApplicationFactory: los criterios hablan de registros del proceso,
/// respuestas HTTP y de lo que puede consultar una persona administradora.
/// </summary>
public sealed class CierreM04SeguridadYCorreoTests
{
    private const string AdminEmail = "m04-cierre-admin@sillar.test";
    private const string AdminPassword = "cedro-niebla-veintisiete-lunas";
    private const string CustomerPassword = "m04-sentinela-clave-no-debe-salir-47";

    [Fact]
    public async Task Criterio13_password_de_registro_no_aparece_en_respuesta_logs_ni_auditoria()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConCrmActivoAsync(async (cadena, baseAddress, host, token) =>
        {
            var email = $"criterio13-{Guid.NewGuid():N}@sillar.test";

            using var client = new HttpClient { BaseAddress = baseAddress };

            var responseBody = await RegistrarAsync(
                client,
                email,
                CustomerPassword,
                token);

            // Respuesta HTTP observable por quien acaba de registrarse.
            Assert.False(
                responseBody.Contains(CustomerPassword, StringComparison.Ordinal),
                $"La respuesta de registro expuso la contraseña: {responseBody}");

            // Esperar el intento de correo también demuestra que la petición
            // terminó y que los callbacks posteriores al response ya actuaron.
            var summary = await EsperarResumenDeCorreoAsync(
                cadena,
                email,
                token);

            // El rastro persistente tampoco puede contener el secreto.
            Assert.False(
                summary.Contains(CustomerPassword, StringComparison.Ordinal),
                $"La auditoría expuso la contraseña: {summary}");

            Assert.False(
                await AuditoriaContieneAsync(
                    cadena,
                    CustomerPassword,
                    token),
                "core.audit_log contiene la contraseña de registro.");

            // Y el registro stdout/stderr del proceso real tampoco.
            Assert.False(
                host.Registro.Contains(CustomerPassword, StringComparison.Ordinal),
                $"El host escribió la contraseña en sus logs:{Environment.NewLine}{host.Registro}");
        }, ct);
    }

    [Fact]
    public async Task Criterio17_fallo_de_correo_de_un_registro_concreto_es_visible_para_admin()
    {
        var ct = TestContext.Current.CancellationToken;

        await ConCrmActivoAsync(async (cadena, baseAddress, host, token) =>
        {
            var email = $"criterio17-{Guid.NewGuid():N}@sillar.test";

            using var client = new HttpClient { BaseAddress = baseAddress };

            // Sin SMTP configurado, el hecho principal no se revierte:
            // el registro tiene que seguir respondiendo correctamente.
            var registrationBody = await RegistrarAsync(
                client,
                email,
                CustomerPassword,
                token);

            Assert.Contains(
                "Solicitud de registro procesada.",
                registrationBody,
                StringComparison.Ordinal);

            // La auditoría se escribe después de responder. Primero esperamos
            // el rastro exacto correspondiente a ESTE correo.
            var persistedSummary = await EsperarResumenDeCorreoAsync(
                cadena,
                email,
                token);

            Assert.Contains(email, persistedSummary, StringComparison.Ordinal);
            Assert.Contains("falló", persistedSummary, StringComparison.Ordinal);
            Assert.Contains("smtp_server", persistedSummary, StringComparison.Ordinal);

            // Ahora se comprueba la parte "lo ve quien administra":
            // login real de super_admin y GET real del API de auditoría.
            var cookie = await EntrarComoAdminAsync(client, token);

            var visibleSummary = await EsperarResumenDesdeApiAdminAsync(
                client,
                cookie,
                email,
                token);

            Assert.Equal(persistedSummary, visibleSummary);
            Assert.Contains(email, visibleSummary, StringComparison.Ordinal);
            Assert.Contains("falló", visibleSummary, StringComparison.Ordinal);
            Assert.Contains("smtp_server", visibleSummary, StringComparison.Ordinal);

            Assert.False(
                visibleSummary.Contains(CustomerPassword, StringComparison.Ordinal),
                $"La vista administrativa de auditoría expuso la contraseña: {visibleSummary}");

            Assert.False(
                host.Registro.Contains(CustomerPassword, StringComparison.Ordinal),
                $"El host escribió la contraseña en sus logs:{Environment.NewLine}{host.Registro}");
        }, ct);
    }

    private static async Task ConCrmActivoAsync(
        Func<string, Uri, ProcesoDelHost, CancellationToken, Task> body,
        CancellationToken ct)
    {
        var media = Path.Combine(
            Path.GetTempPath(),
            $"sillar-m04-cierre-{Guid.NewGuid():N}");

        Directory.CreateDirectory(media);

        try
        {
            await ConBaseVaciaAsync(async cadena =>
            {
                await InstalarAsync(cadena, media, ct);

                // Primer arranque normal: sincroniza el catálogo y crea
                // core.module_activations para los módulos desplegados.
                using (var limite = Limite(ct, 120))
                await using (var sync = Lanzar(cadena, media))
                {
                    _ = await sync.PuertoAsync(limite.Token);
                }

                // Preparación de la prueba, no comportamiento bajo prueba:
                // el schema ya fue migrado por el instalador.
                await EjecutarAsync(
                    cadena,
                    """
                    UPDATE core.module_activations AS activation
                       SET is_active = true,
                           activated_at = now(),
                           deactivated_at = NULL
                      FROM core.modules AS module
                     WHERE module.module_id = activation.module_id
                       AND module.code = 'crm';
                    """,
                    ct);

                var active = await EscalarAsync(
                    cadena,
                    """
                    SELECT count(*)
                      FROM core.module_activations AS activation
                      JOIN core.modules AS module
                        ON module.module_id = activation.module_id
                     WHERE module.code = 'crm'
                       AND activation.is_active;
                    """,
                    ct);

                Assert.Equal(1L, (long)active!);

                using var hostLimit = Limite(ct, 120);
                await using var host = Lanzar(cadena, media);

                var port = await host.PuertoAsync(hostLimit.Token);
                var baseAddress = new Uri($"http://127.0.0.1:{port}");

                await body(cadena, baseAddress, host, ct);
            }, ct);
        }
        finally
        {
            Directory.Delete(media, recursive: true);
        }
    }

    private static async Task InstalarAsync(
        string cadena,
        string media,
        CancellationToken ct)
    {
        using var limite = Limite(ct, 180);
        await using var setup = Lanzar(cadena, media);

        var port = await setup.PuertoAsync(limite.Token);

        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}")
        };

        using var response = await client.PostAsJsonAsync(
            "/api/setup",
            new
            {
                businessName = "Cierre M04",
                licenseType = "trial",
                admin = new
                {
                    fullName = "Administradora M04",
                    email = AdminEmail,
                    password = AdminPassword
                }
            },
            limite.Token);

        var body = await response.Content.ReadAsStringAsync(limite.Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(
            body.Contains(AdminPassword, StringComparison.Ordinal),
            $"La instalación devolvió la contraseña administrativa: {body}");

        var exitCode = await setup.CodigoDeSalidaAsync(limite.Token);
        Assert.Equal(0, exitCode);

        Assert.False(
            setup.Registro.Contains(AdminPassword, StringComparison.Ordinal),
            $"El host escribió la contraseña administrativa:{Environment.NewLine}{setup.Registro}");
    }

    private static async Task<string> RegistrarAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/customer/auth/register")
        {
            Content = JsonContent.Create(new
            {
                fullName = "Cliente Cierre M04",
                email,
                password,
                phone = "999111222"
            })
        };

        request.Headers.TryAddWithoutValidation(
            "Sec-Fetch-Site",
            "same-origin");

        using var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return body;
    }

    private static async Task<string> EntrarComoAdminAsync(
        HttpClient client,
        CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new
            {
                email = AdminEmail,
                password = AdminPassword
            },
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(
            body.Contains(AdminPassword, StringComparison.Ordinal),
            $"El login devolvió la contraseña administrativa: {body}");

        var setCookie = response.Headers
            .GetValues("Set-Cookie")
            .Single(value =>
                value.StartsWith(
                    "sillar_panel=",
                    StringComparison.Ordinal));

        return setCookie.Split(';', 2)[0];
    }

    private static async Task<string> EsperarResumenDeCorreoAsync(
        string cadena,
        string email,
        CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(cadena);
        await connection.OpenAsync(ct);

        for (var intento = 0; intento < 100; intento++)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT summary
                  FROM core.audit_log
                 WHERE action = 'email_send'
                   AND module_code = 'crm'
                   AND entity_type = 'email'
                   AND entity_id = 'email_verification'
                   AND summary LIKE '%' || @email || '%'
                 ORDER BY occurred_at DESC, audit_log_id DESC
                 LIMIT 1;
                """;
            command.Parameters.AddWithValue("email", email);

            if (await command.ExecuteScalarAsync(ct) is string summary)
            {
                return summary;
            }

            await Task.Delay(100, ct);
        }

        throw new InvalidOperationException(
            $"No apareció en auditoría el intento de correo para '{email}'.");
    }

    private static async Task<bool> AuditoriaContieneAsync(
        string cadena,
        string secret,
        CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(cadena);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*)
              FROM core.audit_log AS entry
             WHERE row_to_json(entry)::text LIKE '%' || @secret || '%';
            """;
        command.Parameters.AddWithValue("secret", secret);

        return (long)(await command.ExecuteScalarAsync(ct))! > 0;
    }

    private static async Task<string> EsperarResumenDesdeApiAdminAsync(
        HttpClient client,
        string cookie,
        string email,
        CancellationToken ct)
    {
        for (var intento = 0; intento < 100; intento++)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "/api/admin/audit"
                + "?moduleCode=crm"
                + "&action=email_send"
                + "&entityType=email"
                + "&entityId=email_verification"
                + "&pageSize=50");

            request.Headers.TryAddWithoutValidation("Cookie", cookie);

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var document = JsonDocument.Parse(body);

            foreach (var item in document.RootElement
                         .GetProperty("items")
                         .EnumerateArray())
            {
                if (!item.TryGetProperty("summary", out var summaryElement))
                {
                    continue;
                }

                var summary = summaryElement.GetString();

                if (summary is not null
                    && summary.Contains(email, StringComparison.Ordinal))
                {
                    return summary;
                }
            }

            await Task.Delay(100, ct);
        }

        throw new InvalidOperationException(
            $"La auditoría administrativa no mostró el fallo para '{email}'.");
    }

    private static CancellationTokenSource Limite(
        CancellationToken parent,
        int seconds)
    {
        var source =
            CancellationTokenSource.CreateLinkedTokenSource(parent);

        source.CancelAfter(TimeSpan.FromSeconds(seconds));
        return source;
    }
}
