using Godot;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using udpBase;


//Client
public class UdpUser : UdpBase
{
    public int tick = 0;
    private UdpUser() : base(false) { }

    public static UdpUser ConnectTo(string hostname, int port)
    {
        var connection = new UdpUser();
        connection.Client.Connect(hostname, port);
        return connection;
    }

    public void Send(string message)
    {
        var datagram = Encoding.ASCII.GetBytes(message);
        Client.Send(datagram, datagram.Length);
    }
}

