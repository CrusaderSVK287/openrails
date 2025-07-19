using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HidSharp;
using Microsoft.Xna.Framework.Input;
using SharpDX.XAudio2;

// This class will probabyl handle communication and parsing into usable data something.
// I will probably create a separate class that will be returned to the UserInputHIDState where it will be used, probably

namespace ORTS.Common.Input
{
    public class HIDControllerDevice
    {
        private static HIDControllerDevice instance;

        public HidStream Stream { get; private set; }
        public HidDevice Device { get; private set; }

        private const int VendorId = 0xCAFE;
        private const int ProductId = 0x4000;

        public bool Enabled { set; get; } = false;

        public static HIDControllerDevice Instance
        {
            get
            {
                if (null == instance)
                {
                    instance = new HIDControllerDevice();
                }
                return instance;
            }
        }

        private HIDControllerDevice()
        {
            Enabled = false;
        }

        public bool Initialize()
        {
            var deviceList = DeviceList.Local;
            Device = deviceList.GetHidDevices(VendorId, ProductId).FirstOrDefault();

            if (Device == null)
            {
                return false;
            }

            if (!Device.TryOpen(out HidStream stream))
            {
                return false;
            }

            Stream = stream;
            Stream.ReadTimeout = 3000;
            Enabled = true;

            return true;
        }

        public HIDDeviceReport ReadInput()
        {
            byte[] inputReport = new byte[HIDDeviceReport.ReportSize];

            if (!Enabled || Stream == null)
            {
                // Dont raise an exception. In this case the device is probably just not active
                return null;
            }

            try
            {
                int bytesRead = Stream.Read(inputReport, 0, HIDDeviceReport.ReportSize);

                if (bytesRead != HIDDeviceReport.ReportSize)
                {
                    throw new IOException($"Unexpected report size: {bytesRead} instead of {HIDDeviceReport.ReportSize}");
                }

                HIDDeviceReport report = new HIDDeviceReport
                {
                    ButtonState = (inputReport[3] & 0x01) == 1,
                    AxisThrottle = (UInt16)(inputReport[1] | (inputReport[2] << 8))
                };
                return report;
            }
            catch (Exception e)
            {
                System.Windows.Forms.MessageBox.Show($"Error reading HID input: {e.Message}", "HID Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }
    }

    public class HIDDeviceReport
    {
        public static int ReportSize = 4;
        public bool ButtonState { get; set; }
        public UInt16 AxisThrottle { get; set; }
        public HIDDeviceReport() {}
    }
}
