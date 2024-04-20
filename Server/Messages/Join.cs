using System.Text;
using System.Text.RegularExpressions;

namespace Server.Messages;

public class Join : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.JOIN;
    public ushort MsgId { get; init; }
    public  string ChannelId  { get; set; }
    public  string DisplayName    { get; set; } 
    
    public Join(string[] words)
    {
        Exception ex = new Exception("Wrong data");
        if (words.Length != 4 )
            throw ex;
        if (words[1].Length > 20)
            throw ex;
        string patternId = @"^[a-zA-Z0-9\-.]+$";
        if (!Regex.IsMatch(words[1], patternId))
            throw ex;
        
        
        if(words[2]!="AS")
            throw ex;
        
        if (words[3].Length > 20)
            throw ex;
        
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(words[3], patternDname))
            throw ex;

        DisplayName = words[3];
        ChannelId = words[1];
    }
    public Join(byte[] bytes)
    {
        Exception ex = new Exception("Wrong data");
        int offset = 0;

        // MessageType (1 byte)
        offset += 1;

        // MessageId (2 bytes)
        MsgId = BitConverter.ToUInt16(bytes, offset);
        offset += 2;

        // ChannelId (variable length)
        int channelIdEnd = Array.IndexOf<byte>(bytes, 0, offset); // Find the null terminator
        ChannelId = Encoding.UTF8.GetString(bytes, offset, channelIdEnd - offset);
        offset = channelIdEnd + 1;

        // DisplayName (variable length)
        int displayNameEnd = Array.IndexOf<byte>(bytes, 0, offset); // Find the null terminator
        DisplayName = Encoding.UTF8.GetString(bytes, offset, displayNameEnd - offset);
        
        string patternId = @"^[a-zA-Z0-9\-.]+$";
        if (!Regex.IsMatch(ChannelId, patternId))
            throw ex;
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(DisplayName, patternDname))
            throw ex;
        
        if (DisplayName.Length > 20)
            throw ex;
        if (ChannelId.Length > 20)
            throw ex;
    }
    
}