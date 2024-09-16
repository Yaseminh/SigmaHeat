
using MQTTnet;
using MQTTnet.Server;
using Serilog;
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using System.Numerics;
using System.Collections.Generic;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities.IO;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Paddings;

namespace MYLO_SERVER_MQTT
{
    class Program
    {
        // Counter to keep track of the number of messages processed
        private static int MessageCounter = 0;
        // Port on which the MQTT server will listen for incoming connections
        const int serverPort = 707;
        static void Main(string[] args)
        {
            // Set up logging configuration using Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();
            // Configure MQTT server options
            MqttServerOptionsBuilder options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint() // Use the default endpoint
                .WithDefaultEndpointPort(serverPort) // Set the server port
                .WithConnectionValidator(OnNewConnection) // Handle new connections
                .WithApplicationMessageInterceptor(OnNewMessage); // Handle new messages

            // Create and start the MQTT server
            IMqttServer mqttServer = new MqttFactory().CreateMqttServer();
            mqttServer.StartAsync(options.Build()).GetAwaiter().GetResult();
            // Get the server's IP address
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            string serverIP = string.Empty;

            for(int i = 0; i < host.AddressList.Length; i++)
            {
                if(host.AddressList[i].AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    serverIP = host.AddressList[i].ToString();
                    //serverIP = "";
                    break; // Exit loop after finding the correct IP address
                }
            }
            // Log the server IP and port information
            Log.Logger.Information(
                "Server IP: {ip}, PORT: {port}",
                serverIP,
                serverPort
                );
            // Keep the server running
            Console.ReadLine();
        }

        // Handle new client connections
        public static void OnNewConnection(MqttConnectionValidatorContext context)
        {
            Log.Logger.Information(
                    "New connection: ClientId = {clientId}, Endpoint = {endpoint}, CleanSession = {cleanSession}",
                    context.ClientId,
                    context.Endpoint,
                    context.CleanSession);
        }
        // Generate a new encryption key for AES
        public static byte[] GenerateEncryptionKey()
        {
            var keyGenerator = GeneratorUtilities.GetKeyGenerator("AES");
            keyGenerator.Init(new KeyGenerationParameters(new SecureRandom(), 256)); // 256-bit key
            return keyGenerator.GenerateKey();
        }
        // Encrypt the plaintext using AES
        public static string Encrypt(string plainText, byte[] key)
        {
            var engine = new AesEngine();// AES engine
            var blockCipher = new PaddedBufferedBlockCipher(engine, new Pkcs7Padding()); // Block cipher with PKCS7 padding
            var keyParam = new KeyParameter(key); // Key parameter
            blockCipher.Init(true, keyParam); // Initialize cipher for encryption
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(plainText); // Convert plaintext to byte array
            byte[] outputBytes = new byte[blockCipher.GetOutputSize(inputBytes.Length)];  // Create output byte array
            int length = blockCipher.ProcessBytes(inputBytes, 0, inputBytes.Length, outputBytes, 0); // Process the input bytes
            blockCipher.DoFinal(outputBytes, length);// Finalize encryption
            return Convert.ToBase64String(outputBytes);// Return the encrypted string as Base64
        }

        public static string Decrypt(string cipherText, byte[] key)
        {
            var engine = new AesEngine();// AES engine
            var blockCipher = new PaddedBufferedBlockCipher(engine, new Pkcs7Padding());// Block cipher with PKCS7 padding
            var keyParam = new KeyParameter(key);// Key parameter
            blockCipher.Init(false, keyParam); // Initialize cipher for decryption
            byte[] inputBytes = Convert.FromBase64String(cipherText);// Convert Base64 string to byte array
            byte[] outputBytes = new byte[blockCipher.GetOutputSize(inputBytes.Length)];// Create output byte array
            int length = blockCipher.ProcessBytes(inputBytes, 0, inputBytes.Length, outputBytes, 0);// Process the input bytes
            blockCipher.DoFinal(outputBytes, length);// Finalize decryption
            return System.Text.Encoding.UTF8.GetString(outputBytes).TrimEnd('\0');// Return the decrypted string
        }
        public static void OnNewMessage(MqttApplicationMessageInterceptorContext context)
        {
            // Get the payload from the incoming message
            var payload = context.ApplicationMessage?.Payload == null ? null : Encoding.UTF8.GetString(context.ApplicationMessage?.Payload);
            // String value to use as encryption key
            string keyString = "secret_key_12345";
            // Convert the string key to a byte array
            byte[] key = Encoding.UTF8.GetBytes(keyString);
            // Encrypt the payload
            string encrypted = Encrypt(payload, key);
            Console.WriteLine($"Encrypted: {encrypted}");
            // Decrypt the encrypted payload back to original
            string decrypted = Decrypt(encrypted, key);
            Console.WriteLine($"Decrypted: {decrypted}");
            // Log the beginning of the database connection
            Log.Logger.Information(
          "connection basliyor"
       
          );
            //for local
            //var connectionString = "Host=localhost;Port=5432;Username=postgres;Password=admin;Database=db1;";
            // Connection string for PostgreSQL (using Docker settings)
            var connectionString = "Host=host.docker.internal;Port=5432;Username=postgres;Password=admin;Database=db1;";


            using (var conn = new NpgsqlConnection(connectionString))
            {           
                conn.Open();// Open the database connection
                Log.Logger.Information("Connection established");
                // Insert the decrypted payload into the database with encryption
                using (var cmd = new NpgsqlCommand("INSERT INTO sensordata3 (time, sigmadata) VALUES (@time, pgp_sym_encrypt(@sigmadata, 'secret_key'))", conn))
                {
                   
                    cmd.Parameters.AddWithValue("time", DateTime.UtcNow);// Set the current time
                    cmd.Parameters.AddWithValue("sigmadata", decrypted);// Set the decrypted data
                    cmd.ExecuteNonQuery();// Execute the command
                }
                Log.Logger.Information(
       "connection bitiyor"

       );
                //using (var cmd = new NpgsqlCommand("SELECT time, pgp_sym_decrypt(sigmadata::bytea, 'secret_key') AS sigmadata FROM sensordata3", conn))
                //{
                //    using (var reader = cmd.ExecuteReader())
                //    {
                //        while (reader.Read())
                //        {
                //            DateTime time = reader.GetDateTime(0);  // 
                //            string decryptedData = reader.GetString(1);  //
                //            BigInteger data = BigInteger.Parse(decryptedData);                
                //            Console.WriteLine($"Time: {time}, Data: {data}");
                //        }
                //    }
                //}
            }
            // Increment the message counter
            MessageCounter++;
            // Log the message details
            Log.Logger.Information(
                "MessageId: {MessageCounter} - TimeStamp: {TimeStamp} -- Message: ClientId = {clientId}, Topic = {topic}, Payload = {payload}, QoS = {qos}, Retain-Flag = {retainFlag}",
                MessageCounter,
                DateTime.Now,
                context.ClientId,
                context.ApplicationMessage?.Topic,
                payload,
                context.ApplicationMessage?.QualityOfServiceLevel,
                context.ApplicationMessage?.Retain);
        }

    }
}
