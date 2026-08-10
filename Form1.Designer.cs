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
            txtTargetOnion = new TextBox();
            btnConnect = new Button();
            rtbChat = new RichTextBox();
            txtMessage = new TextBox();
            btnSend = new Button();
            lstPeers = new ListBox();
            btnMyProfile = new Button();
            lblStatus = new Label();
            lblPeers = new Label();
            btnTheme = new Button();
            btnCancelConnect = new Button();
            btnCopyOnion = new Button();
            btnReconnect = new Button();
            lblOfflinePeers = new Label();
            lstOfflinePeers = new ListBox();
            SuspendLayout();
            // 
            // txtTargetOnion
            // 
            txtTargetOnion.Location = new Point(14, 16);
            txtTargetOnion.Margin = new Padding(3, 4, 3, 4);
            txtTargetOnion.Name = "txtTargetOnion";
            txtTargetOnion.PlaceholderText = "Bağlanmak için .onion adresi girin...";
            txtTargetOnion.Size = new Size(228, 27);
            txtTargetOnion.TabIndex = 0;
            // 
            // btnConnect
            // 
            btnConnect.Location = new Point(249, 15);
            btnConnect.Margin = new Padding(3, 4, 3, 4);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(63, 33);
            btnConnect.TabIndex = 1;
            btnConnect.Text = "Bağlan";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;
            // 
            // rtbChat
            // 
            rtbChat.Location = new Point(14, 56);
            rtbChat.Margin = new Padding(3, 4, 3, 4);
            rtbChat.Name = "rtbChat";
            rtbChat.ReadOnly = true;
            rtbChat.Size = new Size(589, 479);
            rtbChat.TabIndex = 4;
            rtbChat.Text = "";
            // 
            // txtMessage
            // 
            txtMessage.Location = new Point(14, 544);
            txtMessage.Margin = new Padding(3, 4, 3, 4);
            txtMessage.Name = "txtMessage";
            txtMessage.PlaceholderText = "Bir mesaj yazın...";
            txtMessage.Size = new Size(497, 27);
            txtMessage.TabIndex = 7;
            txtMessage.KeyDown += txtMessage_KeyDown;
            // 
            // btnSend
            // 
            btnSend.Location = new Point(518, 543);
            btnSend.Margin = new Padding(3, 4, 3, 4);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(86, 33);
            btnSend.TabIndex = 8;
            btnSend.Text = "Gönder";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Click += btnSend_Click;
            // 
            // lstPeers
            // 
            lstPeers.FormattingEnabled = true;
            lstPeers.Location = new Point(613, 80);
            lstPeers.Margin = new Padding(3, 4, 3, 4);
            lstPeers.Name = "lstPeers";
            lstPeers.Size = new Size(195, 224);
            lstPeers.TabIndex = 6;
            lstPeers.DoubleClick += lstPeers_DoubleClick;
            // 
            // btnMyProfile
            // 
            btnMyProfile.Location = new Point(632, 15);
            btnMyProfile.Margin = new Padding(3, 4, 3, 4);
            btnMyProfile.Name = "btnMyProfile";
            btnMyProfile.Size = new Size(91, 33);
            btnMyProfile.TabIndex = 2;
            btnMyProfile.Text = "Profilim";
            btnMyProfile.UseVisualStyleBackColor = true;
            btnMyProfile.Click += btnMyProfile_Click;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(14, 584);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(164, 20);
            lblStatus.TabIndex = 9;
            lblStatus.Text = "Durum: Tor bekleniyor...";
            // 
            // lblPeers
            // 
            lblPeers.AutoSize = true;
            lblPeers.Location = new Point(613, 56);
            lblPeers.Name = "lblPeers";
            lblPeers.Size = new Size(100, 20);
            lblPeers.TabIndex = 5;
            lblPeers.Text = "Ağdaki Kişiler";
            // 
            // btnTheme
            // 
            btnTheme.Location = new Point(729, 15);
            btnTheme.Margin = new Padding(3, 4, 3, 4);
            btnTheme.Name = "btnTheme";
            btnTheme.Size = new Size(86, 33);
            btnTheme.TabIndex = 3;
            btnTheme.Text = "Tema";
            btnTheme.UseVisualStyleBackColor = true;
            btnTheme.Click += btnTheme_Click;
            // 
            // btnCancelConnect
            // 
            btnCancelConnect.Enabled = false;
            btnCancelConnect.Location = new Point(318, 15);
            btnCancelConnect.Margin = new Padding(3, 4, 3, 4);
            btnCancelConnect.Name = "btnCancelConnect";
            btnCancelConnect.Size = new Size(57, 33);
            btnCancelConnect.TabIndex = 10;
            btnCancelConnect.Text = "İptal";
            btnCancelConnect.UseVisualStyleBackColor = true;
            btnCancelConnect.Click += btnCancelConnect_Click;
            // 
            // btnCopyOnion
            // 
            btnCopyOnion.Location = new Point(381, 15);
            btnCopyOnion.Margin = new Padding(3, 4, 3, 4);
            btnCopyOnion.Name = "btnCopyOnion";
            btnCopyOnion.Size = new Size(115, 33);
            btnCopyOnion.TabIndex = 11;
            btnCopyOnion.Text = "Onion Kopyala";
            btnCopyOnion.UseVisualStyleBackColor = true;
            btnCopyOnion.Click += btnCopyOnion_Click;
            // 
            // btnReconnect
            // 
            btnReconnect.Location = new Point(502, 15);
            btnReconnect.Margin = new Padding(3, 4, 3, 4);
            btnReconnect.Name = "btnReconnect";
            btnReconnect.Size = new Size(125, 33);
            btnReconnect.TabIndex = 12;
            btnReconnect.Text = "Yeniden Bağlan";
            btnReconnect.UseVisualStyleBackColor = true;
            btnReconnect.Click += btnReconnect_Click;
            // 
            // lblOfflinePeers
            // 
            lblOfflinePeers.AutoSize = true;
            lblOfflinePeers.Location = new Point(613, 327);
            lblOfflinePeers.Name = "lblOfflinePeers";
            lblOfflinePeers.Size = new Size(122, 20);
            lblOfflinePeers.TabIndex = 13;
            lblOfflinePeers.Text = "Çevrimdışı Kişiler";
            // 
            // lstOfflinePeers
            // 
            lstOfflinePeers.FormattingEnabled = true;
            lstOfflinePeers.Location = new Point(613, 351);
            lstOfflinePeers.Margin = new Padding(3, 4, 3, 4);
            lstOfflinePeers.Name = "lstOfflinePeers";
            lstOfflinePeers.Size = new Size(195, 164);
            lstOfflinePeers.TabIndex = 14;
            lstOfflinePeers.DoubleClick += lstOfflinePeers_DoubleClick;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(822, 615);
            Controls.Add(lblOfflinePeers);
            Controls.Add(lstOfflinePeers);
            Controls.Add(lblStatus);
            Controls.Add(btnSend);
            Controls.Add(txtMessage);
            Controls.Add(lstPeers);
            Controls.Add(lblPeers);
            Controls.Add(rtbChat);
            Controls.Add(btnTheme);
            Controls.Add(btnMyProfile);
            Controls.Add(btnConnect);
            Controls.Add(btnCopyOnion);
            Controls.Add(btnReconnect);
            Controls.Add(btnCancelConnect);
            Controls.Add(txtTargetOnion);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Margin = new Padding(3, 4, 3, 4);
            MaximizeBox = false;
            Name = "Form1";
            Text = "Tor P2P Grup Chat";
            FormClosing += Form1_FormClosing;
            Load += Form1_Load;
            ResumeLayout(false);
            PerformLayout();

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
