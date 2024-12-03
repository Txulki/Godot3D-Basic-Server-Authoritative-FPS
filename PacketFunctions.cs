using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using udpBase;

public static class PacketFunctions
{
    public static UdpBase udpBase;

    private static PacketType[] multipleUserDependentAcknowledgedTypes = { PacketType.USER_SERVER_SENT_DATA, PacketType.MOVEMENT_SEND_AUTHORITY_POSITION, PacketType.USER_LOGIN_REQUEST };
    private static PacketType[] singleUserDependent = { PacketType.USER_LOGIN_SERVER_RESPONSE, PacketType.REFUSE_CONNECTION, PacketType.WEAPON_HIT };
    public static PacketType[] acknowledgedTypes = { PacketType.USER_LOGIN_REQUEST, PacketType.USER_LOGIN_SERVER_RESPONSE,
            PacketType.REFUSE_CONNECTION, PacketType.USER_SERVER_SENT_DATA, PacketType.WEAPON_HIT };

    

    public static PacketData CreatePacket(PacketType type, ref int ticks, params string[] parts)
    {
        PacketData createdPacket = new PacketData();
        createdPacket.type = type;
        createdPacket.messageParts = parts;
        createdPacket.millisecondsTimeTag = ticks;
        ticks++;
        return createdPacket;
    }

    public static void AddToPacket(ref PacketData packet, params string[] parts)
    {
        string[] concatParts = new string[packet.messageParts.Length + parts.Length];
        packet.messageParts.CopyTo(concatParts, 0);
        parts.CopyTo(concatParts, packet.messageParts.Length);

        packet.messageParts = concatParts;
    }

    public static string PacketSetupper(PacketData packet)
    {
        string packedPacket = "";

        packedPacket = packedPacket + (int)packet.type;

        packedPacket = packedPacket + ';' + packet.millisecondsTimeTag;

        foreach (string part in packet.messageParts)
        {
            if(!string.IsNullOrEmpty(part))
            {
                packedPacket += ";";
                packedPacket += part;
            }
        }

        return packedPacket;
    }

    public static void AcknowledgementRemoveIfLessRelevant(UdpBase bas, PacketType type, int tick)
    {
        List<PacketData> packets = bas.packetsAwaitingAcknowledgement;
        List<PacketData> toRemove = new List<PacketData>();

        foreach(PacketData packet in packets)
        {
            if(packet.type == type)
            {
                if(packet.millisecondsTimeTag <= tick)
                {
                    toRemove.Add(packet);
                }
            }
        }

        foreach(PacketData packet in toRemove)
        {
            bas.packetsAwaitingAcknowledgement.Remove(packet);
        }
    }


    public static void ProcessAcknowledgement(UdpBase bas, PacketData packet)
    {
        int ackType = int.Parse(packet.messageParts[0]);
        int ackTick = int.Parse(packet.messageParts[1]);

        for (int i = 0; i < bas.packetsAwaitingAcknowledgement.Count; i++)
        {
            if ((int)bas.packetsAwaitingAcknowledgement[i].type == ackType && bas.packetsAwaitingAcknowledgement[i].millisecondsTimeTag == ackTick)
            {
                bas.packetsAwaitingAcknowledgement.RemoveAt(i);
                break;
            }
        }

        AcknowledgementRemoveIfLessRelevant(bas, (PacketType)ackType, ackTick);
    }

    public static void Send(PacketData packet, bool acknowledgement = false)
    {
        Task.Delay(150).ContinueWith(o => { SendPacket(packet, acknowledgement); });
    }

    private static void SendPacket(PacketData packet, bool acknowledgement = false)
    {
        IPEndPoint svEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 27016);
        packet.destination = svEndPoint;
        udpBase.Send(PacketSetupper(packet), svEndPoint);

        if (!acknowledgement)
        {
            if (acknowledgedTypes.Contains<PacketType>(packet.type))
            {
                AcknowledgementRemoveIfLessRelevant(udpBase, packet.type, packet.millisecondsTimeTag);
                udpBase.packetsAwaitingAcknowledgement.Add(packet);
            }
        }
        udpBase.tick++;
    }
    

    public static void Send(PacketData packet, ServerPlayer target)
    {
        Task.Delay(150).ContinueWith(o => { SendPacket(packet, target); });
       
    }

    private static void SendPacket(PacketData packet, ServerPlayer target)
    {
        packet.destination = target.endPoint;
        udpBase.Send(PacketFunctions.PacketSetupper(packet), target.endPoint);

        if (acknowledgedTypes.Contains<PacketType>(packet.type))
        {
            AcknowledgementRemoveIfLessRelevant(udpBase, packet.type, packet.millisecondsTimeTag);
            udpBase.packetsAwaitingAcknowledgement.Add(packet);
        }

        udpBase.tick++;
    }

    //
    // ACKNOWLEDGEMENTS
    //

    public static void AcknowledgeIfNeeded(PacketData packet)
    {
        if(HasToSendAcknowledgement(packet))
        {
            PacketData ackPacket = CreateAcknowledgement(packet.type, packet.millisecondsTimeTag);
            PacketFunctions.Send(ackPacket);
        }
    }

    private static bool HasToSendAcknowledgement(PacketData packet)
    {
        bool hasToSend = false;
        if (acknowledgedTypes.Contains<PacketType>(packet.type))
        {
            hasToSend = true;
        }
        return hasToSend;
    }

    private static PacketData CreateAcknowledgement(PacketType confirmingType, int confirmingTime)
    {
        string[] acknowledgingInfo = { ((int)confirmingType).ToString(), confirmingTime.ToString() };

        PacketData createdPacket = new PacketData()
        {
            type = PacketType.ACKNOWLEDGEMENT,
            messageParts = acknowledgingInfo,
            millisecondsTimeTag = 1
        };

        return createdPacket;
    }






    //
    // PROCESS RECEIVED
    //

    public static PacketData TurnReceivedIntoPacketData(string Message)
    {
        //Example: We receive 6;24;8792;0^7^0
        string[] parts = SplitDotCommasInArray(Message);
        //This splits the message into four parts:
        // 6   24   8792   0^7^0

        PacketData packet = new PacketData(); //Create a new PacketData struct instance

        (packet.type, packet.category) = GetPacketTypeAndCategoryFromHeader(parts[0]); //We get the packet type and category from the header.
        packet.millisecondsTimeTag = GetPacketTimeID(parts[1]); //Second part of the message is the time tag.

        packet.messageParts = GetPacketSubcontent(parts); //The remaining parts are all "optional", specific to each type of packet, so we put them in a string[]

        return packet; //We return the packet with all the needed info.
    }

    private static string[] SplitDotCommasInArray(string toSplit){
        return toSplit.Split(';');
    }

    private static int GetPacketTimeID(string timeID){
        return int.Parse(timeID);
    }


    private static string[] GetPacketSubcontent(string[] parts){

        string[] subcontent = new string[0];
        if(parts.Length > 2)
        {
            List<string> subcontentList = new List<string>();
            for (int i = 2; i < parts.Length; i++)
            {
                subcontentList.Add(parts[i]);
            }
            subcontent = subcontentList.ToArray();
        }
        return subcontent;
    }

    //
    //	PACKET CATEGORY
    //

    private static (PacketType, PacketCategory) GetPacketTypeAndCategoryFromHeader(string header)
    {
        PacketType type = (PacketType)int.Parse(header);
        PacketCategory category = GetPacketCategory(type);

        return (type, category);
    }

    public static PacketCategory GetPacketCategory(PacketType type)
    {
        PacketCategory category = PacketCategory.SINGLE_USER_DEPENDENT;

        if (PacketFunctions.IsPacketCategoryMultipleUserDependent(type))
        {
            category = PacketCategory.MULTIPLE_USER_DEPENDENT;
        }
        else if (PacketFunctions.IsPacketCategorySingle(type))
        {
            category = PacketCategory.SINGLE_USER_DEPENDENT;
        }

        return category;
    }

    public static bool IsPacketCategoryMultipleUserDependent(PacketType type)
    {
        return multipleUserDependentAcknowledgedTypes.Contains<PacketType>(type);
    }

    public static bool IsPacketCategorySingle(PacketType type)
    {
        return singleUserDependent.Contains<PacketType>(type);
    }


    //
    //	GET PLAYER IN DIFFERENT WAYS
    //

    public static (bool, NetObject) GetClientFromID(int id)
    {
        bool isClientIn = false;
        NetObject toReturn = null;
        foreach (NetObject clData in udpBase.clientsInServer)
        {
            if (clData.id.Equals(id))
            {
                isClientIn = true;
                toReturn = clData;
            }
        }

        return (isClientIn, toReturn);
    }

    public static bool IsClientAlreadyLoggedInServerById(int id)
    {
        (bool alreadyLoggedIn, NetObject netPlayer) = GetClientFromID(id);
        return alreadyLoggedIn;
    }

    //
    //	REGISTER A NET PLAYER
    //

    public static void LogNewNetPlayer(NetPlayer toLog)
    {
        udpBase.clientsInServer.Add(toLog);
    }



}
