using System.Text;
using System.Text.RegularExpressions;
namespace Server.Messages;

public class Auth : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.AUTH;
    public  string Username { get; set; }
    public  string DisplayName { get; set; }
    public  string Secret { get; set; }

    public Auth(string[] words)
    {
        Exception ex = new Exception("Wrong data");
        
        if(words.Length<6)
            throw ex;
        
        Username = words[1];
        DisplayName = words[3];
        Secret = words[5];
        
        if(words[2]!="AS" || words[4]!="USING")
            throw ex;
        
        if (Username.Length > 20 || DisplayName.Length > 20 || Secret.Length>128)
        {
            throw ex;
        }
        
        string patternId = @"^[a-zA-Z0-9\-]+$";
        if (!Regex.IsMatch(Username, patternId))
            throw ex;
        string patternDname = @"^[\x20-\x7E]*$";
        if (!Regex.IsMatch(DisplayName, patternDname))
            throw ex;
        if (!Regex.IsMatch(Secret, patternId))
            throw ex;
    }
    
}