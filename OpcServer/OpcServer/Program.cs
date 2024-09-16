using Opc.UaFx;
using Opc.UaFx.Server;
using Org.BouncyCastle.Crypto.Tls;
using Org.BouncyCastle.Ocsp;
using System.Security.Cryptography.X509Certificates;

namespace OpcUa
{

    public class Program
    {

        public static void Main()
        {
            try
            {
                string url = "https://localhost:4840/";
                var temperatureNode = new OpcDataVariableNode<double>("Temperature", 100.0);

                using (var server = new OpcServer(url, temperatureNode))
                {
                    // Sertifika dosya yollarını belirtin
                    string certPath = "/app/myPrivateCert.pfx";



                    // Sertifikayı oluşturun
                    var certificate = new X509Certificate2(certPath, "1234");
                    // Sertifikayı sunucuya ekleyin
                    server.Certificate = certificate;
                    server.Started += (s, e) => Console.WriteLine("Successfully started OPC-UA server!\nServer URL: " + url + "\nAvailable datapoint(s): " + temperatureNode.Id);
                    server.Start();
                    // foreach(var e in server.GetEndpoints()) Console.WriteLine(e.Url);

                    while (true)
                    {
                        if (temperatureNode.Value == 110)
                            temperatureNode.Value = 100;
                        else
                            temperatureNode.Value++;

                        temperatureNode.ApplyChanges(server.SystemContext);
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