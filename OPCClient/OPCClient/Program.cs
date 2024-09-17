using Opc.UaFx.Client;
using Npgsql;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using System.Text;
using System.Security.Cryptography.X509Certificates;

namespace OpcUa
{
    public class Program
    {
        public static string Encrypt(string plainText, byte[] key)
        {
            var engine = new AesEngine();
            var blockCipher = new PaddedBufferedBlockCipher(engine, new Pkcs7Padding());
            var keyParam = new KeyParameter(key);

            blockCipher.Init(true, keyParam);

            byte[] inputBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] outputBytes = new byte[blockCipher.GetOutputSize(inputBytes.Length)];

            int length = blockCipher.ProcessBytes(inputBytes, 0, inputBytes.Length, outputBytes, 0);
            blockCipher.DoFinal(outputBytes, length);

            return Convert.ToBase64String(outputBytes);
        }

        public static string Decrypt(string cipherText, byte[] key)
        {
            var engine = new AesEngine();
            var blockCipher = new PaddedBufferedBlockCipher(engine, new Pkcs7Padding());
            var keyParam = new KeyParameter(key);

            blockCipher.Init(false, keyParam);

            byte[] inputBytes = Convert.FromBase64String(cipherText);
            byte[] outputBytes = new byte[blockCipher.GetOutputSize(inputBytes.Length)];

            int length = blockCipher.ProcessBytes(inputBytes, 0, inputBytes.Length, outputBytes, 0);
            blockCipher.DoFinal(outputBytes, length);

            return Encoding.UTF8.GetString(outputBytes).TrimEnd('\0');
        }

        public static void Main()
        {
            // Sertifika dosya yollarını belirtin
            string certPath = "/app/myPrivateCert.pfx";
            string certPassword = "1234"; // Sertifika şifrenizi buraya girin

            // Sertifikayı oluşturun
            var certificate = new X509Certificate2(certPath, certPassword);

            string url = "https://host.docker.internal:4840/";
            string dp = "ns=2;s=Temperature";

            try
            {
                Console.WriteLine("OPC UA sample client started successfully.");
                Console.WriteLine("Trying to connect to: " + url);
                using (var client = new OpcClient(url))
                {
                    client.Certificate = certificate;
                    client.Connected += (s, e) => Console.WriteLine("Successfully connected to server!");
                    client.Disconnected += (s, e) => Console.WriteLine("Disconnected from server..");
                    client.Connect();

                    Console.WriteLine("Listening for datapoint: " + dp);
                    while (true)
                    {
                        var value = client.ReadNode(dp);
                        Console.WriteLine(DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss") + " - " + value);

                        string keyString = "secret_key_12345";
                        byte[] key = Encoding.UTF8.GetBytes(keyString);

                        string encrypted = Encrypt(value.ToString(), key);
                        Console.WriteLine($"Encrypted: {encrypted}");

                        string decrypted = Decrypt(encrypted, key);
                        Console.WriteLine($"Decrypted: {decrypted}");

                        var connectionString = "Host=4.236.179.131;Port=5432;Username=postgres;Password=admin;Database=postgres;";
                        using (var conn = new NpgsqlConnection(connectionString))
                        {
                            conn.Open();

                            using (var cmd = new NpgsqlCommand("INSERT INTO sensordata3 (time, sigmadata) VALUES (@time, pgp_sym_encrypt(@sigmadata, 'secret_key'))", conn))
                            {

                                cmd.Parameters.AddWithValue("time", DateTime.UtcNow);
                                cmd.Parameters.AddWithValue("sigmadata", decrypted);
                                cmd.ExecuteNonQuery();
                            }

                            // Terminate active connections to the source database
                            using (var terminateCmd = new NpgsqlCommand($@"
                                SELECT pg_terminate_backend(pg_stat_activity.pid)
                                FROM pg_stat_activity
                                WHERE pg_stat_activity.datname = 'postgres'
                                AND pid <> pg_backend_pid();", conn))
                            {
                                terminateCmd.ExecuteNonQuery();
                                Console.WriteLine("Terminated all active connections to the database.");
                            }

                            // Drop the existing cloned_db if it exists
                            using (var dropCmd = new NpgsqlCommand("DROP DATABASE IF EXISTS cloned_db;", conn))
                            {
                                dropCmd.ExecuteNonQuery();
                                Console.WriteLine("Dropped existing cloned_db.");
                            }

                            // Clone the database
                            using (var cmd = new NpgsqlCommand("CREATE DATABASE cloned_db WITH TEMPLATE postgres OWNER postgres;", conn))
                            {
                                cmd.ExecuteNonQuery();
                                Console.WriteLine("Database cloned at: " + DateTime.Now);
                            }

                            // 5 dakikalık dilimler için agregat işlemleri sorgusu
                            using (var cmd = new NpgsqlCommand(@"
                                SELECT
                                    time_bucket('5 minutes', time) AS bucket,
                                    AVG(CAST(pgp_sym_decrypt(sigmadata::bytea, 'secret_key') AS numeric)) AS avg_data,
                                    MIN(CAST(pgp_sym_decrypt(sigmadata::bytea, 'secret_key') AS numeric)) AS min_data,
                                    MAX(CAST(pgp_sym_decrypt(sigmadata::bytea, 'secret_key') AS numeric)) AS max_data,
                                    STDDEV(CAST(pgp_sym_decrypt(sigmadata::bytea, 'secret_key') AS numeric)) AS stddev_data
                                FROM
                                    sensordata3
                                GROUP BY
                                    bucket;", conn))
                            {
                                using (var reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        Console.WriteLine($"Bucket: {reader["bucket"]}, AVG: {reader["avg_data"]}, MIN: {reader["min_data"]}, MAX: {reader["max_data"]}, STDDEV: {reader["stddev_data"]}");
                                    }
                                }
                            }
                        }

                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                Console.WriteLine("Exiting..");
                Console.ReadKey();
            }
        }
    }
}
