# Multi-stage build for NSRL gRPC service

# Database stage - copy database first for better caching
FROM scratch AS database
ARG DATABASE_SOURCE_PATH
COPY ${DATABASE_SOURCE_PATH} /nsrl.db

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Set working directory
WORKDIR /src

# Copy project files
COPY ["Dotnist/Dotnist.csproj", "Dotnist/"]
COPY ["Dotnist.Grpc/Dotnist.Grpc.csproj", "Dotnist.Grpc/"]
COPY ["Dotnist.sln", "./"]

# Restore dependencies
RUN dotnet restore "Dotnist.Grpc/Dotnist.Grpc.csproj"

# Copy source code (excluding database files)
COPY Dotnist/ Dotnist/
COPY Dotnist.Grpc/ Dotnist.Grpc/

# Build the application
RUN dotnet build "Dotnist.Grpc/Dotnist.Grpc.csproj" -c Release -o /app/build

# Publish the application
RUN dotnet publish "Dotnist.Grpc/Dotnist.Grpc.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Set working directory
WORKDIR /app

# Create directory for database
RUN mkdir -p /app/database

# Copy database from database stage (this layer won't be invalidated by code changes)
COPY --from=database /nsrl.db /app/database/nsrl.db

# Copy the published application
COPY --from=build /app/publish .


# Set environment variables
ENV ASPNETCORE_URLS=http://+:${PORT:-3380}
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DATABASE_PATH=/app/database/nsrl.db

# Expose port (will be overridden by Cloud Run)
EXPOSE ${PORT:-3380}

# Run the application
ENTRYPOINT ["dotnet", "Dotnist.Grpc.dll"] 