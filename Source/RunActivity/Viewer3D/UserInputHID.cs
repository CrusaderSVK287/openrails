using System;
using System.Diagnostics;
using Orts.Formats.Msts;
using ORTS.Settings;
using Orts.Viewer3D.Processes;
using ORTS.Common.Input;
using System.Windows.Forms;
using static Swan.Terminal;
using Orts.Viewer3D.Popups;
using Orts.Common;

// For HIDPantographStateService
using Orts.Simulation.Utilities;


namespace Orts.Viewer3D
{
    public class UserInputHIDState : ExternalDeviceState
    {
        public bool Active { get; private set; }
        private readonly HIDControllerDevice Device;

        private int _lastTrainBrakeRaw = -1;
        private int _trainBrakeStableCount = 0;

        private DateTime? _pressStartTime = null;
        private bool _longPressTriggered = false;
        private const int LongPressMilliseconds = 2000;
        
        private readonly int StableThreshold = 30;

        public readonly byte[] UserCommands = new byte[Enum.GetNames(typeof(UserCommand)).Length];

        public ExternalDeviceCabControl AThrottle = new ExternalDeviceCabControl();       // 0 to 100
        public ExternalDeviceCabControl ADirection = new ExternalDeviceCabControl();       // 0 to 100
        public ExternalDeviceCabControl AEngineBreak = new ExternalDeviceCabControl();       // 0 to 100
        // Train break is implemented as digital in HID, but analog in ORTS
        public ExternalDeviceCabControl ATrainBreak = new ExternalDeviceCabControl();       // 0 to 100

        HIDSwitch SwPantograph1;
        HIDSwitch SwPantograph2;

        // Headlights are a switch but are implemented like buttons in ORTS
        HIDButton BHeadlightsIncrease;
        HIDButton BHeadlightsDecrease;
        // View is weird. I have a 3-state switch for it but its going to emulate buttons
        HIDButton BView1;
        HIDButton BView2;
        HIDButton BView3;

        HIDSwitch SwTrackMonitor;
        HIDSwitch SwNextStation;
        HIDButton BPause;

        // ctrl + m, switched the mode from auto-signal to manual etc, to allow recovery from running red signal etc.
        HIDButton BChangeTCSMode;

        public UserInputHIDState(Game game)
        {
            Device = HIDControllerDevice.Instance;

            // Display information that the device is not enable, probably failed to connect
            if (Device is null || !Device.Initialize())
            {
                if (MessageBox.Show("EN: HID Controller not detected. The game will start as normal but you will not be able to " +
                    "connect the device mid-game. Plug in the device and restart the game to use it.\n\n" +
                    "SK: HID Ovládač nebol nájdený. Hra sa spustí normálne, ale zariadenie nebude možné pripojiť počas hry. " +
                    "Pripojte zariadenie a reštartujte hru, aby ste ho mohli používať.", "HID Controller Error", 
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation)
                    == DialogResult.Cancel)
                {
                    Environment.Exit(1);
                }

                return;
            }

            // Initialize controls. Some controls are actually switching, but OR only supports buttons, so we emulate switches with virtual buttons
            // for example the View is a single 3-state switch, and depending on its position, a different button is "pressed" on update
            // Similiar with Train Brake. Its a 4-state switch, but in OR its analog, so we read digital input and convert to analog percentage

            /*
            Illustration on how the panel is laid out:
            .___________________________________________.
            |  P   V N T                           EB   |
            |                DIR       THR              |
            |  H   P1 P2                           TB   | 
            |___________________________________________|
            
            P  = Pause button
            V  = View switch (3 positions)
            N  = Next Station window switch
            T  = Track Monitor window switch
            EB = Engine Brake lever
            DIR= Direction lever
            THR= Throttle lever
            H  = Headlight control (2 positions)
            P1 = Pantograph 1 control
            P2 = Pantograph 2 control
            TB = Train Brake lever
             */

            // Switches
            SwPantograph1 = new HIDSwitch();
            SwPantograph2 = new HIDSwitch();
            SwNextStation = new HIDSwitch();
            SwTrackMonitor = new HIDSwitch();
            RegisterCommand(UserCommand.ControlPantograph1, SwPantograph1);                        // pantograph1
            RegisterCommand(UserCommand.ControlPantograph2, SwPantograph2);                        // pantograph2
            RegisterCommand(UserCommand.DisplayNextStationWindow, SwNextStation);                  // F10 window
            RegisterCommand(UserCommand.DisplayTrackMonitorWindow, SwTrackMonitor);                // F4 window

            // Buttons
            BPause = new HIDButton();
            BChangeTCSMode = new HIDButton();
            BView1 = new HIDButton();
            BView2 = new HIDButton();
            BView3 = new HIDButton();
            RegisterCommand(UserCommand.GamePauseMenu, BPause);                                    // Pause / close activity window
            RegisterCommand(UserCommand.GameSwitchManualMode, BChangeTCSMode);                     // Change TCS signal mode (ctrl + m)
            RegisterCommand(UserCommand.CameraCab, BView1);                                        // Cab view
            RegisterCommand(UserCommand.CameraOutsideFront, BView2);                               // Outside front view
            RegisterCommand(UserCommand.CameraSpecialTracksidePoint, BView3);                      // Special trackside point view

            BHeadlightsIncrease = new HIDButton();
            BHeadlightsDecrease = new HIDButton();
            RegisterCommand(UserCommand.ControlHeadlightIncrease, BHeadlightsIncrease);            // Headlight increase
            RegisterCommand(UserCommand.ControlHeadlightDecrease, BHeadlightsDecrease);            // Headlight decrease

            CabControls[(new CabViewControlType(CABViewControlTypes.THROTTLE), -1)] = AThrottle;       // Throttle
            CabControls[(new CabViewControlType(CABViewControlTypes.DIRECTION), -1)] = ADirection;      // Direction
            CabControls[(new CabViewControlType(CABViewControlTypes.ENGINE_BRAKE), -1)] = AEngineBreak;   // Engine break
            CabControls[(new CabViewControlType(CABViewControlTypes.TRAIN_BRAKE), -1)] = ATrainBreak;      // Train break

            Active = true;
            HIDPantographStateService.Instance.Enable();
        }
        public void Update()
        {
            if (Device is null || !Device.Enabled || !Active)
            {
                return; // Device not connected or not enabled
            }
            HIDDeviceReport report;
            try
            {
                report = Device.ReadInput();
            }
            catch (Exception ex)
            {
                // Something went wrong while reading the device.
                // Log the error
                Console.WriteLine($"Error reading HID device: {ex.Message}");
                return;
            } 

            if (report == null) return;

            /*
             * Minimum is 0, maximum is 4096. Good idea to add some deadzone near the edges of the range
             * Direction 10-1600 
             * Throttle 2400-4090
             * Engine brake 0-anything really. For now it will be the max value
             */
            AThrottle.Value = PercentageTrim(report.AxisThrottle, 2400, 4090) / 100; // MSTSLocomitveViewer.cs:253 for some reason does *100 again
            float directionRawPercentage = PercentageTrim(report.AxisDirection, 10, 1600); // there is no 100 here because we dont deal with %
            // 0-33 = reverse, 34-65 = neutral, 66-100 = forward.
            ADirection.Value =  (directionRawPercentage <= 25) ? -1.0f : 
                                (directionRawPercentage >= 75) ? 1.0f : 0.0f;
            
            AEngineBreak.Value = PercentageTrim(report.AxisEngineBrake, 10, 4090) / 100;

            // --- stabilize raw switch value ---
            if (report.TrainBrake == _lastTrainBrakeRaw) { _trainBrakeStableCount++; }
            else
            {
                _lastTrainBrakeRaw = report.TrainBrake;
                _trainBrakeStableCount = 1;
            }

            // Only accept change when stable
            if (_trainBrakeStableCount >= StableThreshold)
            {
                float TrainBrakeValue = ATrainBreak.Value;

                switch (report.TrainBrake)
                {
                    case 0: TrainBrakeValue = 2.0f; break;
                    case 1: TrainBrakeValue = 0f; break;
                    case 2: TrainBrakeValue = 0.30f; break;
                    case 3: TrainBrakeValue = 0.70f; break;
                }

                ATrainBreak.Value = TrainBrakeValue;
            }


            if (report.Pause)
            {
                if (_pressStartTime == null)
                {
                    _pressStartTime = DateTime.UtcNow;
                    _longPressTriggered = false;
                }

                if (!_longPressTriggered &&
                    (DateTime.UtcNow - _pressStartTime.Value).TotalMilliseconds >= LongPressMilliseconds)
                {
                    BChangeTCSMode.Update(true);
                    _longPressTriggered = true;    // Prevent retrigger
                }
            }
            else
            {
                // Button released
                if (_pressStartTime != null)
                {
                    // If long press never triggered → short press
                    if (!_longPressTriggered)
                    {
                        BPause.Update(true);
                    }

                    _pressStartTime = null;
                    _longPressTriggered = false;
                } else
                {
                    // Ensure buttons are reset after the button was released
                    BChangeTCSMode.Update(false);
                    BPause.Update(false);
                }
            }

            SwTrackMonitor.Update(report.TrackMonitor);
            SwNextStation.Update(report.NextStation);

            BHeadlightsIncrease.Update(report.Headlights);
            BHeadlightsDecrease.Update(!report.Headlights);

            // TODO: get info on panto state: if it doesnt match up, send a signal anyway so it matches
            HIDPantographStateService.Instance.SetPantoState(report.Panto1, 1);
            HIDPantographStateService.Instance.SetPantoState(report.Panto2, 2);
            SwPantograph1.Update(report.Panto1);
            SwPantograph2.Update(report.Panto2);
            // View switch emulated as buttons
            BView1.Update(report.View == 0);
            BView2.Update(report.View == 1);
            BView3.Update(report.View == 2);
        }

        private static float PercentageTrim(int value, int min, int max)
        {
            if (value < min) value = min;
            if (value > max) value = max;
            return (value - min) * 100 / (max - min);
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
