namespace SENTRY_BETA_
{
    partial class Log_In
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Log_In));
            this.btnPinConfirm = new Guna.UI2.WinForms.Guna2Button();
            this.txtPin = new Guna.UI2.WinForms.Guna2TextBox();
            this.lblFullName = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // btnPinConfirm
            // 
            this.btnPinConfirm.Animated = true;
            this.btnPinConfirm.BackColor = System.Drawing.Color.Transparent;
            this.btnPinConfirm.BorderRadius = 15;
            this.btnPinConfirm.DisabledState.BorderColor = System.Drawing.Color.DarkGray;
            this.btnPinConfirm.DisabledState.CustomBorderColor = System.Drawing.Color.DarkGray;
            this.btnPinConfirm.DisabledState.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(169)))), ((int)(((byte)(169)))), ((int)(((byte)(169)))));
            this.btnPinConfirm.DisabledState.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(141)))), ((int)(((byte)(141)))), ((int)(((byte)(141)))));
            this.btnPinConfirm.FillColor = System.Drawing.Color.WhiteSmoke;
            this.btnPinConfirm.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnPinConfirm.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(56)))), ((int)(((byte)(97)))));
            this.btnPinConfirm.Image = ((System.Drawing.Image)(resources.GetObject("btnPinConfirm.Image")));
            this.btnPinConfirm.Location = new System.Drawing.Point(418, 635);
            this.btnPinConfirm.Name = "btnPinConfirm";
            this.btnPinConfirm.Size = new System.Drawing.Size(239, 46);
            this.btnPinConfirm.TabIndex = 186;
            this.btnPinConfirm.Text = "Confirm";
            this.btnPinConfirm.UseTransparentBackground = true;
            this.btnPinConfirm.Click += new System.EventHandler(this.btnPinConfirm_Click);
            // 
            // txtPin
            // 
            this.txtPin.Animated = true;
            this.txtPin.AutoRoundedCorners = true;
            this.txtPin.BackColor = System.Drawing.Color.Transparent;
            this.txtPin.BorderColor = System.Drawing.Color.White;
            this.txtPin.BorderThickness = 0;
            this.txtPin.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtPin.DefaultText = "";
            this.txtPin.DisabledState.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(208)))), ((int)(((byte)(208)))), ((int)(((byte)(208)))));
            this.txtPin.DisabledState.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(226)))), ((int)(((byte)(226)))));
            this.txtPin.DisabledState.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(138)))), ((int)(((byte)(138)))), ((int)(((byte)(138)))));
            this.txtPin.DisabledState.PlaceholderForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(138)))), ((int)(((byte)(138)))), ((int)(((byte)(138)))));
            this.txtPin.FillColor = System.Drawing.Color.WhiteSmoke;
            this.txtPin.FocusedState.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(94)))), ((int)(((byte)(148)))), ((int)(((byte)(255)))));
            this.txtPin.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtPin.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(56)))), ((int)(((byte)(97)))));
            this.txtPin.HoverState.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(94)))), ((int)(((byte)(148)))), ((int)(((byte)(255)))));
            this.txtPin.Location = new System.Drawing.Point(199, 565);
            this.txtPin.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.txtPin.Name = "txtPin";
            this.txtPin.PlaceholderForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(56)))), ((int)(((byte)(97)))));
            this.txtPin.PlaceholderText = "ENTER ADMIN PIN";
            this.txtPin.SelectedText = "";
            this.txtPin.Size = new System.Drawing.Size(684, 60);
            this.txtPin.TabIndex = 185;
            this.txtPin.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.txtPin.TextOffset = new System.Drawing.Point(5, 0);
            // 
            // lblFullName
            // 
            this.lblFullName.BackColor = System.Drawing.Color.Transparent;
            this.lblFullName.Font = new System.Drawing.Font("Segoe UI", 10.875F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFullName.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(56)))), ((int)(((byte)(97)))));
            this.lblFullName.Location = new System.Drawing.Point(137, 502);
            this.lblFullName.Name = "lblFullName";
            this.lblFullName.Size = new System.Drawing.Size(832, 42);
            this.lblFullName.TabIndex = 187;
            this.lblFullName.Text = "Welcome to SENTRY! Enter PIN below to get started.";
            this.lblFullName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Log_In
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("$this.BackgroundImage")));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.ClientSize = new System.Drawing.Size(1920, 1080);
            this.Controls.Add(this.lblFullName);
            this.Controls.Add(this.txtPin);
            this.Controls.Add(this.btnPinConfirm);
            this.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "Log_In";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Log_In";
            this.Load += new System.EventHandler(this.Log_In_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private Guna.UI2.WinForms.Guna2Button btnPinConfirm;
        private Guna.UI2.WinForms.Guna2TextBox txtPin;
        private System.Windows.Forms.Label lblFullName;
    }
}