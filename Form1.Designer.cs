namespace Chat
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.txtTargetOnion = new System.Windows.Forms.TextBox();
            this.btnConnect = new System.Windows.Forms.Button();
            this.rtbChat = new System.Windows.Forms.RichTextBox();
            this.txtMessage = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.lstPeers = new System.Windows.Forms.ListBox();
            this.btnMyProfile = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblPeers = new System.Windows.Forms.Label();
            this.btnTheme = new System.Windows.Forms.Button();
            this.btnCancelConnect = new System.Windows.Forms.Button();
            this.btnCopyOnion = new System.Windows.Forms.Button();
            this.btnReconnect = new System.Windows.Forms.Button();
            this.lblOfflinePeers = new System.Windows.Forms.Label();
            this.lstOfflinePeers = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // txtTargetOnion
            // 
            this.txtTargetOnion.Location = new System.Drawing.Point(12, 12);
            this.txtTargetOnion.Name = "txtTargetOnion";
            this.txtTargetOnion.PlaceholderText = "Bağlanmak için .onion adresi girin...";
            this.txtTargetOnion.Size = new System.Drawing.Size(200, 23);
            this.txtTargetOnion.TabIndex = 0;
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(218, 11);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(55, 25);
            this.btnConnect.TabIndex = 1;
            this.btnConnect.Text = "Bağlan";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // btnCancelConnect
            // 
            this.btnCancelConnect.Location = new System.Drawing.Point(278, 11);
            this.btnCancelConnect.Name = "btnCancelConnect";
            this.btnCancelConnect.Size = new System.Drawing.Size(50, 25);
            this.btnCancelConnect.TabIndex = 10;
            this.btnCancelConnect.Text = "İptal";
            this.btnCancelConnect.Enabled = false;
            this.btnCancelConnect.UseVisualStyleBackColor = true;
            this.btnCancelConnect.Click += new System.EventHandler(this.btnCancelConnect_Click);
            // 
            // btnCopyOnion
            // 
            this.btnCopyOnion.Location = new System.Drawing.Point(333, 11);
            this.btnCopyOnion.Name = "btnCopyOnion";
            this.btnCopyOnion.Size = new System.Drawing.Size(95, 25);
            this.btnCopyOnion.TabIndex = 11;
            this.btnCopyOnion.Text = "Onion Kopyala";
            this.btnCopyOnion.UseVisualStyleBackColor = true;
            this.btnCopyOnion.Click += new System.EventHandler(this.btnCopyOnion_Click);
            // 
            // btnReconnect
            // 
            this.btnReconnect.Location = new System.Drawing.Point(433, 11);
            this.btnReconnect.Name = "btnReconnect";
            this.btnReconnect.Size = new System.Drawing.Size(95, 25);
            this.btnReconnect.TabIndex = 12;
            this.btnReconnect.Text = "Yeniden Bağlan";
            this.btnReconnect.UseVisualStyleBackColor = true;
            this.btnReconnect.Click += new System.EventHandler(this.btnReconnect_Click);
            // 
            // btnMyProfile
            // 
            this.btnMyProfile.Location = new System.Drawing.Point(533, 11);
            this.btnMyProfile.Name = "btnMyProfile";
            this.btnMyProfile.Size = new System.Drawing.Size(80, 25);
            this.btnMyProfile.TabIndex = 2;
            this.btnMyProfile.Text = "Profilim";
            this.btnMyProfile.UseVisualStyleBackColor = true;
            this.btnMyProfile.Click += new System.EventHandler(this.btnMyProfile_Click);
            // 
            // btnTheme
            // 
            this.btnTheme.Location = new System.Drawing.Point(618, 11);
            this.btnTheme.Name = "btnTheme";
            this.btnTheme.Size = new System.Drawing.Size(75, 25);
            this.btnTheme.TabIndex = 3;
            this.btnTheme.Text = "Tema";
            this.btnTheme.UseVisualStyleBackColor = true;
            this.btnTheme.Click += new System.EventHandler(this.btnTheme_Click);
            // 
            // rtbChat
            // 
            this.rtbChat.Location = new System.Drawing.Point(12, 42);
            this.rtbChat.Name = "rtbChat";
            this.rtbChat.ReadOnly = true;
            this.rtbChat.Size = new System.Drawing.Size(516, 360);
            this.rtbChat.TabIndex = 4;
            this.rtbChat.Text = "";
            // 
            // lblPeers
            // 
            this.lblPeers.AutoSize = true;
            this.lblPeers.Location = new System.Drawing.Point(536, 42);
            this.lblPeers.Name = "lblPeers";
            this.lblPeers.Size = new System.Drawing.Size(125, 15);
            this.lblPeers.TabIndex = 5;
            this.lblPeers.Text = "Ağdaki Kişiler";
            // 
            // lstPeers
            // 
            this.lstPeers.FormattingEnabled = true;
            this.lstPeers.ItemHeight = 15;
            this.lstPeers.Location = new System.Drawing.Point(536, 60);
            this.lstPeers.Name = "lstPeers";
            this.lstPeers.Size = new System.Drawing.Size(171, 180);
            this.lstPeers.TabIndex = 6;
            this.lstPeers.DoubleClick += new System.EventHandler(this.lstPeers_DoubleClick);
            // 
            // lblOfflinePeers
            // 
            this.lblOfflinePeers.AutoSize = true;
            this.lblOfflinePeers.Location = new System.Drawing.Point(536, 245);
            this.lblOfflinePeers.Name = "lblOfflinePeers";
            this.lblOfflinePeers.Size = new System.Drawing.Size(100, 15);
            this.lblOfflinePeers.TabIndex = 13;
            this.lblOfflinePeers.Text = "Çevrimdışı Kişiler";
            // 
            // lstOfflinePeers
            // 
            this.lstOfflinePeers.FormattingEnabled = true;
            this.lstOfflinePeers.ItemHeight = 15;
            this.lstOfflinePeers.Location = new System.Drawing.Point(536, 263);
            this.lstOfflinePeers.Name = "lstOfflinePeers";
            this.lstOfflinePeers.Size = new System.Drawing.Size(171, 130);
            this.lstOfflinePeers.TabIndex = 14;
            this.lstOfflinePeers.DoubleClick += new System.EventHandler(this.lstOfflinePeers_DoubleClick);
            // 
            // txtMessage
            // 
            this.txtMessage.Location = new System.Drawing.Point(12, 408);
            this.txtMessage.Name = "txtMessage";
            this.txtMessage.PlaceholderText = "Bir mesaj yazın...";
            this.txtMessage.Size = new System.Drawing.Size(435, 23);
            this.txtMessage.TabIndex = 7;
            this.txtMessage.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtMessage_KeyDown);
            // 
            // btnSend
            // 
            this.btnSend.Location = new System.Drawing.Point(453, 407);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(75, 25);
            this.btnSend.TabIndex = 8;
            this.btnSend.Text = "Gönder";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(12, 438);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(127, 15);
            this.lblStatus.TabIndex = 9;
            this.lblStatus.Text = "Durum: Tor bekleniyor...";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(719, 461);
            this.Controls.Add(this.lblOfflinePeers);
            this.Controls.Add(this.lstOfflinePeers);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.txtMessage);
            this.Controls.Add(this.lstPeers);
            this.Controls.Add(this.lblPeers);
            this.Controls.Add(this.rtbChat);
            this.Controls.Add(this.btnTheme);
            this.Controls.Add(this.btnMyProfile);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.btnCopyOnion);
            this.Controls.Add(this.btnReconnect);
            this.Controls.Add(this.btnCancelConnect);
            this.Controls.Add(this.txtTargetOnion);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "Form1";
            this.Text = "Tor P2P Grup Chat";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtTargetOnion;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnMyProfile;
        private System.Windows.Forms.Button btnTheme;
        private System.Windows.Forms.RichTextBox rtbChat;
        private System.Windows.Forms.Label lblPeers;
        private System.Windows.Forms.ListBox lstPeers;
        private System.Windows.Forms.Label lblOfflinePeers;
        private System.Windows.Forms.ListBox lstOfflinePeers;
        private System.Windows.Forms.TextBox txtMessage;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnCancelConnect;
        private System.Windows.Forms.Button btnCopyOnion;
        private System.Windows.Forms.Button btnReconnect;
    }
}
