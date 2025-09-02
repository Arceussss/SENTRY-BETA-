using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Runtime.InteropServices;

namespace SENTRY_BETA_
{
    public partial class Log_In : Form
    {
        private int pinAttempts = 0;
        private int lockoutSeconds = 30;
        private bool isLockedOut = false;
        private Timer lockoutTimer;
        private string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\BACKUP\\SENTRY(BETA)\\SENTRY.mdf;Integrated Security=True";

        public Log_In()
        {
            InitializeComponent();

            this.Shown += (s, e) => ApplyRoundedCorners();
        }

        private void Log_In_Load(object sender, EventArgs e)
        {
            ApplyRoundedCorners();

            txtPin.UseSystemPasswordChar = true;
            txtPin.KeyDown += txtPin_KeyDown;
            txtPin.KeyPress += txtPin_KeyPress;
            txtPin.MouseDown += txtPin_MouseDown;

        }

        // Declare DLL
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect, int nTopRect, int nRightRect, int nBottomRect,
            int nWidthEllipse, int nHeightEllipse);

        // Apply rounded corners
        private void ApplyRoundedCorners()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Region  = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
        }

      
        private void btnPinConfirm_Click(object sender, EventArgs e)
        {
            string pin = txtPin.Text.Trim();

            if (string.IsNullOrWhiteSpace(pin))
            {
                MessageBox.Show("Please enter your PIN.");
                return;
            }

            VerifyAdminPin(pin);
        }

        private void VerifyAdminPin(string pin)
        {
            if (isLockedOut)
            {
                MessageBox.Show($"⏳ Try again in {lockoutSeconds} seconds.");
                return;
            }

            // MASTER PIN override
            if (pin == "1000101")
            {
                // Default fallback admin values
                string username = "Master";
                string fullName = "Master Admin";
                Image profileImage = null; // or load a default avatar if you prefer

                mainWindow main = new mainWindow(username, fullName, profileImage);
                main.Show();
                this.Hide();
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT Username, AdminId, FullName, ProfileImage FROM Admins WHERE Pin = @Pin";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Pin", pin);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string username = reader["Username"].ToString();
                        string fullName = reader["FullName"].ToString();
                        string adminId = reader["AdminId"].ToString();
                        byte[] imageData = reader["ProfileImage"] as byte[];

                        Image profileImage = null;
                        if (imageData != null && imageData.Length > 0)
                        {
                            using (MemoryStream ms = new MemoryStream(imageData))
                            {
                                profileImage = Image.FromStream(ms);
                            }
                        }

                        LogAdminLogin(adminId, fullName, pin);

                        mainWindow main = new mainWindow(username, fullName, profileImage);
                        main.Show();
                        this.Hide();
                    }
                    else
                    {
                        pinAttempts++;
                        if (pinAttempts >= 3)
                        {
                            BeginLockout();
                            txtPin.Clear();
                        }
                        else
                        {
                            MessageBox.Show($"❌ Incorrect Pin. {3 - pinAttempts} attempts left.");
                            txtPin.Clear();
                        }
                           
                    }
                }
            }
        }

        private void BeginLockout()
        {
            isLockedOut = true;
            lockoutSeconds = 30;
            MessageBox.Show("⛔ Too many incorrect attempts. Try again in 30 seconds.");

            lockoutTimer = new Timer();
            lockoutTimer.Interval = 1000;
            lockoutTimer.Tick += (s, e) =>
            {
                lockoutSeconds--;
                if (lockoutSeconds <= 0)
                {
                    lockoutTimer.Stop();
                    isLockedOut = false;
                    pinAttempts = 0;
                    MessageBox.Show("🔓 You may try again now.");
                }
            };
            lockoutTimer.Start();
        }

        private void LogAdminLogin(string adminId, string fullName, string pin)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand(
                    "INSERT INTO AdminLogHistory (AdminId, FullName, Pin) VALUES (@AdminId, @FullName, @Pin)", conn);
                cmd.Parameters.AddWithValue("@AdminId", adminId);
                cmd.Parameters.AddWithValue("@FullName", fullName);
                cmd.Parameters.AddWithValue("@Pin", pin);
                cmd.ExecuteNonQuery();
            }
        }

        private void txtPin_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Control && (e.KeyCode == Keys.C || e.KeyCode == Keys.V)))
                e.SuppressKeyPress = true;
        }

        private void txtPin_MouseDown(object sender, MouseEventArgs e)
        {
            txtPin.ContextMenu = new ContextMenu(); // disables right-click
        }

        private void txtPin_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                VerifyAdminPin(txtPin.Text.Trim());
            }
        }

    }
}
