using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.ClientInfo;
using System.Text;
using System.Threading.Channels;
using Server.Messages;
namespace Server;

public class TcpServer
{
    private TcpListener listener;
    private bool isRunning;
    public List<TcpClientInfo> clients;
    private UdpServer _udpServer;
    public TcpServer(IPAddress ip, int port)
    {
        listener = new TcpListener(ip, port);
        isRunning = false;
        clients = new List<TcpClientInfo>();
    }

    public async Task Start(UdpServer udpServer)
    {
        try
        {
            _udpServer = udpServer;
            listener.Start();
            isRunning = true;
            Console.WriteLine("TCP Server started.");

            while (isRunning)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                
                IPEndPoint clientEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;
                
                IPAddress clientIpAddress = clientEndPoint.Address;
                int clientPort = clientEndPoint.Port;
                
                TcpClientInfo clientInfo = new TcpClientInfo(client,clientIpAddress,clientPort);
                clients.Add(clientInfo);
                HandleClient(clientInfo,client);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }

    public void Stop()
    {
        isRunning = false;
        listener.Stop();
        Console.WriteLine("TCP Server stopped.");
    }

    private async void HandleClient(TcpClientInfo clientInfo,TcpClient client)
    {
        try
        {
            Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");

            NetworkStream stream = client.GetStream();
            clientInfo.Stream = stream;
            byte[] buffer = new byte[1600];
            int bytesRead;
            
            bool isRunning = true;
            while (isRunning)
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead <= 0)
                {
                    break; // Клиент закрыл соединение или отправил EOF
                }
                string receivedMessagesS = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                string[] receivedMessages = receivedMessagesS.Split("\r\n");
                foreach (string receivedMessage in receivedMessages)
                {
                    if(receivedMessage=="")
                        continue;
                    
                    string firstPart;
                    string secondPart;
                    string[] words;
                    int index = receivedMessage.IndexOf("IS", StringComparison.Ordinal);
                    // If substring "IS" is found
                    if (index != -1)
                    {
                        // Get the substring up to the first occurrence of "IS"
                        firstPart = receivedMessage.Substring(0, index + 2);
                        // Get the substring after the first occurrence of "IS"
                        secondPart = receivedMessage.Substring(index + 3);
                        words = firstPart.Split(' ');
                        Array.Resize(ref words, words.Length + 1);
                        words[words.Length - 1] = secondPart;
                    }
                    else
                    {
                        words = receivedMessage.Split(' ');
                    }
                    
                    switch (clientInfo.State)
                    {
                        case ClientState.Auth:
                            int result = await HandleAuthClient(stream, clientInfo, words);
                            break;
                        case ClientState.Open:
                            break;
                        case ClientState.Error:
                            break;
                        case ClientState.End:
                            break;
                    }
                    if(clientInfo.State == ClientState.End)
                        break;
                }
                if(clientInfo.State == ClientState.End)
                    break;
            }
            
            Msg msg = new Msg("Server", $"{clientInfo.DisplayName} has left {clientInfo.Channel}");
            SendMessageToChannel(msg,clientInfo,true);
            Console.WriteLine($"{clientInfo.DisplayName} has left {clientInfo.Channel}");
            client.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            clients.Remove(clientInfo);
        }
    }

    private async Task<int> HandleOpenClient(NetworkStream stream, TcpClientInfo clientInfo, string[] words)
    {
        try
        {
            switch (words[0])
            {
                case "JOIN":
                    
                    break;
                
                case "MSG":
                    
                    break;
                
                case "ERR":
                    
                    break;
                
                case "BYE":
                    
                    break;
                
                default:
                    //TODO: send err send bye
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 0;
        }

        return 0;
    }

    private async Task<int> HandleAuthClient(NetworkStream stream,TcpClientInfo clientInfo, string[] words)
    {
        
        try
        {
            if (words[0] == "AUTH")
            {
                
                Auth auth = new Auth(words);

                foreach (var client in clients)
                {
                    if (auth.Username == client.Username)
                    {
                        Reply replyNo = new Reply("Error: You are already logged in", false);
                        await SendMessageToUser(replyNo.ToTcpString(), stream, clientInfo);
                        return 0;
                    }
                }
                // foreach (var client in _udpServer.clients)
                // {
                //     if (auth.Username == client.Username)
                //     {
                //         Reply replyNo = new Reply("Error: You are already logged in", false);
                //         await SendMessageToUser(replyNo.ToTcpString(), stream, clientInfo);
                //         return 0;
                //     }
                // }
                
                Reply replyOk = new Reply("Success: You're logged in", true);
                await SendMessageToUser(replyOk.ToTcpString(), stream, clientInfo);
                clientInfo.Username = auth.Username;
                clientInfo.DisplayName = auth.DisplayName;
                clientInfo.State = ClientState.Open;
                clientInfo.Channel = "default";
                Msg msg = new Msg("Server", $"{clientInfo.DisplayName} has joined {clientInfo.Channel}");
                SendMessageToChannel(msg,clientInfo,true);

                return 0;
            }

            if (words[0] == "BYE")
                clientInfo.State = ClientState.End;


        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 0;
        }

        return 0;
    }
    
    private async Task SendMessageToChannel(Msg msg,TcpClientInfo clientInfo,bool includeSender)
    {
        foreach (var client in clients)
        {
            if (client.Channel == clientInfo.Channel)
            {
                if(client.Username==clientInfo.Username && includeSender==false)
                    continue;
                SendMessageToUser(msg.ToTcpString(), client.Stream, clientInfo);
            }
        }
    }
    


    private async Task SendMessageToUser(string message, NetworkStream stream,TcpClientInfo clientInfo)
    {
        try
        {
            string[] words = message.Split(' ');
            Console.WriteLine($"SENT {clientInfo.ClientIpAddress}:{clientInfo.ClientPort} | {words[0]}");
            // Конвертируем сообщение в байтовый массив
            byte[] buffer = Encoding.UTF8.GetBytes(message);

            // Отправляем данные по сетевому потоку
            await stream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error sending message: {ex.Message}");
        }
    }
}