using System.Text;
using System.Text.RegularExpressions;
namespace Server.Messages;

public class Auth : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.AUTH;
    
    public ushort MsgId { get; init; }
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
    
    public  Auth(byte[] data)
    {
        Exception ex = new Exception("Wrong data");
        if (data == null || data.Length < 7) // Минимальный размер сообщения
        {
            throw ex;
        }
        
        MsgId = BitConverter.ToUInt16(data, 1);
        int offset = 3;

        string username = ReadNullTerminatedString(data, ref offset);
        string displayName = ReadNullTerminatedString(data, ref offset);
        string secret = ReadNullTerminatedString(data, ref offset);
        
        Username = username;
        DisplayName = displayName;
        Secret = secret;
        
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

    private string ReadNullTerminatedString(byte[] data, ref int offset)
    {
        int length = Array.IndexOf<byte>(data, 0, offset) - offset;
        if (length < 0)
        {
            throw new ArgumentException("Invalid null-terminated string format");
        }

        string str = Encoding.UTF8.GetString(data, offset, length);
        offset += length + 1; // Пропускаем нулевой терминатор
        return str;
    }
    
}