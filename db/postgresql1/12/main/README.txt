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

local   all             all                                     trust

# IPv4 local connections:
host    all             all             127.0.0.1/32            trust
# IPv6 local connections:
host    all             all             ::1/128                 trust
host all all 4.236.179.131/32 trust # other machine’s IP
host all all 20.121.51.85/32 trust # this machine’s IP

host    all             all             0.0.0.0/0               md5

#add your local computer ip
host all all  91.53.92.143/32 md5 # other machine’s IP


postgres.conf

# Add settings for extensions here

listen_addresses = '*'
port = 5432

wal_level = 'logical'
max_worker_processes = 10   # one per database needed on provider node
                            # one per node needed on subscriber node
max_replication_slots = 10  # one per node needed on provider node
max_wal_senders = 10        # one per node needed on provider node
shared_preload_libraries = 'pglogical'
track_commit_timestamp = on

pglogical.conflict_resolution = 'last_update_wins'



references
https://bonesmoses.org/2016/pg-phriday-perfectly-logical/


SQL COMMANDS FOR PGLOGICAL 


CREATE TABLE sensor_log (
  id            SERIAL PRIMARY KEY NOT NULL,
  location      VARCHAR NOT NULL,
  reading       BIGINT NOT NULL,
  reading_date  TIMESTAMP NOT NULL
);

INSERT INTO sensor_log (location, reading, reading_date)
SELECT s.id % 1000, s.id % 100,
       CURRENT_DATE - (s.id || 's')::INTERVAL
  FROM generate_series(1, 1000000) s(id);


CREATE EXTENSION pglogical;


 SELECT pglogical.create_node(
    node_name := 'prod_sensors',
    dsn := 'host=4.236.179.131 port=5432 dbname=postgres'
);

SELECT pglogical.create_replication_set(
    set_name := 'logging',
    replicate_insert := TRUE, replicate_update := FALSE,
    replicate_delete := FALSE, replicate_truncate := FALSE
);


SELECT pglogical.replication_set_add_table(
    set_name := 'logging', relation := 'sensor_log',
    synchronize_data := TRUE
);



select * from pglogical.show_subscription_status();