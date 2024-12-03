using Godot;
using System;
using System.Linq;
using System.Net;
using udpBase;
using static PacketFunctions;

public static class ServerFunctions
{
	public static UdpListener server;


	public static int GenerateNewClientID()
	{
		Random rand = new Random();

		int id = 0;
		bool isAlreadyUsed = false;
		while(id == 0 && !isAlreadyUsed)
		{
			id = rand.Next(1, 10000);

			isAlreadyUsed = IsClientAlreadyLoggedInServerById(id);
		}

		return id;
	}

	public static ServerPlayer GetPlayerFromEndpoint(IPEndPoint endpoint)
	{
		ServerPlayer player = null;
		foreach (NetObject netObject in server.clientsInServer)
		{
			if(((ServerPlayer)netObject).endPoint.Equals(endpoint))
			{
				player = (ServerPlayer)netObject;
			}
		}
		return player;
	}

	public static void LoginClient(ref ServerPlayer serverPlayer, Node clObject, Node dummy)
	{
		if(serverPlayer == null || serverPlayer.id == 0)
		{
			serverPlayer.id = GenerateNewClientID();
            serverPlayer.clObject = clObject;
            server.clientsInServer.Add(serverPlayer);
			serverPlayer.dummy = dummy;
			serverPlayer.dummyBody = dummy.GetChild<StaticBody3D>(0);
		}
	}

    
	public static void SendToAllBut(PacketData packet, ServerPlayer exclude)
	{
		foreach(ServerPlayer clData in server.clientsInServer)
		{
			if(clData.endPoint != exclude.endPoint)
			{
				Send(packet, clData);
			}
		}
	}

    public static void SendRepeatedWaitingAcknowledgement()
    {
        foreach (PacketData packet in server.packetsAwaitingAcknowledgement)
        {
            server.Send(PacketFunctions.PacketSetupper(packet), packet.destination);
        }
    }

	public static bool isMessageNewestOfSameType(PacketData packet, ServerPlayer target)
	{
		bool isMessageNewest = false;
		int lastTimeTag = target.typesTimesLog[(int)packet.type].millisecondsTimeTag;

		if(packet.millisecondsTimeTag > lastTimeTag)
		{
			isMessageNewest = true;

			if (packet.millisecondsTimeTag > target.tempTypesTimesLog[(int)packet.type].millisecondsTimeTag)
			{
				target.tempTypesTimesLog[(int)packet.type] = packet;
			}
        }

		return isMessageNewest;
	}

	public static void SendAllPlayerDataTo(ServerPlayer clData, int ticks)
	{
		foreach(ServerPlayer otherClient in server.clientsInServer)
		{
			if(otherClient.endPoint != clData.endPoint)
			{
                PacketData packet = PacketFunctions.CreatePacket(PacketType.USER_SERVER_SENT_DATA, ref ticks, otherClient.id.ToString());
                Send(packet, clData);
            }
		}
	}

	public static void SendToAll(PacketData packet)
	{
		foreach(ServerPlayer clData in server.clientsInServer)
		{
            Send(packet, clData);
		}
	}

	public static void SendMovementUpdates(int ticks)
	{
		long currentServerTicks = DateTime.Now.Ticks;
		foreach(ServerPlayer clData in server.clientsInServer)
		{
			CharacterBody3D clBody = clData.clObject.GetChild<CharacterBody3D>(0);
			PacketData packet = PacketFunctions.CreatePacket(PacketType.MOVEMENT_SEND_AUTHORITY_POSITION, ref ticks, clData.id.ToString(), 
				GeneralFunctions.getStringFromVector3(clBody.GlobalPosition),
				GeneralFunctions.getStringFromVector3(clBody.Rotation),
				clData.typesTimesLog[(int)PacketType.MOVEMENT_SEND_INPUT].millisecondsTimeTag.ToString());

			//We log in a new entry for each client. This means players will keep the following log:

			//AT Time (Amount of DateTime.Now.Ticks), we only had info from player up to TimeTag X, so at that time we know that that player would be seen by other clients in clBody.Position.
			MovementLogEntry newEntry = new MovementLogEntry();
			newEntry.serverTime = currentServerTicks;
			newEntry.playerPosition = clBody.GlobalPosition;
			newEntry.timeTag = clData.typesTimesLog[(int)PacketType.MOVEMENT_SEND_INPUT].millisecondsTimeTag;
			clData.movementLog.Add(newEntry);

			if(clData.movementLog.Count > 10)
			{
                clData.movementLog.RemoveAt(0); //We only keep track of the last 10 packets.
            }

			SendToAll(packet);
		}
	}

	public static void CleanTempLastPacketArrays()
	{
		for(int i = 0; i < server.clientsInServer.Count; i++)
		{
			ServerPlayer clData = (ServerPlayer)server.clientsInServer[i];
			clData.tempTypesTimesLog = GeneralFunctions.InitializePacketArrayPerType();
		}
	}

    public static void SetUpdateLastPacketArrays()
    {
        for (int i = 0; i < server.clientsInServer.Count; i++)
        {
            ServerPlayer clData = (ServerPlayer)server.clientsInServer[i];

			for(int j = 0; j < clData.tempTypesTimesLog.Length; j++)
			{
				if (clData.tempTypesTimesLog[j].millisecondsTimeTag > clData.typesTimesLog[j].millisecondsTimeTag)
				{
					clData.typesTimesLog[j] = clData.tempTypesTimesLog[j];
				}
			}

            server.clientsInServer[i] = clData;
        }
    }


    // 
    //  MESSAGE TIME ID
    //

    public static bool IsPacketTimeValid(PacketData packet, IPEndPoint endpoint)
    {
        bool timeValid = true;

		IsUserDependentPacketTimeValid(packet, endpoint, ref timeValid);

        return timeValid;
    }

    private static void IsUserDependentPacketTimeValid(PacketData packet, IPEndPoint endpoint, ref bool timeValid)
    {
        timeValid = true;

        ServerPlayer player = ServerFunctions.GetPlayerFromEndpoint(endpoint);

        if (player != null)
        {
            int lastTimeTag = player.typesTimesLog[(int)packet.type].millisecondsTimeTag;
            if (packet.millisecondsTimeTag <= lastTimeTag)
            {
				
                timeValid = false;
                
            }
			else
			{
				timeValid = true;
				if (player.tempTypesTimesLog[(int)packet.type].millisecondsTimeTag <= packet.millisecondsTimeTag)
				{
					player.tempTypesTimesLog[(int)packet.type] = packet;
				}
            }

			//GD.Print("TEMP: " + player.tempTypesTimesLog[(int)packet.type].millisecondsTimeTag + " PACKET: " + packet.millisecondsTimeTag + " TOTAL: " + lastTimeTag);
        }
    }
}
