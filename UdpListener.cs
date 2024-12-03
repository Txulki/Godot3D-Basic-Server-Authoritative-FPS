using Godot;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using udpBase;

//Server
public class UdpListener : UdpBase
{
    private IPEndPoint _listenOn;

    public UdpListener() : this(new IPEndPoint(IPAddress.Any, 27016))
    {
    }

    public UdpListener(IPEndPoint endpoint) : base(true)
    {
        _listenOn = endpoint;
        Client = new UdpClient(_listenOn);
    }


}
