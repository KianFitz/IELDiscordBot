using Microsoft.Extensions.Configuration;
using NLog;
using Shared;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Services
{
    public class GoogleApiService
    {
        private readonly IConfigurationRoot _config;
        private readonly TcpClient _client;
        private NetworkStream _stream;
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private int clientId;
        private byte[] _buffer;
        private Dictionary<int, Func<ByteBuffer, Task>> OpcodeHandlers;

        public GoogleApiService(IConfigurationRoot config)
        {
            _config = config;
            _client = new TcpClient();
            _buffer = new byte[4096];
            OpcodeHandlers = new Dictionary<int, Func<ByteBuffer, Task>>()
            {
                {Opcodes.SMSG_IDENTIFY_ACK, HandleIdentifyAck},
                {Opcodes.SMSG_REQUEST_ACK, HandleRequestAck },
                {Opcodes.SMSG_LEAGUE_RESPONSE, HandleLeagueResponse }
            };
        }

        private async Task HandleLeagueResponse(ByteBuffer arg)
        {
            currentLeague = arg.ReadString();
            responded = true;
        }

        private async Task HandleRequestAck(ByteBuffer arg)
        {
            await Task.Run(() =>
            {
                int errorCode = arg.ReadInt();
                _log.Info($"Server acknowledged request and sent response {errorCode}");
            });
        }

        private async Task HandleIdentifyAck(ByteBuffer arg)
        {
            await Task.Run(() =>
            {
                clientId = arg.ReadInt();
            });
        }

        public async Task ConnectToServer()
        {
            try
            {
                await _client.ConnectAsync("127.0.0.1", 9990);
                _stream = _client.GetStream();
                await SendIdentifyAsync().ConfigureAwait(false);
                await ReadDataFromServer().ConfigureAwait(false);
            }
            catch { }
        }

        private async Task SendIdentifyAsync()
        {
            ByteBuffer buff = new ByteBuffer(Opcodes.CMSG_IDENTIFY, 0);
            await SendDataToServer(buff.ToByteArray());
        }

        public async Task SendDataToServer(byte[] buffer, [CallerMemberName] string methodName = "")
        {
            if (_client.Connected)
            {
                await _stream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                await _stream.FlushAsync().ConfigureAwait(false);
                _log.Info($"Sent {buffer.Length} bytes to server from method {methodName}");
            }
            else
            {
                _log.Error($"{methodName} tried to send {buffer.Length} bytes to server, but client is not connected");
            }
        }

        private async Task ReadDataFromServer()
        {
            await StartReadingAsync().ConfigureAwait(false);
        }

        private async Task StartReadingAsync()
        {
            Console.WriteLine($"Client {clientId} started reading for data.");

            while (_client.Connected)
            {
                int bytesRead = await _stream.ReadAsync(_buffer, 0, _buffer.Length);
                if (bytesRead > 0)
                {
                    Console.WriteLine($"Read {bytesRead} from client {clientId}");
                    await ParseData().ConfigureAwait(false);

                    _buffer = new byte[4096];
                }

                await Task.Delay(100);
            }
        }

        private async Task ParseData()
        {
            byte[] arr = new byte[4096];
            Array.Copy(_buffer, arr, _buffer.Length);

            ByteBuffer buff = new ByteBuffer(arr);
            int opcode = buff.ReadInt();
            int length = buff.ReadInt();

            _log.Info($"Read {opcode} from server with length {length}");

            if (OpcodeHandlers.ContainsKey(opcode))
            {
                Console.WriteLine($"Found handler for {opcode}");
                await OpcodeHandlers[opcode].Invoke(buff).ConfigureAwait(false);
            }
        }

        internal async Task UpdateSpreadSheet(List<object> obj1, int row)
        {
            ByteBuffer buff = new ByteBuffer(Opcodes.CMSG_DSN_CALCULATION, 4 * 7);

            buff.WriteInt((int)obj1[0]);
            buff.WriteInt((int)obj1[1]);
            buff.WriteInt((int)obj1[2]);
            buff.WriteInt(0);
            buff.WriteInt((int)obj1[4]);
            buff.WriteInt((int)obj1[5]);
            buff.WriteInt((int)obj1[6]);
            buff.WriteInt(row);

            await SendDataToServer(buff.ToByteArray()).ConfigureAwait(false);
        }

        internal async Task<string> RequestLeagueAsync(string username)
        {
            ByteBuffer buff = new ByteBuffer(Opcodes.CMSG_REQUEST_LEAGUE, username.Length + 4);
            buff.WriteString(username);

            await SendDataToServer(buff.ToByteArray());
            
            while (responded is false)
            {
                await Task.Delay(100);
            }

            string retVal = currentLeague;
            currentLeague = "";
            responded = false;
            return retVal;
        }

        bool responded = false;
        string currentLeague = "";
    }
}
