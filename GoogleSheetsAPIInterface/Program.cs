using System;
using System.Threading.Tasks;
using GoogleSheetsAPIInterface.Network;

namespace GoogleSheetsAPIInterface
{
    class Program
    {
        static void Main(string[] args) => new Program().StartAsync().GetAwaiter().GetResult();
        
        private async Task StartAsync()
        {
            Server _server = new Server();
            ushort port = 9990;
            await _server.StartServer(port);

            await Task.Delay(-1);
        }
    }
}
