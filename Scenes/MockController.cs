using Godot;
using System;
using System.Collections.Specialized;

public partial class MockController : CharacterBody3D
{
    public const float Speed = 5.0f;
    public const float JumpVelocity = 4.5f;

    public Vector3 direction;
    public Vector3 previousAuthorityPosition = Vector3.Zero;
    public Vector3 previousAuthorityRotation = Vector3.Zero;
    public Vector3 rotateCameraAngleTarget = Vector3.Zero;
    public float movementAmount = 0.0f;

    public override void _PhysicsProcess(double delta)
    {
        Vector3 velocity = Velocity;

        float speed = (movementAmount / 0.58333f) * Speed;
        //Movemos 0.58
        //Velocidad 5 en 10 frames = 0.58

        // Add the gravity.
        if (!IsOnFloor())
        {
            velocity += (GetGravity() * (float)delta);
        }

        if (direction != Vector3.Zero)
        {
            velocity.X = direction.X * speed;
            velocity.Z = direction.Z * speed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
            velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
            
        }

        float lerpedY = Mathf.LerpAngle(Rotation.Y, rotateCameraAngleTarget.Y, (float)delta);
        Rotation = new Vector3(Rotation.X, lerpedY, Rotation.Z);

        Velocity = velocity;
        MoveAndSlide();
        Velocity = new Vector3(0, Velocity.Y, 0);
    }

}