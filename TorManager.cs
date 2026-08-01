using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Polly;

namespace Chat
{
    public class TorManager
    {
        public int SocksPort { get; }
        public int LocalTcpPort { get; }
        public string? OnionAddress { get; private set; }

        private readonly string _dataDir;
        private Process? _torProcess;

        public event Action<string>? OnLog;
        public event Action<string>? OnReady;

        public TorManager()
        {
            Log("[START] TorManager constructor started.");

            Log("Allocating SocksPort...");
            SocksPort = AllocatePort();
            Log($"SocksPort allocated successfully: {SocksPort}");

            Log("Allocating LocalTcpPort...");
            LocalTcpPort = AllocatePort();
            Log($"LocalTcpPort allocated successfully: {LocalTcpPort}");

            Log($"Finding available DataDir slot starting from InstanceId '{Program.InstanceId}'...");
            int slotId = int.Parse(Program.InstanceId);
            bool slotFound = false;

            while (!slotFound)
            {
                _dataDir = Path.Combine(Path.GetTempPath(), $"TorChat_Data_{slotId}");
                Log($"Checking DataDir slot: '{_dataDir}'...");
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
                            Log($"Slot {slotId} is in use by PID {oldPid}. Trying next slot...");
                            slotId++;
                            continue;
                        }
                    }
                }

                // Slot available: old process is dead or no PID file exists
                // Delete hs/ to force fresh onion address generation
                string hsCleanup = Path.Combine(_dataDir, "hs");
                if (Directory.Exists(hsCleanup))
                {
                    Log($"Deleting stale hs/ directory at '{hsCleanup}' to generate fresh onion address...");
                    Directory.Delete(hsCleanup, true);
                }

                // Delete stale lock file if present
                string lockFile = Path.Combine(_dataDir, "lock");
                if (File.Exists(lockFile))
                {
                    Log($"Deleting stale lock file at '{lockFile}'...");
                    File.Delete(lockFile);
                }

                slotFound = true;
                Log($"DataDir slot {slotId} is available. Using '{_dataDir}'.");
            }

            Log($"[END] TorManager constructor finished. SocksPort={SocksPort}, LocalTcpPort={LocalTcpPort}, DataDir='{_dataDir}'");
        }

        public Task StartAsync()
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
                    string hsDir = Path.Combine(_dataDir, "hs");
                    Log($"HiddenService directory path: '{hsDir}'. Creating directory...");
                    Directory.CreateDirectory(hsDir);
                    Log($"HiddenService directory created: '{hsDir}'");

                    string torrcPath = Path.Combine(_dataDir, "torrc");
                    Log($"torrc path: '{torrcPath}'. Formatting torrc configuration content...");

                    string torrc = string.Join('\n',
                        $"SocksPort 127.0.0.1:{SocksPort}",
                        "SocksTimeout 20",
                        $"HiddenServiceDir \"{hsDir.Replace('\\', '/')}\"",
                        $"HiddenServicePort 80 127.0.0.1:{LocalTcpPort}",
                        "Log notice stdout",
                        $"DataDirectory \"{_dataDir.Replace('\\', '/')}\"");

                    Log($"Writing torrc configuration to '{torrcPath}' (Content Length: {torrc.Length} chars)...");
                    File.WriteAllText(torrcPath, torrc);
                    Log($"torrc written successfully:\n{torrc}");
                });

                string torrcFile = Path.Combine(_dataDir, "torrc");
                Log("Initializing Tor Process instance...");
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
                Log($"Tor process StartInfo configured (FileName='tor', Arguments='{_torProcess.StartInfo.Arguments}')");

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

                Log("Starting Tor process execution...");
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

                Log($"Tor process started successfully with PID={_torProcess.Id}. Enabling output & error line reading...");
                _torProcess.BeginOutputReadLine();
                _torProcess.BeginErrorReadLine();

                string torPidFile = Path.Combine(_dataDir, "tor.pid");
                File.WriteAllText(torPidFile, _torProcess.Id.ToString());
                Log($"Wrote Tor PID {_torProcess.Id} to '{torPidFile}'.");

                string hsFolder = Path.Combine(_dataDir, "hs");
                Log("Spawning background ReadOnionAddressAsync watcher immediately...");
                _ = ReadOnionAddressAsync(hsFolder);

                Log("[END] TorManager.StartAsync completed successfully.");
            }
            catch (Exception ex)
            {
                Log($"[FATAL] TorManager.StartAsync failed: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                throw;
            }

            return Task.CompletedTask;
        }

        private async Task ReadOnionAddressAsync(string hsDir)
        {
            Log($"[START] ReadOnionAddressAsync started for HiddenService dir: '{hsDir}'");
            string hostnameFile = Path.Combine(hsDir, "hostname");
            Log($"Hostname file path: '{hostnameFile}'");

            var fileReadPolicy = Policy
                .Handle<IOException>()
                .Or<UnauthorizedAccessException>()
                .WaitAndRetryAsync(5, attempt => TimeSpan.FromMilliseconds(200 * attempt),
                    (ex, span, attempt, ctx) =>
                    {
                        Log($"[RETRY] Reading hostname file attempt {attempt} failed: {ex.Message}", LogLevel.Warning);
                    });

            for (int i = 0; i < 30; i++)
            {
                Log($"Checking existence of hostname file '{hostnameFile}' (Attempt {i + 1}/30)...");
                if (File.Exists(hostnameFile))
                {
                    Log($"Hostname file exists. Executing Polly read policy...");
                    try
                    {
                        string rawAddress = await fileReadPolicy.ExecuteAsync(async () =>
                        {
                            return await File.ReadAllTextAsync(hostnameFile);
                        });

                        Log($"Raw address read from file: '{rawAddress}'. Trimming...");
                        OnionAddress = rawAddress.Trim();
                        Log($"OnionAddress set to '{OnionAddress}'. Invoking OnReady event...");
                        OnReady?.Invoke(OnionAddress);
                        Log("OnReady event invoked successfully.");
                        Log($"[END] ReadOnionAddressAsync completed successfully for OnionAddress='{OnionAddress}'");
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Failed reading hostname file on attempt {i + 1}: {ex.Message}", LogLevel.Error);
                    }
                }

                Log($"Hostname file not ready yet on attempt {i + 1}. Delaying 1000ms...");
                await Task.Delay(1000);
            }

            Log("[FATAL] ReadOnionAddressAsync failed to read onion address after 30 seconds limit reached.", LogLevel.Error);
        }

        public void Stop()
        {
            Log("[START] TorManager.Stop started.");

            try
            {
                if (_torProcess is { HasExited: false })
                {
                    Log($"Tor process PID={_torProcess.Id} is active. Terminating process...");
                    try
                    {
                        _torProcess.Kill();
                        Log("Process kill called. Waiting for process exit...");
                        _torProcess.WaitForExit();
                        Log("Tor process exited.");
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Failed to kill Tor process: {ex.Message}", LogLevel.Error);
                    }
                }
                else
                {
                    Log("Tor process is null or already exited.");
                }
                Log("Preserving DataDir cache for fast circuit bootstrap.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in TorManager.Stop: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] TorManager.Stop finished.");
        }

        private static int AllocatePort()
        {
            Log("[START] AllocatePort started.");

            var policy = Policy
                .Handle<SocketException>()
                .WaitAndRetry(4, attempt => TimeSpan.FromMilliseconds(100 * attempt),
                    (ex, span, attempt, ctx) =>
                    {
                        Log($"[RETRY] AllocatePort attempt {attempt} failed: {ex.Message}", LogLevel.Warning);
                    });

            try
            {
                return policy.Execute(() =>
                {
                    Log("Creating TcpListener on IPAddress.Loopback port 0...");
                    var listener = new TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                    Log($"TcpListener started on LocalEndpoint={listener.LocalEndpoint}");
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    Log($"Extracted port: {port}. Stopping listener...");
                    listener.Stop();
                    Log("TcpListener stopped.");
                    Log($"[END] AllocatePort returning port {port}");
                    return port;
                });
            }
            catch (Exception ex)
            {
                Log($"[FATAL] AllocatePort failed after retries: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                throw;
            }
        }
    }
}
