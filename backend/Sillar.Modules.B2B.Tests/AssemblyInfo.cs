using Xunit;

// Estas pruebas crean y migran bases PostgreSQL completas por caso. Ejecutar
// varias colecciones a la vez multiplica CREATE DATABASE + migraciones y hace
// que el testhost sea inestable en la puerta canónica. La serialización es una
// propiedad del arnés de M07; no modifica el contrato ni la lógica del módulo.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
