using System;

[Flags]
public enum NetInputBits : ushort
{
    None      = 0,
    Left      = 1 << 0,
    Right     = 1 << 1,
    Jump      = 1 << 2,
    Crouch    = 1 << 3,
    KeyTop    = 1 << 4,
    KeyBottom = 1 << 5,
    KeyLeft   = 1 << 6,
    KeyRight  = 1 << 7,
    TagSwitch = 1 << 8,
    Assist    = 1 << 9
}

public struct NetTickInput
{
    public int Tick;
    public ushort InputBitmask;
    public uint StateChecksum;
}

public enum NetMessageType : byte
{
    InputBundle = 1,
    FighterSelection = 2,
    ModScriptRequest = 3,
    ModScriptChunk = 4,
    ModScriptAck = 5,
    MatchStart = 6,
    Ping = 7,
    Pong = 8,
    DesyncNotice = 9
}
