#!/bin/bash

# SQLite Database Migration Script
# This script creates a minimal database from an existing RDS SQLite database

set -e  # Exit on any error

# Configuration
SOURCE_DB="${1:-rds_database.db}"
TARGET_DB="${2:-minimal_database.db}"
SQL_SCRIPT="scripts/flatten-database.sql"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo "${RED}[ERROR]${NC} $1"
}

print_info() {
    echo "${BLUE}[DETAIL]${NC} $1"
}

# Check if source database exists
if [ ! -f "$SOURCE_DB" ]; then
    print_error "Source database '$SOURCE_DB' not found!"
    echo "Usage: $0 [source_database.db] [target_database.db]"
    echo "Example: $0 rds_database.db minimal_database.db"
    exit 1
fi

# Check if SQL script exists
if [ ! -f "$SQL_SCRIPT" ]; then
    print_error "SQL script '$SQL_SCRIPT' not found!"
    exit 1
fi

print_status "Starting SQLite database migration..."
print_status "Source: $SOURCE_DB"
print_status "Target: $TARGET_DB"

# Remove target database if it exists
if [ -f "$TARGET_DB" ]; then
    print_warning "Target database '$TARGET_DB' already exists. Removing it..."
    rm "$TARGET_DB"
fi

# Create a temporary SQL script with the correct database path
TEMP_SQL="$(mktemp)"
trap "rm -f $TEMP_SQL" EXIT

# Replace the source database path in the ATTACH command
sed "s|'source_database.db'|'$SOURCE_DB'|g" "$SQL_SCRIPT" > "$TEMP_SQL"

# Run the migration
print_status "Creating new database schema and migrating data with consistent ordering..."
sqlite3 "$TARGET_DB" < "$TEMP_SQL"

# Verify the migration
print_status "Verifying migration..."
echo "Record counts in the new database:"
sqlite3 "$TARGET_DB" "
SELECT 'FILE' as table_name, COUNT(*) as record_count FROM FILE
UNION ALL
SELECT 'MFG' as table_name, COUNT(*) as record_count FROM MFG
UNION ALL
SELECT 'OS' as table_name, COUNT(*) as record_count FROM OS
UNION ALL
SELECT 'PKG' as table_name, COUNT(*) as record_count FROM PKG
UNION ALL
SELECT 'VERSION' as table_name, COUNT(*) as record_count FROM VERSION;
"

# Show some sample data
print_status "Sample data from FILE table:"
sqlite3 "$TARGET_DB" "SELECT * FROM FILE LIMIT 5;"

# Calculate and display database hash for verification
if command -v sha256sum >/dev/null 2>&1; then
    print_status "Database file hash (for reproducibility verification):"
    sha256sum "$TARGET_DB"
elif command -v shasum >/dev/null 2>&1; then
    print_status "Database file hash (for reproducibility verification):"
    shasum -a 256 "$TARGET_DB"
else
    print_warning "sha256sum or shasum not available - cannot calculate hash"
fi

print_status "Migration completed successfully!"
print_status "New database created: $TARGET_DB"
