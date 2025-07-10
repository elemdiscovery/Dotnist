#!/bin/bash

# Script to build and push the dotnist-db image
# This should be run manually when the database is updated

set -e

echo "Building dotnist-db image..."

# Build the database image
docker build \
  --build-arg DATABASE_SOURCE_PATH=rds/RDS_2025.06.1_modern_minimal_patched_flattened.db \
  -f Dockerfile.db \
  -t ghcr.io/elemdiscovery/dotnist-db:latest \
  .

echo "Pushing dotnist-db image to GHCR..."

# Push the image
docker push ghcr.io/elemdiscovery/dotnist-db:latest

echo "Database image pushed successfully!"
echo "You can now trigger the GitHub Actions workflow to build the gRPC image." 