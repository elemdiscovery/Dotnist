# syntax=docker/dockerfile:1
# Multi-stage build for NSRL gRPC service

# Database stage - copy from the database image
FROM ghcr.io/elemdiscovery/dotnist-db:latest AS database

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

# Accept build arguments for version information
ARG VERSION=0.0.0-dev
ARG ASSEMBLY_VERSION=0.0.0.0
ARG FILE_VERSION=0.0.0.0
ARG INFORMATIONAL_VERSION=0.0.0-dev+local

# Set working directory
WORKDIR /src

# Copy project files
COPY ["Dotnist/Dotnist.csproj", "Dotnist/"]
COPY ["Dotnist.Grpc/Dotnist.Grpc.csproj", "Dotnist.Grpc/"]
COPY ["Dotnist.sln", "./"]

# Restore dependencies
RUN dotnet restore "Dotnist.Grpc/Dotnist.Grpc.csproj"

# Copy source code
COPY Dotnist/ Dotnist/
COPY Dotnist.Grpc/ Dotnist.Grpc/

# Build the application with version information
RUN dotnet build "Dotnist.Grpc/Dotnist.Grpc.csproj" -c Release \
    --no-restore \
    /p:Version=${VERSION} \
    /p:AssemblyVersion=${ASSEMBLY_VERSION} \
    /p:FileVersion=${FILE_VERSION} \
    /p:InformationalVersion=${INFORMATIONAL_VERSION} \
    -o /app/build

# Publish the application
RUN dotnet publish "Dotnist.Grpc/Dotnist.Grpc.csproj" -c Release -o /app/publish

# Test stage - runs the test suite against the bundled flattened database.
# Build this stage explicitly with `--target test` (see build-grpc.yml).
# The database is bind-mounted read-only from the `database` stage during the
# test run, so it is never copied into an image layer and is pulled at most
# once via the shared build cache. NsrlDatabase opens with Mode=ReadOnly, so a
# read-only mount is sufficient.
FROM build AS test
COPY Dotnist.Tests/ Dotnist.Tests/
RUN dotnet restore "Dotnist.Tests/Dotnist.Tests.csproj"
RUN --mount=type=bind,from=database,source=/nsrl.db,target=/tmp/nsrl.db \
    MINIMAL_DB_PATH_FLATTENED=/tmp/nsrl.db \
    dotnet test "Dotnist.Tests/Dotnist.Tests.csproj" -c Release --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS runtime

LABEL org.opencontainers.image.description="gRPC server for the dotnist project, database included."
LABEL org.opencontainers.image.source="https://github.com/elemdiscovery/dotnist"
LABEL org.opencontainers.image.licenses="MIT"

# Accept build arguments for runtime configuration
ENV PORT=3380

# Set working directory
WORKDIR /app

# Copy database from database image
COPY --from=database --chown=app:app /nsrl.db /app/database/nsrl.db

# Copy the published application
COPY --from=build --chown=app:app /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATABASE_PATH=/app/database/nsrl.db

# Expose port (will be overridden by Cloud Run)
EXPOSE ${PORT}

# Run the application as the non-root 'app' user (UID 1654, provided by the chiseled base)
USER app

# Run the application
ENTRYPOINT ["dotnet", "Dotnist.Grpc.dll"]