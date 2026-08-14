using Microsoft.OpenApi;
using Sillar.Api.Modularity;
using Sillar.Shared.Configuration;
using Sillar.Shared.Platform;

// Antes de construir nada: el proveedor de configuración por variables de
// entorno lee una sola vez, así que .env tiene que estar cargado ya.
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

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
    // El modo instalación no monta rutas de negocio. Las de instalación llegan
    // con la siguiente entrega de CORE.
    app.Logger.LogWarning("SILLAR arrancó en MODO INSTALACIÓN. No hay rutas de negocio disponibles.");
}
else
{
    foreach (var module in boot.Active)
    {
        module.MapEndpoints(app);
    }
}

app.Run();
return 0;
