using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi;
using Sillar.Api.Modularity;
using Sillar.Core.Endpoints;
using Sillar.Core.Media;
using Sillar.Shared.Configuration;
using Sillar.Shared.Platform;

// Antes de construir nada: el proveedor de configuración por variables de
// entorno lee una sola vez, así que .env tiene que estar cargado ya.
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// El límite de tamaño se aplica TAMBIÉN aquí, no solo al validar el contenido.
// Sin esto, alguien que anuncie dos gigabytes consigue que el servidor los
// reciba enteros antes de que ninguna validación llegue a opinar.
//
// Se deja un margen sobre el máximo por archivo para las fronteras y cabeceras
// del multipart, que viajan en el mismo cuerpo.
var maxUpload = builder.Configuration.GetValue<long?>($"{MediaOptions.SectionName}:MaxSizeBytes")
    ?? new MediaOptions().MaxSizeBytes;
var maxBody = maxUpload + 64 * 1024;

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = maxBody);
builder.Services.Configure<FormOptions>(form =>
{
    form.MultipartBodyLengthLimit = maxBody;
    form.MultipartHeadersLengthLimit = 16 * 1024;
});

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = $"{SillarProduct.Name} API",
        Version = SillarProduct.Version,
        Description = "Los endpoints visibles dependen de los módulos activos en esta instalación."
    });

    // Comentarios XML de todos los módulos publicados: cada uno documenta los
    // suyos y aquí aparecen juntos.
    foreach (var documentation in Directory.EnumerateFiles(AppContext.BaseDirectory, "Sillar.*.xml"))
    {
        options.IncludeXmlComments(documentation);
    }
});

// El arranque modular ocurre aquí, antes de construir la aplicación: de él
// depende qué servicios existen.
using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging
    .AddConfiguration(builder.Configuration.GetSection("Logging"))
    .AddSimpleConsole(console => console.SingleLine = true));

var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Sillar.Arranque");

ModuleBootstrapResult boot;

try
{
    boot = await ModuleBootstrapper.RunAsync(builder, bootstrapLoggerFactory, CancellationToken.None);
}
catch (StartupAbortedException exception)
{
    // Sin traza de pila: el mensaje ya dice qué arreglar. Prefiere caerse aquí,
    // donde alguien lo ve, a funcionar a medias.
    bootstrapLogger.LogCritical("SILLAR no puede arrancar.{NewLine}{Reason}", Environment.NewLine, exception.Message);
    return 1;
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = $"{SillarProduct.Name} API");
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (boot.IsSetupMode)
{
    // Modo instalación: solo /api/setup*. Ninguna ruta de negocio existe
    // todavía, y las de instalación dejarán de existir en cuanto se complete.
    app.MapSetupEndpoints();
    app.Logger.LogWarning(
        "SILLAR arrancó en MODO INSTALACIÓN. Solo responde /api/setup*. " +
        "Completa la instalación con POST /api/setup y el host se reiniciará en modo normal.");
}
else
{
    app.UseAuthentication();
    app.UseAuthorization();

    foreach (var module in boot.Active)
    {
        module.MapEndpoints(app);
    }
}

app.Run();
return 0;
