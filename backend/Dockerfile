# ============================================================================
# SILLAR · API
#
# Compilación en dos etapas: el SDK construye y publica, y la imagen final
# solo lleva el runtime. La imagen resultante no contiene código fuente ni
# herramientas de compilación.
#
# Se construye desde backend/:  docker compose --profile full build api
# ============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero los archivos de proyecto y de versiones: mientras no cambien, Docker
# reutiliza la capa de restauración y no vuelve a descargar los paquetes en
# cada compilación.
COPY Directory.Build.props Directory.Packages.props ./
COPY Sillar.Shared/Sillar.Shared.csproj                 Sillar.Shared/
COPY Sillar.Core.Contracts/Sillar.Core.Contracts.csproj Sillar.Core.Contracts/
COPY Sillar.Core/Sillar.Core.csproj                     Sillar.Core/
COPY Sillar.Api/Sillar.Api.csproj                       Sillar.Api/

RUN dotnet restore Sillar.Api/Sillar.Api.csproj

# Ahora el código. Los proyectos de pruebas no entran en la imagen: se ejecutan
# en la máquina de desarrollo y en el pipeline, no en el despliegue.
COPY Sillar.Shared/          Sillar.Shared/
COPY Sillar.Core.Contracts/  Sillar.Core.Contracts/
COPY Sillar.Core/            Sillar.Core/
COPY Sillar.Api/             Sillar.Api/

RUN dotnet publish Sillar.Api/Sillar.Api.csproj -c Release -o /app --no-restore

# ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Imagen Debian, no Alpine: el producto ordena y compara texto en español con
# ICU, y las imágenes recortadas vienen sin esos datos.
COPY --from=build /app .

# Sin HTTPS dentro del contenedor: el certificado lo termina el proxy de
# delante. La cookie de sesión sigue emitiéndose con Secure.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Usuario sin privilegios. La imagen base ya define 'app' con UID 1654.
USER app

ENTRYPOINT ["dotnet", "Sillar.Api.dll"]
