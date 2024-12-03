using Godot;
using System;
using System.Collections.Generic;
using udpBase;

public partial class PlayerController : CharacterBody3D
{
	public const float Speed = 5.0f;
	public const float JumpVelocity = 4.5f;

    public const float rotateSens = 0.3f;
    public const float rotateThreshold = 1.2f;

    double lastDelta = 0f;

	private Vector2 lastInput = Vector2.Zero;

	public Client cl;
	public Vector3 transform;

	private Camera3D camera;

    public override void _Ready()
    {
        base._Ready();
        camera = GetNode<Camera3D>("Camera3D");
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _PhysicsProcess(double delta)
	{
		lastDelta = delta;
		Vector3 velocity = Velocity;

		// Add the gravity.
		if (!IsOnFloor())
		{
			velocity += GetGravity() * (float)delta;
		}

		// Handle Jump.
		if (Input.IsActionJustPressed("ui_accept") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}

		// Get the input direction and handle the movement/deceleration.
		// As good practice, you should replace UI actions with custom gameplay actions.
		Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_backwards");

		PacketData inputSend = PacketFunctions.CreatePacket(PacketType.MOVEMENT_SEND_INPUT, ref ClientFunctions.user.tick, 
			GeneralFunctions.getStringFromVector2(inputDir),
			GeneralFunctions.getStringFromVector3(Rotation));
        cl.movementLog.Add(inputSend);
        PacketFunctions.Send(inputSend);
		
		lastInput = inputDir;

		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
		}

        Velocity = velocity;
		MoveAndSlide();
        Velocity = new Vector3(0, Velocity.Y, 0);
    }

	public void CalculateTick(Vector2 tickInput, Vector3 setPosition, Vector3 setRotation)
	{
        Quaternion quat = Quaternion.FromEuler(setRotation);
        Basis basis = new Basis(quat);
        Vector3 direction = (basis * new Vector3(tickInput.X, 0, tickInput.Y)).Normalized();
        Vector3 velocity = Velocity;

        // Add the gravity.
        if (!IsOnFloor())
        {
            velocity += GetGravity() * 0.1666f;
        }

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * Speed;
            velocity.Z = direction.Z * Speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
        }


        //this.GlobalPosition = setPosition;
        Velocity = velocity;
        MoveAndSlide();
        Velocity = new Vector3(0, Velocity.Y, 0);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motionEvent)
        {
            RotateY((MathF.PI / 180) * -motionEvent.Relative.X * rotateSens);

            float AngleX = ((MathF.PI / 180) * -motionEvent.Relative.Y * rotateSens);
            float TotalRot = AngleX + camera.Rotation.X;
            if (TotalRot > rotateThreshold)
            {
                camera.Rotation = new Vector3(rotateThreshold, camera.Rotation.Y, camera.Rotation.Z);
            }
            else if (TotalRot < -rotateThreshold)
            {
                camera.Rotation = new Vector3(-rotateThreshold, camera.Rotation.Y, camera.Rotation.Z);
            }
            else
            {
                camera.RotateX(AngleX);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("interact"))
        {
            cl.SendShootInputToServer(camera.GlobalRotation);
        }
    }
}
