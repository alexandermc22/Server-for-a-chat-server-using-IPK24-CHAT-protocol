using System.Text;
using System.Text.RegularExpressions;
namespace Server.Messages;

public class Err: IMessage
{
    public MessageType MessageType { get; set; } = MessageType.ERR;
    public  string DisplayName { get; set; } 
    public  string MessageContents { get; set; } 
    
    public  string ToTcpString( )
    {
        Exception ex = new Exception("Wrong input data");
        // Check the length of the channel identifier and channel name
        if (DisplayName.Length > 20 || MessageContents.Length > 1400)
        {
            throw new ArgumentException("MessageContents and Display Name cannot exceed 20 characters in length.");
        }
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(DisplayName, patternDname))
            throw ex;
        string pattern = @"^[\x20-\x7E\s]*$";
        if (!Regex.IsMatch(MessageContents, pattern))
            throw ex;
        // Build a string in the format "JOIN SP ID SP AS SP DNAME \r\n".
        return string.Format("ERR FROM {0} IS {1}\r\n", DisplayName, MessageContents);
    }
    
    public  Err(string[] words)
    {
        Exception ex = new Exception("Wrong data from server");
        if (words.Length != 5 )
            throw ex;
        if (words[1] != "FROM")
            throw ex;
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(words[2], patternDname))
            throw ex;
        
        if(words[3]!="IS")
            throw ex;
        if (words[4].Length > 1400)
            throw ex;

        string pattern = @"^[\x20-\x7E\s]*$";
        if (!Regex.IsMatch(words[3], pattern))
            throw ex;

        DisplayName = words[2];
        MessageContents = words[4];
    }

    public Err(string displayName, string messageContents)
    {
        DisplayName = displayName;
        MessageContents = messageContents;
    }
}