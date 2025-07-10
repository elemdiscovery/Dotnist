#!/bin/bash

# Database update script for NSRL RDS (macOS/Linux)
# This script handles applying delta updates to the SQLite database

set -e

# Default values
DATABASE_PATH=""
DELTA_SQL_PATH=""
BACKUP_PATH=""
VALIDATE_ONLY=false
CREATE_BACKUP=false

# Function to display usage
usage() {
    echo "Usage: $0 -d DATABASE_PATH [-s DELTA_SQL_PATH] [-b BACKUP_PATH] [-v] [-c]"
    echo ""
    echo "Options:"
    echo "  -d DATABASE_PATH    Path to the SQLite database file (required)"
    echo "  -s DELTA_SQL_PATH   Path to the delta SQL file (optional)"
    echo "  -b BACKUP_PATH      Path for backup file (optional)"
    echo "  -v                  Validate only, don't apply updates"
    echo "  -c                  Create backup before applying updates"
    echo "  -h                  Display this help message"
    echo ""
    echo "Examples:"
    echo "  $0 -d /path/to/database.db -s /path/to/delta.sql -c"
    echo "  $0 -d /path/to/database.db -v"
    exit 1
}

# Parse command line arguments
while getopts "d:s:b:vch" opt; do
    case $opt in
        d) DATABASE_PATH="$OPTARG" ;;
        s) DELTA_SQL_PATH="$OPTARG" ;;
        b) BACKUP_PATH="$OPTARG" ;;
        v) VALIDATE_ONLY=true ;;
        c) CREATE_BACKUP=true ;;
        h) usage ;;
        *) usage ;;
    esac
done

# Check required parameters
if [ -z "$DATABASE_PATH" ]; then
    echo "Error: Database path is required"
    usage
fi

# Function to log messages
log() {
    local level="$1"
    shift
    local message="$*"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[$timestamp] [$level] $message"
}

# Function to test SQLite database
test_sqlite_database() {
    local path="$1"
    
    if [ ! -f "$path" ]; then
        log "ERROR" "Database file not found: $path"
        return 1
    fi
    
    # Check file size
    local file_size=$(stat -f%z "$path" 2>/dev/null || stat -c%s "$path" 2>/dev/null)
    if [ "$file_size" -lt 1048576 ]; then  # 1MB
        log "WARN" "Database file appears to be too small: $file_size bytes"
    fi
    
    # Try to open database and check basic connectivity by querying VERSION table
    if command -v sqlite3 &> /dev/null; then
        # Use a single connection to get both table count and version info
        local db_info=$(sqlite3 "$path" "SELECT COUNT(*) FROM sqlite_master WHERE type='table'; SELECT version, build_date FROM VERSION LIMIT 1;" 2>/dev/null)
        
        if [ $? -eq 0 ] && [ -n "$db_info" ]; then
            local table_count=$(echo "$db_info" | head -n1)
            local version_info=$(echo "$db_info" | tail -n1)
            
            if [ -n "$version_info" ]; then
                log "INFO" "Database connectivity test passed. Found $table_count tables. Version: $version_info"
                return 0
            else
                log "ERROR" "VERSION table is empty or not accessible"
                return 1
            fi
        else
            log "ERROR" "Could not query database or VERSION table not found"
            return 1
        fi
    else
        log "WARN" "sqlite3 not found, skipping database validation"
        return 0
    fi
}

# Function to backup database
backup_database() {
    local source_path="$1"
    local backup_path="$2"
    
    log "INFO" "Creating backup of database..."
    
    # Create backup directory if it doesn't exist
    local backup_dir=$(dirname "$backup_path")
    mkdir -p "$backup_dir"
    
    # Copy database file
    cp "$source_path" "$backup_path"
    
    # Verify backup
    if test_sqlite_database "$backup_path"; then
        log "INFO" "Backup created successfully: $backup_path"
        return 0
    else
        log "ERROR" "Backup verification failed"
        return 1
    fi
}

# Function to apply delta update
apply_delta_update() {
    local database_path="$1"
    local delta_sql_path="$2"
    
    log "INFO" "Applying delta update..."
    
    # Check if delta SQL file exists
    if [ ! -f "$delta_sql_path" ]; then
        log "ERROR" "Delta SQL file not found: $delta_sql_path"
        return 1
    fi
    
    # Check if file is not empty
    if [ ! -s "$delta_sql_path" ]; then
        log "ERROR" "Delta SQL file is empty"
        return 1
    fi
    
    # Apply the delta using sqlite3
    if command -v sqlite3 &> /dev/null; then
        log "INFO" "Applying delta using sqlite3..."
        if sqlite3 "$database_path" < "$delta_sql_path"; then
            log "INFO" "Delta update completed successfully"
            return 0
        else
            log "ERROR" "Failed to apply delta update"
            return 1
        fi
    else
        log "ERROR" "sqlite3 not found, cannot apply delta update"
        return 1
    fi
}

# Main execution
log "INFO" "Starting database update process..."

# Validate input parameters
if [ ! -f "$DATABASE_PATH" ]; then
    log "ERROR" "Database path does not exist: $DATABASE_PATH"
    exit 1
fi

# Test current database
log "INFO" "Testing current database..."
if ! test_sqlite_database "$DATABASE_PATH"; then
    log "ERROR" "Current database is invalid"
    exit 1
fi

# Create backup if requested
if [ "$CREATE_BACKUP" = true ] || [ -n "$BACKUP_PATH" ]; then
    if [ -z "$BACKUP_PATH" ]; then
        BACKUP_PATH="${DATABASE_PATH}.backup.$(date +%Y%m%d-%H%M%S)"
    fi
    
    if ! backup_database "$DATABASE_PATH" "$BACKUP_PATH"; then
        log "ERROR" "Failed to create backup"
        exit 1
    fi
fi

# Apply delta update if provided
if [ -n "$DELTA_SQL_PATH" ]; then
    if [ "$VALIDATE_ONLY" = true ]; then
        log "INFO" "Validation only mode - skipping delta application"
    else
        if ! apply_delta_update "$DATABASE_PATH" "$DELTA_SQL_PATH"; then
            log "ERROR" "Failed to apply delta update"
            exit 1
        fi
    fi
fi

# Final validation
log "INFO" "Performing final database validation..."
if test_sqlite_database "$DATABASE_PATH"; then
    log "INFO" "Database update completed successfully"
    exit 0
else
    log "ERROR" "Database validation failed after update"
    exit 1
fi 