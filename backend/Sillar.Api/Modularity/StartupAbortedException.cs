namespace Sillar.Api.Modularity;

/// <summary>
/// El arranque no puede continuar y hay que arreglar algo antes de reintentar.
/// </summary>
/// <remarks>
/// Se lanza solo con un mensaje que basta para resolver el problema sin depurar.
/// Nunca se captura para seguir adelante: la alternativa a caerse aquí es
/// funcionar a medias en producción.
/// </remarks>
public sealed class StartupAbortedException(string message) : Exception(message);
