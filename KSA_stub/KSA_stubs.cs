// Minimal compile-time stub of the KSA types used by KittenRemoteControl.
//
// IMPORTANT: member KIND (field vs property vs method), SIGNATURES and enum VALUES here must
// match the real KSA.dll exactly. The mod binds to the real assembly at runtime by member
// reference, so e.g. declaring a real field as a property emits a get_X() call that the runtime
// cannot find ("MissingMethodException"). These definitions mirror KSA.dll (KSA 2026.7.x).
using System;

namespace KSA
{
    public static class Program
    {
        // Real KSA: a public static FIELD (not a property).
        public static Vehicle? ControlledVehicle;
    }

    public class Vehicle
    {
        public Orbit? Orbit { get; set; }
        public FlightComputer FlightComputer { get; set; } = new FlightComputer();

        private NavBallData _navBallData;
        // Real KSA: a ref-returning property over a NavBallData struct.
        public ref NavBallData NavBallData => ref _navBallData;

        public double OrbitalSpeed { get; set; }
        public float PropellantMass { get; set; }
        public float TotalMass { get; set; }

        // Private field accessed by reflection in ManualControlHelper.
        private ManualControlInputs _manualControlInputs;

        public void SetNavBallFrame(VehicleReferenceFrame frame) { /* noop in stub */ }
        public void SetStabilization(bool on) { /* noop in stub */ }
    }

    public class Orbit
    {
        public double Apoapsis { get; set; }
        public double Periapsis { get; set; }
        public IParentBody? Parent { get; set; }
    }

    public interface IParentBody
    {
        double GetNearSurfaceRadius();
    }

    public class FlightComputer
    {
        // Real KSA: a public FIELD (not a property).
        public FlightComputerAttitudeMode AttitudeMode;
        public void RateHold(VehicleReferenceFrame frame) { /* noop */ }
    }

    public enum FlightComputerAttitudeMode
    {
        Manual = 0,
        Auto = 1,
    }

    // Real KSA: a struct; Frame is a public field.
    public struct NavBallData
    {
        public VehicleReferenceFrame Frame;
    }

    public enum VehicleReferenceFrame
    {
        EclBody = 0,
        EnuBody = 1,
        Lvlh = 2,
        VlfBody = 3,
        BurnBody = 4,
        Dock = 5,
    }

    // Manual control inputs struct, accessed via reflection in ManualControlHelper.
    public struct ManualControlInputs
    {
        public bool EngineOn;
        public float EngineThrottle;
        public ThrusterMapFlags ThrusterCommandFlags;
    }

    [Flags]
    public enum ThrusterMapFlags
    {
        None = 0,
        RollRight = 2,
        RollLeft = 4,
        PitchUp = 8,
        PitchDown = 16,
        YawRight = 32,
        YawLeft = 64,
        TranslateForward = 128,
        TranslateBackward = 256,
        TranslateRight = 512,
        TranslateLeft = 1024,
        TranslateDown = 2048,
        TranslateUp = 4096,
    }
}
