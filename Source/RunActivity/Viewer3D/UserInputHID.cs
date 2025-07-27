using System;
using System.Diagnostics;
using Orts.Formats.Msts;
using ORTS.Settings;
using Orts.Viewer3D.Processes;
using ORTS.Common.Input;
using System.Windows.Forms;
using static Swan.Terminal;


namespace Orts.Viewer3D
{
    public class UserInputHIDState : ExternalDeviceState
    {
        public bool Active { get; private set; }
        private readonly HIDControllerDevice Device;

        //         [GetString("Display Track Monitor Window")] DisplayTrackMonitorWindow,

        public ExternalDeviceCabControl Throttle = new ExternalDeviceCabControl();       // 0 to 100
        public readonly byte[] UserCommands = new byte[Enum.GetNames(typeof(UserCommand)).Length];
        HIDSwitch TrackMonitor;
        HIDButton ButtonGamePause;

        public UserInputHIDState(Game game)
        {
            Device = HIDControllerDevice.Instance;
            
            // Display information that the device is not enable, probably failed to connect
            if (Device is null || !Device.Initialize())
            {
                MessageBox.Show("HID Controller not enabled. Please check your settings.", "HID Controller Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Switches
            TrackMonitor = new HIDSwitch();
            //RegisterCommand(UserCommand.DisplayTrackMonitorWindow, TrackMonitor);                     // F4 window
            //RegisterCommand(UserCommand.DisplayNextStationWindow, TrackMonitor);                        // F10 window

            // Buttons
            ButtonGamePause = new HIDButton();
            //RegisterCommand(UserCommand.GamePauseMenu, ButtonGamePause);                              // Pause / close activity window
            //TODO: not yet sure, but I probably want 3-state switch and simulate the button clicking. But can probably be done from rpi
            RegisterCommand(UserCommand.ControlHeadlightIncrease, ButtonGamePause);

            //CabControls[(new CabViewControlType(CABViewControlTypes.THROTTLE), -1)] = Throttle;       // Throttle
            //TODO: Direction will be digital, but ORTS implements it as analog.
            //CabControls[(new CabViewControlType(CABViewControlTypes.DIRECTION), -1)] = Throttle;      // Direction
            //CabControls[(new CabViewControlType(CABViewControlTypes.ENGINE_BRAKE), -1)] = Throttle;   // Engine break
            // TODO: Cannot go to emergency
            CabControls[(new CabViewControlType(CABViewControlTypes.TRAIN_BRAKE), -1)] = Throttle;      // Train break
            
            Active = true;
        }
        public void Update()
        {
            if (Device is null || !Device.Enabled || !Active)
            {
                return; // Device not connected or not enabled
            }
            HIDDeviceReport report = Device.ReadInput();

            if (report is null) return;

            // Analog controls
            Throttle.Value = Percentage(report.AxisThrottle, 4096) / 100; // MSTSLocomitveViewer.cs:253 for some reason does *100 again

            // HID Switches
            //TrackMonitor.Update(report.ButtonState);
            // HID Buttons
            ButtonGamePause.Update(report.ButtonState);
        }
        public void Activate()
        {
    
        }
        private static float Percentage(UInt16 x, UInt16 max)
        {
            if (max == 0)
                return 0; // Avoid division by zero

            float rv = ((float)x / max) * 100f;
            return rv;
        }


        private static float Percentage(float x, float x0, float x100)
        {
            float p = 100 * (x - x0) / (x100 - x0);
            if (p < 0)
                return 0;
            if (p > 100)
                return 100;
            return p;
        }

        private static float Percentage(byte value, (byte p0, byte p100) range)
        {
            float p = 100 * (value - range.p0) / (range.p100 - range.p0);
            if (p < 0)
                return 0;
            if (p > 100)
                return 100;
            return p;
        }

        private static float Percentage(byte value, (byte p100Minus, byte p0, byte p100Plus) range)
        {
            float p = 100 * (value - range.p0) / (range.p100Plus - range.p0);
            if (p < 0)
                p = 100 * (value - range.p0) / (range.p0 - range.p100Minus);
            if (p < -100)
                return -100;
            if (p > 100)
                return 100;
            return p;
        }

    }

    public class HIDButton : ExternalDeviceButton
    {
        public HIDButton() { }
        public void Update(bool value)
        {
            IsDown = value;
        }
    }

    // Switches work identical to buttons but they fire the commands in different ways.
    // Buttons fire the commands when pressed, switches once when turned on, and once when turned off.
    // Since OR doesnt (or I couldnt find) have support for switches, I emulate them by changing the
    // IsDown property of a button to true whenever the switch changes its state and 
    // setting IsDown to false every update. This ensures that turning a switch up and down is seen
    // as pressing a button twice
    public class HIDSwitch : ExternalDeviceButton
    {
        private bool isFlicked = false;
        public HIDSwitch() { }
        public void Update(bool value)
        {
            IsDown = false;
            if (isFlicked != value)
            {
                IsDown = true;
                isFlicked = value;
            }
        }
    }

}

