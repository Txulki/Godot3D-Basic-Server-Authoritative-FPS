using Godot;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Xml.Linq;
using udpBase;

public partial class Server : Node
{
    UdpListener sv;

    bool running = false;
    int tick = 0;

    double updatePositionTimer = 0f;
    private async Task ListenForMessages()
    {
        while (true)
        { 
            Received received = await sv.Receive();

            sv.messagesInQueue.Add(received);
           // GD.Print("server:" + received.Message);

            if (received.Message == "quit")
                break;
        }
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{

        if(running)
        {
            if (GeneralFunctions.isTimerOverTimeoutDelta(ref sv.acknowledgementResendTimer, delta, 0.1f))
            {
                AcknowledgementUpdater();
                tick++;
            }

            if(GeneralFunctions.isTimerOverTimeoutDelta(ref updatePositionTimer, delta, 0.1f))
            {
                ReadMessageQueue();
                tick++;
                ServerFunctions.SendMovementUpdates(tick);
            }
        }
	}

    private void ReadMessageQueue()
    {
        if (sv.messagesInQueue.Count > 0)
        {
            ServerFunctions.CleanTempLastPacketArrays();
            List<Received> toRemove = new List<Received>();
            //There are messages awaiting to be read.
            foreach (Received awaitingMessage in sv.messagesInQueue)
            {
                toRemove.Add(awaitingMessage);
                ReadReceived(awaitingMessage.Message, awaitingMessage.Sender);
            }

            foreach (Received messageToRemove in toRemove)
            {
                sv.messagesInQueue.Remove(messageToRemove);
            }
            ServerFunctions.SetUpdateLastPacketArrays();
        }
    }

    private void AcknowledgementUpdater()
    {
        ServerFunctions.SendRepeatedWaitingAcknowledgement();
    }
 
    private void ReadReceived(string Message, IPEndPoint Sender)
    {
        Received received = new Received();
        received.Message = Message;
        received.Sender = Sender;

        bool loggedIn = false;

        //We receive the packet with format:
        //  PackageType  ;   TimeTag   ;  SpecificParts
        //      (int)    ;    (int)     ;   (string[])

        PacketData packet = PacketFunctions.TurnReceivedIntoPacketData(Message);

        //We got a PacketData struct with the following parameters:
        /* PACKET
         * 
         *  PacketType type -> We get it from the first header int. Determines the type of the message and its purpose.
         *  PacketCategory category -> Mainly for client, tells a client if it is a packet that carries other client's information (Or its own)
         *  string[] messageParts -> All MultipleUserDependent packets have as first item the ID of the client.
         * 
         */

        ServerPlayer serverPlayer = (ServerPlayer)ServerFunctions.GetPlayerFromEndpoint(Sender);
        if (serverPlayer == null) serverPlayer = new ServerPlayer(received.Sender);
        else {
            loggedIn = true;
        }

        PacketFunctions.AcknowledgeIfNeeded(packet);

        if (!ServerFunctions.IsPacketTimeValid(packet, serverPlayer.endPoint))
        {
            return;
        }

        switch (packet.type)
        {
            case PacketType.USER_LOGIN_REQUEST:
                if(!loggedIn)
                {
                    Node clObject = ResourceLoader.Load<PackedScene>("res://Scenes/playerServerMock.tscn").Instantiate();
                    GetTree().Root.AddChild(clObject);

                    Node dummy = ResourceLoader.Load<PackedScene>("res://Scenes/dummy.tscn").Instantiate();
                    GetTree().Root.AddChild(dummy);

                    ServerFunctions.LoginClient(ref serverPlayer, clObject, dummy);
                    PacketData toSend = PacketFunctions.CreatePacket(PacketType.USER_LOGIN_SERVER_RESPONSE, ref tick, serverPlayer.id.ToString());
                    PacketFunctions.Send(toSend, serverPlayer);

                    PacketData toSendToOtherUsers = PacketFunctions.CreatePacket(PacketType.USER_SERVER_SENT_DATA, ref tick, serverPlayer.id.ToString());
                    ServerFunctions.SendToAllBut(toSendToOtherUsers, serverPlayer);

                    ServerFunctions.SendAllPlayerDataTo(serverPlayer, tick);
                }
                break;
            case PacketType.MOVEMENT_SEND_INPUT:
                if(loggedIn)
                {
                    UpdatePlayerPositionLocally(serverPlayer, packet);
                }
                break;
            case PacketType.WEAPON_SHOOT:
                if(loggedIn)
                {
                    VerifyInteract(serverPlayer, packet);
                }
                break;
            case PacketType.ACKNOWLEDGEMENT:
                PacketFunctions.ProcessAcknowledgement(sv, packet);
                break;


            default:
                break;
        }

    }

    public void OnHostButtonPressed()
    {
        sv = new UdpListener();
        _ = ListenForMessages();
        ServerFunctions.server = sv;

        var scene = ResourceLoader.Load<PackedScene>("res://Scenes/world.tscn");
        GetTree().ChangeSceneToPacked(scene);

        running = true;
    }

    public void UpdatePlayerPositionLocally(ServerPlayer serverPlayer, PacketData packet)
    {
        serverPlayer.clObject.GetChild<CharacterBody3D>(0).Rotation = GeneralFunctions.getVector3FromString(packet.messageParts[1]);
        serverPlayer.clObject.GetChild<MockServerController>(0).CalculateMockTick(GeneralFunctions.getVector2FromString(packet.messageParts[0]), 0.1666f);
    }

    private void VerifyInteract(ServerPlayer serverPlayer, PacketData packet)
    {
        //GD.Print("Server player has amount of entries equal to: " + serverPlayer.movementLog.Count);
        for(int i = 0; i < serverPlayer.movementLog.Count; i++)
        {
            //GD.Print("CLIENT SEND ID: " + int.Parse(packet.messageParts[0]) + " SERVER STORED: " + serverPlayer.movementLog[i].timeTag);
            if (serverPlayer.movementLog[i].timeTag == int.Parse(packet.messageParts[0]))
            {
                CharacterBody3D playerBody = serverPlayer.clObject.GetChild<CharacterBody3D>(0);

                Vector3 startPosition = playerBody.GlobalPosition;
                Vector3 direction = GeneralFunctions.getVector3FromString(packet.messageParts[1]);

                foreach(ServerPlayer sp in sv.clientsInServer)
                {
                    if(sp != serverPlayer)
                    {
                        sp.dummyBody.GlobalPosition = sp.movementLog[i].playerPosition;
                        GD.Print("DUMMY POSITION: " + sp.dummyBody.GlobalPosition);
                        sp.dummyBody.ForceUpdateTransform();
                    }
                }

                // Calculate the end position for the ray
                float rayLength = 100f; // Define how far you want to shoot the ray
                Vector3 endPosition = startPosition + direction * rayLength;
                var rayQuery = PhysicsRayQueryParameters3D.Create(startPosition, endPosition);

                CheckRayResult(rayQuery);
            }
        }
    }

    private void CheckRayResult(PhysicsRayQueryParameters3D rayQuery)
    {
        // Create the raycast parameters
        var spaceState = GetViewport().World3D.DirectSpaceState;
        rayQuery.CollisionMask = 2;

        // Perform the raycast
        var result = spaceState.IntersectRay(rayQuery);
        // Check for collisions
        if (result.Count > 0)
        {
            Node collider = (Node)result["collider"];
            GD.Print(collider.Name);
            if ((collider.Name == "DummyBody") || (collider.Name == "Dummy"))
            {
                foreach(ServerPlayer sp in sv.clientsInServer)
                {
                    if(sp.dummyBody == collider)
                    {
                        GD.Print("SERVER HIT");
                        //int damage = sp.weapon.damage;
                        PacketData hitPacket = PacketFunctions.CreatePacket(PacketType.WEAPON_HIT, ref tick, 5.ToString());
                        PacketFunctions.Send(hitPacket, sp);
                    }
                }
            }
        }
        foreach (ServerPlayer sp in sv.clientsInServer)
        {
            sp.dummyBody.GlobalPosition = new Vector3(0, -25, 0);
        }
    }
}
