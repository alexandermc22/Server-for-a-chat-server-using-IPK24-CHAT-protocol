﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
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
    
    // the function waits for incoming connections and processes them and clears the memory after itself.
    public async Task Start(TcpServer tcpServer,CancellationToken ct)
    {
        _tcpServer = tcpServer;
        isRunning = true;
        try
        {
            while (isRunning)
            {
                UdpReceiveResult res = await udpListener.ReceiveAsync(ct);
                HandleClient(res,res.Buffer);
            }
        }
        catch (OperationCanceledException)
        {
            byte[] bye = new byte[1 + 2];
            bye[0] = 0xFF;
            
            //cleanall
            foreach (var clientInfo in clients )
            {
                byte[] messageIdBytes = BitConverter.GetBytes(clientInfo.MessageIdCounter);
                Array.Copy(messageIdBytes, 0, bye, 1, 2);
                SendMessageAsync(bye, clientInfo);
                
            }
            await Task.Delay(MaxRetries * UdpTimeout*2);
            foreach (var clientInfo in clients )
            {
                clientInfo.Channel = null;
                clientInfo.CancellationTokenSource.Cancel();
            }
            
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            throw;
        }
    }
    
    // Main function that receives a message from the user, sends an acknowledgement and calls a task to process the message. 
    private async void HandleClient(UdpReceiveResult res,byte[] buffer)
    {
        
        IPEndPoint clientEndPoint = res.RemoteEndPoint;
        IPEndPoint localEndPoint = new IPEndPoint(globalEndPoint.Address, 0);
        UdpClient client = new UdpClient(localEndPoint);
        UdpClientInfo clientInfo = new UdpClientInfo(client, clientEndPoint);
        clients.Add(clientInfo);
        if(res.Buffer[0]==(byte)MessageType.AUTH)
            Console.WriteLine($"RECV {clientInfo.ClientEndPoint} | AUTH");
        SendConfirm(res.Buffer, clientInfo);
        clientInfo.LastMsgId =  BitConverter.ToUInt16(res.Buffer, 1);
        
        try
        {
            //Console.WriteLine($"Client connected: {clientInfo.ClientEndPoint}");
            
            
            HandleAuthClient(clientInfo, buffer);
            
            while (!clientInfo.CancellationToken.IsCancellationRequested)
            {
                UdpReceiveResult result = await clientInfo.Client.ReceiveAsync(clientInfo.CancellationToken);
                string msg;
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
                    default:
                        msg = "UNKNOWN";
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
                
                if (clientInfo.State==ClientState.End)
                    continue;
                
                
                
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
                
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            clientInfo.CancellationTokenSource.Cancel();
            //todo send error
            Console.Error.WriteLine($"Error handling client: {ex}");
        }
        finally
        {
            if (clientInfo.Channel != null)
            {
                Msg msgLeft = new Msg("Server", $"{clientInfo.DisplayName} has left {clientInfo.Channel}");
                Task a= SendMessageToChannel(msgLeft,clientInfo,false);
                Task b =  _tcpServer.SendMessageToChannel(msgLeft, clientInfo, false);
                await Task.WhenAll(a, b);
            }
            clientInfo.Client.Close();
            clients.Remove(clientInfo);
            
        }
        
    }
    
    // the function describes the work with the user at the stage when the user is authenticated and connected to the channel
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
                    if (join.DisplayName.Length > 20)
                        throw new Exception();
                    clientInfo.Channel = join.ChannelId;
                    
                    Reply replyOk = new Reply("You're join to channel", true,BitConverter.ToUInt16(buffer, 1));
                    SendMessageAsync(replyOk.ToBytes(clientInfo.MessageIdCounter), clientInfo);
                    
                    Msg msgJoin = new Msg("Server", $"{clientInfo.DisplayName} has join {clientInfo.Channel}");
                    SendMessageToChannel(msgJoin,clientInfo,false);
                    _tcpServer.SendMessageToChannel(msgJoin, clientInfo, false);
                    break;
                
                case (byte)MessageType.MSG:
                    Msg msg = new Msg(buffer);
                    clientInfo.DisplayName = msg.DisplayName;
                    if (msg.DisplayName.Length > 20)
                        throw new Exception();
                    SendMessageToChannel(msg,clientInfo,false);
                    _tcpServer.SendMessageToChannel(msg, clientInfo, false);
                    break;
                
                case (byte)MessageType.ERR:
                        Console.Error.WriteLine($"ERR from {clientInfo.DisplayName}");
                        byte[] bye = new byte[1 + 2];
                        bye[0] = 0xFF;
                        byte[] messageIdBytes = BitConverter.GetBytes(clientInfo.MessageIdCounter);
                        Array.Copy(messageIdBytes, 0, bye, 1, 2);
                        SendMessageAsync(bye, clientInfo);
                        await Task.Delay(MaxRetries * UdpTimeout);
                        clientInfo.CancellationTokenSource.Cancel();
                        break;
                case (byte)MessageType.BYE:
                    clientInfo.State = ClientState.End;
                    await Task.Delay(MaxRetries * UdpTimeout);
                    clientInfo.CancellationTokenSource.Cancel();
                        break;
                
                default:
                    Err err = new Err("Server", "unknown data");
                    SendMessageAsync(err.ToBytes(clientInfo.MessageIdCounter), clientInfo);
                    bye = new byte[1 + 2];
                    bye[0] = 0xFF;
                    messageIdBytes = BitConverter.GetBytes(clientInfo.MessageIdCounter);
                    Array.Copy(messageIdBytes, 0, bye, 1, 2);
                    SendMessageAsync(bye, clientInfo);
                    clientInfo.State=ClientState.End;
                    await Task.Delay(MaxRetries * UdpTimeout);
                    clientInfo.CancellationTokenSource.Cancel();
                    break;
            }
        }
        catch (Exception e)
        {
            clientInfo.State=ClientState.End;
            Console.Error.WriteLine(e.Message);
            Err err = new Err("Server", "unknown data");
            SendMessageAsync(err.ToBytes(clientInfo.MessageIdCounter), clientInfo);
            byte[] bye = new byte[1 + 2];
            bye[0] = 0xFF;
            byte[] messageIdBytes = BitConverter.GetBytes(clientInfo.MessageIdCounter);
            Array.Copy(messageIdBytes, 0, bye, 1, 2);
            SendMessageAsync(bye, clientInfo);
            await Task.Delay(MaxRetries * UdpTimeout);
            clientInfo.CancellationTokenSource.Cancel();
            return;
        }
    }
    // the function sends a message to all users in a certain channel
    public async Task SendMessageToChannel(Msg msg, ClientInfo1 clientInfo, bool includeSender)
    {
        try
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
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
        }
    }

    // the function describes how to work with the user at the authentication stage
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
                    if (auth.DisplayName.Length > 20)
                        throw new Exception();
                    clientInfo.State = ClientState.Open;
                    clientInfo.Channel = "default";
                    
                    Msg msgJoin = new Msg("Server", $"{clientInfo.DisplayName} has join {clientInfo.Channel}");
                    SendMessageToChannel(msgJoin,clientInfo,false);
                    _tcpServer.SendMessageToChannel(msgJoin, clientInfo, false);
                    break;
                
                case (byte)MessageType.BYE:
                    clientInfo.State = ClientState.End;
                    await Task.Delay(MaxRetries * UdpTimeout);
                    clientInfo.CancellationTokenSource.Cancel();
                    break;
                
                
            }
        }
        catch (Exception e)
        {
            clientInfo.State=ClientState.End;
            Console.Error.WriteLine(e.Message);
            Err err = new Err("Server", "unknown data");
            SendMessageAsync(err.ToBytes(clientInfo.MessageIdCounter), clientInfo);
            byte[] bye = new byte[1 + 2];
            bye[0] = 0xFF;
            byte[] messageIdBytes = BitConverter.GetBytes(clientInfo.MessageIdCounter);
            Array.Copy(messageIdBytes, 0, bye, 1, 2);
            SendMessageAsync(bye, clientInfo);
            await Task.Delay(MaxRetries * UdpTimeout);
            clientInfo.CancellationTokenSource.Cancel();
            return;
        }
        
    }
    
    
    // the function sends the confirmation message to the user
    private async void SendConfirm(byte[] message, UdpClientInfo clientInfo)
    {
        Console.WriteLine($"SENT {clientInfo.ClientEndPoint} | CONFIRM");
        Confirm confirm = new Confirm(BitConverter.ToUInt16(message, 1));
        byte[] confirmByte = confirm.ToBytes();
        await clientInfo.Client.SendAsync(confirmByte, confirmByte.Length, clientInfo.ClientEndPoint);
    }
    
    
    // the function sends the specified message to the user
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
                clientInfo.ConfirmQueue.Enqueue(confirm);
            }

        }
        // clientInfo.MessageIdCounter++;
        Console.Error.WriteLine($"ERR: No confirm from {clientInfo.ClientEndPoint}");
        byte[] bye = new byte[1 + 2];
        bye[0] = 0xFF;
        byte[] messageIdBytes = BitConverter.GetBytes(clientInfo.MessageIdCounter);
        Array.Copy(messageIdBytes, 0, bye, 1, 2);
            //await clientInfo.Client.SendAsync(bye, bye.Length, clientInfo.ClientEndPoint);
            try
            {
                for (int i = 0; i < MaxRetries; i++)
                {
                    Console.WriteLine($"SENT {clientInfo.ClientEndPoint} | BYE");
                    await clientInfo.Client.SendAsync(bye, bye.Length, clientInfo.ClientEndPoint);
                    // wait confirm 
                    await Task.Delay(UdpTimeout);
            
                    while (clientInfo.ConfirmQueue.Count > 0) // use queue to receive confirm
                    {
                        Confirm confirm = clientInfo.ConfirmQueue.Dequeue();
                        if (confirm.MessageId == BitConverter.ToUInt16(message, 1))
                        {
                            clientInfo.CancellationTokenSource.Cancel();
                            return;
                        }
                        clientInfo.ConfirmQueue.Enqueue(confirm);
                    }
                }
                clientInfo.CancellationTokenSource.Cancel();
            }
            catch (Exception e)
            {
                clientInfo.CancellationTokenSource.Cancel();
            }

    }
}