using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Polly;

namespace Chat
{
    public partial class Form1 : Form
    {
        private P2PNode _node = null!;
        private bool _isDarkMode;
        private readonly List<ChatMessage> _messages = new();
        private SecureRamKey? _mySecretKey;

        // TestBuild command injection
        private FileSystemWatcher? _cmdWatcher;

        public Form1()
        {
            Log("[START] Form1 constructor started.");
            Log("Calling InitializeComponent()...");
            InitializeComponent();
            Log("[END] InitializeComponent() completed. Form1 constructor finished.");
        }

        // --- Startup ---

        private async void Form1_Load(object sender, EventArgs e)
        {
            Log("[START] Form1_Load event handler started.");
            Log("Setting initial UI state to disconnected (SetUiState(false))...");
            SetUiState(connected: false);
            Log("UI state set to disconnected.");

            Log($"Evaluating Program.TestBuild flag ({Program.TestBuild})...");
            if (Program.TestBuild)
            {
                Log("Program.TestBuild is true. Initializing command listener (InitCommandListener())...");
                try
                {
                    InitCommandListener();
                    Log("InitCommandListener() completed successfully.");
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] InitCommandListener failed: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                }
            }

            Log("Instantiating P2PNode instance...");
            _node = new P2PNode();
            Log("P2PNode instance created. Subscribing to events (OnLog, OnPeerUpdated, OnMessageReceived)...");
            _node.OnLog += OnTorLog;
            _node.OnPeerUpdated += OnPeerUpdated;
            _node.OnMessageReceived += OnMessageReceived;
            Log("Node events subscribed.");

            SecureRamKey? secretKey = null;
            bool wantsNewKey = false;
            bool saveNewKey = false;
            string newKeyMode = "";
            string newKeyPass = "";

            Log("Showing StartupForm...");
            using (var startupForm = new StartupForm())
            {
                if (startupForm.ShowDialog(this) == DialogResult.OK)
                {
                    secretKey = startupForm.ResultKey;
                    wantsNewKey = startupForm.WantsNewKey;
                    saveNewKey = startupForm.SaveNewKeyToSettings;
                    newKeyMode = startupForm.NewKeyStorageMode;
                    newKeyPass = startupForm.NewKeyPassword;
                }
                else
                {
                    Log("StartupForm closed without OK. Exiting.");
                    Application.Exit();
                    return;
                }
            }

            try
            {
                Log("Updating status label to 'Durum: Tor baslatiliyor...'...");
                lblStatus.Text = "Durum: Tor başlatılıyor...";
                Log("Executing _node.StartAsync(secretKey) asynchronously...");
                await _node.StartAsync(secretKey);
                Log("_node.StartAsync() returned successfully. Updating status label to 'Durum: Tor aga baglaniliyor...'...");
                lblStatus.Text = "Durum: Tor ağa bağlanılıyor...";

                _mySecretKey = secretKey ?? _node.GeneratedKey;

                if (wantsNewKey && saveNewKey)
                {
                    var newKey = _node.GeneratedKey;
                    if (newKey != null)
                    {
                        string b64 = newKey.GetBase64();
                        string encryptedBase64 = b64;

                        if (newKeyMode == "DPAPI")
                        {
                            encryptedBase64 = CryptographyHelpers.ProtectWithDpapi(b64);
                        }
                        else if (newKeyMode == "Type1")
                        {
                            encryptedBase64 = CryptographyHelpers.ProtectWithType1(b64, newKeyPass);
                        }

                        Chat.Models.Settings.SecretStorageMode.Value = newKeyMode;
                        Chat.Models.Settings.DefaultSecret.Value = encryptedBase64;
                        Chat.Models.Settings.SecretFilePath.Value = "";
                        Chat.Stores.SettingsManager.SaveSettings();
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[FATAL] Startup error in Form1_Load: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                lblStatus.Text = "Durum: Tor başlatılamadı!";
                MessageBox.Show("Tor başlatılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            Log("[END] Form1_Load event handler completed.");
        }

        private void SetUiState(bool connected)
        {
            Log($"[START] SetUiState started with parameter connected={connected}");
            Log($"Updating controls enabled state: btnConnect={connected}, btnSend={connected}, txtMessage={connected}, btnCopyOnion={connected}...");
            btnConnect.Enabled = connected;
            btnSend.Enabled = connected;
            txtMessage.Enabled = connected;
            btnCancelConnect.Enabled = false;
            btnCopyOnion.Enabled = connected;
            Log($"[END] SetUiState completed for connected={connected}.");
        }

        // --- Tor Log (bootstrap progress) ---

        private void OnTorLog(string log)
        {
            Log($"[START] OnTorLog handler received log length={log.Length}: '{log}'");
            if (!log.Contains("Bootstrapped "))
            {
                Log("Log line does not contain 'Bootstrapped '. Skipping parsing.");
                return;
            }

            try
            {
                Log("Parsing bootstrap percentage from log string...");
                int start = log.IndexOf("Bootstrapped ") + 13;
                int end = log.IndexOf('%', start);
                if (start <= 12 || end <= start)
                {
                    Log("[WARNING] Invalid index bounds during bootstrap percentage parse.", LogLevel.Warning);
                    return;
                }

                string pct = log[start..end];
                int msgStart = log.IndexOf("): ", end);
                string detail = msgStart > -1 ? log[(msgStart + 3)..] : "";
                Log($"Bootstrap progress parsed: %{pct}, Detail: '{detail}'");

                Log("Invoking status label UI update...");
                if (!IsDisposed && InvokeRequired)
                {
                    Invoke(() =>
                    {
                        lblStatus.Text = $"Durum: Tor %{pct} - {detail}";
                    });
                }
                else if (!IsDisposed)
                {
                    lblStatus.Text = $"Durum: Tor %{pct} - {detail}";
                }
                Log("Status label UI update invoked.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in OnTorLog bootstrap parser: {ex.Message}", LogLevel.Error);
            }

            Log("[END] OnTorLog handler finished.");
        }

        private const string BaseTitle = "Tor P2P Grup Chat";

        private void UpdateWindowTitle()
        {
            string username = _node?.MyProfile?.Username ?? "";
            if (string.IsNullOrWhiteSpace(username))
            {
                Text = BaseTitle;
            }
            else
            {
                Text = $"{BaseTitle} - {username}";
            }
            Log($"Window title updated to: '{Text}'");
        }

        // --- Peer Events ---

        private void OnPeerUpdated(PeerInfo peer)
        {
            Log($"[START] OnPeerUpdated received for peer Username='{peer.Username}', Onion='{peer.OnionAddress}', Online={peer.IsOnline}");

            try
            {
                if (IsDisposed) return;

                Invoke(() =>
                {
                    Log($"Checking if updated peer is self ({peer.OnionAddress} == {_node.MyOnion})...");
                    if (peer.OnionAddress == _node.MyOnion)
                    {
                        UpdateWindowTitle();
                        if (!btnConnect.Enabled)
                        {
                            Log("First time own onion address received. Enabling UI state (SetUiState(true))...");
                            SetUiState(connected: true);
                            lblStatus.Text = $"Durum: Hazir | {_node.MyOnion}";
                        }
                    }
                    Log("Refreshing peer list via RefreshPeerList()...");
                    RefreshPeerList();
                });
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in OnPeerUpdated UI thread dispatch: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] OnPeerUpdated finished.");
        }

        private void RefreshPeerList()
        {
            Log($"[START] RefreshPeerList started. Total KnownPeers count={_node.KnownPeers.Count}");

            try
            {
                lstPeers.Items.Clear();
                lstOfflinePeers.Items.Clear();
                Log("lstPeers and lstOfflinePeers cleared. Re-populating items...");

                foreach (var p in _node.KnownPeers.Values.OrderBy(x => x.Username))
                {
                    if (p.OnionAddress == _node.MyOnion) continue;

                    string display = string.IsNullOrWhiteSpace(p.Username)
                        ? p.OnionAddress[..Math.Min(8, p.OnionAddress.Length)]
                        : p.Username;

                    string itemText = $"{display} - {p.OnionAddress[..Math.Min(8, p.OnionAddress.Length)]}";

                    if (p.IsOnline)
                    {
                        Log($"Adding online peer item to lstPeers: '{itemText}'");
                        lstPeers.Items.Add($"[+] {itemText}");
                    }
                    else
                    {
                        Log($"Adding offline peer item to lstOfflinePeers: '{itemText}'");
                        lstOfflinePeers.Items.Add($"[-] {itemText}");
                    }
                }
                Log($"[END] RefreshPeerList completed. Online count={lstPeers.Items.Count}, Offline count={lstOfflinePeers.Items.Count}");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in RefreshPeerList: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }
        }

        // --- Messages ---

        private void OnMessageReceived(ChatMessage msg)
        {
            Log($"[START] OnMessageReceived handler started. MessageId='{msg.Id}', Sender='{msg.SenderName}', Text='{msg.Text}'");

            try
            {
                if (IsDisposed) return;

                Invoke(() =>
                {
                    Log($"Adding message Id '{msg.Id}' to local message cache...");
                    _messages.Add(msg);
                    Log("Sorting message list by timestamp...");
                    _messages.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                    Log("Calling RenderMessages()...");
                    RenderMessages();
                });
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in OnMessageReceived UI dispatch: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] OnMessageReceived finished.");
        }

        private void RenderMessages()
        {
            Log($"[START] RenderMessages started for {_messages.Count} cached messages.");

            try
            {
                rtbChat.Clear();
                Log("rtbChat cleared. Rendering messages...");

                foreach (var msg in _messages)
                {
                    var dt = DateTimeOffset.FromUnixTimeMilliseconds(msg.Timestamp).ToLocalTime().DateTime;
                    bool isMe = msg.SenderOnion == _node.MyOnion;
                    Color nameColor = _isDarkMode
                        ? (isMe ? Color.Cyan : Color.Orange)
                        : (isMe ? Color.Blue : Color.Red);

                    AppendRtb($"[{dt:HH:mm:ss}] ", _isDarkMode ? Color.LightGray : Color.Gray);
                    AppendRtb($"{msg.SenderName}: ", nameColor, bold: true);
                    AppendRtb($"{msg.Text}\n", _isDarkMode ? Color.White : Color.Black);
                }

                rtbChat.ScrollToCaret();
                Log("[END] RenderMessages completed successfully.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in RenderMessages: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }
        }

        private void AppendRtb(string text, Color color, bool bold = false)
        {
            Log($"AppendRtb: Text='{text.Replace("\n", "\\n")}', Color={color}, Bold={bold}");
            try
            {
                rtbChat.SelectionStart = rtbChat.TextLength;
                rtbChat.SelectionLength = 0;
                rtbChat.SelectionColor = color;
                rtbChat.SelectionFont = new Font(rtbChat.Font, bold ? FontStyle.Bold : FontStyle.Regular);
                rtbChat.AppendText(text);
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in AppendRtb: {ex.Message}", LogLevel.Error);
            }
        }

        // --- Button Handlers ---

        private void btnConnect_Click(object sender, EventArgs e)
        {
            Log($"[START] btnConnect_Click started. Input text='{txtTargetOnion.Text}'");
            string onion = txtTargetOnion.Text.Trim();
            Log($"Trimmed target onion: '{onion}'");

            if (!onion.EndsWith(".onion"))
            {
                Log("[WARNING] Target onion validation failed! Missing .onion suffix.", LogLevel.Warning);
                MessageBox.Show("Geçerli bir .onion adresi girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Log("Target onion validated. Updating UI controls state for connection attempt...");
            btnConnect.Enabled = false;
            btnCancelConnect.Enabled = true;
            btnCancelConnect.Tag = onion;
            lblStatus.Text = $"Durum: {onion[..Math.Min(8, onion.Length)]}... adresine bağlanılıyor...";

            Log($"Spawning background task for ConnectToPeerAsync('{onion}')...");
            _ = Task.Run(async () =>
            {
                try
                {
                    Log($"Executing ConnectToPeerAsync('{onion}')...");
                    await _node.ConnectToPeerAsync(onion);
                    Log($"ConnectToPeerAsync('{onion}') execution completed.");
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] ConnectToPeerAsync task failed for '{onion}': {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    if (!IsDisposed)
                    {
                        Invoke(() =>
                        {
                            if (btnCancelConnect.Tag?.ToString() == onion)
                            {
                                Log("Resetting UI controls state after connection attempt completion...");
                                lblStatus.Text = "Durum: Bağlantı isteği tamamlandı.";
                                btnCancelConnect.Enabled = false;
                                btnConnect.Enabled = true;
                                btnCancelConnect.Tag = null;
                                txtTargetOnion.Clear();
                            }
                        });
                    }
                }
            });

            Log("[END] btnConnect_Click handler completed.");
        }

        private void btnCancelConnect_Click(object sender, EventArgs e)
        {
            Log($"[START] btnCancelConnect_Click started. Current Tag='{btnCancelConnect.Tag}'");

            try
            {
                if (btnCancelConnect.Tag?.ToString() is string onion)
                {
                    Log($"Invoking CancelConnection for onion '{onion}'...");
                    _node.CancelConnection(onion);
                    lblStatus.Text = $"Durum: Bağlantı iptal edildi ({onion[..Math.Min(8, onion.Length)]}...)";
                    btnCancelConnect.Enabled = false;
                    btnConnect.Enabled = true;
                    btnCancelConnect.Tag = null;
                    Log($"Connection attempt to '{onion}' cancelled and UI reset.");
                }
                else
                {
                    Log("[WARNING] btnCancelConnect.Tag is empty. No active connection to cancel.", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in btnCancelConnect_Click: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] btnCancelConnect_Click finished.");
        }

        private void btnCopyOnion_Click(object sender, EventArgs e)
        {
            Log($"[START] btnCopyOnion_Click started. MyOnion='{_node?.MyOnion}'");

            try
            {
                if (!string.IsNullOrEmpty(_node?.MyOnion))
                {
                    Clipboard.SetText(_node.MyOnion);
                    lblStatus.Text = "Durum: Onion adresi kopyalandi.";
                    Log("Onion address successfully copied to clipboard.");
                }
                else
                {
                    Log("[WARNING] MyOnion address is null or empty. Clipboard not set.", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception setting clipboard text: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] btnCopyOnion_Click finished.");
        }

        private async void btnSend_Click(object sender, EventArgs e)
        {
            Log($"[START] btnSend_Click started. Input message text length={txtMessage.Text.Length}");
            string text = txtMessage.Text.Trim();

            if (string.IsNullOrEmpty(text))
            {
                Log("Message text is empty. Skipping send.");
                return;
            }

            txtMessage.Clear();
            btnSend.Enabled = false;

            try
            {
                Log($"Calling _node.BroadcastMessageAsync for text length={text.Length}...");
                await _node.BroadcastMessageAsync(text);
                Log("_node.BroadcastMessageAsync completed successfully.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Failed broadcasting message: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                MessageBox.Show("Mesaj gonderilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnSend.Enabled = true;
            }

            Log("[END] btnSend_Click completed.");
        }

        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            Log($"txtMessage_KeyDown: KeyCode={e.KeyCode}");
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Log("Enter key pressed. Performing btnSend.PerformClick()...");
                btnSend.PerformClick();
            }
        }

        private void btnMyProfile_Click(object sender, EventArgs e)
        {
            Log("[START] btnMyProfile_Click started.");

            try
            {
                if (_node.MyProfile == null)
                {
                    Log("[WARNING] _node.MyProfile is null. Aborting profile form display.", LogLevel.Warning);
                    return;
                }

                Log("Instantiating ProfileForm for local profile...");
                var form = new ProfileForm(_node.MyProfile, isMe: true, connectedAt: _node.ConnectedAt, myKey: _mySecretKey);
                form.OnSave += async (name, bio) =>
                {
                    Log($"ProfileForm OnSave triggered: Name='{name}', Bio='{bio}'. Calling UpdateProfileAsync...");
                    try
                    {
                        await _node.UpdateProfileAsync(name, bio);
                        Log("UpdateProfileAsync completed.");
                        if (!IsDisposed)
                        {
                            Invoke(UpdateWindowTitle);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[ERROR] Failed to update profile: {ex.Message}", LogLevel.Error);
                    }
                };

                Log("Displaying ProfileForm dialog...");
                form.ShowDialog(this);
                Log("ProfileForm dialog closed.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in btnMyProfile_Click: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] btnMyProfile_Click finished.");
        }

        private async void btnReconnect_Click(object sender, EventArgs e)
        {
            Log("[START] btnReconnect_Click started.");
            try
            {
                btnReconnect.Enabled = false;
                lblStatus.Text = "Durum: Ağ bağlantıları sıfırlanıyor, tüm kişilerle yeniden tanışılıyor...";
                Log("Calling _node.ReconnectAsync()...");
                await _node.ReconnectAsync();
                lblStatus.Text = $"Durum: Hazır | {_node.MyOnion}";
                Log("_node.ReconnectAsync() completed successfully.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in btnReconnect_Click: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                btnReconnect.Enabled = true;
            }
            Log("[END] btnReconnect_Click completed.");
        }

        private void lstPeers_DoubleClick(object sender, EventArgs e)
        {
            Log($"[START] lstPeers_DoubleClick started. SelectedItem='{lstPeers.SelectedItem}'");

            try
            {
                if (lstPeers.SelectedItem == null)
                {
                    Log("lstPeers.SelectedItem is null. Returning.");
                    return;
                }

                string selected = lstPeers.SelectedItem.ToString()!;
                Log($"Searching KnownPeers for match with '{selected}'...");
                var peer = _node.KnownPeers.Values.FirstOrDefault(p => selected.Contains(p.OnionAddress[..Math.Min(8, p.OnionAddress.Length)]));

                if (peer != null)
                {
                    Log($"Peer match found: Username='{peer.Username}', Onion='{peer.OnionAddress}'. Displaying ProfileForm dialog...");
                    new ProfileForm(peer, isMe: false).ShowDialog(this);
                    Log("Peer ProfileForm dialog closed.");
                }
                else
                {
                    Log("[WARNING] Matching peer not found in KnownPeers dictionary.", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in lstPeers_DoubleClick: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] lstPeers_DoubleClick finished.");
        }

        private void lstOfflinePeers_DoubleClick(object sender, EventArgs e)
        {
            Log($"[START] lstOfflinePeers_DoubleClick started. SelectedItem='{lstOfflinePeers.SelectedItem}'");

            try
            {
                if (lstOfflinePeers.SelectedItem == null) return;

                string selected = lstOfflinePeers.SelectedItem.ToString()!;
                var peer = _node.KnownPeers.Values.FirstOrDefault(p => selected.Contains(p.OnionAddress[..Math.Min(8, p.OnionAddress.Length)]));

                if (peer != null)
                {
                    new ProfileForm(peer, isMe: false).ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in lstOfflinePeers_DoubleClick: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }
        }

        // --- Theme ---

        private void btnTheme_Click(object sender, EventArgs e)
        {
            Log($"[START] btnTheme_Click started. Current _isDarkMode={_isDarkMode}");
            _isDarkMode = !_isDarkMode;
            Log($"Toggled _isDarkMode to {_isDarkMode}. Calling ApplyTheme()...");
            ApplyTheme();
            Log("[END] btnTheme_Click finished.");
        }

        private void ApplyTheme()
        {
            Log($"[START] ApplyTheme started. _isDarkMode={_isDarkMode}");

            try
            {
                Color bg = _isDarkMode ? Color.FromArgb(30, 30, 30) : SystemColors.Control;
                Color fg = _isDarkMode ? Color.White : SystemColors.ControlText;
                Color inputBg = _isDarkMode ? Color.FromArgb(45, 45, 45) : SystemColors.Window;
                Color btnBg = _isDarkMode ? Color.FromArgb(55, 55, 55) : SystemColors.Control;

                BackColor = bg;
                ForeColor = fg;

                rtbChat.BackColor = inputBg;
                rtbChat.ForeColor = fg;
                lstPeers.BackColor = inputBg;
                lstPeers.ForeColor = fg;
                lstOfflinePeers.BackColor = inputBg;
                lstOfflinePeers.ForeColor = fg;
                lblOfflinePeers.ForeColor = fg;
                txtMessage.BackColor = inputBg;
                txtMessage.ForeColor = fg;
                txtTargetOnion.BackColor = inputBg;
                txtTargetOnion.ForeColor = fg;

                foreach (var btn in new[] { btnConnect, btnSend, btnMyProfile, btnTheme, btnCancelConnect, btnCopyOnion, btnReconnect })
                {
                    btn.BackColor = btnBg;
                    btn.ForeColor = fg;
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderColor = _isDarkMode ? Color.FromArgb(80, 80, 80) : Color.Gray;
                }

                Log("Re-rendering messages to update chat theme colors...");
                RenderMessages();
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in ApplyTheme: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] ApplyTheme completed.");
        }

        // --- Cleanup ---

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Log($"[START] Form1_FormClosing started. CloseReason={e.CloseReason}");
            try
            {
                _node?.Stop();
                Log("P2PNode stopped in Form1_FormClosing.");
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception stopping node during form close: {ex.Message}", LogLevel.Error);
            }
            Log("[END] Form1_FormClosing completed.");
        }

        // --- TestBuild Command Injection ---

        private void InitCommandListener()
        {
            Log("[START] InitCommandListener started.");
            string cmdFile = Path.Combine(Path.GetTempPath(), $"TorChat_Cmd_{Program.InstanceId}.txt");
            Log($"cmdFile path: '{cmdFile}'. Initializing with empty text via Polly...");

            var ioPolicy = Policy
                .Handle<IOException>()
                .Or<UnauthorizedAccessException>()
                .WaitAndRetry(3, attempt => TimeSpan.FromMilliseconds(200 * attempt),
                    (ex, span, attempt, ctx) =>
                    {
                        Log($"[RETRY] Command file write attempt {attempt} failed: {ex.Message}", LogLevel.Warning);
                    });

            ioPolicy.Execute(() =>
            {
                File.WriteAllText(cmdFile, "");
            });
            Log("Command file initialized.");

            _cmdWatcher = new FileSystemWatcher(Path.GetDirectoryName(cmdFile)!)
            {
                Filter = Path.GetFileName(cmdFile),
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            _cmdWatcher.Changed += (_, _) =>
            {
                Log($"Command file '{cmdFile}' Changed event fired. Triggering ProcessCommandAsync...");
                _ = ProcessCommandAsync(cmdFile);
            };

            Log("[END] InitCommandListener completed successfully.");
        }

        private async Task ProcessCommandAsync(string cmdFile)
        {
            Log($"[START] ProcessCommandAsync started for file '{cmdFile}'");

            var fileReadPolicy = Policy
                .Handle<IOException>()
                .Or<UnauthorizedAccessException>()
                .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(200 * attempt),
                    (ex, span, attempt, ctx) =>
                    {
                        Log($"[RETRY] ProcessCommandAsync file read attempt {attempt} failed: {ex.Message}", LogLevel.Warning);
                    });

            try
            {
                string cmd = await fileReadPolicy.ExecuteAsync(async () =>
                {
                    return (await File.ReadAllTextAsync(cmdFile)).Trim();
                });

                Log($"Command file text read: '{cmd}'");

                if (string.IsNullOrEmpty(cmd))
                {
                    Log("Command text is empty. Returning.");
                    return;
                }

                await fileReadPolicy.ExecuteAsync(async () =>
                {
                    await File.WriteAllTextAsync(cmdFile, "");
                });
                Log($"Command file cleared. Executing command '{cmd}'...");

                if (cmd.StartsWith("CONNECT "))
                {
                    string onion = cmd[8..].Trim();
                    Log($"CONNECT command detected for '{onion}'. Executing ConnectToPeerAsync...");
                    await _node.ConnectToPeerAsync(onion);
                    Log("ConnectToPeerAsync completed.");
                }
                else if (cmd.StartsWith("SEND "))
                {
                    string text = cmd[5..].Trim();
                    Log($"SEND command detected for '{text}'. Executing BroadcastMessageAsync...");
                    await _node.BroadcastMessageAsync(text);
                    Log("BroadcastMessageAsync completed.");
                }
                else
                {
                    Log($"[WARNING] Unrecognized command text: '{cmd}'", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Exception in ProcessCommandAsync: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] ProcessCommandAsync completed.");
        }
    }
}
