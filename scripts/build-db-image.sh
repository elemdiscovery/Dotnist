#!/bin/bash

# Script to build and push the dotnist-db image
# This should be run manually when the database is updated

set -e

echo "Building dotnist-db image for multiple architectures..."

# Build and push multi-architecture image
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg DATABASE_SOURCE_PATH=rds/RDS_2025.06.1_modern_minimal_patched_flattened.db \
  -f Dockerfile.db \
  -t ghcr.io/elemdiscovery/dotnist-db:latest \
  --push \
  .

echo "Database image built and pushed successfully!"
echo "You can now trigger the GitHub Actions workflow to build the gRPC image." 