using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Polly;

namespace Chat
{
    public class TorManager
    {
        public int SocksPort { get; }
        public int LocalTcpPort { get; }
        public int ControlPort { get; }
        public string? OnionAddress { get; private set; }

        private readonly string _dataDir = string.Empty;
        private Process? _torProcess;
        public SecureRamKey? GeneratedKey { get; private set; }

        public event Action<string>? OnLog;
        public event Action<string>? OnReady;

        public enum LogLevel { Info, Warning, Error }

        private void Log(string message, LogLevel level = LogLevel.Info)
        {
            System.Diagnostics.Debug.WriteLine($"[{level}] {message}");
        }

        public TorManager()
        {
            Log("[START] TorManager constructor started.");

            SocksPort = AllocatePort();
            LocalTcpPort = AllocatePort();
            ControlPort = AllocatePort();

            int slotId = int.Parse(Program.InstanceId);
            bool slotFound = false;

            while (!slotFound)
            {
                _dataDir = Path.Combine(Path.GetTempPath(), $"TorChat_Data_{slotId}");
                Directory.CreateDirectory(_dataDir);

                string pidFile = Path.Combine(_dataDir, "tor.pid");
                if (File.Exists(pidFile))
                {
                    string pidText = File.ReadAllText(pidFile).Trim();
                    if (int.TryParse(pidText, out int oldPid))
                    {
                        bool isAlive = false;
                        try
                        {
                            var proc = Process.GetProcessById(oldPid);
                            isAlive = !proc.HasExited;
                        }
                        catch { }

                        if (isAlive)
                        {
                            slotId++;
                            continue;
                        }
                    }
                }

                // Slot available
                string hsCleanup = Path.Combine(_dataDir, "hs");
                if (Directory.Exists(hsCleanup))
                {
                    Directory.Delete(hsCleanup, true);
                }

                string lockFile = Path.Combine(_dataDir, "lock");
                if (File.Exists(lockFile))
                {
                    File.Delete(lockFile);
                }
                
                string cookieFile = Path.Combine(_dataDir, "control_auth_cookie");
                if (File.Exists(cookieFile))
                {
                    File.Delete(cookieFile);
                }

                slotFound = true;
            }
        }

        public Task StartAsync(SecureRamKey? secretKey)
        {
            Log("[START] TorManager.StartAsync started.");

            var ioPolicy = Policy
                .Handle<IOException>()
                .Or<UnauthorizedAccessException>()
                .WaitAndRetry(3, attempt => TimeSpan.FromMilliseconds(300 * attempt),
                    (ex, span, attempt, ctx) =>
                    {
                        Log($"[RETRY] TorManager.StartAsync I/O attempt {attempt} failed: {ex.Message}", LogLevel.Warning);
                    });

            try
            {
                ioPolicy.Execute(() =>
                {
                    string torrcPath = Path.Combine(_dataDir, "torrc");
                    string torrc = string.Join('\n',
                        $"SocksPort 127.0.0.1:{SocksPort}",
                        $"ControlPort 127.0.0.1:{ControlPort}",
                        "CookieAuthentication 1",
                        "SocksTimeout 20",
                        "Log notice stdout",
                        $"DataDirectory \"{_dataDir.Replace('\\', '/')}\"");

                    File.WriteAllText(torrcPath, torrc);
                });

                string torrcFile = Path.Combine(_dataDir, "torrc");
                _torProcess = new Process();
                _torProcess.StartInfo = new ProcessStartInfo
                {
                    FileName = "tor",
                    Arguments = $"-f \"{torrcFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _torProcess.OutputDataReceived += (_, e) =>
                {
                    if (e.Data == null) return;
                    Log($"[Tor Output] {e.Data}");
                    try
                    {
                        OnLog?.Invoke(e.Data);
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Exception in OnLog event handler: {ex.Message}", LogLevel.Error);
                    }
                };

                _torProcess.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        Log($"[Tor Error Output] {e.Data}", LogLevel.Warning);
                    }
                };

                var processPolicy = Policy
                    .Handle<InvalidOperationException>()
                    .Or<System.ComponentModel.Win32Exception>()
                    .WaitAndRetry(3, attempt => TimeSpan.FromMilliseconds(500 * attempt),
                        (ex, span, attempt, ctx) =>
                        {
                            Log($"[RETRY] Tor process start attempt {attempt} failed: {ex.Message}", LogLevel.Warning);
                        });

                processPolicy.Execute(() =>
                {
                    _torProcess.Start();
                });

                _torProcess.BeginOutputReadLine();
                _torProcess.BeginErrorReadLine();

                string torPidFile = Path.Combine(_dataDir, "tor.pid");
                File.WriteAllText(torPidFile, _torProcess.Id.ToString());

                // Spawn background task to connect to ControlPort
                _ = ConnectControlPortAsync(secretKey);
            }
            catch (Exception ex)
            {
                Log($"[FATAL] TorManager.StartAsync failed: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                throw;
            }

            return Task.CompletedTask;
        }

        private async Task ConnectControlPortAsync(SecureRamKey? secretKey)
        {
            string cookieFile = Path.Combine(_dataDir, "control_auth_cookie");
            
            var fileReadPolicy = Policy
                .Handle<IOException>()
                .Or<UnauthorizedAccessException>()
                .WaitAndRetryAsync(10, attempt => TimeSpan.FromMilliseconds(500 * attempt),
                    (ex, span, attempt, ctx) =>
                    {
                        Log($"[RETRY] Reading cookie file attempt {attempt} failed: {ex.Message}", LogLevel.Warning);
                    });

            byte[]? cookie = null;
            for (int i = 0; i < 30; i++)
            {
                if (File.Exists(cookieFile))
                {
                    try
                    {
                        cookie = await fileReadPolicy.ExecuteAsync(async () => await File.ReadAllBytesAsync(cookieFile));
                        break;
                    }
                    catch { }
                }
                await Task.Delay(1000);
            }

            if (cookie == null)
            {
                Log("[FATAL] Could not read Tor control cookie.", LogLevel.Error);
                return;
            }

            string hexCookie = Convert.ToHexString(cookie);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, ControlPort);
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

                await writer.WriteLineAsync($"AUTHENTICATE {hexCookie}");
                string? authResponse = await reader.ReadLineAsync();
                if (authResponse == null || !authResponse.StartsWith("250"))
                {
                    Log($"[FATAL] ControlPort auth failed: {authResponse}", LogLevel.Error);
                    return;
                }

                string keyArg = "NEW:ED25519-V3";
                if (secretKey != null)
                {
                    string base64 = secretKey.GetBase64();
                    if (!string.IsNullOrEmpty(base64))
                    {
                        keyArg = $"ED25519-V3:{base64}";
                    }
                }

                await writer.WriteLineAsync($"ADD_ONION {keyArg} Port=80,127.0.0.1:{LocalTcpPort}");

                string newKeyBase64 = "";
                string serviceId = "";
                
                while (true)
                {
                    string? line = await reader.ReadLineAsync();
                    if (line == null) break;
                    
                    if (line.StartsWith("250-ServiceID="))
                    {
                        serviceId = line.Substring(14).Trim();
                    }
                    else if (line.StartsWith("250-PrivateKey="))
                    {
                        string pk = line.Substring(15).Trim();
                        if (pk.StartsWith("ED25519-V3:"))
                        {
                            newKeyBase64 = pk.Substring(11).Trim();
                        }
                    }
                    else if (line.StartsWith("250 OK"))
                    {
                        break;
                    }
                    else if (line.StartsWith("5")) // Error
                    {
                        Log($"[FATAL] ADD_ONION failed: {line}", LogLevel.Error);
                        return;
                    }
                }

                if (!string.IsNullOrEmpty(serviceId))
                {
                    OnionAddress = serviceId;
                    if (!string.IsNullOrEmpty(newKeyBase64))
                    {
                        GeneratedKey = new SecureRamKey(Convert.FromBase64String(newKeyBase64));
                    }
                    
                    OnReady?.Invoke(OnionAddress);
                    Log($"Onion address generated: {OnionAddress}");
                }
                else
                {
                    Log("[FATAL] Did not receive ServiceID from ADD_ONION.", LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                Log($"[FATAL] ControlPort connection failed: {ex.Message}", LogLevel.Error);
            }
        }

        public void Stop()
        {
            Log("[START] TorManager.Stop started.");

            try
            {
                if (_torProcess is { HasExited: false })
                {
                    try
                    {
                        _torProcess.Kill();
                        _torProcess.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Failed to kill Tor process: {ex.Message}", LogLevel.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in TorManager.Stop: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] TorManager.Stop finished.");
        }

        private static int AllocatePort()
        {
            var policy = Policy
                .Handle<SocketException>()
                .WaitAndRetry(4, attempt => TimeSpan.FromMilliseconds(100 * attempt));

            return policy.Execute(() =>
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                listener.Stop();
                return port;
            });
        }
    }
}
