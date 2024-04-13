namespace Server.ClientInfo;


public enum ClientState
{
    Auth,
    Open,
    End,
    Error
}
public interface ClientInfo
{
    string DisplayName { get; set; }
    string Username { get; set; }
    string? Channel { get; set; }
    ClientState State { get; set; }
    
    Task SendMessageToChannel(string message,string channel,ClientInfo client); // Отправка сообщения в канал клиенту
}