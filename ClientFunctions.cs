using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using udpBase;
using static PacketFunctions;

public static class ClientFunctions
{
	public static UdpUser user;

   

    /// <summary>
    /// setups the UdpUser (Client) reference.
    /// </summary>
    /// <param name="u"></param>
    public static void SetupClientFunctions(UdpUser u)
	{
		user = u;
	}

	public static void SendRepeatedWaitingAcknowledgement()
	{
		foreach(PacketData packet in user.packetsAwaitingAcknowledgement)
		{
			PacketFunctions.Send(packet, true);
		}
	}

	public static NetPlayer GetCreatedClientDataFromPacket(PacketData packet)
	{
        NetPlayer createdData = new NetPlayer(int.Parse(packet.messageParts[0]));

		return createdData;
	}

    public static NetObject GetPlayerFromNetuserPacketOrReturnPlaceholder(PacketData packet)
    {
        int id = int.Parse(packet.messageParts[0]);

        (bool found, NetObject player) = GetClientFromID(id);

        if (!found) player = new NetPlayer(id);

        return player;
    }





    /*public static bool isClientAlreadyLocallyAccountedFor(ref NetPlayer toSearch)
	{
		bool isClientAlreadyLocal = false;

		foreach(NetPlayer clData in user.clientsInServer)
		{
			if(clData.id == toSearch.id)
			{
				isClientAlreadyLocal = true;
				toSearch = clData;
			}
		}

		return isClientAlreadyLocal;
	}*/



    /*public static (bool, NetPlayer) ProcessPlayerDataReceivedAndReturnIfNew(PacketData packet)
	{
		bool playerDataNew = false;
		NetPlayer tempClient = getCreatedClientDataFromPacket(packet);
		if(!isClientAlreadyLocallyAccountedFor(ref tempClient))
		{
			playerDataNew = true;
			user.clientsInServer.Add(tempClient);
		}

		return (playerDataNew, tempClient);
	}*/







    // 
    //  MESSAGE TIME ID
    //

    public static bool IsPacketTimeValid(PacketData packet)
    {
        bool timeValid = true;

        switch (packet.category)
        {
            case PacketCategory.SINGLE_USER_DEPENDENT:
                IsSingleUserDependentPacketTimeValid(packet, ref timeValid);
                break;

            case PacketCategory.MULTIPLE_USER_DEPENDENT:
                IsMultipleUserDependentPacketTimeValid(packet, ref timeValid);
                break;
        }

        return timeValid;
    }

    private static void IsSingleUserDependentPacketTimeValid(PacketData packet, ref bool timeValid)
    {
        timeValid = false;
        int lastTimeTag = user.mainTypesTimesLog[(int)packet.type].millisecondsTimeTag;

        if (packet.millisecondsTimeTag > lastTimeTag)
        {
            timeValid = true;
            user.mainTypesTimesLog[(int)packet.type] = packet;
        }
    }

    private static void IsMultipleUserDependentPacketTimeValid(PacketData packet, ref bool timeValid)
    {
        timeValid = true;

        NetObject player = ClientFunctions.GetPlayerFromNetuserPacketOrReturnPlaceholder(packet);

        if (player.clObject != null)
        {
            int lastTimeTag = player.typesTimesLog[(int)packet.type].millisecondsTimeTag;
            if (packet.millisecondsTimeTag <= lastTimeTag)
            {
                timeValid = false;
                player.typesTimesLog[(int)packet.type] = packet;
            }
        }
    }



}
