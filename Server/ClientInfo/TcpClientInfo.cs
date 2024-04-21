using System.Net;
using System.Net.Sockets;
namespace Server.ClientInfo;

public class TcpClientInfo : ClientInfo1
{
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
    public string? Channel { get; set; }
    public ClientState State { get; set; }
    
    // Add a reference to TcpClient for data exchange with the client
    public TcpClient Client { get; set; }

    // Other properties such as client IP address and port
    public IPAddress ClientIpAddress { get; set; }
    public int ClientPort { get; set; }
    
    public CancellationTokenSource CancellationTokenSource;
    public CancellationToken CancellationToken;
    
    public NetworkStream Stream { get; set; }
    
    public TcpClientInfo(TcpClient client, IPAddress clientIpAddress, int clientPort)
    {
        Client = client;
        ClientIpAddress = clientIpAddress;
        ClientPort = clientPort;
        
        
        DisplayName = null;
        Username = null;
        Channel = null;
        State = ClientState.Auth; 
        
        CancellationTokenSource = new CancellationTokenSource();
        CancellationToken = CancellationTokenSource.Token;
    }
    

}