namespace PocketCam.Core.Protocol;

public sealed class ProtocolException(string message) : IOException(message);

