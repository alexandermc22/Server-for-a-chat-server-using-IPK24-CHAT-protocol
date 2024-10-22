﻿namespace Server.Messages;

public class Confirm : IMessage
{
    public MessageType MessageType { get; set; } = MessageType.CONFIRM;
    public ushort MessageId { get; set; }
    
    public byte[] ToBytes()
    {
        // Create an array to combine all bytes
        byte[] result = new byte[1 + 2];
    
        result[0] = (byte)MessageType;
    
        byte[] messageIdBytes = BitConverter.GetBytes(MessageId);
        Array.Copy(messageIdBytes, 0, result, 1, 2);
    
        return result;
    }
    public  Confirm(byte[] data)
    {
        if (data.Length < 3)
        {
            throw new ArgumentException("Invalid data array", nameof(data));
        }
        
        ushort refMessageId = BitConverter.ToUInt16(data, 1);

        MessageType = MessageType.CONFIRM;
        MessageId = refMessageId;

    }
    
    public  Confirm(ushort messageId)
    {
        MessageType = MessageType.CONFIRM;
        MessageId = messageId;
    }
}