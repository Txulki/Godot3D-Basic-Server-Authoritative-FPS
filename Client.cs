using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Mail;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using udpBase;

public partial class Client : Node
{
    private UdpUser user;

    //Connection status
    bool loggedIn = false;
    bool attemptConnect = false;

    private static int id;
    private Node playerObj;
    private CharacterBody3D playerBody;
    private PlayerController playerController;

    private int serverLastKnownMovementTag = 0;
    private int hp = 100;

    public List<PacketData> movementLog = new List<PacketData>();

    private Node HUD;

    public void ConnectClient()
    {
        user = UdpUser.ConnectTo("127.0.0.1", 27016);
        ClientFunctions.user = user;

        _ = ListenForMessages();

        attemptConnect = true;
    }


    private async Task ListenForMessages()
    {
        while (true)
        {
            try
            {
                var received = await user.Receive();

                user.messagesInQueue.Add(received);

                if (received.Message.Contains("quit"))
                    break;
            }
            catch (Exception ex)
            {
                GD.Print(ex.Message);
            }
        }
    }

    private void ReadReceived(string Message)
    {
        //We receive the packet with format:
        //  PackageType  ;   TimeTag   ;  SpecificParts
        //      (int)    ;    (int)     ;   (string[])

        PacketData packet = PacketFunctions.TurnReceivedIntoPacketData(Message); //Process packet parts

        //We got a PacketData struct with the following parameters:
        /* PACKET
         * 
         *  PacketType type -> We get it from the first header int. Determines the type of the message and its purpose.
         *  PacketCategory category -> Mainly for client, tells a client if it is a packet that carries other client's information (Or its own)
         *  string[] messageParts -> All MultipleUserDependent packets have as first item the ID of the client.
         * 
         */
        
        PacketFunctions.AcknowledgeIfNeeded(packet); //Send acknowledgement if server needs it.

        if (!ClientFunctions.IsPacketTimeValid(packet)) return; //Check if the time ID is correct

        switch(packet.category) //We check the packet category
        {
            case PacketCategory.MULTIPLE_USER_DEPENDENT: //For packets about other clients (Has a client ID in MessageParts[0])
                ReadMultipleUserDependentPacket(packet);
                break;
            case PacketCategory.SINGLE_USER_DEPENDENT: //Other types of packets that do not include other client IDs nor info.
                ReadSingleUserDependentPacket(packet);
                break;
        }
    }

    private void ReadSingleUserDependentPacket(PacketData packet)
    {
        switch(packet.type)
        {
            case PacketType.USER_LOGIN_SERVER_RESPONSE:
                VerifiedClientStart();
                id = int.Parse(packet.messageParts[0]);
                loggedIn = true;
                break;

            case PacketType.ACKNOWLEDGEMENT:
                PacketFunctions.ProcessAcknowledgement(user, packet);
                break;

            /*case PacketType.WEAPON_HIT:
                HitByBullet(int.Parse(packet.messageParts[0]));
                break;*/
        }
    }

    private void ReadMultipleUserDependentPacket(PacketData packet)
    {
        NetPlayer netPlayer = (NetPlayer)ClientFunctions.GetPlayerFromNetuserPacketOrReturnPlaceholder(packet);
        bool netPlayerRegistered = (netPlayer != null);

        switch(packet.type)
        {
            case PacketType.MOVEMENT_SEND_AUTHORITY_POSITION:
                if(netPlayerRegistered){
                    MovementUpdateProcess(packet);
                }  
                break;

            case PacketType.USER_SERVER_SENT_DATA:
                if(netPlayer.clObject == null)
                {
                    InstantiateNewMockForPlayer(ref netPlayer);
                }
                break;
        }
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
        if(loggedIn || attemptConnect)
        {
            ReadMessageQueue();

            if (GeneralFunctions.isTimerOverTimeoutDelta(ref user.acknowledgementResendTimer, delta, 0.1f))
            {
                AcknowledgementUpdater();

                if (attemptConnect && !loggedIn)
                {
                    AttemptLogin();
                }
            }
        }

	}

    private void ReadMessageQueue()
    {
        if (user.messagesInQueue.Count > 0)
        {
            List<Received> toRemove = new List<Received>();
            //There are messages awaiting to be read.
            foreach (Received awaitingMessage in user.messagesInQueue)
            {
                toRemove.Add(awaitingMessage);
                ReadReceived(awaitingMessage.Message);
            }

            foreach(Received messageToRemove in toRemove)
            {
                user.messagesInQueue.Remove(messageToRemove);
            }
        }
    }

    private void AttemptLogin()
    {
        PacketData loginPacket = PacketFunctions.CreatePacket(PacketType.USER_LOGIN_REQUEST, ref user.tick, "");
        PacketFunctions.Send(loginPacket);
        GD.Print("Client: Attempting logging");
    }

    private void AcknowledgementUpdater()
    {
        ClientFunctions.SendRepeatedWaitingAcknowledgement();
    }

    private void VerifiedClientStart()
    {
        GD.Print("Client verified");

        var scene = ResourceLoader.Load<PackedScene>("res://Scenes/world.tscn");
        GetTree().ChangeSceneToPacked(scene);

        /*Node changedScene = GetTree().CurrentScene;
        GD.Print(changedScene);*/

        Node player = ResourceLoader.Load<PackedScene>("res://Scenes/player.tscn").Instantiate();
        GetTree().Root.AddChild(player);

        playerObj = player;
        playerBody = playerObj.GetChild<CharacterBody3D>(0);
        playerController = playerObj.GetChild<PlayerController>(0);
        playerController.cl = this;

        HUD = ResourceLoader.Load<PackedScene>("res://Scenes/hud.tscn").Instantiate();
        GetTree().Root.AddChild(HUD);
    }

    private void InstantiateNewMockForPlayer(ref NetPlayer netPlayer)
    {
        GD.Print("Other player verified");

        
        Node playerMock = ResourceLoader.Load<PackedScene>("res://Scenes/playerMock.tscn").Instantiate();
        GetTree().Root.AddChild(playerMock);

        netPlayer.clObject = playerMock;
        netPlayer.mock = playerMock.GetChild<MockController>(0);
        netPlayer.mockBody = playerMock.GetChild<CharacterBody3D>(0);
        netPlayer.mockTransform = netPlayer.mock.Transform;
        PacketFunctions.LogNewNetPlayer(netPlayer);
    }

    public void MovementUpdateProcess(PacketData packet)
    {
        int playerId = int.Parse(packet.messageParts[0]);
        Vector3 newPosition = GeneralFunctions.getVector3FromString(packet.messageParts[1]);

        if (playerId == id)
        {
            serverLastKnownMovementTag = int.Parse(packet.messageParts[3]);
            MovementLogRemovePastEntries(serverLastKnownMovementTag);

            Vector3 newForcedPosition = newPosition;
            MovementLogTickAllEntries(newForcedPosition);
        }
        else
        {
            (bool found, NetObject netObject) =  PacketFunctions.GetClientFromID(playerId);
            NetPlayer clData = (NetPlayer)netObject;
            if(found)
            {
                
                MockController mock = clData.mock;
                CharacterBody3D mockBody = clData.mockBody;
                Vector3 oldPosition = mock.previousAuthorityPosition;
                Vector3 oldRotation = mock.previousAuthorityRotation;
                mockBody.CallDeferred("set", "global_position", oldPosition);

                //Set new rotation
                Vector3 newRotation = GeneralFunctions.getVector3FromString(packet.messageParts[2]);
                mockBody.CallDeferred("set", "global_rotation", oldRotation);
                mock.previousAuthorityRotation = newRotation;

                float movementAmount = new Vector2(oldPosition.X, oldPosition.Z).DistanceTo(new Vector2(newPosition.X, newPosition.Z));
                mock.movementAmount = movementAmount;

                if(movementAmount > 0.01f)
                {
                    //clData.mock.direction = mockBody.Transform.Basis * new Vector3(newPosition.X - oldPosition.X, 0, newPosition.Z - oldPosition.Z).Normalized();
                    Vector3 direction = clData.mockTransform.Basis * new Vector3(newPosition.X - oldPosition.X, 0, newPosition.Z - oldPosition.Z).Normalized();
                    mock.CallDeferred("set", "direction", direction);
                }
                else
                {
                    mock.CallDeferred("set", "direction", Vector3.Zero);
                }

                //ROTATION INTERPOLATION THING
                mock.rotateCameraAngleTarget = newRotation;
                

                
                mock.previousAuthorityPosition = newPosition;
    
            }
        }
    }

    public void MovementLogRemovePastEntries(int tagReference)
    {
        List<PacketData> toRemove = new List<PacketData>();
        foreach(PacketData packet in movementLog)
        {
            if(packet.millisecondsTimeTag <= tagReference)
            {
                toRemove.Add(packet);
            }
        }
        foreach (PacketData remove in toRemove) movementLog.Remove(remove);
    }

    public void MovementLogTickAllEntries(Vector3 setPosition)
    {
        Vector2 totalInput = Vector2.Zero;
        int tickAmount = 0;
        playerBody.GlobalPosition = setPosition;
        foreach (PacketData packet in movementLog)
        {
            playerController.CalculateTick(GeneralFunctions.getVector2FromString(packet.messageParts[0]), setPosition, GeneralFunctions.getVector3FromString(packet.messageParts[1]));
            tickAmount++;
        }
    }

    public void SendShootInputToServer(Vector3 cameraRotation)
    {
        Vector3 directionInEulerForDebug = new Vector3(Mathf.RadToDeg(cameraRotation.X), Mathf.RadToDeg(cameraRotation.Y), Mathf.RadToDeg(cameraRotation.Z));
        //GD.Print("Camera angle: " + directionInEulerForDebug);
        Quaternion quat = Quaternion.FromEuler(cameraRotation);
        Vector3 forwardDir = -new Basis(quat).Z;

        Vector3 startPosition = playerBody.GlobalPosition;

        // Calculate the end position for the ray
        float rayLength = 100f; // Define how far you want to shoot the ray
        Vector3 endPosition = startPosition + forwardDir * rayLength;

        // Create the raycast parameters
        var spaceState = GetViewport().World3D.DirectSpaceState;
        var rayQuery = PhysicsRayQueryParameters3D.Create(startPosition, endPosition);

        // Perform the raycast
        var result = spaceState.IntersectRay(rayQuery);

        // Check for collisions
        if (result.Count > 0)
        {
            Node collider = (Node)result["collider"];

            if(collider.Name == "MockCharacterBody3D" || collider.Name == "PlayerMock")
            {
                Vector3 position = (Vector3)result["position"];

                PacketData shootPacket = PacketFunctions.CreatePacket(PacketType.WEAPON_SHOOT, ref user.tick,
               serverLastKnownMovementTag.ToString(),
               GeneralFunctions.getStringFromVector3(forwardDir));
                //For example a time tag of '68' would mean that the server has to check what were other clients doing when this one had tag 68.
                PacketFunctions.Send(shootPacket);
            }

           
            // Additional logic for what happens when a target is hit
        }

        //GD.Print("Forward Vector Z: " + forwardDir);

       
    }

}
