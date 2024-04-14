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
    
    public Reply(string messageContent,bool result,ushort refMessageId)
    {
        Result = result;
        MessageContent = messageContent;
        RefMessageId = refMessageId;
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
    public byte[] ToBytes(ushort id)
    {
        byte[] messageContentBytes = Encoding.UTF8.GetBytes(MessageContent);

        // Create an array to combine all bytes
        byte[] result = new byte[1 + 2 + 1 + 2 + messageContentBytes.Length + 1];

        result[0] = (byte)MessageType;

        byte[] messageIdBytes = BitConverter.GetBytes(id);
        Array.Copy(messageIdBytes, 0, result, 1, 2);

        result[3] = (byte)(Result ? 1 : 0);

        byte[] refMessageIdBytes = BitConverter.GetBytes(RefMessageId);
        Array.Copy(refMessageIdBytes, 0, result, 4, 2);

        int offset = 6;

        Array.Copy(messageContentBytes, 0, result, offset, messageContentBytes.Length);
        offset += messageContentBytes.Length;
        result[offset] = 0; // Null terminator after MessageContent

        return result;
    }
}