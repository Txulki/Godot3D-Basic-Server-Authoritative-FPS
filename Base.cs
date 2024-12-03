using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


namespace udpBase
{

    public enum PacketType
	{
		USER_LOGIN_REQUEST = 0,
		USER_LOGIN_SERVER_RESPONSE = 1,
		USER_SERVER_SENT_DATA = 2,
		REFUSE_CONNECTION = 3,
		ACKNOWLEDGEMENT = 4,

		MOVEMENT_SEND_INPUT = 5,
		MOVEMENT_SEND_AUTHORITY_POSITION = 6,

		WEAPON_SHOOT = 7,
		WEAPON_HIT = 8,
	}

	public struct PacketData
	{
        public IPEndPoint destination; //Only really for server I guess...

        public PacketType type;
		public PacketCategory category;

		public int millisecondsTimeTag;
		public string[] messageParts;
    }

	public enum PacketCategory
	{
		SINGLE_USER_DEPENDENT = 0,
		MULTIPLE_USER_DEPENDENT = 1,
	}

	public struct Received
	{
		public IPEndPoint Sender;
		public string Message;
	}

	public abstract class UdpBase
	{
		protected UdpClient Client;

        public List<Received> messagesInQueue = new List<Received>();
        public List<PacketData> packetsAwaitingAcknowledgement = new List<PacketData>();

        public List<NetObject> clientsInServer = new List<NetObject>();

        public PacketData[] mainTypesTimesLog = GeneralFunctions.InitializePacketArrayPerType();

        public double acknowledgementResendTimer = 0f;

		public int tick = 0;

		public bool isServer = false;

        protected UdpBase(bool isServer)
		{
			Client = new UdpClient();
			PacketFunctions.udpBase = this;
			this.isServer = isServer;
		}

		public async Task<Received> Receive()
		{
			var result = await Client.ReceiveAsync();
			return new Received()
			{
				Message = Encoding.ASCII.GetString(result.Buffer, 0, result.Buffer.Length),
				Sender = result.RemoteEndPoint
			};
		}

		public void Send(string message, IPEndPoint endpoint)
		{
			var datagram = Encoding.ASCII.GetBytes(message);

			if (isServer) Client.Send(datagram, datagram.Length, endpoint);
			else Client.Send(datagram, datagram.Length);
		}
    }


}