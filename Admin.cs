using DocumentFormat.OpenXml.Office2019.Excel.RichData2;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management; // required for USB/COM port detection
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SENTRY_BETA_
{
    public partial class Admin : Form
    {
        private byte[] profileImageBytes = null;
        private string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\BACKUP\\SENTRY(BETA)\\SENTRY.mdf;Integrated Security=True";
        private SerialPort rfidPort;
        private ManagementEventWatcher deviceWatcher;
        public string _Id;
        private bool isConnecting = false;
        private CancellationTokenSource reconnectToken;
        private System.Timers.Timer watchdogTimer = new System.Timers.Timer(30000);
        private DateTime lastTagTime = DateTime.Now;

        public Admin()
        {
            InitializeComponent();
        }

        private void Admin_Load(object sender, EventArgs e)
        {
            // TODO: This line of code loads data into the 'sENTRYDataSet18.Students' table. You can move, or remove it, as needed.
            this.studentsTableAdapter4.Fill(this.sENTRYDataSet18.Students);
            // TODO: This line of code loads data into the 'sENTRYDataSet17.Students' table. You can move, or remove it, as needed.
            // TODO: This line of code loads data into the 'sENTRYDataSet12.Students' table. You can move, or remove it, as needed.
            this.studentsTableAdapter3.Fill(this.sENTRYDataSet12.Students);
            // TODO: This line of code loads data into the 'sENTRYDataSet11.AdminLogHistory' table. You can move, or remove it, as needed.
            this.adminLogHistoryTableAdapter1.Fill(this.sENTRYDataSet11.AdminLogHistory);
            // TODO: This line of code loads data into the 'sENTRYDataSet10.AdminLogHistory' table. You can move, or remove it, as needed.
            this.adminLogHistoryTableAdapter.Fill(this.sENTRYDataSet10.AdminLogHistory);
            // TODO: This line of code loads data into the 'sENTRYDataSet9.Admins' table. You can move, or remove it, as needed.
            this.adminsTableAdapter.Fill(this.sENTRYDataSet9.Admins);
            // TODO: This line of code loads data into the 'sENTRYDataSet2.Students' table. You can move, or remove it, as needed.
            this.studentsTableAdapter2.Fill(this.sENTRYDataSet2.Students);
            // TODO: This line of code loads data into the 'sENTRYDataSet1.Students' table. You can move, or remove it, as needed.
            this.studentsTableAdapter1.Fill(this.sENTRYDataSet1.Students);
            // TODO: This line of code loads data into the 'sENTRYDataSet.Students' table. You can move, or remove it, as needed.
            this.studentsTableAdapter.Fill(this.sENTRYDataSet.Students);

            StartDeviceWatcher();
            TryConnectRfid();
            LoadToGrid();
            LoadLogToGrid();

            pnl.Dock = DockStyle.Fill;

            CenterDataGridViewText(dgvStudents);
            CenterDataGridViewText(dgvLogHistory);

            txtPin.UseSystemPasswordChar = true;
            txtPin.KeyDown += txtPin_KeyDown;
            txtPin.KeyPress += txtPin_KeyPress;
            txtPin.MouseDown += txtPin_MouseDown;

            txtAdminPin2.UseSystemPasswordChar = true;
            txtAdminPin2.KeyDown += txtAdminPin2_KeyDown;
            txtAdminPin2.KeyPress += txtAdminPin2_KeyPress;
            txtAdminPin2.MouseDown += txtAdminPin2_MouseDown;

            txtPin.Focus();
            cbDepartment.Items.AddRange(new[] { "SACE", "SABH", "SHAS", "SECAP" });

            cbDepartment.SelectedIndexChanged += cbDepartment_SelectedIndexChanged;
            cbSortDepartment.Items.AddRange(departmentPrograms.Keys.ToArray());
            cbSortDepartment.SelectedIndexChanged += cbSortDepartment_SelectedIndexChanged;
            cbSortProgram.Enabled = false;

        }

        private readonly Dictionary<string, List<string>> departmentPrograms = new Dictionary<string, List<string>>()
        {
            { "SACE", new List<string> { "BSCPE", "BSEE", "BSECE", "BSCS", "BSIT", "BSA", "BSCE" } },
            { "SABH", new List<string> { "BSBA", "BSHRM", "BSTM" } },
            { "SHAS", new List<string> { "ABEnglish", "BSED", "BEED" } },
            { "SECAP", new List<string> { "BSE", "BTVTED", "BTLED" } }
        };

        private void CenterDataGridViewText(DataGridView dgv)
        {
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        private void StartDeviceWatcher()
        {
            WqlEventQuery query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
            deviceWatcher = new ManagementEventWatcher(query);
            deviceWatcher.EventArrived += (s, e) =>
            {
                Task.Run(() =>
                {
                    Thread.Sleep(1000);
                    TryConnectRfid();
                });
            };
            deviceWatcher.Start();
        }

        private string DetectRfidPortByDeviceName()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%)'"))
            {
                foreach (var device in searcher.Get())
                {
                    string name = device["Name"]?.ToString();
                    if (name != null && (name.Contains("CH340") || name.Contains("Arduino")))
                    {
                        int start = name.IndexOf("(COM");
                        if (start > 0)
                        {
                            string port = name.Substring(start + 1, name.Length - start - 2);
                            return port;
                        }
                    }
                }
            }
            return null;
        }

        // Top-level fields
        

        // Retry helper
        private bool TryOpenPort(SerialPort port, int retries = 3)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    port.Open();
                    return true;
                }
                catch
                {
                    Thread.Sleep(500);
                }
            }
            return false;
        }

        // Watchdog for idle timeout
        private void StartWatchdog()
        {
            watchdogTimer.Elapsed += (_, __) =>
            {
                if ((DateTime.Now - lastTagTime).TotalSeconds > 30)
                {
                    BeginInvoke((Action)(() => TryConnectRfid()));
                }
            };
            watchdogTimer.Start();
        }

        // Improved RFID connection logic
        private void TryConnectRfid(Action onComplete = null)
        {
            if (isConnecting) return;
            isConnecting = true;

            Task.Run(() =>
            {
                string port = DetectRfidPortByDeviceName();

                this.Invoke((MethodInvoker)delegate
                {
                    try
                    {
                        if (rfidPort != null)
                        {
                            if (rfidPort.IsOpen) rfidPort.Close();

                            rfidPort.DataReceived -= RfidPort_DataReceived;
                            rfidPort.Dispose();
                            rfidPort = null;
                        }

                        if (!string.IsNullOrEmpty(port))
                        {
                            rfidPort = new SerialPort(port, 9600)
                            {
                                DtrEnable = true,
                                ReadTimeout = 3000,
                                NewLine = "\n"
                            };

                            rfidPort.DataReceived += RfidPort_DataReceived;
                            rfidPort.ErrorReceived += (s, e) =>
                            {
                                BeginInvoke((Action)(() =>
                                {
                                    lblRFID.Text = "Serial Error (reconnecting...)";
                                    lblRFID.ForeColor = Color.Orange;
                                    TryConnectRfid();
                                }));
                            };

                            if (TryOpenPort(rfidPort))
                            {
                                Thread.Sleep(200);
                                rfidPort.DiscardInBuffer();
                                lblRFID.Text = $"Connected ({port})";
                                lblRFID.ForeColor = Color.LimeGreen;
                            }
                            else
                            {
                                lblRFID.Text = "Failed to open RFID port";
                                lblRFID.ForeColor = Color.Red;
                            }
                        }
                        else
                        {
                            lblRFID.Text = "Scanner not detected";
                            lblRFID.ForeColor = Color.Red;
                        }
                    }
                    catch (Exception ex)
                    {
                        lblRFID.Text = "Connection Error";
                        lblRFID.ForeColor = Color.Red;
                        Console.WriteLine("RFID error: " + ex.Message);
                    }
                    finally
                    {
                        isConnecting = false;
                        onComplete?.Invoke();
                    }
                });
            });
        }

        // Updated data receive
        private void RfidPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (rfidPort?.IsOpen != true) return;

                string tag = rfidPort.ReadLine().Trim();

                if (!string.IsNullOrWhiteSpace(tag))
                {
                    lastTagTime = DateTime.Now;

                    BeginInvoke((MethodInvoker)(() =>
                    {
                        lblRFID.Text = tag;
                        lblRFID.ForeColor = Color.Green;
                    }));
                }
            }
            catch (Exception ex)
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    lblRFID.Text = "Read Error";
                    lblRFID.ForeColor = Color.Red;
                    MessageBox.Show("Read Error: " + ex.Message);
                }));
            }
        }

        // Ensure cleanup
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                deviceWatcher?.Stop();
                deviceWatcher?.Dispose();

                if (rfidPort != null)
                {
                    if (rfidPort.IsOpen) rfidPort.Close();
                    rfidPort.DataReceived -= RfidPort_DataReceived;
                    rfidPort.Dispose();
                }

                watchdogTimer?.Stop();
                watchdogTimer?.Dispose();
            }
            catch { }

            base.OnFormClosing(e);
        }


        private void guna2Panel10_Paint(object sender, PaintEventArgs e)
        {

        }

        private void guna2Button2_Click(object sender, EventArgs e)
        {

        }

        private void btnReg_Click(object sender, EventArgs e)
        {
            txtStudentName.Enabled = true;
            txtStudentId.Enabled = true;
            cbDepartment.Enabled = true;
            cbProgram.Enabled = true;
            cbYearLevel.Enabled = true;
            pbStudentPicture.Enabled = true;
            btnAdd.Enabled = true;
            btnClear.Enabled = true;
            btnImport.Enabled = true;
            btnCancel.Enabled = true;
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            txtStudentName.Enabled = false;
            txtStudentId.Enabled = false;
            cbDepartment.Enabled = false;
            cbProgram.Enabled = false;
            cbYearLevel.Enabled = false;
            pbStudentPicture.Enabled = false;
            btnAdd.Enabled = false;
            btnUpdate.Enabled = false;
            btnClear.Enabled = false;
            btnImport.Enabled = false;
            btnCancel.Enabled = false;

            txtStudentName.Clear();
            txtStudentId.Clear();
            cbDepartment.SelectedItem = null;
            cbProgram.SelectedItem = null;
            cbYearLevel.SelectedItem = null;
            lblRFID.Text = string.Empty;
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtStudentName.Clear();
            txtStudentId.Clear();
            cbDepartment.SelectedItem = null;
            cbProgram.SelectedItem = null;
            cbYearLevel.SelectedItem = null;
            pbStudentPicture.Image = null;
            profileImageBytes = null;
            lblRFID.Text = string.Empty;
        }

        private void btnAddAdmin_Click(object sender, EventArgs e)
        {
            pnlPin2.Enabled = true;
            pnlPin2.Visible = true;
            pnlPin2.Dock = DockStyle.Fill;
        }



        private void btnBackAdminReg_Click(object sender, EventArgs e)
        {
            pnlPin2.Enabled = false;
            pnlPin2.Visible = false;
            pnlPin2.Dock = DockStyle.None;
        }

        private void btnConfirm2_Click(object sender, EventArgs e)
        {
            if (txtAdminPin2.Text == "1000101")
            {
                pnlAddAdmin.Enabled = true;
                pnlAddAdmin.Visible = true;
                pnlAddAdmin.Dock = DockStyle.Fill;

                pnlPin2.Enabled = false;
                pnlPin2.Visible = false;
                pnlPin2.Dock = DockStyle.None;
            }
            else
            {
                pnlPin2.Enabled = false;
                pnlPin2.Visible = false;
                pnlPin2.Dock = DockStyle.None;
            }
        }

        private void btnCancelAdmin_Click(object sender, EventArgs e)
        {
            pnlAddAdmin.Dock = DockStyle.None;
            pnlAddAdmin.Enabled = false;
            pnlAddAdmin.Visible = false;
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
            if (openFile.ShowDialog() == DialogResult.OK)
            {
                Image img = Image.FromFile(openFile.FileName);
                pbStudentPicture.Image = img;
                using (MemoryStream ms = new MemoryStream())
                {
                    img.Save(ms, img.RawFormat);
                    profileImageBytes = ms.ToArray();
                }
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtStudentName.Text) ||
                string.IsNullOrWhiteSpace(txtStudentId.Text) ||
                string.IsNullOrWhiteSpace(lblRFID.Text) ||
                string.IsNullOrWhiteSpace(cbDepartment.Text) ||
                string.IsNullOrWhiteSpace(cbProgram.Text) ||
                string.IsNullOrWhiteSpace(cbYearLevel.Text))
            {
                MessageBox.Show("Please complete all required fields.");
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string checkQuery = "SELECT COUNT(*) FROM Students WHERE StudentId = @StudentId OR RfidTag = @RfidTag";
                SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@StudentId", txtStudentId.Text);
                checkCmd.Parameters.AddWithValue("@RfidTag", lblRFID.Text);
                conn.Open();
                int exists = (int)checkCmd.ExecuteScalar();
                if (exists > 0)
                {
                    MessageBox.Show("This Student ID or RFID tag already exists.");
                    return;
                }

                string query = "INSERT INTO Students (FullName, StudentId, RfidTag, Department, Program, YearLevel, ProfileImage) " +
                               "VALUES (@FullName, @StudentId, @RfidTag, @Department, @Program, @YearLevel, @ProfileImage)";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@FullName", txtStudentName.Text);
                cmd.Parameters.AddWithValue("@StudentId", txtStudentId.Text);
                cmd.Parameters.AddWithValue("@RfidTag", lblRFID.Text);
                cmd.Parameters.AddWithValue("@Department", cbDepartment.Text);
                cmd.Parameters.AddWithValue("@Program", cbProgram.Text);
                cmd.Parameters.AddWithValue("@YearLevel", cbYearLevel.Text);

                // 🔐 Explicitly set VARBINARY for image
                SqlParameter imageParam = new SqlParameter("@ProfileImage", SqlDbType.VarBinary);
                imageParam.Value = (object)profileImageBytes ?? DBNull.Value;
                cmd.Parameters.Add(imageParam);

                cmd.ExecuteNonQuery();
                MessageBox.Show("Student added successfully.");
                LoadToGrid();
                shortcut();
            }
        }

        private void btnUpdate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtStudentId.Text))
            {
                MessageBox.Show("Student ID is required to update.");
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string checkQuery = "SELECT COUNT(*) FROM Students WHERE StudentId = @StudentId";
                SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@StudentId", txtStudentId.Text);
                conn.Open();
                int exists = (int)checkCmd.ExecuteScalar();
                if (exists == 0)
                {
                    MessageBox.Show("Student ID not found.");
                    return;
                }

                string query = "UPDATE Students SET FullName=@FullName, Department=@Department, Program=@Program, " +
                               "YearLevel=@YearLevel, ProfileImage=@ProfileImage WHERE StudentId=@StudentId";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@FullName", txtStudentName.Text);
                cmd.Parameters.AddWithValue("@Department", cbDepartment.Text);
                cmd.Parameters.AddWithValue("@Program", cbProgram.Text);
                cmd.Parameters.AddWithValue("@YearLevel", cbYearLevel.Text);
                cmd.Parameters.AddWithValue("@StudentId", txtStudentId.Text);

                var imgParam = new SqlParameter("@ProfileImage", SqlDbType.VarBinary);
                imgParam.Value = (object)profileImageBytes ?? DBNull.Value;
                cmd.Parameters.Add(imgParam);


                cmd.ExecuteNonQuery();
                MessageBox.Show("Student updated successfully.");
                LoadToGrid();
                shortcut();


            }
        }

        private void LoadLogToGrid()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter("SELECT * FROM AdminLogHistory", conn);
                DataTable table = new DataTable();
                adapter.Fill(table);
                dgvLogHistory.DataSource = table;
            }
        }

        private void LoadToGrid()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter("SELECT * FROM Students", conn);
                DataTable table = new DataTable();
                adapter.Fill(table);
                dgvStudents.DataSource = table;
            }
        }

        private void shortcut()
        {
            txtStudentName.Clear();
            txtStudentId.Clear();
            cbDepartment.SelectedItem = null;
            cbProgram.SelectedItem = null;
            cbYearLevel.SelectedItem = null;
            pbStudentPicture.Image = null;
            profileImageBytes = null;
            lblRFID.Text = string.Empty;

            txtStudentName.Enabled = false;
            txtStudentId.Enabled = false;
            cbDepartment.Enabled = false;
            cbProgram.Enabled = false;
            cbYearLevel.Enabled = false;
            pbStudentPicture.Enabled = false;
            btnAdd.Enabled = false;
            btnUpdate.Enabled = false;
            btnClear.Enabled = false;
            btnImport.Enabled = false;
            btnCancel.Enabled = false;
        }
        private void pnlRegisterPin_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btnRegAdmin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUSernameAdmin.Text) ||
        string.IsNullOrWhiteSpace(txtNameAdmin.Text) ||
        string.IsNullOrWhiteSpace(txtIAdmin.Text) ||
        string.IsNullOrWhiteSpace(txtPinAdmin.Text) ||
        string.IsNullOrWhiteSpace(cbYearLevelAdmin.Text) ||
        string.IsNullOrWhiteSpace(cbProgramAdmin.Text))
            {
                MessageBox.Show("Please fill out all fields.");
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string checkQuery = "SELECT COUNT(*) FROM Admins WHERE Username = @Username OR AdminId = @AdminId";
                SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@Username", txtUSernameAdmin.Text);
                checkCmd.Parameters.AddWithValue("@AdminId", txtIAdmin.Text);

                int exists = (int)checkCmd.ExecuteScalar();
                if (exists > 0)
                {
                    MessageBox.Show("Username or ID already exists.");
                    return;
                }

                string insertQuery = @"INSERT INTO Admins 
            (Username, FullName, AdminId, Pin, YearLevel, Program, ProfileImage)
            VALUES (@Username, @FullName, @AdminId, @Pin, @YearLevel, @Program, @ProfileImage)";

                SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
                insertCmd.Parameters.AddWithValue("@Username", txtUSernameAdmin.Text);
                insertCmd.Parameters.AddWithValue("@FullName", txtNameAdmin.Text);
                insertCmd.Parameters.AddWithValue("@AdminId", txtIAdmin.Text);
                insertCmd.Parameters.AddWithValue("@Pin", txtPinAdmin.Text);
                insertCmd.Parameters.AddWithValue("@YearLevel", cbYearLevelAdmin.Text);
                insertCmd.Parameters.AddWithValue("@Program", cbProgramAdmin.Text);

                var imageParam = new SqlParameter("@ProfileImage", SqlDbType.VarBinary);
                imageParam.Value = (object)adminImageBytes ?? DBNull.Value;
                insertCmd.Parameters.Add(imageParam);


                insertCmd.ExecuteNonQuery();
                MessageBox.Show("✅ Admin added.");
                ClearAdminForm();
            }
        }

        private void btnClearRegAdmin_Click(object sender, EventArgs e)
        {
            ClearAdminForm();
        }

        private void ClearAdminForm()
        {
            txtUSernameAdmin.Clear();
            txtNameAdmin.Clear();
            txtIAdmin.Clear();
            txtPinAdmin.Clear();
            cbYearLevelAdmin.SelectedIndex = -1;
            cbProgramAdmin.SelectedIndex = -1;
            pbAdminPic.Image = null;
            adminImageBytes = null;
        }

        private byte[] adminImageBytes = null;

        private void btnImportAdmin_Click(object sender, EventArgs e)
        {
            OpenFileDialog open = new OpenFileDialog();
            open.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
            if (open.ShowDialog() == DialogResult.OK)
            {
                Image img = Image.FromFile(open.FileName);
                pbAdminPic.Image = img;

                using (MemoryStream ms = new MemoryStream())
                {
                    img.Save(ms, img.RawFormat);
                    adminImageBytes = ms.ToArray();
                }
            }
        }

        private void btnPinConfirm_Click(object sender, EventArgs e)
        {
            if (txtPin.Text == "1000101")
            {
                pnl1.Enabled = true;
                pnl2.Enabled = true;
                pnl3.Enabled = true;

                txtPin.Enabled = false;
                txtPin.Visible = false;
                btnPinConfirm.Visible = false;
                btnPinConfirm.Enabled = false;
            }
            else
            {
                txtPin.Clear();
                MessageBox.Show($"❌ Incorrect Pin.");
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

                if (txtPin.Text == "1000101")
                {
                    pnl1.Enabled = true;
                    pnl2.Enabled = true;
                    pnl3.Enabled = true;

                    txtPin.Enabled = false;
                    txtPin.Visible = false;
                    btnPinConfirm.Visible = false;
                    btnPinConfirm.Enabled = false;
                    pnl.Enabled = false;
                    pnl.Dock = DockStyle.None;
                }
                else
                {
                    txtPin.Clear();
                    MessageBox.Show($"❌ Incorrect Pin.");
                }
            }
        }

        //Add admin
        private void txtAdminPin2_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Control && (e.KeyCode == Keys.C || e.KeyCode == Keys.V)))
                e.SuppressKeyPress = true;
        }

        private void txtAdminPin2_MouseDown(object sender, MouseEventArgs e)
        {
            txtPin.ContextMenu = new ContextMenu(); // disables right-click
        }

        private void txtAdminPin2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;

                if (txtAdminPin2.Text == "1000101")
                {
                    txtAdminPin2.Clear();
                    pnlAddAdmin.Enabled = true;
                    pnlAddAdmin.Visible = true;
                    pnlAddAdmin.Dock = DockStyle.Fill;

                    pnlPin2.Enabled = false;
                    pnlPin2.Visible = false;
                    pnlPin2.Dock = DockStyle.None;
                }
                else
                {
                    MessageBox.Show($"❌ Incorrect Pin.");
                    txtAdminPin2.Clear();
                    pnlPin2.Enabled = false;
                    pnlPin2.Visible = false;
                    pnlPin2.Dock = DockStyle.None;
                }
            }
        }

        private void pnl_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btnPinConfirm_Click_1(object sender, EventArgs e)
        {
            if (txtPin.Text == "1000101")
            {
                pnl1.Enabled = true;
                pnl2.Enabled = true;
                pnl3.Enabled = true;

                txtPin.Enabled = false;
                txtPin.Visible = false;
                btnPinConfirm.Visible = false;
                btnPinConfirm.Enabled = false;
                pnl.Enabled = false;
                pnl.Dock = DockStyle.None;
            }
            else
            {
                txtPin.Clear();
                MessageBox.Show($"❌ Incorrect Pin.");
            }
        }

        private void dgvStudents_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                string colname = dgvStudents.Columns[e.ColumnIndex].Name;

                DataGridViewRow selectedRow = dgvStudents.Rows[e.RowIndex];
                if (!(selectedRow.DataBoundItem is DataRowView rowView))
                {
                    MessageBox.Show("⚠ Invalid row format.");
                    return;
                }

                DataRow dataRow = rowView.Row;
                if (!dataRow.Table.Columns.Contains("Id"))
                {
                    MessageBox.Show("⚠ 'Id' column not found in the data source.");
                    return;
                }

                int studentId = Convert.ToInt32(dataRow["Id"]);

                if (colname == "btnEdit" || colname == "btnShow")
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        SqlCommand cmd = new SqlCommand("SELECT ProfileImage, * FROM Students WHERE Id = @ID", conn);
                        cmd.Parameters.AddWithValue("@ID", studentId);

                        SqlDataReader dr = cmd.ExecuteReader();
                        if (dr.Read())
                        {
                            byte[] buffer = dr["ProfileImage"] != DBNull.Value ? (byte[])dr["ProfileImage"] : null;

                            _Id = dr["Id"].ToString();
                            txtStudentName.Text = dr["FullName"].ToString();
                            txtStudentId.Text = dr["StudentId"].ToString();
                            lblRFID.Text = dr["RfidTag"].ToString();
                            cbDepartment.Text = dr["Department"].ToString();
                            cbProgram.Text = dr["Program"].ToString();
                            cbYearLevel.Text = dr["YearLevel"].ToString();

                            if (buffer != null)
                            {
                                using (MemoryStream ms = new MemoryStream(buffer))
                                    pbStudentPicture.Image = Image.FromStream(ms);
                            }
                            else
                            {
                                pbStudentPicture.Image = null;
                            }
                        }
                        dr.Close();
                    }

                    if (colname == "btnEdit")
                    {
                        EnableStudentFields(true);
                        btnAdd.Enabled = false;
                        btnUpdate.Enabled = true;
                        btnClear.Enabled = true;
                        txtStudentId.Enabled = false;
                        btnCancel.Enabled = true;
                    }
                    else if (colname == "btnShow")
                    {
                        EnableStudentFields(false);
                        btnAdd.Enabled = false;
                        btnUpdate.Enabled = false;
                        btnClear.Enabled = false;
                        btnCancel.Enabled = true;
                    }
                }
                else if (colname == "btnDelete")
                {
                    DialogResult confirm = MessageBox.Show("Delete this student record?", "Confirm",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (confirm == DialogResult.Yes)
                    {
                        using (SqlConnection conn = new SqlConnection(connectionString))
                        {
                            conn.Open();

                            SqlCommand deleteCmd = new SqlCommand("DELETE FROM Students WHERE Id = @ID", conn);
                            deleteCmd.Parameters.AddWithValue("@ID", studentId);
                            deleteCmd.ExecuteNonQuery();

                            SqlCommand countCmd = new SqlCommand("SELECT COUNT(*) FROM Students", conn);
                            int count = (int)countCmd.ExecuteScalar();

                            if (count == 0)
                            {
                                SqlCommand resetCmd = new SqlCommand("DBCC CHECKIDENT ('Students', RESEED, 0)", conn);
                                resetCmd.ExecuteNonQuery();
                            }
                        }

                        MessageBox.Show("✅ Student record deleted.");
                        LoadToGrid();
                        ClearStudentFields();
                        EnableStudentFields(false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Error: " + ex.Message, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EnableStudentFields(bool enabled)
        {
            txtStudentName.Enabled = enabled;
            txtStudentId.Enabled = enabled;
            cbDepartment.Enabled = enabled;
            cbProgram.Enabled = enabled;
            cbYearLevel.Enabled = enabled;
            pbStudentPicture.Enabled = enabled;
            btnImport.Enabled = enabled;
        }

        private void ClearStudentFields()
        {
            txtStudentName.Clear();
            txtStudentId.Clear();
            lblRFID.Text = "";
            cbDepartment.SelectedIndex = -1;
            cbProgram.SelectedIndex = -1;
            cbYearLevel.SelectedIndex = -1;
            pbStudentPicture.Image = null;
            profileImageBytes = null;
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            FilterStudents();
        }

        private void cbSortProgram_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterStudents();
        }

        private void cbSortDepartment_SelectedIndexChanged(object sender, EventArgs e)
        {
            cbSortProgram.Items.Clear();
            cbSortProgram.Text = "";
            cbSortProgram.Enabled = false;

            if (cbSortDepartment.SelectedItem is string selectedDept &&
                departmentPrograms.TryGetValue(selectedDept, out var programs))
            {
                cbSortProgram.Items.AddRange(programs.ToArray());
                cbSortProgram.Enabled = true;
                cbSortProgram.SelectedIndex = 0;
            }
            FilterStudents();
        }

        private void FilterStudents()
        {
            string search = txtSearch.Text.Trim();
            string program = cbSortProgram.SelectedItem?.ToString();
            string department = cbSortDepartment.SelectedItem?.ToString();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                StringBuilder query = new StringBuilder("SELECT Id, FullName, StudentId, RfidTag, Department, Program, YearLevel, CreatedAt FROM Students WHERE 1=1");

                if (!string.IsNullOrEmpty(search))
                    query.Append(" AND (FullName LIKE @search OR StudentId LIKE @search)");
                if (!string.IsNullOrEmpty(program))
                    query.Append(" AND Program = @program");
                if (!string.IsNullOrEmpty(department))
                    query.Append(" AND Department = @department");

                SqlCommand cmd = new SqlCommand(query.ToString(), conn);
                if (!string.IsNullOrEmpty(search))
                    cmd.Parameters.AddWithValue("@search", "%" + search + "%");
                if (!string.IsNullOrEmpty(program))
                    cmd.Parameters.AddWithValue("@program", program);
                if (!string.IsNullOrEmpty(department))
                    cmd.Parameters.AddWithValue("@department", department);

                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                adapter.Fill(dt);
                dgvStudents.DataSource = dt;

                if (dgvStudents.Columns.Contains("Id"))
                    dgvStudents.Columns["Id"].Visible = false;
            }
        }

        private void cbDepartment_SelectedIndexChanged(object sender, EventArgs e)
        {
            cbProgram.Items.Clear();
            cbProgram.Text = "";

            if (cbDepartment.SelectedItem is string selected && departmentPrograms.TryGetValue(selected, out var programs))
            {
                cbProgram.Items.AddRange(programs.ToArray());
                cbProgram.SelectedIndex = 0;
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            string selectedDept = cbSortDepartment.Text.Trim();
            string selectedProg = cbSortProgram.Text.Trim();

            if (string.IsNullOrWhiteSpace(selectedDept) || string.IsNullOrWhiteSpace(selectedProg))
            {
                MessageBox.Show("⚠ Please select both Department and Program.");
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string selectQuery = "SELECT * FROM Students WHERE Department = @Dept AND Program = @Prog";
                SqlCommand selectCmd = new SqlCommand(selectQuery, conn);
                selectCmd.Parameters.AddWithValue("@Dept", selectedDept);
                selectCmd.Parameters.AddWithValue("@Prog", selectedProg);

                using (SqlDataReader reader = selectCmd.ExecuteReader())
                {
                    List<Dictionary<string, object>> recordsToInsert = new List<Dictionary<string, object>>();

                    while (reader.Read())
                    {
                        string studentId = reader["StudentId"].ToString();

                        // Store record for later insert
                        var row = new Dictionary<string, object>
                        {
                            ["StudentId"] = studentId,
                            ["RfidTag"] = reader["RfidTag"].ToString(),
                            ["FullName"] = reader["FullName"].ToString(),
                            ["Program"] = reader["Program"].ToString(),
                            ["YearLevel"] = reader["YearLevel"].ToString(),
                            ["Department"] = reader["Department"].ToString()
                        };

                        recordsToInsert.Add(row);
                    }

                    reader.Close(); // Important: close reader before issuing another query

                    int inserted = 0;

                    foreach (var row in recordsToInsert)
                    {
                        // Check for duplicates
                        string checkQuery = "SELECT COUNT(*) FROM Attendance WHERE StudentId = @StudentId";
                        SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                        checkCmd.Parameters.AddWithValue("@StudentId", row["StudentId"]);
                        int exists = (int)checkCmd.ExecuteScalar();
                        if (exists > 0) continue;

                        // Count is always 4 since we insert with all NULLs
                        int count = 4;

                        string insertQuery = @"
                    INSERT INTO Attendance (StudentId, RfidTag, FullName, Program, YearLevel, Department, AmIN, AmOut, PmIn, PmOut, Count)
                    VALUES (@StudentId, @RfidTag, @FullName, @Program, @YearLevel, @Department, NULL, NULL, NULL, NULL, @Count)";

                        SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
                        insertCmd.Parameters.AddWithValue("@StudentId", row["StudentId"]);
                        insertCmd.Parameters.AddWithValue("@RfidTag", row["RfidTag"]);
                        insertCmd.Parameters.AddWithValue("@FullName", row["FullName"]);
                        insertCmd.Parameters.AddWithValue("@Program", row["Program"]);
                        insertCmd.Parameters.AddWithValue("@YearLevel", row["YearLevel"]);
                        insertCmd.Parameters.AddWithValue("@Department", row["Department"]);
                        insertCmd.Parameters.AddWithValue("@Count", count);

                        insertCmd.ExecuteNonQuery();
                        inserted++;
                    }

                    MessageBox.Show($"✅ {inserted} student(s) exported to Attendance.");
                }
            }
        }

        private void btnRetry_Click(object sender, EventArgs e)
        {
            btnRetry.Enabled = false;
            lblRFID.Text = "Reconnecting...";
            lblRFID.ForeColor = Color.Orange;

            TryConnectRfid(() =>
            {
                btnRetry.Enabled = true;
            });
        }


    }
}
