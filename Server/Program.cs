using CommandLine;
using System.Net;

namespace Server;

public class Server
{
    public static Options? ProgramOptions { get; private set; }
    
    static async Task<int> Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                ProgramOptions = options;
            });
        
        if (ProgramOptions != null)
        {
            if (ProgramOptions.DisplayHelp)
            {
                Options.PrintHelp();
                return 0;
            }
            IPAddress? ip;
            if (IPAddress.TryParse(ProgramOptions.IpString, out ip))
            {
                
                ProgramOptions.Ip = ip;
                try
                {
                    TcpServer tcpServer = new TcpServer(ProgramOptions.Ip, ProgramOptions.Port);
                    UdpServer udpServer = new UdpServer(ProgramOptions.Ip, ProgramOptions.Port);
                
                    Task tcpTask = Task.Run(async () => await tcpServer.Start(udpServer));
                    //Task udpTask = Task.Run(async () => await udpServer.Start(tcpServer));
                
                    await Task.WhenAny(tcpTask);

                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    return -1;
                }
            }
            else
            {
                await Console.Error.WriteLineAsync("ERR: Wrong IP");
            }
            
        }
        else
        {
            // Если аргументы не были распарсены, выводим сообщение об ошибке
            await Console.Error.WriteLineAsync("Failed to parse arguments.");
        }
        return 0;
    }
}