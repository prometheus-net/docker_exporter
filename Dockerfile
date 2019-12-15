FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

# Separate layers here to avoid redoing dependencies on code change.
COPY *.sln .
COPY DockerExporter/*.csproj ./DockerExporter/
RUN dotnet restore

# Now the code.
COPY DockerExporter/. ./DockerExporter/
WORKDIR /app/DockerExporter
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS runtime
WORKDIR /app
COPY --from=build /app/DockerExporter/out ./

ENTRYPOINT ["dotnet", "DockerExporter.dll"]