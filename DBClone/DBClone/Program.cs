using System;
using System.Threading.Tasks;
using Npgsql;

class Program
{
    private static System.Timers.Timer _timer;

    static async Task Main(string[] args)
    {
        _timer = new System.Timers.Timer(1000); // 5 dakikada bir çalışacak (300000 ms = 5 dakika)
        _timer.Elapsed += async (sender, e) => await RunReplicationProcess();
        _timer.AutoReset = true;
        _timer.Enabled = true;

        Console.WriteLine("Replication process started. Press Enter to exit...");
        Console.ReadLine();
    }

    private static async Task RunReplicationProcess()
    {
        string publisherConnString = "Host=4.236.179.131;Port=5432;Username=postgres;Password=admin;Database=postgres;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=20;SSL Mode=Disable;";
        string subscriberConnString = "Host=20.121.51.85;Port=5432;Username=postgres;Password=admin;Database=postgres;SSL Mode=Disable;";

        using (var publisherConnection = new NpgsqlConnection(publisherConnString))
        using (var subscriberConnection = new NpgsqlConnection(subscriberConnString))
        {
            try
            {
                // İlk olarak Publisher veritabanı işlemlerini gerçekleştir
                await publisherConnection.OpenAsync();
                await CreateSensorLogTable(publisherConnection);
                await ConfigurePglogicalForPublisher(publisherConnection);

                // Publisher işlemleri tamamlandıktan sonra Subscriber işlemlerini gerçekleştir
                await subscriberConnection.OpenAsync();
                await ConfigurePglogicalForSubscriber(subscriberConnection);
                await CreateSubscriptionIfNotExists(subscriberConnection);

                // Verilerin başarılı şekilde kopyalanıp kopyalanmadığını kontrol et
                await CheckDataReplication(subscriberConnection);

                Console.WriteLine("Replication process completed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    // Publisher'da tablo oluşturma metodu
    private static async Task CreateSensorLogTable(NpgsqlConnection connection)
    {
        string createTableQuery = @"
        CREATE EXTENSION IF NOT EXISTS timescaledb;
        CREATE EXTENSION IF NOT EXISTS pgcrypto;

         CREATE TABLE IF NOT EXISTS sensordata3 (
        sigmadata BYTEA,
        time TIMESTAMPTZ
    );

        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM _timescaledb_catalog.hypertable WHERE table_name='sensordata3') THEN
                  PERFORM create_hypertable('sensordata3', 'time');
            END IF;
         END $$;";


        using (var command = new NpgsqlCommand(createTableQuery, connection))
        {
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("sensordata3 table created with TimescaleDB extensions.");
        }

        // Continuous aggregate view oluşturma
        string createMaterializedViewQuery = @"
        CREATE MATERIALIZED VIEW IF NOT EXISTS sensordata_aggregates3
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
            bucket;";

        using (var command = new NpgsqlCommand(createMaterializedViewQuery, connection))
        {
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("Continuous aggregate view sensordata_aggregates3 created.");
        }
    }

    // Publisher'da pglogical yapılandırma işlemleri
    private static async Task ConfigurePglogicalForPublisher(NpgsqlConnection connection)
    {
        string createExtensionQuery = "CREATE EXTENSION IF NOT EXISTS pglogical;";
        using (var command = new NpgsqlCommand(createExtensionQuery, connection))
        {
            await command.ExecuteNonQueryAsync();
        }


        // Create pglogical node
        string createNodeQuery = @"
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pglogical.node WHERE node_name = 'prod_sensors') THEN
                PERFORM pglogical.create_node(node_name := 'prod_sensors', dsn := 'host=4.236.179.131 port=5432 dbname=postgres user=postgres password=admin');
            END IF;
        END $$;";
        using (var command = new NpgsqlCommand(createNodeQuery, connection))
        {
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("pglogical node created in the publisher database.");
        }

        


        // Replikasyon setini kontrol et
        string checkReplicationSetQuery = "SELECT 1 FROM pglogical.replication_set WHERE set_name = 'sensorlogging';";

        using (var command = new NpgsqlCommand(checkReplicationSetQuery, connection))
        {
            var result = await command.ExecuteScalarAsync();

            // Eğer sonuç 1 değilse tabloyu replikasyon setine ekle
            if (result ==null)
            {
                Console.WriteLine("sensorlogging replication set does not exist, skipping table addition.");
            

            // Create new replication set
            string createReplicationSetQuery = @"
        DO $$
        BEGIN
            PERFORM pglogical.create_replication_set(
                set_name := 'sensorlogging',
                replicate_insert := TRUE,
                replicate_update := FALSE,
                replicate_delete := FALSE,
                replicate_truncate := FALSE
            );
        END $$;";
            using (var command1 = new NpgsqlCommand(createReplicationSetQuery, connection))
            {
                await command1.ExecuteNonQueryAsync();
                Console.WriteLine("Replication set created in the publisher database.");
            }
            Console.WriteLine("sensorlogging replication set exists, adding sensordata3 to the replication set.");

            // Tabloyu replikasyon setine ekle
            string addTableToReplicationSetQuery = @"
            DO $$
            BEGIN
                PERFORM pglogical.replication_set_add_table(
                    set_name := 'sensorlogging',
                    relation := 'sensordata3',
                    synchronize_data := TRUE
                );
            END $$;";

            using (var addCommand = new NpgsqlCommand(addTableToReplicationSetQuery, connection))
            {
                await addCommand.ExecuteNonQueryAsync();
                Console.WriteLine("sensordata3 table added to replication set.");
            }
        }
            
        }


        Console.WriteLine("sensor3 ayarlanmaya basladiktan sorra biti");
    }
 
    // Subscriber'da pglogical yapılandırma işlemleri
    private static async Task ConfigurePglogicalForSubscriber(NpgsqlConnection connection)
    {
        Console.WriteLine("subscriber basladi");
        string createTableQuery = @"
      CREATE TABLE IF NOT EXISTS sensordata3 (
        sigmadata BYTEA,
        time TIMESTAMPTZ
    );";

        using (var command = new NpgsqlCommand(createTableQuery, connection))
        {
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("sensor_log table checked/created in the subscriber database.");
        }

        string createExtensionQuery = "CREATE EXTENSION IF NOT EXISTS pglogical;";
        using (var command = new NpgsqlCommand(createExtensionQuery, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        

        // Create pglogical node
        string createNodeQuery = @"
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pglogical.node WHERE node_name = 'sensor_warehouse') THEN
                PERFORM pglogical.create_node(node_name := 'sensor_warehouse', dsn := 'host=20.121.51.85 port=5432 dbname=postgres user=postgres password=admin');
            END IF;
        END $$;";
        using (var command = new NpgsqlCommand(createNodeQuery, connection))
        {
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("pglogical node created in the subscriber database.");
        }
    }

    // Subscriber'da subscription kontrolü ve yoksa oluşturulması
    private static async Task CreateSubscriptionIfNotExists(NpgsqlConnection connection)
    {
       

        // Create new subscription
        string createSubscriptionQuery = @"
        DO $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM pglogical.subscription WHERE sub_name  = 'wh_sensor_data') THEN
                PERFORM pglogical.create_subscription(
                    subscription_name := 'wh_sensor_data',
                    provider_dsn := 'host=4.236.179.131 port=5432 dbname=postgres user=postgres password=admin',
                    replication_sets := ARRAY['sensorlogging']
                );
            END IF;
        END $$;";
        using (var command = new NpgsqlCommand(createSubscriptionQuery, connection))
        {
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("Subscription created in the subscriber database.");
        }

      
    }

    // Verilerin başarılı şekilde kopyalandığını kontrol etme
    private static async Task CheckDataReplication(NpgsqlConnection connection)
    {
        string checkQuery = "SELECT COUNT(*) FROM sensordata3;";
        using (var command = new NpgsqlCommand(checkQuery, connection))
        {
            var result = await command.ExecuteScalarAsync();
            Console.WriteLine($"Data replication check: {result} records found in the subscriber database.");
        }
    }
}
