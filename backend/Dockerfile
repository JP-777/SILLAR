# ============================================================================
# SILLAR · API
#
# Compilación en dos etapas: el SDK construye y publica, y la imagen final
# solo lleva el runtime. La imagen resultante no contiene código fuente ni
# herramientas de compilación.
#
# Copia el contexto entero y publica Sillar.Api/Sillar.Api.csproj: MSBuild
# resuelve solo las referencias de proyecto que ese .csproj declara, así que
# un módulo nuevo entra en la imagen sin tocar este archivo. Antes esto
# enumeraba cada proyecto a mano, y esa lista se quedó corta con Catalog sin
# que nadie lo notara. Lo que NO debe entrar —los *.Tests, bin/, obj/— vive
# en .dockerignore: ahí se enumera la excepción, no la regla.
#
# Se construye desde backend/:  docker compose --profile full build api
#
# BUILD_CONFIGURATION existe solo para e2e/: la imagen real nunca la toca —
# el valor por defecto es Release, igual que antes de que este ARG existiera
# — y la única otra imagen que la cambia es sillar_e2e-api, construida en
# Debug para que Sillar.Modules.Demo compile. La construye el mismo
# docker-compose.yml de la raíz, con BUILD_CONFIGURATION=Debug puesto por
# e2e/.env.e2e (ver e2e/README.md).
# Publicar en Debug no relaja ninguna de las tres barreras que mantienen a
# Demo fuera de producción (ENTREGA-04A §0): solo abre la puerta que esas
# barreras vuelven a cerrar salvo que además se pida Development y el flag
# Modules:IncludeDemoModules.
# ============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY . .

RUN dotnet publish Sillar.Api/Sillar.Api.csproj -c $BUILD_CONFIGURATION -o /app

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
