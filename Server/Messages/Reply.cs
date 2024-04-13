using System.Text;
using System.Text.RegularExpressions;

namespace Server.Messages;

public class Reply : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.REPLY;
    
    public bool Result { get; set; }
    
    public ushort RefMessageId { get; set; }
    
    public  string MessageContent { get; set; }

    public Reply(string messageContent,bool result)
    {
        Result = result;
        MessageContent = messageContent;
    }

    public string ToTcpString()
    {
        string answer;
        if (Result)
            answer = "OK";
        else
            answer = "NOK";
        string result = $"REPLY {answer} IS {MessageContent}\r\n";
        return result;
    }
}