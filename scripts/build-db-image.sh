#!/bin/bash

# Script to build and push the dotnist-db image
# This should be run manually when the database is updated
# Usage: ./build-db-image.sh <database_path>

set -e

# Check if database path is provided
if [ -z "$1" ]; then
  echo "Error: Database source path is required"
  echo "Usage: $0 <database_path>"
  echo "Example: $0 rds/RDS_2025.09.1_modern_minimal_patched_flattened.db"
  exit 1
fi

DATABASE_SOURCE_PATH="$1"

# Validate that the file exists
if [ ! -f "$DATABASE_SOURCE_PATH" ]; then
  echo "Error: Database file not found: $DATABASE_SOURCE_PATH"
  exit 1
fi

echo "Building dotnist-db image for multiple architectures..."
echo "Using database: $DATABASE_SOURCE_PATH"

# Build and push multi-architecture image
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg DATABASE_SOURCE_PATH="$DATABASE_SOURCE_PATH" \
  -f Dockerfile.db \
  -t ghcr.io/elemdiscovery/dotnist-db:latest \
  --push \
  .

echo "Database image built and pushed successfully!"
echo "You can now trigger the GitHub Actions workflow to build the gRPC image." 