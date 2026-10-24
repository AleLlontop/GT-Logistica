FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/ backend/
RUN dotnet publish backend/tools/GT.SeedDemo/GT.SeedDemo.csproj -c Release -o /published

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /published .
ENTRYPOINT ["dotnet", "GT.SeedDemo.dll"]
CMD ["--seed", "--prepare", "--confirm"]
