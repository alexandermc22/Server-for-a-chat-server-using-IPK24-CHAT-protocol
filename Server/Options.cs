using CommandLine;
using System.Net;
namespace Server;

public class Options
{
    [Option('l', Required = false, Default = "0.0.0.0", HelpText = "Server1 IP address")]
    public string IpString { get; init; }

    [Option('p',  Required = false, Default =4567, HelpText = "Server1 port")]
    public int Port { get; set; }

    [Option('d', Required = false, Default =250, HelpText = "UDP acknowledgement timeout")]
    public int UdpTimeout { get; init; }

    [Option('r', Required = false, Default = 3, HelpText = "Maximum number of repeated transmissions UDP")]
    public int MaxRetries { get; init; }
    
    [Option('h',  Required = false, HelpText = "Prints the program help and terminates the program.")]
    public bool DisplayHelp { get; init; }
    
    public  IPAddress?  Ip { get; set; }

    public static void PrintHelp()
    {
        Console.WriteLine("Program help:");
        Console.WriteLine($"-l: Server1 IP address. Required");
        Console.WriteLine($"-p: Server1 port. Default 4567");
        Console.WriteLine($"-d: UDP acknowledgement timeout. Default 250");
        Console.WriteLine($"-r: Maximum number of repeated transmissions UDP. Default 3");
    }
}