using StarMap.API;

namespace KittenRemoteControl
{
    [StarMapMod]
    public class RemoteControlMain
    {
        private const int Port = 8080;
        private const int TelemachusPort = 8085;
        private TcpHttpServer? _httpServer;
        private TcpHttpServer? _telemachusServer;

        [StarMapAfterGui]
        public void OnAfterUi(double dt)
        {
            // Runs on the main/UI thread each frame — execute work queued by HTTP handler threads
            // (e.g. staging) here so game-state mutations don't race the simulation thread.
            MainThreadDispatcher.Drain();
        }

        [StarMapAllModsLoaded]
        public void OnFullyLoaded()
        {
            Patcher.Patch();

            // Initialize the built-in TCP HTTP server.
            // We deliberately avoid HttpListener (used by Grapevine) because it relies on the
            // Windows HTTP Server API (http.sys), which is not implemented under Wine/CrossOver.
            try
            {
                _httpServer = new TcpHttpServer(Port);

                var resource = new RemoteControlResource();
                resource.RegisterRoutes(_httpServer);

                _httpServer.Start();

                Console.WriteLine($"Remote Control REST Server started successfully on http://localhost:{Port}");
                Console.WriteLine($"Registered {_httpServer.RouteCount} routes. Available endpoints:");
                Console.WriteLine("  GET/PUT /control/throttle");
                Console.WriteLine("  GET/PUT /control/engineOn");
                Console.WriteLine("  GET/POST /control/thrusters");
                Console.WriteLine("  GET/PUT /control/referenceFrame");
                Console.WriteLine("  GET /control/referenceFrames");
                Console.WriteLine("  GET/PUT /control/flightComputer/attitudeMode");
                Console.WriteLine("  GET /control/flightComputer/attitudeModes");
                Console.WriteLine("  PUT /control/flightComputer/stabilization");
                Console.WriteLine("  GET /telemetry/apoapsis");
                Console.WriteLine("  GET /telemetry/periapsis");
                Console.WriteLine("  GET /telemetry/orbitingBody/meanRadius");
                Console.WriteLine("  GET /telemetry/apoapsis_elevation");
                Console.WriteLine("  GET /telemetry/periapsis_elevation");
                Console.WriteLine("  GET /telemetry/orbitalSpeed");
                Console.WriteLine("  GET /telemetry/propellantMass");
                Console.WriteLine("  GET /telemetry/totalMass");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start REST server: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            // Telemachus-compatible datalink server (separate port, bound to all interfaces).
            try
            {
                _telemachusServer = new TcpHttpServer(TelemachusPort);
                new TelemachusDatalink().RegisterRoutes(_telemachusServer);
                _telemachusServer.Start();
                Console.WriteLine($"Telemachus datalink started on http://0.0.0.0:{TelemachusPort}/telemachus/datalink");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start Telemachus datalink server: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        [StarMapImmediateLoad]
        public void OnImmediatLoad()
        {
        }

        [StarMapUnload]
        public void Unload()
        {
            try
            {
                _httpServer?.Dispose();
                _httpServer = null;
                _telemachusServer?.Dispose();
                _telemachusServer = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping REST server: {ex.Message}");
            }

            Patcher.Unload();
        }
    }
}
