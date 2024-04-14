using System.Net;
using System.Net.Sockets;
namespace Server.ClientInfo;

public class TcpClientInfo : ClientInfo1
{
    public string DisplayName { get; set; }
    public string Username { get; set; }
    public string? Channel { get; set; }
    public ClientState State { get; set; }
    
    // Добавляем ссылку на TcpClient для обмена данными с клиентом
    public TcpClient Client { get; set; }

    // Другие свойства, например IP-адрес и порт клиента
    public IPAddress ClientIpAddress { get; set; }
    public int ClientPort { get; set; }
    
    public NetworkStream Stream { get; set; }
    
    public TcpClientInfo(TcpClient client, IPAddress clientIpAddress, int clientPort)
    {
        Client = client;
        ClientIpAddress = clientIpAddress;
        ClientPort = clientPort;
        
        // Инициализация других свойств по умолчанию или в нужном состоянии
        DisplayName = null;
        Username = null;
        Channel = null;
        State = ClientState.Auth; // Например, устанавливаем начальное состояние подключения
    }
    

}