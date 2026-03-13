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
        public HidDevice Device { get; private set; }

        private const int VendorId = 0xCAFE;
        private const int ProductId = 0x4000;

        public bool Enabled { private set; get; } = false;

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


            Enabled = true;
            return true;
        }

        public HIDDeviceReport ReadInput()
        {
            byte[] inputReport = new byte[HIDDeviceReport.ReportSize];

            if (!Enabled)
            {
                // Dont raise an exception. In this case the device is probably just not active
                return null;
            }

            try
            {
                if (!Device.TryOpen(out HidStream stream))
                {
                    return null;
                }

                using (stream)
                {
                    int bytesRead = stream.Read(inputReport, 0, HIDDeviceReport.ReportSize);

                    if (bytesRead != HIDDeviceReport.ReportSize)
                    {
                        throw new IOException($"Unexpected report size: {bytesRead} instead of {HIDDeviceReport.ReportSize}");
                    }


                    HIDDeviceReport report = new HIDDeviceReport
                    {
                        // Analog axes
                        AxisThrottle = (UInt16)(inputReport[1] | (inputReport[2] << 8)),
                        AxisDirection = (UInt16)(inputReport[3] | (inputReport[4] << 8)),
                        AxisEngineBrake = (UInt16)(inputReport[5] | (inputReport[6] << 8)),

                        // Digital byte 1
                        Pause = (inputReport[7] & 0x01) != 0,
                        TrackMonitor = (inputReport[7] & (1 << 1)) != 0,
                        NextStation = (inputReport[7] & (1 << 2)) != 0,
                        Headlights = (inputReport[7] & (1 << 3)) != 0,
                        Panto1 = (inputReport[7] & (1 << 4)) != 0,
                        Panto2 = (inputReport[7] & (1 << 5)) != 0,

                        // Digital byte 2 (multi-state)
                        View = inputReport[8] & 0x03,
                        TrainBrake = (ushort)((inputReport[8] >> 2) & 0x03)
                    };

                    return report;
                }
            }
            catch (Exception e)
            {
                System.Windows.Forms.MessageBox.Show($"Error reading HID input: {e.Message}", "HID Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }
    }

    /// <summary>
    /// HID input report layout (9 bytes total, including report ID).
    ///
    /// Byte 0:
    ///   Report ID (ignored by parsing logic).
    ///
    /// Byte 1–2 (UInt16, little-endian):
    ///   Throttle axis value.
    ///   - Byte 1: Least significant byte
    ///   - Byte 2: Most significant byte
    ///
    /// Byte 3–4 (UInt16, little-endian):
    ///   Direction axis value.
    ///   - Byte 3: Least significant byte
    ///   - Byte 4: Most significant byte
    ///
    /// Byte 5–6 (UInt16, little-endian):
    ///   Engine brake axis value.
    ///   - Byte 5: Least significant byte
    ///   - Byte 6: Most significant byte
    ///
    /// Byte 7 (Digital inputs – bit field):
    ///   Bit 0 (0x01): Pause button
    ///   Bit 1 (0x02): Track monitor switch
    ///   Bit 2 (0x04): Next station button
    ///   Bit 3 (0x08): Headlights switch
    ///   Bit 4 (0x10): Pantograph 1 switch
    ///   Bit 5 (0x20): Pantograph 2 switch
    ///   Bits 6–7: Reserved (unused)
    ///
    /// Byte 8 (Multi-state switches – bit field):
    ///   Bits 0–1 (0x03): View selector (2-bit value)
    ///     - 0: View state 0
    ///     - 1: View state 1
    ///     - 2: View state 2
    ///     - 3: View state 3 // unused
    ///
    ///   Bits 2–3 (0x0C): Train brake selector (2-bit value)
    ///     - 0: Brake state 0
    ///     - 1: Brake state 1
    ///     - 2: Brake state 2
    ///     - 3: Brake state 3
    ///
    ///   Bits 4–7: Reserved (unused)
    /// </summary>

    public class HIDDeviceReport
    {
        public static int ReportSize = 9;

        // Analog axes
        public UInt16 AxisThrottle { get; set; }
        public UInt16 AxisDirection { get; set; }
        public UInt16 AxisEngineBrake { get; set; }

        // Digital controls (byte 1)
        public bool Pause { get; set; }
        public bool TrackMonitor { get; set; }
        public bool NextStation { get; set; }
        public bool Headlights { get; set; }
        public bool Panto1 { get; set; }
        public bool Panto2 { get; set; }

        // Multi-state controls (byte 2)
        public int View { get; set; }
        public UInt16 TrainBrake { get; set; }

        public HIDDeviceReport()
        {
        }
    }

}
