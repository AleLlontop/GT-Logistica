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
WORKDIR /app
COPY --from=build /publicado .
EXPOSE 8080
ENTRYPOINT ["dotnet", "GT.Api.dll"]
