using System.Net.Sockets;
using System.Net;
using Server.Messages;

namespace Server.ClientInfo;

public class UdpClientInfo  : ClientInfo1
{
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
    public string? Channel { get; set; }
    public ClientState State { get; set; }

    public UdpClient Client { get; set; }
    public IPEndPoint LocalEndPoint { get; set; }
    public IPEndPoint ClientEndPoint { get; set; }

    public Queue<Confirm> ConfirmQueue;
    public ushort MessageIdCounter;
    public ushort LastMsgId;

 
    public UdpClientInfo(UdpClient client, IPEndPoint clientEndPoint)
    {
        Client = client;
        ClientEndPoint = clientEndPoint;

        // Инициализация других свойств по умолчанию или в нужном состоянии
        DisplayName = null;
        Username = null;
        Channel = null;
        State = ClientState.Auth; // Например, устанавливаем начальное состояние подключения
        LastMsgId=0;
        ConfirmQueue = new Queue<Confirm>();
        MessageIdCounter = 0;
    }


}