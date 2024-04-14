namespace Server.ClientInfo;


public enum ClientState
{
    Auth,
    Open,
    End,
    Error
}
public interface ClientInfo1
{
    string DisplayName { get; set; }
    string Username { get; set; }
    string? Channel { get; set; }
    ClientState State { get; set; }
    

}