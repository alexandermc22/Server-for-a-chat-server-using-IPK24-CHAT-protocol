using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Server.ClientInfo;

namespace Server;

public class UdpServer
{
    public List<UdpClientInfo> clients;
    private bool isRunning;
    
    public UdpServer(IPAddress ip, int port)
    {
        isRunning = false;
    }

    public async Task Start(TcpServer tcpServer)
    {
    }
}