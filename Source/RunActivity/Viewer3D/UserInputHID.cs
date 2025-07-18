using System;
using System.Diagnostics;
using Orts.Formats.Msts;
using ORTS.Settings;
using Orts.Viewer3D.Processes;
using ORTS.Common.Input;
using System.Windows.Forms;


namespace Orts.Viewer3D
{
    public class UserInputHIDState : ExternalDeviceState
    {
        private readonly HIDControllerDevice Device;

        public UserInputHIDState(Game game)
        {
            Device = HIDControllerDevice.Instance;

            // Display information that the device is not enable, probably failed to connect
            if (Device is null || !Device.Initialize())
            {
                MessageBox.Show("HID Controller not enabled. Please check your settings.", "HID Controller Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        public void Update()
        {
            if (Device is null || !Device.Enabled)
            {
                return; // Device not initialized or not enabled
            }
            HIDDeviceReport report = Device.ReadInput();
        }
        public void Activate()
        {
    
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

        private (byte, byte) UpdateCutOff((byte, byte) range, byte cutOff)
        {
            if (range.Item1 < range.Item2)
            {
                range.Item1 += cutOff;
                range.Item2 -= cutOff;
            }
            else
            {
                range.Item2 += cutOff;
                range.Item1 -= cutOff;
            }
            return range;
        }

        private (byte, byte, byte) UpdateCutOff((byte, byte, byte) range, byte cutOff)
        {
            if (range.Item1 < range.Item3)
            {
                range.Item1 += cutOff;
                range.Item3 -= cutOff;
            }
            else
            {
                range.Item3 += cutOff;
                range.Item1 -= cutOff;
            }
            return range;
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
        public void Update(byte[] data)
        {
            IsDown = (data[Index] & Mask) != 0;
        }
    }

}

