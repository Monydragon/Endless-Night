using SharpDX.XInput;

namespace EndlessNight;

/// <summary>
/// Handles Xbox/PlayStation controller input via XInput
/// </summary>
public class ControllerInput : IDisposable
{
    private readonly Controller _controller;
    private State _previousState;
    private float _prevThumbY = 0f; // Track previous thumbstick Y position
    
    public bool IsConnected => _controller.IsConnected;
    
    public ControllerInput(UserIndex userIndex = UserIndex.Any)
    {
        // If UserIndex.Any is specified, try all slots to find a connected controller
        if (userIndex == UserIndex.Any)
        {
            // Try all four controller slots
            var indices = new[] { UserIndex.One, UserIndex.Two, UserIndex.Three, UserIndex.Four };
            
            foreach (var index in indices)
            {
                var testController = new Controller(index);
                if (testController.IsConnected)
                {
                    _controller = testController;
                    _previousState = _controller.GetState();
                    return;
                }
            }
            
            // No controller found, default to slot one
            _controller = new Controller(UserIndex.One);
        }
        else
        {
            _controller = new Controller(userIndex);
            if (_controller.IsConnected)
            {
                _previousState = _controller.GetState();
            }
        }
    }

    /// <summary>
    /// Gets the current controller state
    /// </summary>
    private State GetCurrentState()
    {
        if (!_controller.IsConnected)
        {
            return _previousState;
        }
        return _controller.GetState();
    }

    /// <summary>
    /// Check if a button was just pressed (wasn't pressed before, is pressed now)
    /// </summary>
    public bool IsButtonPressed(GamepadButtonFlags button)
    {
        if (!_controller.IsConnected) return false;

        var currentState = GetCurrentState();
        
        bool wasPressed = (_previousState.Gamepad.Buttons & button) != 0;
        bool isPressed = (currentState.Gamepad.Buttons & button) != 0;
        
        _previousState = currentState;
        return !wasPressed && isPressed;
    }

    /// <summary>
    /// Check if D-pad Up was just pressed
    /// </summary>
    public bool IsDPadUpPressed() => IsButtonPressed(GamepadButtonFlags.DPadUp);

    /// <summary>
    /// Check if D-pad Down was just pressed
    /// </summary>
    public bool IsDPadDownPressed() => IsButtonPressed(GamepadButtonFlags.DPadDown);

    /// <summary>
    /// Check if D-pad Left was just pressed
    /// </summary>
    public bool IsDPadLeftPressed() => IsButtonPressed(GamepadButtonFlags.DPadLeft);

    /// <summary>
    /// Check if D-pad Right was just pressed
    /// </summary>
    public bool IsDPadRightPressed() => IsButtonPressed(GamepadButtonFlags.DPadRight);

    /// <summary>
    /// Check if A button (Xbox) / X button (PlayStation) was just pressed
    /// </summary>
    public bool IsAButtonPressed() => IsButtonPressed(GamepadButtonFlags.A);

    /// <summary>
    /// Check if B button (Xbox) / Circle button (PlayStation) was just pressed
    /// </summary>
    public bool IsBButtonPressed() => IsButtonPressed(GamepadButtonFlags.B);

    /// <summary>
    /// Check if X button (Xbox) / Square button (PlayStation) was just pressed
    /// </summary>
    public bool IsXButtonPressed() => IsButtonPressed(GamepadButtonFlags.X);

    /// <summary>
    /// Check if Y button (Xbox) / Triangle button (PlayStation) was just pressed
    /// </summary>
    public bool IsYButtonPressed() => IsButtonPressed(GamepadButtonFlags.Y);

    /// <summary>
    /// Get left thumbstick position (-1.0 to 1.0 for both X and Y)
    /// Applies deadzone filtering
    /// </summary>
    public (float x, float y) GetLeftThumbstick(float deadzone = 0.2f)
    {
        if (!_controller.IsConnected) return (0, 0);

        var state = _controller.GetState();
        float x = state.Gamepad.LeftThumbX / 32768f;
        float y = state.Gamepad.LeftThumbY / 32768f;

        // Apply deadzone
        if (Math.Abs(x) < deadzone) x = 0;
        if (Math.Abs(y) < deadzone) y = 0;

        return (x, y);
    }

    /// <summary>
    /// Check if left thumbstick moved up (just now)
    /// </summary>
    public bool IsLeftThumbstickUp()
    {
        var (_, y) = GetLeftThumbstick();
        bool wasUp = _prevThumbY > 0.5f;
        bool isUp = y > 0.5f;
        _prevThumbY = y;
        return !wasUp && isUp; // Edge detection
    }

    /// <summary>
    /// Check if left thumbstick moved down (just now)
    /// </summary>
    public bool IsLeftThumbstickDown()
    {
        var (_, y) = GetLeftThumbstick();
        bool wasDown = _prevThumbY < -0.5f;
        bool isDown = y < -0.5f;
        _prevThumbY = y;
        return !wasDown && isDown; // Edge detection
    }

    /// <summary>
    /// Vibrate the controller
    /// </summary>
    public void Vibrate(float leftMotor = 0.5f, float rightMotor = 0.5f, int durationMs = 200)
    {
        if (!_controller.IsConnected) return;

        var vibration = new Vibration
        {
            LeftMotorSpeed = (ushort)(Math.Clamp(leftMotor, 0f, 1f) * 65535),
            RightMotorSpeed = (ushort)(Math.Clamp(rightMotor, 0f, 1f) * 65535)
        };

        _controller.SetVibration(vibration);

        // Stop vibration after duration
        Task.Delay(durationMs).ContinueWith(_ =>
        {
            _controller.SetVibration(new Vibration { LeftMotorSpeed = 0, RightMotorSpeed = 0 });
        });
    }

    public void Dispose()
    {
        // Stop any vibration
        if (_controller.IsConnected)
        {
            _controller.SetVibration(new Vibration { LeftMotorSpeed = 0, RightMotorSpeed = 0 });
        }
    }
}

