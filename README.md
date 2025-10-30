# Dotnist

Like deNIST except dotnet. Intended to help identify NIST files in ediscovery processing.

## NIST List

The underlying NIST list comes from the [National Software Reference Library](https://www.nist.gov/itl/ssd/software-quality-group/national-software-reference-library-nsrl/about-nsrl/nsrl-introduction) and is formally known as the Reference Data Set.

## Components

This project consists of a variety of components:

- The `Dotnist` library, which does look ups against a provided sqlite database.
- The `Dotnist.Grpc` gRPC server, which is bundled into the `dotnist-grpc` container image (db included).
- The `Dotnist.Client` client library, which is just a package that directly provides the generated gRPC client library. You could alternately copy the [`dotnist.proto`](./Dotnist.Grpc/Protos/dotnist.proto) file directly, which is what you would need to do for other languages.

## Usage

For usage, look at the tests and the `dotnist.proto` file.

## RDS Database

The 'minimal' modern RDS database is comically large at >170 GB after extraction, so we have a manual workflow to shrink the database into just a single package reference for each unique Sha256 hash, discarding the filename and other hash types from the database as well. The library is designed to work with both a full database and the flattened database, but the Docker container bundles this hand made flattened database.

### Versions

| Version | RDS Release |
|---------|-------------|
| 1.0.x   | 2025-06-01  |
| 1.1.x   | 2025-09-01  |

### Quarterly Updates

In order to apply the quarterly RDS patches:

1. Download the NSRL provided .sql patch for the modern minimal dataset.
2. Use `update-database.sh` to apply the patch to a complete (non-flattened) sqlite database.
3. Use `flatten-sqlite.sh` to generate a flattened database.
4. Use `build-db-image.sh` to bundle the new flattened database and push it to GHCR.

After that the build process that runs in Github Actions will be able to use the updated .db file.

## TODO

Some hypothetical improvements that aren't worth the time at the moment:

- Running tests in CI is a hassle since you need the database, one option would be to run the tests in a docker container.
- A version of this that works against a NIST list stored in a regular hosted database.
- I didn't bother to configure the image builds to run during pull requests, like you would normally want in a development workflow.

If anyone else finds this library useful definitely feel free to reach out! I'd be curious about what you are working on.
