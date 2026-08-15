#if DEBUG
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sillar.Shared.Modularity;

namespace Sillar.Modules.Demo;

/// <summary>
/// Base de los módulos de mentira.
/// </summary>
/// <remarks>
/// No registran servicios ni montan rutas: lo único que aportan es su
/// declaración en el grafo, que es justo lo que hace falta para probar la
/// pantalla de módulos y el validador de dependencias.
///
/// El prefijo <c>demo_</c> en el código los delata en <c>/api/capabilities</c>,
/// en <c>core.modules</c> y en el panel. Si alguien ve uno en una instalación
/// real, sabe de inmediato que algo se hizo mal.
/// </remarks>
public abstract class DemoModule : IModule
{
    /// <summary>Prefijo que llevan todos los códigos de mentira.</summary>
    public const string CodePrefix = "demo_";

    /// <inheritdoc />
    public abstract string Code { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
    public abstract int DisplayOrder { get; }

    /// <inheritdoc />
    public abstract string[] HardDependencies { get; }

    /// <inheritdoc />
    public virtual string[] SoftDependencies => [];

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}

/// <summary>Catálogo de productos. Ejercita el estado activo.</summary>
public sealed class DemoCatalogModule : DemoModule
{
    public override string Code => "demo_catalog";
    public override string DisplayName => "Catálogo de Productos (demostración)";
    public override string Description =>
        "Categorías, productos, imágenes y búsqueda. Es la base de casi todo lo comercial.";
    public override int DisplayOrder => 10;
    public override string[] HardDependencies => ["core"];
}

/// <summary>Clientes y contacto. Sirve de dependencia blanda de otros.</summary>
public sealed class DemoCrmModule : DemoModule
{
    public override string Code => "demo_crm";
    public override string DisplayName => "Clientes y Contacto (demostración)";
    public override string Description =>
        "Registro de clientes y mensajes del formulario de contacto. Se vende solo.";
    public override int DisplayOrder => 20;
    public override string[] HardDependencies => ["core"];
}

/// <summary>
/// Ventas online. Dependencia dura de catálogo y blanda de clientes.
/// </summary>
/// <remarks>
/// Es el caso que demuestra la diferencia: sin catálogo no puede activarse,
/// pero sin clientes funciona igual guardando los datos como snapshot.
/// </remarks>
public sealed class DemoSalesModule : DemoModule
{
    public override string Code => "demo_sales";
    public override string DisplayName => "Ventas Online (demostración)";
    public override string Description =>
        "Carrito, pedidos y confirmación. No se vende lo que no está catalogado.";
    public override int DisplayOrder => 30;
    public override string[] HardDependencies => ["core", "demo_catalog"];
    public override string[] SoftDependencies => ["demo_crm"];
}

/// <summary>Servicios, vitrina. Dependencia dura de las órdenes.</summary>
public sealed class DemoServicesModule : DemoModule
{
    public override string Code => "demo_services";
    public override string DisplayName => "Servicios — Vitrina (demostración)";
    public override string Description =>
        "Catálogo de servicios permanentes para mostrarlos en la web.";
    public override int DisplayOrder => 40;
    public override string[] HardDependencies => ["core"];
}

/// <summary>Órdenes de servicio. Lo que le falta a Seguimiento.</summary>
public sealed class DemoServiceOrdersModule : DemoModule
{
    public override string Code => "demo_service_orders";
    public override string DisplayName => "Servicios — Órdenes (demostración)";
    public override string Description =>
        "Registro de encargos: qué se pidió, quién, cuándo y a qué precio.";
    public override int DisplayOrder => 50;
    public override string[] HardDependencies => ["core", "demo_services"];
    public override string[] SoftDependencies => ["demo_crm"];
}

/// <summary>
/// Seguimiento de servicios. Ejercita el estado bloqueado.
/// </summary>
/// <remarks>
/// Con Órdenes de Servicio inactivo, este no se puede activar. Es exactamente el
/// caso que ilustra la tarjeta del sistema de diseño, y el que la pantalla tiene
/// que saber explicar nombrando lo que falta.
/// </remarks>
public sealed class DemoTrackingModule : DemoModule
{
    public override string Code => "demo_tracking";
    public override string DisplayName => "Seguimiento de Servicios (demostración)";
    public override string Description =>
        "Historial de estados de una orden y tablero de trabajo para el cliente.";
    public override int DisplayOrder => 60;
    public override string[] HardDependencies => ["core", "demo_service_orders"];
}
#endif
