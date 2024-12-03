using Godot;
using System;
using System.Collections.Generic;
using System.Net;
using udpBase;

public abstract class NetObject
{
    public int id { get; set; }
    public PacketData[] typesTimesLog { get; set; }

    public Node clObject;

    public NetObject(int id, Node clObject)
    {
        this.id = id;
        this.clObject = clObject;
        typesTimesLog = GeneralFunctions.InitializePacketArrayPerType();
    }

    public NetObject(int id)
    {
        this.id = id;
        typesTimesLog = GeneralFunctions.InitializePacketArrayPerType();
    }

    public NetObject() {
        typesTimesLog = GeneralFunctions.InitializePacketArrayPerType();
    }
}
public class NetPlayer : NetObject
{
    public MockController mock;
    public CharacterBody3D mockBody;
    public Transform3D mockTransform;
    public NetPlayer(int id, Node clObject) : base(id, clObject) {}
    public NetPlayer(int id) : base(id) { }
}

public struct MovementLogEntry
{
    public int timeTag;
    public Vector3 playerPosition;
    public long serverTime;
}

public partial class ServerPlayer : NetObject
{
    public IPEndPoint endPoint;
    public PacketData[] tempTypesTimesLog;

    public List<MovementLogEntry> movementLog = new List<MovementLogEntry>();

    public Node dummy;
    public StaticBody3D dummyBody;
       
    private void InstantiateDummy()
    {
        dummyBody = dummy.GetChild<StaticBody3D>(0);
    }

    public ServerPlayer(int id, Node clObject, IPEndPoint endPoint) : base(id, clObject)
    {
        this.tempTypesTimesLog = GeneralFunctions.InitializePacketArrayPerType();
        this.endPoint = endPoint;
        movementLog = new List<MovementLogEntry>();
    }

    public ServerPlayer() : base() 
    {
        this.tempTypesTimesLog = GeneralFunctions.InitializePacketArrayPerType();
        movementLog = new List<MovementLogEntry>();
    }

    public ServerPlayer(IPEndPoint endpoint) : base()
    {
        this.tempTypesTimesLog = GeneralFunctions.InitializePacketArrayPerType();
        this.endPoint = endpoint;
        movementLog = new List<MovementLogEntry>();
    }
}

