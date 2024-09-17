# MqttOpcUaTimescaleDbInstanceChecker

### Prerequisites
+ Docker installed on the local machine
+ Install [Postgresql](https://www.postgresql.org/download/)
+ Install [timescaledb](https://docs.timescale.com/self-hosted/latest/install/installation-windows/#add-the-timescaledb-extension-to-your-database) in your operating system
+ Install [pglogical](https://github.com/2ndQuadrant/pglogical) in your operating system for replication

### To create a database for testing

+ Run following Queries by sorting

```
 CREATE DATABASE testdb;
```

```
CREATE EXTENSION IF NOT EXISTS timescaledb;
```
```
CREATE EXTENSION IF NOT EXISTS pgcrypto;
```
```
CREATE TABLE sensordata3 (
    sigmadata BYTEA,
    time TIMESTAMPTZ);
```
```
SELECT create_hypertable('sensordata3', 'time');
```
```
CREATE MATERIALIZED VIEW sensordata_aggregates3
WITH (timescaledb.continuous) AS
SELECT
time_bucket('5 minutes', time) AS bucket,
AVG(CAST(pgp_sym_decrypt(sigmadata::bytea, 'secret_key') AS numeric)) AS avg_data,
MIN(CAST(pgp_sym_decrypt(sigmadata::bytea, 'secret_key') AS numeric)) AS min_data,
MAX(CAST(pgp_sym_decrypt(sigmadata::bytea, 'secret_key') AS numeric)) AS max_data,
STDDEV(CAST(pgp_sym_decrypt(sigmadata::bytea, 'secret_key') AS numeric)) AS stddev_data
FROM
sensordata3
GROUP BY
bucket;
```

## How to run

+ Run the ``` docker compose up``` in the project directory.
