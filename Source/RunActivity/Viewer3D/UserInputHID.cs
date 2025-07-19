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
        private readonly HIDControllerDevice Device;

        //         [GetString("Display Track Monitor Window")] DisplayTrackMonitorWindow,

        public ExternalDeviceCabControl Throttle = new ExternalDeviceCabControl();       // 0 to 100
        public readonly byte[] UserCommands = new byte[Enum.GetNames(typeof(UserCommand)).Length];
        ExternalDeviceButton TrackMonitor;


        public UserInputHIDState(Game game)
        {
            Device = HIDControllerDevice.Instance;
            
            // Display information that the device is not enable, probably failed to connect
            if (Device is null || !Device.Initialize())
            {
                MessageBox.Show("HID Controller not enabled. Please check your settings.", "HID Controller Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            /*for (int i = 0; i < 1; i++)
            {
                var userCommand = (UserCommand)i;
                byte button = UserCommands[i];
                if (button >= 0 && button != byte.MaxValue)
                {
                    RegisterCommand(userCommand, new HIDButton(button));
                }
            }*/

            TrackMonitor = new ExternalDeviceButton();
            RegisterCommand(UserCommand.DisplayTrackMonitorWindow, TrackMonitor);

            CabControls[(new CabViewControlType(CABViewControlTypes.THROTTLE), -1)] = Throttle;
        }
        public void Update()
        {
            if (Device is null || !Device.Enabled)
            {
                return; // Device not connected or not enabled
            }
            HIDDeviceReport report = Device.ReadInput();
            Trace.TraceInformation($"HID Input: ButtonState={report.ButtonState}, AxisThrottle={report.AxisThrottle}");

            Throttle.Value = Percentage(report.AxisThrottle, 4096) / 100; // MSTSLocomitveViewer.cs:253 for some reason does *100 again

            TrackMonitor.IsDown = report.ButtonState;

            foreach (var command in Commands.Keys)
            {
                var buttonList = Commands[command];
                foreach (var button in buttonList)
                {
                    if (button is HIDButton rd && (Active || command == UserCommand.GameExternalCabController))
                    {
                        rd.Update(report.ButtonState);
                    }
                }
            }
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

        public bool Active { get; private set; }
    }

    public class HIDButton : ExternalDeviceButton
    {
        int Index;
        byte Mask;
        public HIDButton(byte button)
        {
            Index = 8 + button / 8;
            Mask = (byte)(1 << (button % 8));
        }
        public void Update(bool data)
        {
            IsDown = data;
        }
    }

}

