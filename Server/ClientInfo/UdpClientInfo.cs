using System.Net.Sockets;
using System.Net;
namespace Server.ClientInfo;

public class UdpClientInfo  : ClientInfo
{
    public string DisplayName { get; set; }
    public string Username { get; set; }
    public string? Channel { get; set; }
    public ClientState State { get; set; }

    public UdpClient Client { get; set; }
    public IPEndPoint LocalEndPoint { get; set; }
    public IPEndPoint ClientEndPoint { get; set; }


 
    public UdpClientInfo(UdpClient client, IPEndPoint localEndPoint, IPEndPoint clientEndPoint,string login,string username)
    {
        Client = client;
        LocalEndPoint = localEndPoint;
        ClientEndPoint = clientEndPoint;

        // Инициализация других свойств по умолчанию или в нужном состоянии
        DisplayName = login;
        Username = username;
        Channel = null;
        State = ClientState.Auth; // Например, устанавливаем начальное состояние подключения
    }

    public Task HandleClient() // Обработка взаимодействия с клиентом
    {
        return Task.CompletedTask;
    }

    public Task SendMessageToChannel(string message, string channel, ClientInfo client)
    {
        return Task.CompletedTask;
    }
}