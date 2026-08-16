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
# que nadie lo notara. Lo que NO debe entrar —Demo, los *.Tests, bin/, obj/—
# vive en .dockerignore: ahí se enumera la excepción, no la regla.
#
# Se construye desde backend/:  docker compose --profile full build api
# ============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish Sillar.Api/Sillar.Api.csproj -c Release -o /app

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
