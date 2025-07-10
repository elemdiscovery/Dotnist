-- Migration script to create a minimal database from an existing RDS SQLite database
-- This script uses ORDER BY clauses to ensure consistent results and database hashes
-- Step 1: Attach the source database
-- Note: Replace 'source_database.db' with your actual source database file path
ATTACH DATABASE 'source_database.db' AS source_db;
-- Step 2: Create the new minimal database schema
-- Create the FILE table keeping only sha256 and package_id
CREATE TABLE FILE (
    package_id INTEGER NOT NULL,
    sha256 VARCHAR NOT NULL,
    CONSTRAINT PK_FILE__SHA256 PRIMARY KEY (sha256)
);
-- Copy over the MFG table
CREATE TABLE MFG (
    manufacturer_id INTEGER NOT NULL,
    name VARCHAR NOT NULL,
    CONSTRAINT PK_MFG__MFG_ID PRIMARY KEY (manufacturer_id)
);
-- Copy over the OS table
CREATE TABLE OS (
    operating_system_id INTEGER NOT NULL,
    name VARCHAR NOT NULL,
    version VARCHAR NOT NULL,
    manufacturer_id INTEGER NOT NULL,
    CONSTRAINT PK_OS__OS_ID PRIMARY KEY (operating_system_id, manufacturer_id)
);
-- Copy over the PKG table
CREATE TABLE PKG (
    package_id INTEGER NOT NULL,
    name VARCHAR NOT NULL,
    version VARCHAR NOT NULL,
    operating_system_id INTEGER NOT NULL,
    manufacturer_id INTEGER NOT NULL,
    language VARCHAR NOT NULL,
    application_type VARCHAR NOT NULL,
    CONSTRAINT PK_PGK__PKG_ID PRIMARY KEY (
        package_id,
        operating_system_id,
        manufacturer_id,
        language,
        application_type
    )
);
-- Copy over the VERSION table
CREATE TABLE VERSION (
    version VARCHAR UNIQUE NOT NULL,
    build_set VARCHAR NOT NULL,
    build_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    release_date TIMESTAMP NOT NULL,
    description VARCHAR NOT NULL,
    CONSTRAINT PK_VERSION__VERSION PRIMARY KEY (version)
);
-- Step 3: Copy data from the source database with consistent ordering
-- Copy MFG data (ordered by manufacturer_id for consistency)
INSERT INTO MFG (manufacturer_id, name)
SELECT manufacturer_id,
    name
FROM source_db.MFG
ORDER BY manufacturer_id;
-- Copy OS data (ordered by operating_system_id, manufacturer_id for consistency)
INSERT INTO OS (
        operating_system_id,
        name,
        version,
        manufacturer_id
    )
SELECT operating_system_id,
    name,
    version,
    manufacturer_id
FROM source_db.OS
ORDER BY operating_system_id,
    manufacturer_id;
-- Copy PKG data (ordered by package_id, operating_system_id, manufacturer_id for consistency)
INSERT INTO PKG (
        package_id,
        name,
        version,
        operating_system_id,
        manufacturer_id,
        language,
        application_type
    )
SELECT package_id,
    name,
    version,
    operating_system_id,
    manufacturer_id,
    language,
    application_type
FROM source_db.PKG
ORDER BY package_id,
    operating_system_id,
    manufacturer_id,
    language,
    application_type;
-- Copy VERSION data (ordered by version for consistency)
INSERT INTO VERSION (
        version,
        build_set,
        build_date,
        release_date,
        description
    )
SELECT version,
    build_set,
    build_date,
    release_date,
    description
FROM source_db.VERSION
ORDER BY version;
-- Populate the FILE table with data from the original FILE table
-- This creates the equivalent of the FILE view as a table
-- Ordered by sha256 for consistency, removed md5 field
INSERT INTO FILE (package_id, sha256)
SELECT MIN(package_id) as package_id,
    sha256
FROM source_db.FILE
GROUP BY sha256
ORDER BY sha256;
-- Step 4: Detach the source database
DETACH DATABASE source_db;
-- Step 5: Verify the migration
-- You can run these queries to verify the data was copied correctly
-- Check record counts
SELECT 'FILE' as table_name,
    COUNT(*) as record_count
FROM FILE
UNION ALL
SELECT 'MFG' as table_name,
    COUNT(*) as record_count
FROM MFG
UNION ALL
SELECT 'OS' as table_name,
    COUNT(*) as record_count
FROM OS
UNION ALL
SELECT 'PKG' as table_name,
    COUNT(*) as record_count
FROM PKG
UNION ALL
SELECT 'VERSION' as table_name,
    COUNT(*) as record_count
FROM VERSION;
-- Note: The DISTINCT_HASH view is intentionally excluded as requested