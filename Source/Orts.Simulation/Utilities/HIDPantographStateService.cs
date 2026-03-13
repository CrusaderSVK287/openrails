using System;

namespace Orts.Simulation.Utilities
{
    public sealed class HIDPantographStateService
    {
        private static readonly Lazy<HIDPantographStateService> _instance =
            new Lazy<HIDPantographStateService>(() => new HIDPantographStateService());

        public static HIDPantographStateService Instance => _instance.Value;

        private readonly object _lock = new object();

        private bool[] _pantoState = new bool[2];
        private bool _enabled;

        private HIDPantographStateService() { _enabled = false; _pantoState[0] = false; _pantoState[1] = false; }

        public bool GetPantoState(int item)
        {
            lock (_lock)
            {
                return _pantoState[item - 1];
            }
        }

        public void SetPantoState(bool value, int item)
        {
            lock (_lock)
            {
                _pantoState[item - 1] = value;
            }
        }

        public void Enable() { _enabled = true; }
        public bool Enabled() { return _enabled; }
    }
}
