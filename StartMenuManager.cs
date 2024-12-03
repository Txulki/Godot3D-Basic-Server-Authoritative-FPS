using Godot;
using System;

public partial class StartMenuManager : Node
{
	public void ConnectClient()
	{
        var Client = (Client)GetNode("/root/Client");
        Client.ConnectClient();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public void OnHostButtonPressed()
	{
		var Server = (Server)GetNode("/root/Server");
		Server.OnHostButtonPressed();
	}
}
