using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace GoogleSheetsAPIInterface.Network
{
    public class Server
    {
        TcpListener _server;
        List<Client> _connectedClients;
        bool running = false;


        public Server()
        {
            _connectedClients = new List<Client>();
            Console.WriteLine($"Created new Server instance");
            GoogleAPI.Instance().Setup().GetAwaiter().GetResult();
        }

        public async Task StartServer(ushort port)
        {
            _server = new TcpListener(IPAddress.Any,port);
            _server.Start();
            Console.WriteLine($"Server started on {port}");

            running = true;

            await ListenForConnections().ConfigureAwait(false);
        }

        private async Task ListenForConnections()
        {
            Console.WriteLine($"Listening for connections.");

            while (running)
            {
                TcpClient c = await _server.AcceptTcpClientAsync().ConfigureAwait(false);
                await AcceptConnectionAsync(c).ConfigureAwait(false);
            }
        }

        private async Task AcceptConnectionAsync(TcpClient client)
        {
            Console.WriteLine($"New connection made");

            int nextClientId = _connectedClients.Count == 0 ? 1 : _connectedClients.Max(x => x.Id) + 1;
            Client c = new Client(client, nextClientId);
            _connectedClients.Add(c);
            Console.WriteLine($"Client added to list with Id {nextClientId}");

            await c.StartReadingAsync().ConfigureAwait(false);
        }
        
        public void StopServer()
        {
            running = false;
            _server.Stop();
        }
    }
}
