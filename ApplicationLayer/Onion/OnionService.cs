using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using DotNetTor.SocksPort;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TangramCypher.ApplicationLayer;
using TangramCypher.Helpers;
using TangramCypher.Helpers.LibSodium;

namespace Cypher.ApplicationLayer.Onion
{
    public class OnionService : HostedService, IOnionService, IDisposable
    {
        const string ONION = "onion";
        const string TORRC = "torrc";
        const string SOCKS_HOST = "onion_socks_host";
        const string SOCKS_PORT = "onion_socks_port";
        const string CONTROL_HOST = "onion_control_host";
        const string CONTROL_PORT = "onion_control_port";
        const string HASHED_CONTROL_PASSWORD = "onion_hashed_control_password";

        readonly ICryptography cryptography;
        readonly IConfigurationSection onionSection;
        readonly ILogger logger;
        readonly IConsole console;
        readonly string socksHost;
        readonly int socksPort;
        readonly string controlHost;
        readonly int controlPort;
        readonly string onionDirectory;
        readonly string torrcPath;
        readonly string controlPortPath;

        string hashedPassword;

        Process TorProcess { get; set; }

        public bool OnionStarted { get; private set; }

        public OnionService(ICryptography cryptography, IConfiguration configuration, ILogger logger, IConsole console)
        {
            this.cryptography = cryptography;
            onionSection = configuration.GetSection(ONION);

            this.logger = logger;
            this.console = console;

            socksHost = onionSection.GetValue<string>(SOCKS_HOST);
            socksPort = onionSection.GetValue<int>(SOCKS_PORT);
            controlHost = onionSection.GetValue<string>(CONTROL_HOST);
            controlPort = onionSection.GetValue<int>(CONTROL_PORT);

            onionDirectory = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), ONION);
            torrcPath = Path.Combine(onionDirectory, TORRC);
            controlPortPath = Path.Combine(onionDirectory, "control-port");
        }

        public async Task<string> GetAsync(string url, object data)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Url cannot be null or empty!", nameof(url));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                using (var httpClient = new HttpClient(new SocksPortHandler(socksHost, socksPort)))
                {
                    string requestUrl = $"{url}?{GetQueryString(data)}";

                    logger.LogInformation($"GetAsync Start, requestUrl:{requestUrl}");

                    var response = await httpClient.GetAsync(requestUrl).ConfigureAwait(false);
                    string result = await response.Content.ReadAsStringAsync();

                    logger.LogInformation($"GetAsync End, requestUrl:{requestUrl}, HttpStatusCode:{response.StatusCode}, result:{result}");

                    return result;
                }

            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    string responseContent = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
                    throw new Exception($"Response :{responseContent}", ex);
                }
                throw;
            }
        }

        public void ChangeCircuit(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty!", nameof(password));
            }

            try
            {
                var controlPortClient = new DotNetTor.ControlPort.Client(controlHost, controlPort, password);
                controlPortClient.ChangeCircuitAsync().Wait();
            }
            catch (DotNetTor.TorException ex)
            {
                console.WriteLine(ex.Message);
            }
        }

        public string GenerateHashPassword(string password)
        {
            var torProcessStartInfo = new ProcessStartInfo(GetTorFileName())
            {
                Arguments = $"--hash-password {password}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };

            TorProcess = Process.Start(torProcessStartInfo);

            var sOut = TorProcess.StandardOutput;

            var raw = sOut.ReadToEnd();

            var lines = raw.Split(Environment.NewLine);

            string result = string.Empty;

            //  If it's multi-line use the last non-empty line.
            //  We don't want to pull in potential warnings.
            if(lines.Length > 1)
            {
                var rlines = lines.Reverse();
                foreach(var line in rlines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        result = Regex.Replace(line, Environment.NewLine, string.Empty);
                        logger.LogInformation($"Hopefully password line: {line}");
                        break;
                    }
                }
            }

            if (!TorProcess.HasExited)
            {
                TorProcess.Kill();
            }

            sOut.Close();
            TorProcess.Close();
            TorProcess = null;

            hashedPassword = Regex.Match(result, "16:[0-9A-F]+")?.Value ?? string.Empty;

            return password;
        }

        void SendCommands(string command, string password)
        {
            if (string.IsNullOrEmpty(command))
            {
                throw new ArgumentException("Command cannot be null or empty!", nameof(command));
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty!", nameof(password));
            }

            try
            {
                var controlPortClient = new DotNetTor.ControlPort.Client(controlHost, controlPort, password);
                var result = controlPortClient.SendCommandAsync(command).GetAwaiter().GetResult();
            }
            catch (DotNetTor.TorException ex)
            {
                console.WriteLine(ex.Message);
            }
        }

        public async Task<T> PostAsync<T>(string url, object data) where T : class, new()
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("Url cannot be null or empty!", nameof(url));
            }

            try
            {
                using (var httpClient = new HttpClient(new SocksPortHandler(socksHost, socksPort)))
                {
                    string content = JsonConvert.SerializeObject(data);
                    var buffer = Encoding.UTF8.GetBytes(content);
                    var byteContent = new ByteArrayContent(buffer);

                    byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    var response = await httpClient.PostAsync(url, byteContent).ConfigureAwait(false);
                    string result = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        logger.LogError($"GetAsync End, url:{url}, HttpStatusCode:{response.StatusCode}, result:{result}");
                        return new T();
                    }

                    logger.LogInformation($"GetAsync End, url:{url}, result:{result}");

                    return JsonConvert.DeserializeObject<T>(result);
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                {
                    string responseContent = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
                    throw new Exception($"response :{responseContent}", ex);
                }
                throw;
            }
        }

        public void StartOnion(string password)
        {
            OnionStarted = false;

            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password cannot be null or empty!", nameof(password));
            }

            CreateTorrc();
            StartTorProcess();

            Task.Delay(3000).GetAwaiter().GetResult();

            var controlPortClient = new DotNetTor.ControlPort.Client(controlHost, ReadControlPort(), password);

            try
            {
                controlPortClient.IsCircuitEstablishedAsync().GetAwaiter().GetResult();
                OnionStarted = true;
            }
            catch
            {
                var established = false;
                var count = 0;

                while (!established)
                {
                    if (count >= 21) throw new Exception("Couldn't establish circuit in time.");

                    try
                    {
                        established = controlPortClient.IsCircuitEstablishedAsync().GetAwaiter().GetResult();
                        OnionStarted = true;

                        Task.Delay(1000).GetAwaiter().GetResult();
                    }
                    catch (DotNetTor.TorException ex)
                    {
                        logger.LogWarning(string.Format("Trying to establish circuit: {0}", ex.Message));
                    }

                    count++;
                }
            }

            if (OnionStarted)
            {
                logger.LogInformation("Tor is running...... ;)");
            }
        }

        static string GetQueryString(object obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            var properties = from p in obj.GetType().GetProperties()
                             where p.GetValue(obj, null) != null
                             select p.Name + "=" + HttpUtility.UrlEncode(p.GetValue(obj, null).ToString());

            return String.Join("&", properties.ToArray());
        }

        static string GetTorFileName()
        {
            var sTor = "tor";

            if (Util.GetOSPlatform() == OSPlatform.Windows)
                sTor = "tor.exe";

            return sTor;
        }

        void CreateTorrc()
        {
            if (string.IsNullOrEmpty(hashedPassword))
            {
                throw new ArgumentException("Hashed control password is not set.", nameof(hashedPassword));
            }

            if (!Directory.Exists(onionDirectory))
            {
                try
                {
                    Directory.CreateDirectory(onionDirectory);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                    throw new Exception(ex.Message);
                }
            }

            if (File.Exists(torrcPath))
                return;

            var torrcContent = new string[] {
                "AvoidDiskWrites 1",
                string.Format("HashedControlPassword {0}", hashedPassword),
                "SocksPort auto IPv6Traffic PreferIPv6 KeepAliveIsolateSOCKSAuth",
                "ControlPort auto",
                "CookieAuthentication 1",
                "CircuitBuildTimeout 10",
                "KeepalivePeriod 60",
                "NumEntryGuards 8",
                "SocksPort 9050",
                "Log notice stdout",
                $"DataDirectory {onionDirectory}",
                $"ControlPortWriteToFile {controlPortPath}"
            };

            try
            {
                using (StreamWriter outputFile = new StreamWriter(torrcPath))
                {
                    foreach (string content in torrcContent)
                        outputFile.WriteLine(content);
                }
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex.Message);
                throw new Exception(ex.Message);
            }

            logger.LogInformation($"Created torrc file: {torrcPath}");
        }

        int ReadControlPort()
        {
            int port = 0;

            if (File.Exists(controlPortPath))
            {
                try
                {
                    int.TryParse(Util.Pop(File.ReadAllText(controlPortPath, Encoding.UTF8), ":"), out port);
                }
                catch { }
            }

            return port == 0 ? controlPort : port;
        }

        void StartTorProcess()
        {
            TorProcess = new Process();
            TorProcess.StartInfo.FileName = GetTorFileName();
            TorProcess.StartInfo.Arguments = $"-f \"{torrcPath}\"";
            TorProcess.StartInfo.UseShellExecute = false;
            TorProcess.StartInfo.CreateNoWindow = true;
            TorProcess.StartInfo.RedirectStandardOutput = true;
            TorProcess.OutputDataReceived += (sender, e) =>
            {
                if (!String.IsNullOrEmpty(e.Data))
                {
                    logger.LogInformation(e.Data);
                }
            };

            TorProcess.Start();
            TorProcess.BeginOutputReadLine();
        }

        public void Dispose()
        {
            TorProcess?.Kill();
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            console.WriteLine("Starting Onion Service");
            logger.LogInformation("Starting Onion Service");

            StartOnion(GenerateHashPassword("ILoveTangram"));

            return;
        }
    }
}
