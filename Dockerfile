# Multi-stage build for NSRL gRPC service

# Database stage - copy from the database image
FROM ghcr.io/elemdiscovery/dotnist-db:latest AS database

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

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

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

LABEL org.opencontainers.image.description="gRPC server for the dotnist project, database included."
LABEL org.opencontainers.image.source="https://github.com/elemdiscovery/dotnist"
LABEL org.opencontainers.image.licenses="MIT"

# Accept build arguments for runtime configuration
ENV PORT=3380

# Set working directory
WORKDIR /app

# Create directory for database
RUN mkdir -p /app/database

# Copy database from database image
COPY --from=database /nsrl.db /app/database/nsrl.db

# Copy the published application
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATABASE_PATH=/app/database/nsrl.db

# Expose port (will be overridden by Cloud Run)
EXPOSE ${PORT}

# Run the application
ENTRYPOINT ["dotnet", "Dotnist.Grpc.dll"]