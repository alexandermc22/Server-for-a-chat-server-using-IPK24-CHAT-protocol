using CommandLine;
using System.Net;

namespace Server;

public class Server
{
    public static Options? ProgramOptions { get; private set; }

    static async Task<int> Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options => { ProgramOptions = options; });

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
                    CancellationTokenSource cts = new CancellationTokenSource();

                    // Subscribe to the event of pressing Ctrl+C
                    Console.CancelKeyPress += (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true; // Cancel the default action (program termination)
                        cts.Cancel(); // Cancel the operation or loop
                    };
                    // create cancel token for udp,tcp
                    CancellationTokenSource ctsTcp = new CancellationTokenSource();
                    CancellationTokenSource ctsUdp = new CancellationTokenSource();
                    
                    // create two class elements and start the processes
                    TcpServer tcpServer = new TcpServer(ProgramOptions.Ip, ProgramOptions.Port);
                    UdpServer udpServer = new UdpServer(ProgramOptions.Ip, ProgramOptions.Port,
                        ProgramOptions.MaxRetries, ProgramOptions.UdpTimeout);

                    Task tcpTask = Task.Run(async () => await tcpServer.Start(udpServer,ctsUdp.Token));
                    Task udpTask = Task.Run(async () => await udpServer.Start(tcpServer,ctsTcp.Token));
                    Task completedTask = await Task.WhenAny(tcpTask, udpTask, Task.Delay(-1, cts.Token));
                    // use the cancel token to terminate the program correctly
                    if (completedTask == tcpTask)
                    {
                        ctsUdp.Cancel();
                        await udpTask;
                    }
                    else if (completedTask == udpTask)
                    {
                        ctsTcp.Cancel();
                        await tcpTask;
                    }
                    else
                    {
                        ctsTcp.Cancel();
                        ctsUdp.Cancel();
                        await Task.WhenAll(tcpTask, udpTask);
                    }
                }
                catch (OperationCanceledException)
                {
                    return 0;
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
            // If the arguments were not parsed, print an error message
            await Console.Error.WriteLineAsync("Failed to parse arguments.");
        }

        return 0;
    }
}