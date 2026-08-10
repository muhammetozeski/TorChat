using System.Security.Cryptography;
using Chat.Models;
using Chat.Stores;

namespace Chat;

public class StartupForm : Form
{
    public SecureRamKey? ResultKey { get; private set; }
    public bool WantsNewKey { get; private set; }
    public bool SaveNewKeyToSettings { get; private set; }
    public string NewKeyStorageMode { get; private set; } = "DPAPI";
    public string NewKeyPassword { get; private set; } = "";

    private Label lblInfo = new();
    private TextBox txtInput = new();
    private Button btnBrowse = new();
    private Label lblPassword = new();
    private TextBox txtPassword = new();
    private CheckBox chkRemember = new();
    private ComboBox cmbFormat = new();
    private Label lblFormat = new();
    private Button btnStart = new();
    private Button btnGenerate = new();

    public StartupForm()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        this.Text = "TorChat - Başlangıç";
        this.Size = new Size(500, 320);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;

        lblInfo.Text = "Gizli Anahtar (Base64) veya Dosya Yolu:";
        lblInfo.Location = new Point(20, 20);
        lblInfo.AutoSize = true;

        txtInput.Location = new Point(20, 45);
        txtInput.Size = new Size(350, 25);

        btnBrowse.Text = "Gözat...";
        btnBrowse.Location = new Point(380, 43);
        btnBrowse.Size = new Size(80, 27);
        btnBrowse.Click += BtnBrowse_Click;

        lblFormat.Text = "Format:";
        lblFormat.Location = new Point(20, 80);
        lblFormat.AutoSize = true;

        cmbFormat.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbFormat.Items.AddRange(new object[] { "DPAPI (Windows Şifreli)", "Type1 (Parola Korumalı)", "Type0 (Şifresiz Plaintext)" });
        cmbFormat.SelectedIndex = 0;
        cmbFormat.Location = new Point(90, 77);
        cmbFormat.Size = new Size(180, 25);
        cmbFormat.SelectedIndexChanged += CmbFormat_SelectedIndexChanged;

        lblPassword.Text = "Parola (Type1):";
        lblPassword.Location = new Point(20, 115);
        lblPassword.AutoSize = true;
        lblPassword.Visible = false;

        txtPassword.Location = new Point(120, 112);
        txtPassword.Size = new Size(150, 25);
        txtPassword.UseSystemPasswordChar = true;
        txtPassword.Visible = false;

        chkRemember.Text = "Ayarları ve Anahtarı Hatırla (DefaultSettings'e kaydet)";
        chkRemember.Location = new Point(20, 150);
        chkRemember.AutoSize = true;
        chkRemember.Checked = true;

        btnStart.Text = "BAŞLAT";
        btnStart.Location = new Point(20, 200);
        btnStart.Size = new Size(150, 40);
        btnStart.Font = new Font(btnStart.Font, FontStyle.Bold);
        btnStart.Click += BtnStart_Click;

        btnGenerate.Text = "Rastgele Üret (Yeni Hesap)";
        btnGenerate.Location = new Point(200, 200);
        btnGenerate.Size = new Size(260, 40);
        btnGenerate.Click += BtnGenerate_Click;

        this.Controls.Add(lblInfo);
        this.Controls.Add(txtInput);
        this.Controls.Add(btnBrowse);
        this.Controls.Add(lblFormat);
        this.Controls.Add(cmbFormat);
        this.Controls.Add(lblPassword);
        this.Controls.Add(txtPassword);
        this.Controls.Add(chkRemember);
        this.Controls.Add(btnStart);
        this.Controls.Add(btnGenerate);
    }

    private void CmbFormat_SelectedIndexChanged(object? sender, EventArgs e)
    {
        bool isType1 = cmbFormat.SelectedIndex == 1;
        lblPassword.Visible = isType1;
        txtPassword.Visible = isType1;
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog ofd = new();
        ofd.Filter = "All Files|*.*";
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            txtInput.Text = ofd.FileName;
        }
    }

    private void LoadSettings()
    {
        SettingsManager.LoadSettings();
        if (!string.IsNullOrEmpty(Settings.SecretFilePath.Value))
        {
            txtInput.Text = Settings.SecretFilePath.Value;
        }
        else if (!string.IsNullOrEmpty(Settings.DefaultSecret.Value))
        {
            txtInput.Text = Settings.DefaultSecret.Value;
        }

        string mode = Settings.SecretStorageMode.Value;
        if (mode == "Type1") cmbFormat.SelectedIndex = 1;
        else if (mode == "Type0") cmbFormat.SelectedIndex = 2;
        else cmbFormat.SelectedIndex = 0; // DPAPI default
    }

    private void BtnGenerate_Click(object? sender, EventArgs e)
    {
        WantsNewKey = true;
        
        if (chkRemember.Checked)
        {
            SaveNewKeyToSettings = true;
            NewKeyStorageMode = cmbFormat.SelectedIndex == 0 ? "DPAPI" : (cmbFormat.SelectedIndex == 1 ? "Type1" : "Type0");
            NewKeyPassword = txtPassword.Text;
            
            if (NewKeyStorageMode == "Type1" && string.IsNullOrEmpty(NewKeyPassword))
            {
                MessageBox.Show("Rastgele üretilen anahtarı Type1 olarak kaydetmek için parola girmelisiniz.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private void BtnStart_Click(object? sender, EventArgs e)
    {
        string input = txtInput.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            MessageBox.Show("Lütfen bir anahtar veya dosya yolu girin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string rawBase64 = input;
        
        if (File.Exists(input))
        {
            try
            {
                rawBase64 = File.ReadAllText(input).Trim();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dosya okunamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        string decryptedBase64 = string.Empty;
        
        if (cmbFormat.SelectedIndex == 0) // DPAPI
        {
            decryptedBase64 = CryptographyHelpers.UnprotectWithDpapi(rawBase64);
            if (string.IsNullOrEmpty(decryptedBase64))
            {
                MessageBox.Show("DPAPI çözme başarısız! Anahtar geçersiz veya başka bir bilgisayarda/hesapta şifrelenmiş.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        else if (cmbFormat.SelectedIndex == 1) // Type1
        {
            string pass = txtPassword.Text;
            if (string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Type1 için parola girmelisiniz.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            decryptedBase64 = CryptographyHelpers.UnprotectWithType1(rawBase64, pass);
            if (string.IsNullOrEmpty(decryptedBase64))
            {
                MessageBox.Show("Parola yanlış veya anahtar bozuk.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        else // Type0 Plaintext
        {
            decryptedBase64 = rawBase64;
        }

        byte[] secretBytes;
        try
        {
            secretBytes = Convert.FromBase64String(decryptedBase64);
        }
        catch
        {
            MessageBox.Show("Anahtar geçerli bir Base64 dizgesi değil.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ResultKey = new SecureRamKey(secretBytes);
        
        if (chkRemember.Checked)
        {
            Settings.SecretStorageMode.Value = cmbFormat.SelectedIndex == 0 ? "DPAPI" : (cmbFormat.SelectedIndex == 1 ? "Type1" : "Type0");
            
            if (File.Exists(input))
            {
                Settings.SecretFilePath.Value = input;
                Settings.DefaultSecret.Value = "";
            }
            else
            {
                Settings.SecretFilePath.Value = "";
                Settings.DefaultSecret.Value = rawBase64; // save the raw input (encrypted or DPAPI)
            }
            SettingsManager.SaveSettings();
        }

        this.DialogResult = DialogResult.OK;
        this.Close();
    }
}
