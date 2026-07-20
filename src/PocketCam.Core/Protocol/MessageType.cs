namespace PocketCam.Core.Protocol;

public enum MessageType : byte
{
    Hello = 1,
    Frame = 2,
    Settings = 3,
    Ping = 4,
    Pong = 5,
    Status = 6,
    Error = 255,
}

