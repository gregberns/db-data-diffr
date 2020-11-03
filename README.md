# DB Data Diffr

## Purpose

### Use Case #1 - Database DevOps

Provides a tool set that helps containerize a Production database, also known as [Database DevOps](https://robrich.org/presentation/2019/10/12/database-devops-with-containers.aspx). Migrates selected data and anonymizes PII, so a resulting database image can be pushed to a container repository.

### Use Case #2 - Reverse Engineer Existing System

Understand how a 'black box service' is modifying its database by:

1) taking a snapshot of the database
2) executing an action in the 'black box service'
3) taking a second snapshot of the database
4) diff the snapshots

The resulting diff can be used:

* to analyze the changes the system is making
* as an 'expected' test file when re-writing the existing system

## Example

```bash
# Save the connection string to the `dbconfig.yml` file
echo "connectionString: Data Source=localhost;Initial Catalog=testdb;User ID=sa;Password=Pass@word" > ./workspaces/testdb/dbconfig.yml

# Run `schema` to export the database schema to the `schema.yml` file
db-data-diffr schema -w ./workspaces/testdb

# Run `snapshot` to export the data from the database
# `--name` is the snapshot name
db-data-diffr snapshot --name 02 -w ../workspaces/testdb

# Run `diff` to diff between two snapshots
db-data-diffr diff --name workflow-test --start-snapshot 01 --end-snapshot 02 -w ../workspaces/testdb
```

### Phases

* Prep
  * Extract Database 'schema'/table structure to YML file

* Snapshot
  * Folder for a PASS that holds a set of snapshots
  * Each snapshot contains a file for each table in the database
  * Each file is a CSV file containing the table's data

* Diff
  * Compare either:
    * PASS0/Snapshot0 to PASS0/Snapshot1 - to compare initial snapshot to second
    * PASS0/SnapshotN to PASS0/SnapshotN+M - to compare snapshot N to a later snapshot
    * PASS0/Snapshot0 to PASS0/SnapshotN - to compare initial snapshot to last snapshot

## Clean Module

The 'Clean' module is a utility to help enable Database DevOps in Containers. What does that mean? Rob Rich's [presentation](https://robrich.org/presentation/2019/10/12/database-devops-with-containers.aspx) outlines the need to take a Production database and anonymize, sanitize, and shrink the data within it, so that developers can have robust datasets to develop and test against.

The aim of this module is to provide a set of tools to:

* Reduce the quantity of data by:
  * Truncating unused tables (`truncate` action)
  * Removing any records that fit a condition. Ex: older than 6 months (`cull` action)
  * Reduce the total volume of records. Ex: remove every third record
    * Look at also removing 'downstream' columns in other tables - meaning remove child items from other tables with the same primary key
* Anonymize and Sanitize data sets
  * Convert PII (Personal Info) to anonymous but realistic data
  * Convert Credit Cards

The operations in the Clean module aim to provide a deterministic process that will result in a database being cleaned the same way every time. This will improve automated testing efforts.

## Development

### Build

```bash
./build.sh
```

### Run Tests

```bash
cd workspaces/testdb
./run.sh
```

## Tasks

* Get `Clean` and `Anon` running end to end - with a changes actually executed
* Remove `Clean` and `Anon` excessive log messages
  * Change how logging is done within `Anon` module. Idea: On error, return record that contains everything needed to debug - similar to fcheck report
* Anon service - be able to run as library, not http service that falls over
* Add Tests
  * Clean Module
    * Fcheck verifies the database is up and schema is initialized
    * Runs a Clean task (Truncate), verifies the change has been correctly made
    * Checks the Cull task
    * Checks the Anon task
* Need Runtime yml config file containing
  * SQL Connection String

* Support a 'macro'/template syntax - inject data like `Date.Now()`
  * Problem: System is not deterministic then!
    * Possible solution: allow a set of variables to be supplied. One of those variables can be a 'now' value which can then be accessed by 'vars.now' in the macro scope

### Tasks to Consider

* Marketing: “Enables End-to-End DevOps and Automated Testing of Applications which rely on Databases."
* Checkout GitHub Actions, specifically [the Matrix example here](https://github.blog/2019-08-08-github-actions-now-supports-ci-cd/)
  * Steps - `use` clause could be helpful
  * Steps Serialization - have one object that can have a bunch of properties, which are verified in a processing step - and that step can be run independently via a shell command
  * Run command - to run a sql command
  * In a Run SQL command, allow macro/parameter support, like dates - only thing is then its not **as** deterministic
