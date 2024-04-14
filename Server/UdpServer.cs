using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.ClientInfo;
using Server.Messages;

namespace Server;

public class UdpServer
{
    private int MaxRetries;
    private int UdpTimeout;
    public List<UdpClientInfo> clients;
    private bool isRunning;
    private IPEndPoint globalEndPoint;
    private TcpServer _tcpServer;
    public UdpClient udpListener;
    
    
    public UdpServer(IPAddress ip, int port, int maxRetries, int udpTimeout)
    {
        MaxRetries = maxRetries;
        UdpTimeout = udpTimeout;
        clients = new List<UdpClientInfo>();
        isRunning = false;
        globalEndPoint = new IPEndPoint(ip, port);
        udpListener = new UdpClient(globalEndPoint);
    }

    public async Task Start(TcpServer tcpServer)
    {
        _tcpServer = tcpServer;
        isRunning = true;
        Console.WriteLine("UDP Server started.");
        try
        {
            while (isRunning)
            {
                UdpReceiveResult result = await udpListener.ReceiveAsync();
                
                

                IPEndPoint clientEndPoint = result.RemoteEndPoint;
                IPEndPoint localEndPoint = new IPEndPoint(globalEndPoint.Address, 0);
                UdpClient client = new UdpClient(localEndPoint);
                UdpClientInfo clientInfo = new UdpClientInfo(client, clientEndPoint);
                clients.Add(clientInfo);
                if(result.Buffer[0]==(byte)MessageType.AUTH)
                    Console.WriteLine($"RECV {clientInfo.ClientEndPoint} | AUTH");
                SendConfirm(result.Buffer, clientInfo);
                clientInfo.LastMsgId =  BitConverter.ToUInt16(result.Buffer, 1);
                
                HandleClient(clientInfo,result.Buffer);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private async void HandleClient(UdpClientInfo clientInfo,byte[] buffer)
    {
        try
        {
            Console.WriteLine($"Client connected: {clientInfo.ClientEndPoint}");
            
            HandleAuthClient(clientInfo, buffer);
            
            bool isRunning = true;
            while (isRunning)
            {
                UdpReceiveResult result = await clientInfo.Client.ReceiveAsync();
                string msg="";
                switch (result.Buffer[0])
                {
                    case (byte)MessageType.MSG:
                        msg = "MSG";
                        break;
                    case (byte)MessageType.ERR:
                        msg = "ERR";
                        break;
                    case (byte)MessageType.BYE:
                        msg = "BYE";
                        break;
                    case (byte)MessageType.CONFIRM:
                        msg = "CONFIRM";
                        break;
                    case (byte)MessageType.JOIN:
                        msg = "JOIN";
                        break;
                    case (byte)MessageType.AUTH:
                        msg = "AUTH";
                        break;
                }   
                
                Console.WriteLine($"RECV {clientInfo.ClientEndPoint} | {msg}");
                
                if (result.Buffer[0] == (byte)MessageType.CONFIRM)
                {
                    
                    Confirm c = new Confirm(result.Buffer);
                    clientInfo.ConfirmQueue.Enqueue(c);
                    continue;
                }
                
                SendConfirm(result.Buffer, clientInfo);

                if (result.Buffer[0] == (byte)MessageType.BYE)
                {
                    break;
                }

                if (result.Buffer[0] == (byte)MessageType.ERR)
                {
                    Console.Error.WriteLine($"ERR from {clientInfo.DisplayName}");
                    byte[] bye = new byte[1 + 2];

                    bye[0] = 0xFF;

                    byte[] messageIdBytes = BitConverter.GetBytes(clientInfo.MessageIdCounter);
                    Array.Copy(messageIdBytes, 0, bye, 1, 2);
                    await clientInfo.Client.SendAsync(bye, bye.Length, clientInfo.ClientEndPoint);
                    break;
                }
                
                
                if (clientInfo.LastMsgId>=BitConverter.ToUInt16(result.Buffer, 1))
                    continue;
                clientInfo.LastMsgId =  BitConverter.ToUInt16(result.Buffer, 1);

                switch (clientInfo.State)
                {
                    case ClientState.Auth:
                         HandleAuthClient(clientInfo, result.Buffer);
                        break;
                    case ClientState.Open:
                         HandleOpenClient(clientInfo, result.Buffer);
                        break;
                }
                if (clientInfo.State==ClientState.End)
                    break;
            }
            Msg msgLeft = new Msg("Server", $"{clientInfo.DisplayName} has left {clientInfo.Channel}");
            SendMessageToChannel(msgLeft,clientInfo,false);
            
            _tcpServer.SendMessageToChannel(msgLeft, clientInfo, false);
            clientInfo.Client.Close();
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

    private async void HandleOpenClient(UdpClientInfo clientInfo, byte[] buffer)
    {
        try
        {
            switch (buffer[0])
            {
                case (byte)MessageType.JOIN:
                    Join join = new Join(buffer);
                    Msg msgLeft = new Msg("Server", $"{clientInfo.DisplayName} has left {clientInfo.Channel}");
                    SendMessageToChannel(msgLeft,clientInfo,false);
                    _tcpServer.SendMessageToChannel(msgLeft, clientInfo, false);
                    clientInfo.DisplayName = join.DisplayName;
                    clientInfo.Channel = join.ChannelId;
                    
                    Reply replyOk = new Reply("You're join to channel", true,BitConverter.ToUInt16(buffer, 1));
                    SendMessageAsync(replyOk.ToBytes(clientInfo.MessageIdCounter), clientInfo);
                    
                    Msg msgJoin = new Msg("Server", $"{clientInfo.DisplayName} has join {clientInfo.Channel}");
                    SendMessageToChannel(msgJoin,clientInfo,false);
                    _tcpServer.SendMessageToChannel(msgJoin, clientInfo, false);
                    break;
                
                case (byte)MessageType.MSG:
                    Msg msg = new Msg(buffer);
                    SendMessageToChannel(msg,clientInfo,false);
                    _tcpServer.SendMessageToChannel(msg, clientInfo, false);
                    break;
                default:
                    Err err = new Err("Server", "unknown data");
                    SendMessageAsync(err.ToBytes(clientInfo.MessageIdCounter), clientInfo);
                    byte[] bye = new byte[1 + 2];
                    bye[0] = 0xFF;
                    byte[] messageIdBytes = BitConverter.GetBytes(clientInfo.MessageIdCounter);
                    Array.Copy(messageIdBytes, 0, bye, 1, 2);
                    await clientInfo.Client.SendAsync(bye, bye.Length, clientInfo.ClientEndPoint);
                    clientInfo.State=ClientState.End;
                    break;
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return;
        }
    }

    public async void SendMessageToChannel(Msg msg, ClientInfo1 clientInfo, bool includeSender)
    {
        foreach (var client in clients)
        {
            if (client.Channel == clientInfo.Channel)
            {
                if(client.Username==clientInfo.Username && includeSender==false)
                    continue;

                SendMessageAsync(msg.ToBytes(client.MessageIdCounter),client);
            }
        }
    }


    private async void HandleAuthClient(UdpClientInfo clientInfo, byte[] buffer)
    {
        try
        {
        switch (buffer[0])
        {
            case (byte)MessageType.AUTH:
                Auth auth = new Auth(buffer);
                
                foreach (var client in clients)
                {
                    if (auth.Username == client.Username)
                    {
                        Reply replyNo = new Reply("You are already logged in", false,auth.MsgId);
                        SendMessageAsync(replyNo.ToBytes(clientInfo.MessageIdCounter),clientInfo); 
                        return;
                    }
                }
                foreach (var client in _tcpServer.clients)
                {
                    if (auth.Username == client.Username)
                    {
                        Reply replyNo = new Reply("Error: You are already logged in", false,auth.MsgId);
                        SendMessageAsync(replyNo.ToBytes(clientInfo.MessageIdCounter),clientInfo);
                        return;
                    }
                }
                
                Reply replyOk = new Reply("Success: You're logged in", true,auth.MsgId);
                SendMessageAsync(replyOk.ToBytes(clientInfo.MessageIdCounter),clientInfo); 
                clientInfo.Username = auth.Username;
                clientInfo.DisplayName = auth.DisplayName;
                clientInfo.State = ClientState.Open;
                clientInfo.Channel = "default";
                
                Msg msgJoin = new Msg("Server", $"{clientInfo.DisplayName} has join {clientInfo.Channel}");
                SendMessageToChannel(msgJoin,clientInfo,false);
                _tcpServer.SendMessageToChannel(msgJoin, clientInfo, false);
                break;
        }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.Message);
            return;
        }
        
    }
    
    
    private async void SendConfirm(byte[] message, UdpClientInfo clientInfo)
    {
        Console.WriteLine($"SENT {clientInfo.ClientEndPoint} | CONFIRM");
        Confirm confirm = new Confirm(BitConverter.ToUInt16(message, 1));
        byte[] confirmByte = confirm.ToBytes();
        await clientInfo.Client.SendAsync(confirmByte, confirmByte.Length, clientInfo.ClientEndPoint);
    }
    
    
     async void SendMessageAsync(byte[] message, UdpClientInfo clientInfo)
     {
         
         clientInfo.MessageIdCounter++;
        for (int i = 0; i < MaxRetries; i++)
        {
            string msg="";
            switch (message[0])
            {
                case (byte)MessageType.REPLY:
                    msg = "REPLY";
                    break;
                case (byte)MessageType.MSG:
                    msg = "MSG";
                    break;
                case (byte)MessageType.ERR:
                    msg = "ERR";
                    break;
                case (byte)MessageType.BYE:
                    msg = "BYE";
                    break;
            }   
            Console.WriteLine($"SENT {clientInfo.ClientEndPoint} | {msg}");
            // send message to server
            await clientInfo.Client.SendAsync(message, message.Length, clientInfo.ClientEndPoint);
            // wait confirm 
            await Task.Delay(UdpTimeout);
            while (clientInfo.ConfirmQueue.Count > 0) // use queue to receive confirm
            {
                Confirm confirm = clientInfo.ConfirmQueue.Dequeue();
                if (confirm.MessageId == BitConverter.ToUInt16(message, 1))
                {
                    return;
                }
            }

        }
        // if no response
        Console.Error.WriteLine($"ERR: No confirm from {clientInfo.ClientEndPoint}");
    }
}