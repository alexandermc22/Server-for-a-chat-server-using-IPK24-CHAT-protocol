using System.Text;
using System.Text.RegularExpressions;

namespace Server.Messages;

public class Join : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.JOIN;
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
    
}