using System.Reflection;
using Microsoft.OpenApi;
using Sillar.Shared.Platform;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Sillar.Api.Documentation;

/// <summary>
/// Pone en el documento OpenAPI los cuerpos de ejemplo que declara cada
/// módulo a través de <see cref="ISchemaExamples"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Api no nombra a ningún módulo.</b> Los recoge por reflexión, igual que
/// ya hace con los comentarios XML (<c>Sillar.*.xml</c>): un módulo nuevo trae
/// sus ejemplos y aparecen solos.
/// </para>
/// <para>
/// El filtro vive aquí y no en los módulos a propósito: <b>un módulo de
/// dominio no tiene por qué conocer Swashbuckle</b>. Declara tipos y cadenas;
/// convertirlas en un documento es de la capa que ya lo genera.
/// </para>
/// <para>
/// Hace falta un filtro porque Swashbuckle <b>no lee <c>&lt;example&gt;</c> de
/// un <c>record</c> posicional</b>, y todos los DTO de SILLAR lo son.
/// Comprobado contra el documento real antes de escribir esto: con el
/// <c>&lt;example&gt;</c> puesto en el XML, seguían saliendo cero ejemplos.
/// </para>
/// </remarks>
public sealed class ModuleSchemaExamples : ISchemaFilter
{
    private readonly IReadOnlyDictionary<Type, string> _examples;

    /// <summary>Reúne los ejemplos de todos los módulos cargados.</summary>
    public ModuleSchemaExamples()
    {
        var all = new Dictionary<Type, string>();

        foreach (var provider in Discover())
        {
            foreach (var (type, example) in provider.Examples)
            {
                // El primero que llega manda. Dos módulos no comparten tipos
                // de petición, así que un choque aquí sería un síntoma de otra
                // cosa, no algo que resolver eligiendo.
                all.TryAdd(type, example);
            }
        }

        _examples = all;
    }

    /// <summary>Pone el ejemplo del tipo, si algún módulo tiene uno para él.</summary>
    /// <param name="schema">Esquema recién generado.</param>
    /// <param name="context">Tipo que lo originó.</param>
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is OpenApiSchema concrete && _examples.TryGetValue(context.Type, out var example))
        {
            concrete.Example = example;
        }
    }

    /// <summary>Las implementaciones de <see cref="ISchemaExamples"/> ya cargadas.</summary>
    private static IEnumerable<ISchemaExamples> Discover()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;

            if (name is null || !name.StartsWith("Sillar.", StringComparison.Ordinal))
            {
                continue;
            }

            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                // Un ensamblado que no se deja inspeccionar no impide
                // documentar el resto: la documentación nunca es motivo para
                // no arrancar.
                continue;
            }

            foreach (var type in types)
            {
                if (type.IsAbstract
                    || !type.IsClass
                    || !typeof(ISchemaExamples).IsAssignableFrom(type)
                    || type.GetConstructor(Type.EmptyTypes) is null)
                {
                    continue;
                }

                if (Activator.CreateInstance(type) is ISchemaExamples provider)
                {
                    yield return provider;
                }
            }
        }
    }
}
