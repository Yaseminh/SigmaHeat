Follow --> https://github.com/2ndQuadrant/pglogical

I used ubuntu 20.04

Download same version of pglogial ad postgres

Dowload pglogicalaccording to your Postgres version

Link

https://github.com/2ndQuadrant/pglogical/releases

After downloading you have to adjust your postgres.conf and pg_hba.conf


--------------pg_hba.conf-------------------------------------------

# "local" is for Unix do
 # TYPE  DATA

# "local" is for Unix domain socket connections only
 local   all             all                                     trust
# IPv4 local connections:
host    all             all             127.0.0.1/32            trust
# IPv6 local connections:
host    all             all             ::1/128                 trust                                                
host all all 4.236.179.131/32 trust # other machine’s IP
host all all 20.121.51.85/32 md5 
#add your local computer ip
#add your local computer ip
host all all  91.53.92.143/32 md5 # other machine’s IP

postgres.conf

# Add settings for extensions here
wal_level = 'logical'
max_worker_processes = 10   # one per database needed on provider node
                            # one per node needed on subscriber node
max_replication_slots = 10  # one per node needed on provider node
max_wal_senders = 10        # one per node needed on provider node
shared_preload_libraries = 'pglogical'
track_commit_timestamp = on
pglogical.conflict_resolution = 'last_update_wins'
listen_addresses = '*'

port = 5432



references
https://bonesmoses.org/2016/pg-phriday-perfectly-logical/


SQL COMMANDS FOR PGLOGICAL 

CREATE EXTENSION pglogical;

CREATE TABLE sensor_log (
  id            INT PRIMARY KEY NOT NULL,
  location      VARCHAR NOT NULL,
  reading       BIGINT NOT NULL,
  reading_date  TIMESTAMP NOT NULL
);

CREATE EXTENSION pglogical;

SELECT pglogical.create_node(
    node_name := 'sensor_warehouse',
    dsn := 'host=20.121.51.85 port=5432 dbname=postgres user=postgres password=admin'
);



SELECT pglogical.create_subscription(
    subscription_name := 'wh_sensor_data',
    replication_sets := array['logging'],
    provider_dsn := 'host=4.236.179.131 port=5432 dbname=postgres user=postgres password=admin'
);

SELECT pg_sleep(5);

SELECT count(*) FROM sensor_log;