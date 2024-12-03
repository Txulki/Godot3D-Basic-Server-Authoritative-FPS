using Godot;
using System;
using System.Globalization;
using udpBase;

public static class GeneralFunctions 
{
	public static bool isTimerOverTimeoutDelta(ref double timer, double delta, double limit)
	{
		bool isTimerOver = false;
		timer += delta;

		if(timer >= limit)
		{
			isTimerOver = true;
			timer = 0f;
		}

		return isTimerOver;
	}

	public static PacketData[] InitializePacketArrayPerType()
	{
		PacketData[] newPacketArray = new PacketData[10];

		for(int i = 0; i < newPacketArray.Length; i++)
		{
			PacketData placeholderData = new PacketData();
			placeholderData.millisecondsTimeTag = -1;

			newPacketArray[i] = placeholderData;
		}

		return newPacketArray;
	}

	public static void DebugLastPacketPerTypes(PacketData[] lastPacketPerType)
	{
		string debugMessage = "Last packet types: ";
		
		for(int i = 0; i < lastPacketPerType.Length; i++)
		{
			debugMessage += '\n';
			debugMessage += (int)lastPacketPerType[i].type;
			debugMessage += " TIME TAG: " + lastPacketPerType[i].millisecondsTimeTag;
		}

		GD.Print(debugMessage);
	}

	public static string getStringFromVector2(Vector2 vector)
	{
		string convertedVector = "";
		convertedVector += vector.X.ToString(CultureInfo.InvariantCulture);
		convertedVector += "^";
		convertedVector += vector.Y.ToString(CultureInfo.InvariantCulture);

		return convertedVector;
	}

	public static Vector2 getVector2FromString(string vector)
	{
		Vector2 convertedString = Vector2.Zero;

		string[] coordsStr = vector.Split('^');
		convertedString.X = float.Parse(coordsStr[0], CultureInfo.InvariantCulture);
		convertedString.Y = float.Parse(coordsStr[1], CultureInfo.InvariantCulture);

		return convertedString;
	}

    public static string getStringFromVector3(Vector3 vector)
    {
        string convertedVector = "";
        convertedVector += vector.X.ToString(CultureInfo.InvariantCulture);
        convertedVector += "^";
        convertedVector += vector.Y.ToString(CultureInfo.InvariantCulture);
		convertedVector += "^";
		convertedVector += vector.Z.ToString(CultureInfo.InvariantCulture);

        return convertedVector;
    }

	public static Vector3 getVector3FromString(string vector)
	{
		Vector3 convertedString = Vector3.Zero;

		string[] coordsStr = vector.Split('^');
		convertedString.X = float.Parse(coordsStr[0], CultureInfo.InvariantCulture);
		convertedString.Y = float.Parse(coordsStr[1], CultureInfo.InvariantCulture);
		convertedString.Z = float.Parse(coordsStr[2], CultureInfo.InvariantCulture);

		return convertedString;
	}
}
