FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster-arm64v8 AS build
WORKDIR /app

# Separate layers here to avoid redoing dependencies on code change.
COPY *.sln .
COPY *.csproj .
RUN dotnet restore

# Now the code.
COPY . .
RUN dotnet publish -r linux-musl-arm64 -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime-deps:3.1-alpine-arm64v8 AS runtime
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["./docker_exporter"]

