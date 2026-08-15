FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /origen

COPY backend/GT.sln ./backend/
COPY backend/src/GT.Api/GT.Api.csproj ./backend/src/GT.Api/
COPY backend/src/GT.Application/GT.Application.csproj ./backend/src/GT.Application/
COPY backend/src/GT.Domain/GT.Domain.csproj ./backend/src/GT.Domain/
COPY backend/src/GT.Infrastructure/GT.Infrastructure.csproj ./backend/src/GT.Infrastructure/
RUN dotnet restore backend/src/GT.Api/GT.Api.csproj

COPY backend/src/ ./backend/src/
RUN dotnet publish backend/src/GT.Api/GT.Api.csproj -c Release -o /publicado --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0

# Módulo 6: el motor de texto de QuestPDF los necesita y la imagen de ASP.NET no los trae
# (research §1, §15.3). Sin ellos el backend compila, restaura, arranca y sirve todo, y falla recién
# al emitir la primera factura — el peor momento posible para enterarse. Por eso además va un test de
# integración que genera un PDF de verdad, para que la falta se note en CI y no en producción.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /publicado .
EXPOSE 8080
ENTRYPOINT ["dotnet", "GT.Api.dll"]
