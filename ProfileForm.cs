using System;
using System.Drawing;
using System.Windows.Forms;
using Polly;

namespace Chat
{
    public class ProfileForm : Form
    {
        private readonly TextBox _txtUsername = null!;
        private readonly TextBox _txtBio = null!;
        private readonly bool _isMe;

        public event Action<string, string>? OnSave;

        public ProfileForm(PeerInfo profile, bool isMe, DateTime? connectedAt = null, SecureRamKey? myKey = null)
        {
            Log($"[START] ProfileForm constructor started. IsMe={isMe}, OnionAddress='{profile.OnionAddress}', Username='{profile.Username}', Bio='{profile.Bio}', ConnectedAt='{connectedAt}'");
            _isMe = isMe;

            try
            {
                Log("Configuring ProfileForm properties (Text, Size, FormBorderStyle, StartPosition, MaximizeBox)...");
                Text = isMe ? "Kendi Profilim" : "Profil";
                Size = isMe ? new Size(350, 290) : new Size(350, 250);
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                Log("ProfileForm properties configured.");

                Log("Creating Onion address controls...");
                Controls.Add(new Label { Text = "Onion:", Location = new Point(10, 15), AutoSize = true });
                Controls.Add(new TextBox { Text = profile.OnionAddress, Location = new Point(100, 12), Width = 220, ReadOnly = true });
                Log("Onion address controls created and added to Controls.");

                int yOffset = 45;

                if (isMe)
                {
                    Log("Creating Opening Time controls...");
                    Controls.Add(new Label { Text = "Açılış:", Location = new Point(10, yOffset), AutoSize = true });
                    Controls.Add(new TextBox { Text = Logger.startTime, Location = new Point(100, yOffset - 3), Width = 220, ReadOnly = true });
                    yOffset += 30;

                    Log("Creating Connection Time controls...");
                    Controls.Add(new Label { Text = "Bağlantı:", Location = new Point(10, yOffset), AutoSize = true });
                    string connStr = connectedAt.HasValue ? connectedAt.Value.ToString("dd.MM.yyyy HH:mm:ss") : "Henüz bağlanmadı";
                    Controls.Add(new TextBox { Text = connStr, Location = new Point(100, yOffset - 3), Width = 220, ReadOnly = true });
                    yOffset += 30;
                }

                Log("Creating Username controls...");
                Controls.Add(new Label { Text = "Ad:", Location = new Point(10, yOffset), AutoSize = true });
                _txtUsername = new TextBox { Text = profile.Username, Location = new Point(100, yOffset - 3), Width = 220, ReadOnly = !isMe };
                Controls.Add(_txtUsername);
                yOffset += 30;

                Log("Creating Bio controls...");
                Controls.Add(new Label { Text = "Hakkımda:", Location = new Point(10, yOffset), AutoSize = true });
                _txtBio = new TextBox { Text = profile.Bio, Location = new Point(100, yOffset - 3), Width = 220, Height = 60, Multiline = true, ReadOnly = !isMe };
                Controls.Add(_txtBio);
                yOffset += 70;

                if (isMe)
                {
                    Log("IsMe is true. Creating btnSave button and hooking Click handler...");
                    var btnSave = new Button { Text = "Kaydet", Location = new Point(245, yOffset), Width = 75 };
                    btnSave.Click += (_, _) =>
                    {
                        Log($"[START] btnSave Clicked. Username input='{_txtUsername.Text}', Bio input='{_txtBio.Text}'");

                        try
                        {
                            string nameTrimmed = _txtUsername.Text.Trim();
                            string bioTrimmed = _txtBio.Text.Trim();
                            Log($"Trimmed inputs: Username='{nameTrimmed}', Bio='{bioTrimmed}'");

                            if (string.IsNullOrWhiteSpace(nameTrimmed))
                            {
                                Log("[WARNING] Username validation failed: Empty or whitespace.", LogLevel.Warning);
                                MessageBox.Show("Ad boş olamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                            Log("Username validation passed.");

                            Log($"Invoking OnSave event with Name='{nameTrimmed}', Bio='{bioTrimmed}'...");
                            OnSave?.Invoke(nameTrimmed, bioTrimmed);
                            Log("OnSave event invoked successfully. Closing ProfileForm...");
                            Close();
                            Log("ProfileForm closed.");
                        }
                        catch (Exception ex)
                        {
                            Log($"[ERROR] Exception in btnSave Click handler: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
                            MessageBox.Show("Profil kaydoluşunda hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        Log("[END] btnSave Click finished.");
                    };
                    Controls.Add(btnSave);
                    Log("btnSave added to Controls.");

                    if (myKey != null)
                    {
                        var btnShowKey = new Button { Text = "Anahtarı Göster", Location = new Point(10, yOffset), Width = 110 };
                        btnShowKey.Click += (_, _) =>
                        {
                            try
                            {
                                string base64 = myKey.GetBase64();
                                MessageBox.Show(base64, "Gizli Anahtar (Base64)", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show("Anahtar okunamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        };
                        Controls.Add(btnShowKey);
                    }
                }
                else
                {
                    Log($"IsMe is false. Creating status label (IsOnline={profile.IsOnline}, LastSeen={profile.LastSeen:HH:mm})...");
                    Controls.Add(new Label
                    {
                        Text = profile.IsOnline ? "Çevrimiçi" : $"Çevrimdışı ({profile.LastSeen:HH:mm})",
                        Location = new Point(10, 150),
                        AutoSize = true,
                        ForeColor = profile.IsOnline ? Color.Green : Color.Gray
                    });
                    Log("Status label added to Controls.");
                }
            }
            catch (Exception ex)
            {
                Log($"[FATAL] Exception in ProfileForm constructor: {ex.GetType().Name} - {ex.Message}", LogLevel.Error);
            }

            Log("[END] ProfileForm constructor completed.");
        }
    }
}
