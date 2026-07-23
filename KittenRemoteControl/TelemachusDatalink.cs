using System.Globalization;
using System.Text.Json;
using KSA;

namespace KittenRemoteControl
{
    /// <summary>
    /// Telemachus-compatible "datalink" endpoint (subset).
    ///
    /// GET/POST /telemachus/datalink. Request is a set of label=apiString pairs (query string for
    /// GET, JSON object body for POST). Response is a flat JSON object {"label": value, ...}.
    ///
    /// Implemented so far: a.version (heartbeat), v.altitude (read), f.setThrottle (command).
    /// Unknown/unmapped keys return -1 so the MicroBlocks client keeps working while more keys are
    /// added. Number formatting honours the reserved GET params _int / _precision (see FormatValue).
    /// </summary>
    public sealed class TelemachusDatalink
    {
        // Non-empty, always evaluable — the client uses this as the connection heartbeat.
        private const string Version = "1.0.0";

        public void RegisterRoutes(TcpHttpServer server)
        {
            server.MapGet("/telemachus/datalink", Handle);
            server.MapPost("/telemachus/datalink", Handle);
        }

        private HttpResponse Handle(HttpRequest req)
        {
            try
            {
                var data = new List<KeyValuePair<string, string>>();
                bool asInt = false;
                int precision = -1; // -1 = unset => values returned raw/unrounded

                // Reserved globals (_int/_precision/_scale) come from the GET query only.
                if (!string.IsNullOrEmpty(req.RawQuery))
                    ParseQuery(req.RawQuery, data, ref asInt, ref precision);

                // POST body: JSON object mapping label -> apiString (no globals here).
                if (!string.IsNullOrWhiteSpace(req.Body) && req.Body.TrimStart().StartsWith("{"))
                    ParseJsonBody(req.Body, data);

                var results = new Dictionary<string, object?>();
                foreach (var kv in data)
                {
                    var value = Evaluate(kv.Value);
                    results[kv.Key] = FormatValue(value, asInt, precision);
                }

                return Json(results);
            }
            catch (Exception ex)
            {
                // The client requires valid JSON starting with '{' to detect a live connection.
                return Json(new Dictionary<string, object?> { ["error"] = ex.Message });
            }
        }

        private static HttpResponse Json(object body) => new()
        {
            StatusCode = 200,
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(body)
        };

        private static void ParseQuery(string query, List<KeyValuePair<string, string>> data,
            ref bool asInt, ref int precision)
        {
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                if (eq < 0) continue; // Telemachus drops params without exactly one '='
                var label = Uri.UnescapeDataString(part[..eq]);
                var api = Uri.UnescapeDataString(part[(eq + 1)..]);

                switch (label)
                {
                    case "_int": asInt = string.Equals(api, "true", StringComparison.OrdinalIgnoreCase); break;
                    case "_precision": int.TryParse(api, NumberStyles.Integer, CultureInfo.InvariantCulture, out precision); break;
                    case "_scale": break; // global scale not implemented (pipe |scale: is)
                    default:
                        if (!label.StartsWith("_")) data.Add(new(label, api));
                        break;
                }
            }
        }

        private static void ParseJsonBody(string body, List<KeyValuePair<string, string>> data)
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var api = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? ""
                    : prop.Value.GetRawText();
                data.Add(new(prop.Name, api));
            }
        }

        /// <summary>Parse "name[args]|scale:min,max" and dispatch to a KSA getter/command.</summary>
        private object Evaluate(string api)
        {
            if (string.IsNullOrWhiteSpace(api)) return -1;

            // Trailing pipe modifier, e.g. |scale:0,1023
            double? scaleMin = null, scaleMax = null;
            var core = api;
            var pipe = api.IndexOf('|');
            if (pipe >= 0)
            {
                core = api[..pipe];
                var mod = api[(pipe + 1)..];
                if (mod.StartsWith("scale:"))
                {
                    var mm = mod["scale:".Length..].Split(',');
                    if (mm.Length == 2 &&
                        double.TryParse(mm[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var mn) &&
                        double.TryParse(mm[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var mx))
                    {
                        scaleMin = mn; scaleMax = mx;
                    }
                }
            }

            // name + [args]. Empty brackets "[]" yield a single empty-string arg (Telemachus quirk).
            var name = core;
            var args = Array.Empty<string>();
            var lb = core.IndexOf('[');
            if (lb >= 0 && core.EndsWith("]"))
            {
                name = core[..lb];
                var inner = core[(lb + 1)..^1];
                args = inner.Split(','); // "" -> [""], matching Telemachus
            }

            // Input scaling: map the first numeric arg from [min,max] to [0,1].
            if (scaleMin.HasValue && args.Length >= 1 &&
                double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var rawArg))
            {
                var span = scaleMax!.Value - scaleMin.Value;
                var scaled = Math.Abs(span) > double.Epsilon ? (rawArg - scaleMin.Value) / span : rawArg;
                args = (string[])args.Clone();
                args[0] = scaled.ToString("R", CultureInfo.InvariantCulture);
            }

            return Dispatch(name, args);
        }

        private object Dispatch(string name, string[] args)
        {
            switch (name)
            {
                case "a.version":
                    return Version;

                case "v.altitude":
                {
                    var vehicle = Program.ControlledVehicle;
                    if (vehicle == null) return -1;
                    return Program.CurrentAltitudeKm * 1000.0; // km -> m
                }

                case "f.setThrottle":
                {
                    var t = args.Length >= 1 &&
                            double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                        ? v : 0.0;
                    t = Math.Clamp(t, 0.0, 1.0);
                    try { ManualControlHelper.SetManualControlValue("EngineThrottle", (float)t); }
                    catch { /* no vehicle etc. — still acknowledge the command */ }
                    return 0; // action success
                }

                default:
                    return -1; // unknown / not yet mapped -> missing sentinel
            }
        }

        /// <summary>
        /// Telemachus output formatting: only rounds when _precision >= 0; when _int is also set,
        /// returns (long)(round(value, precision) * 10^precision). Strings/bools pass through.
        /// </summary>
        private static object? FormatValue(object value, bool asInt, int precision)
        {
            if (value is string || value is bool) return value;

            var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (precision < 0 || precision > 15) return d; // raw

            var r = Math.Round(d, precision, MidpointRounding.AwayFromZero);
            if (asInt) return (long)(r * Math.Pow(10, precision));
            return r;
        }
    }
}
