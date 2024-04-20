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

                    // Подписываемся на событие нажатия Ctrl+C
                    Console.CancelKeyPress += (sender, eventArgs) =>
                    {
                        eventArgs.Cancel = true; // Отменяем действие по умолчанию (завершение программы)
                        cts.Cancel(); // Отменяем выполнение операции или цикла
                    };
                    CancellationTokenSource ctsTcp = new CancellationTokenSource();
                    CancellationTokenSource ctsUdp = new CancellationTokenSource();
                    TcpServer tcpServer = new TcpServer(ProgramOptions.Ip, ProgramOptions.Port);
                    UdpServer udpServer = new UdpServer(ProgramOptions.Ip, ProgramOptions.Port,
                        ProgramOptions.MaxRetries, ProgramOptions.UdpTimeout);

                    Task tcpTask = Task.Run(async () => await tcpServer.Start(udpServer,ctsUdp.Token));
                    Task udpTask = Task.Run(async () => await udpServer.Start(tcpServer,ctsTcp.Token));
                    Task completedTask = await Task.WhenAny(tcpTask, udpTask, Task.Delay(-1, cts.Token));
                    if (completedTask == tcpTask)
                    {
                    }
                    else if (completedTask == udpTask)
                    {
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
            // Если аргументы не были распарсены, выводим сообщение об ошибке
            await Console.Error.WriteLineAsync("Failed to parse arguments.");
        }

        return 0;
    }
}