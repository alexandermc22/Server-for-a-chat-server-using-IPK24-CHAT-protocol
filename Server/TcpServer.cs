﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Resources;
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
                HandleClient();
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

    private async void HandleClient()
    {
        TcpClient client = await listener.AcceptTcpClientAsync();

        IPEndPoint clientEndPoint = (IPEndPoint)client.Client.RemoteEndPoint;

        IPAddress clientIpAddress = clientEndPoint.Address;
        int clientPort = clientEndPoint.Port;

        TcpClientInfo clientInfo = new TcpClientInfo(client, clientIpAddress, clientPort);
        clients.Add(clientInfo);

        try
        {
            Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");

            NetworkStream stream = client.GetStream();
            clientInfo.Stream = stream;
            byte[] buffer = new byte[1600];
            int bytesRead;
            int offset = 0;
            byte[] sequenceToFind = new byte[] { (byte)'\r', (byte)'\n' };
            bool isRunning = true;
            string receivedMessage;
            while (isRunning)
            {
                int ind = Array.IndexOf(buffer, sequenceToFind[0]);
                if (ind >= 0 && ind + 1 < buffer.Length && buffer[ind + 1] == sequenceToFind[1])
                {
                    Console.WriteLine("find \\r\\n ");
                    receivedMessage = Encoding.UTF8.GetString(buffer, 0, ind);

                    int remainingLength = 0;
                    for (int i = ind + 2; i < buffer.Length; i++)
                    {
                        if (buffer[i] != 0) // Если байт не нулевой, увеличиваем длину
                        {
                            remainingLength++;
                        }
                        else
                        {
                            break; // Если встретили нулевой байт, прекращаем подсчет
                        }
                    }

                    Array.Copy(buffer, ind + 2, buffer, 0,
                        remainingLength); // Копируем данные после \r\n в начало массива
                    Array.Clear(buffer, remainingLength, buffer.Length - remainingLength);
                    // Устанавливаем новое значение для offset
                    offset = remainingLength;
                }
                else
                {
                    if (buffer.Length == offset)
                    {
                        Err err = new Err("Server", "So long message");
                        SendMessageToUser(err.ToTcpString(), stream, clientInfo);
                        SendMessageToUser("BYE\r\n", stream, clientInfo);
                        break;
                    }
            
                    Console.WriteLine("not f \\r\\n.");
                    bytesRead = await stream.ReadAsync(buffer, offset, buffer.Length - offset);
                    offset += bytesRead;
                    if (bytesRead <= 0)
                    {
                        break; // Клиент закрыл соединение или отправил EOF
                    }
                    continue;
                }

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

                Console.WriteLine($"RECV {clientInfo.ClientIpAddress}:{clientInfo.ClientPort} | {words[0]}");
                switch (clientInfo.State)
                {
                    case ClientState.Auth:
                        int result = await HandleAuthClient(stream, clientInfo, words);
                        break;
                    case ClientState.Open:
                        result = await HandleOpenClient(stream, clientInfo, words);
                        break;
                    case ClientState.Error:
                        break;
                    case ClientState.End:
                        break;
                }

                if (clientInfo.State == ClientState.End)
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            if (clientInfo.Channel != null)
            {
                Msg msg = new Msg("Server", $"{clientInfo.DisplayName} has left {clientInfo.Channel}");
                SendMessageToChannel(msg, clientInfo, false);
                _udpServer.SendMessageToChannel(msg, clientInfo, false);
                Console.Error.WriteLine($"{clientInfo.DisplayName} has left {clientInfo.Channel}");
            }

            client.Close();
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
                    Join join = new Join(words);
                    Msg msgLeft = new Msg("Server", $"{clientInfo.DisplayName} has left {clientInfo.Channel}");
                    SendMessageToChannel(msgLeft, clientInfo, false);
                    _udpServer.SendMessageToChannel(msgLeft, clientInfo, false);

                    clientInfo.DisplayName = join.DisplayName;
                    clientInfo.Channel = join.ChannelId;

                    Reply replyOk = new Reply("You're join to channel", true);
                    await SendMessageToUser(replyOk.ToTcpString(), stream, clientInfo);

                    Msg msgJoin = new Msg("Server", $"{clientInfo.DisplayName} has join {clientInfo.Channel}");
                    SendMessageToChannel(msgJoin, clientInfo, false);
                    _udpServer.SendMessageToChannel(msgJoin, clientInfo, false);
                    break;

                case "MSG":
                    Msg msg = new Msg(words);
                    clientInfo.DisplayName = msg.DisplayName;
                    SendMessageToChannel(msg, clientInfo, false);
                    _udpServer.SendMessageToChannel(msg, clientInfo, false);
                    break;

                case "ERR":
                    Err err = new Err(words);
                    Console.Error.WriteLine($"ERROR FROM {err.DisplayName}: {err.MessageContents}");
                    SendMessageToUser("BYE\r\n", stream, clientInfo);
                    clientInfo.State = ClientState.End;
                    break;

                case "BYE":
                    clientInfo.State = ClientState.End;
                    break;

                default:
                    err = new Err("Server", "Unknown message");
                    SendMessageToUser(err.ToTcpString(), stream, clientInfo);
                    SendMessageToUser("BYE\r\n", stream, clientInfo);
                    clientInfo.State = ClientState.End;
                    break;
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            clientInfo.State = ClientState.End;
            Err err = new Err("Server", "Unknown message");
            SendMessageToUser(err.ToTcpString(), stream, clientInfo);
            SendMessageToUser("BYE\r\n", stream, clientInfo);
            return 0;
        }

        return 0;
    }

    private async Task<int> HandleAuthClient(NetworkStream stream, TcpClientInfo clientInfo, string[] words)
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
                        Reply replyNo = new Reply("You are already logged in", false);
                        await SendMessageToUser(replyNo.ToTcpString(), stream, clientInfo);
                        return 0;
                    }
                }

                foreach (var client in _udpServer.clients)
                {
                    if (auth.Username == client.Username)
                    {
                        Reply replyNo = new Reply("Error: You are already logged in", false);
                        await SendMessageToUser(replyNo.ToTcpString(), stream, clientInfo);
                        return 0;
                    }
                }

                Reply replyOk = new Reply("Success: You're logged in", true);
                await SendMessageToUser(replyOk.ToTcpString(), stream, clientInfo);
                clientInfo.Username = auth.Username;
                clientInfo.DisplayName = auth.DisplayName;
                clientInfo.State = ClientState.Open;
                clientInfo.Channel = "default";
                Msg msg = new Msg("Server", $"{clientInfo.DisplayName} has joined {clientInfo.Channel}");
                SendMessageToChannel(msg, clientInfo, false);
                _udpServer.SendMessageToChannel(msg, clientInfo, false);
                return 0;
            }

            if (words[0] == "BYE")
                clientInfo.State = ClientState.End;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            clientInfo.State = ClientState.End;
            Err err = new Err("Server", "Unknown message");
            SendMessageToUser(err.ToTcpString(), stream, clientInfo);
            SendMessageToUser("BYE\r\n", stream, clientInfo);
            return 0;
        }

        return 0;
    }

    public async Task SendMessageToChannel(Msg msg, ClientInfo1 clientInfo, bool includeSender)
    {
        try
        {
            foreach (var client in clients)
            {
                if (client.Channel == clientInfo.Channel)
                {
                    if (client.Username == clientInfo.Username && includeSender == false)
                        continue;
                    SendMessageToUser(msg.ToTcpString(), client.Stream, client);
                }
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }
    }


    private async Task SendMessageToUser(string message, NetworkStream stream, TcpClientInfo clientInfo)
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