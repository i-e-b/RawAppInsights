using System;
using System.Collections.Generic;
using System.Threading;
using ShiftIt.Http;
using SkinnyJson;

namespace RawAppInsights
{
    class Program
    {
        static Random rnd;

        static void Main()
        {
            // This is a test of sending raw requests to App Insights, to work with wrappers, proxies
            // and generally avoid the AI SDKs and their requirement to continually update.
            //
            // Notes:
            //
            // https://robertoprevato.github.io/Using-Azure-Application-Insights-with-aiohttp/          <-- inspiration
            // https://github.com/Microsoft/ApplicationInsights-Home/tree/master/EndpointSpecs          <-- technical info

            // It's not often mentioned, but this is security sensitive data:
            // NEVER share your AI iKey!
            var instrumentationKey = "34572dd6-37d4-4e26-9c40-5b016e8600b9"; // this is a throw-away on a free account

            // This is the global AI tracking URL
            var targetUrl = "https://dc.services.visualstudio.com/v2/track";

            Json.DefaultParameters.SerializeNullValues = false;
            rnd = new Random();

            while ( ! Console.KeyAvailable)
            {
                Thread.Sleep(250);
                Console.WriteLine("Building sample structure");

                var goodRequest = SamplePageRequest(instrumentationKey);
                var availabilityStruct = SampleAvailability(instrumentationKey);
                var availabilityFail = Sample_Fail_Availability(instrumentationKey);
                var badRequest = Sample_Fail_PageRequest(instrumentationKey);
                var error = SampleException(instrumentationKey);

                // I've been told by DS of Microsoft that packing too many requests into one call
                // can cause issues. Not sure what the actual ceiling is.
                var rawJson = Json.Freeze(new[] { goodRequest, availabilityStruct , availabilityFail , error , badRequest });

                Console.WriteLine("Sending...");
                var rq = new HttpRequestBuilder().Post(new Uri(targetUrl)).Build(rawJson);
                using (var result = new HttpClient().Request(rq))
                {
                    var body = result.BodyReader.ReadStringToLength();
                    Console.WriteLine(body);
                }
            }
            
            Console.ReadKey(); // eat the interrupting key
            Console.WriteLine("\r\n[Done]");
            Console.ReadKey();
        }

        private static object Envelope(string envelopeType, string instrumentationKey, string type, object obj) {
            return new
            {
                ver = 1,
                name = envelopeType,
                time = DateTime.UtcNow.ToString("o"),
                sampleRate = 100.0,
                //seq = "3",
                iKey = instrumentationKey,
                flags = 0L,
                tags = new Dictionary<string, string>
                {
                    {"ai.device.id", Environment.MachineName},
                    {"ai.device.locale", "en-US"},
                    {"ai.device.osVersion", Environment.OSVersion.ToString()},
                    {"ai.device.type", "Other"},
                    {"ai.internal.sdkVersion", "aspnet:2.3.0"}
                },
                data = new
                {
                    baseType = type,
                    baseData = obj
                }
            };
        }

        private static object SampleException(string instrumentationKey)
        {
            return Envelope("Microsoft.ApplicationInsights.Exception", instrumentationKey,
                /*"Microsoft.ApplicationInsights.*/"ExceptionData", new ExceptionRecord
                {
                    ver = 2,
                    exceptions = new List<ExceptionDetails>{
                        new ExceptionDetails{
                            id = 0,
                            message = "Kablammo",
                            hasFullStack = false,
                            stack = "This is the stack trace",
                            parsedStack = new List<StackFrameData>(),
                            typeName = "System.Exception"
                        }
                    },
                    problemId = "sampleproblem",
                    severityLevel = SeverityLevel.Error,
                    measurements = new Dictionary<string, double>(),
                    properties = new Dictionary<string, string>()
                }
            );
        }

        private static object SampleAvailability(string instrumentationKey)
        {
            return Envelope("Microsoft.ApplicationInsights.Availability", instrumentationKey,
                "AvailabilityData", new AvailabilityRecord
                {
                    ver = 2,
                    id = "dc0d1d70-41dc-4216-892c-c043f0575dd8",
                    name = "locA",
                    duration = "00:00:00.125",
                    success = true,
                });
        }

        private static object Sample_Fail_Availability(string instrumentationKey)
        {
            return Envelope("Microsoft.ApplicationInsights.Availability", instrumentationKey,
                "AvailabilityData", new AvailabilityRecord
                {
                    ver = 2,
                    id = "dc0d1d70-41dc-4216-892c-c043f0575dd8",
                    name = "locB",
                    duration = "00:00:00.125",
                    success = false,
                });
        }

        private static object SamplePageRequest(string instrumentationKey)
        {
            return Envelope("Microsoft.ApplicationInsights.Request", instrumentationKey,
                "RequestData", new ApiRequestRecord
                {
                    ver = 2,
                    id = "dc0d1d70-41dc-4216-892c-c043f0575dd8",
                    name = "AddToFavorites",
                    duration = "00:00:00." + rnd.Next(100, 900),
                    responseCode = "200",
                    success = true,
                    url = "/api/v1/favorites"
                });
        }

        private static object Sample_Fail_PageRequest(string instrumentationKey)
        {
            return Envelope("Microsoft.ApplicationInsights.Request", instrumentationKey,
                "RequestData", new ApiRequestRecord
                {
                    ver = 2,
                    id = "dc0d1d70-41dc-4216-892c-c043f0575dd8",
                    name = "RemoveFromFavorites",
                    duration = "00:00:00." + rnd.Next(100, 900),
                    responseCode = "503",
                    success = false,
                    url = "/api/v1/favorites"
                });
        }
    }

    public struct AvailabilityRecord
    {
        public int ver;
        public string id;
        public string name;
        public string duration;
        public bool success;
        public string runLocation;
        public string message;
        public Dictionary<string, string> properties;
        public Dictionary<string, double> measurements;
    }

    public struct ApiRequestRecord
    {
        public int ver;
        public string id;
        public string duration;
        public string responseCode;
        public bool success;
        public string source;
        public string name;
        public string url;
        public Dictionary<string, string> properties;
        public Dictionary<string, double> measurements;
    }

    public enum SeverityLevel
    {
        Verbose, Information, Warning, Error, Critical
    }

    public struct ExceptionRecord
    {
        public int ver;
        public List<ExceptionDetails> exceptions;
        public SeverityLevel severityLevel;
        public string problemId;
        public Dictionary<string, string> properties;
        public Dictionary<string, double> measurements;
    }

    public struct ExceptionDetails
    {
        public int id;
        public int outerId;
        public string typeName;
        public string message;
        public bool hasFullStack;
        public string stack;
        public List<StackFrameData> parsedStack;
    }

    public struct StackFrameData
    {
        public int level;
        public string method;
        public string assembly;
        public string fileName;
        public int line;
    }
}
