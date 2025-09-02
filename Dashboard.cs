using ClosedXML.Excel; // Make sure you’ve installed ClosedXML via NuGet
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading; // ADD THIS for threading support


namespace SENTRY_BETA_
{
    public partial class Dashboard : Form
    {
        private SerialPort rfidPort;
        private ManagementEventWatcher deviceWatcher;
        private string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\BACKUP\\SENTRY(BETA)\\SENTRY.mdf;Integrated Security=True";
        private Dictionary<string, DateTime> recentScans = new Dictionary<string, DateTime>();
        private CancellationTokenSource watcherTokenSource = new CancellationTokenSource();
        private bool isConnecting = false;
        private System.Timers.Timer watchdogTimer = new System.Timers.Timer(30000);
        private DateTime lastTagTime = DateTime.Now;

        public Dashboard()
        {
            InitializeComponent();
        }

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

        private void TryConnectRfid()
        {
            if (isConnecting) return;
            isConnecting = true;

            Task.Run(() =>
            {
                string port = DetectRfidPortByDeviceName();

                BeginInvoke(new Action(() =>
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
                                    UpdateStatusLabel("Serial Error, reconnecting...", Color.Orange);
                                    TryConnectRfid();
                                }));
                            };

                            if (TryOpenPort(rfidPort))
                            {
                                Thread.Sleep(200);
                                rfidPort.DiscardInBuffer();
                                UpdateStatusLabel($"Connected ({port})", Color.LimeGreen);
                            }
                            else
                            {
                                UpdateStatusLabel("Failed to open RFID port", Color.Red);
                            }
                        }
                        else
                        {
                            UpdateStatusLabel("Disconnected", Color.Gray);
                        }
                    }
                    catch (Exception ex)
                    {
                        UpdateStatusLabel("Connection Error", Color.Red);
                        Console.WriteLine("TryConnect Error: " + ex.Message);
                    }
                    finally
                    {
                        isConnecting = false;
                    }
                }));
            });
        }

        private void RfidPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (rfidPort?.IsOpen != true) return;
                string tag = rfidPort.ReadLine().Trim();
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    lastTagTime = DateTime.Now;
                    BeginInvoke((MethodInvoker)(() => ValidateTagFromAttendance(tag)));
                }
            }
            catch
            {
                UpdateStatusLabel("Read Error", Color.Red);
            }
        }

        private void ValidateTagFromAttendance(string tag)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT COUNT(*) FROM Attendance WHERE RfidTag = @tag", conn);
                cmd.Parameters.AddWithValue("@tag", tag);
                int exists = (int)cmd.ExecuteScalar();

                if (exists == 0)
                {
                    MessageBox.Show("⚠ Unregistered RFID Tag.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                LogAttendance(tag);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            deviceWatcher?.Stop();
            deviceWatcher?.Dispose();

            if (rfidPort != null)
            {
                try
                {
                    if (rfidPort.IsOpen)
                        rfidPort.Close();

                    rfidPort.DataReceived -= RfidPort_DataReceived;
                    rfidPort.Dispose();
                }
                catch { }
            }

            watchdogTimer?.Stop();
            watchdogTimer?.Dispose();

            base.OnFormClosed(e);
        }


        private void StartDeviceWatcher()
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
            deviceWatcher = new ManagementEventWatcher(query);
            deviceWatcher.EventArrived += (_, __) => Task.Delay(500).ContinueWith(t =>
            {
                if (!IsDisposed) BeginInvoke((Action)TryConnectRfid);
            });
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

        //protected override void OnFormClosed(FormClosedEventArgs e)
        //{
        //    deviceWatcher?.Stop();
        //    deviceWatcher?.Dispose();

        //    if (rfidPort != null)
        //    {
        //        try
        //        {
        //            if (rfidPort.IsOpen)
        //                rfidPort.Close();

        //            rfidPort.DataReceived -= RfidPort_DataReceived;
        //            rfidPort.Dispose();
        //        }
        //        catch { }
        //    }

        //    base.OnFormClosed(e);
        //}


        private void UpdateStatusLabel(string text, Color color)
        {
            if (!IsDisposed)
            {
                BeginInvoke((MethodInvoker)(() =>
                {
                    lblStatus.Text = text;
                    lblStatus.ForeColor = color;
                }));
            }
        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void guna2Panel6_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btnNewEvent_Click(object sender, EventArgs e)
        {
            pnlEnterEVent.Visible = true;
            pnlEnterEVent.Dock = DockStyle.Fill;
            pnlEnterEVent.Enabled = true;
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            pnlEnterEVent.Visible = false;

            txtEventName.Clear();
            dtpEventTime.Value = DateTime.Now;
            txtEventTime.Clear();
            txtEventPlace.Clear();
        }

        private void txtEventName_TextChanged(object sender, EventArgs e)
        {

        }

        private void txtEventPlace_TextChanged(object sender, EventArgs e)
        {

        }

        private void dtpEventTime_ValueChanged(object sender, EventArgs e)
        {

        }

        private void txtEventTime_TextChanged(object sender, EventArgs e)
        {

        }

        private void Dashboard_Load(object sender, EventArgs e)
        {
            // TODO: This line of code loads data into the 'sENTRYDataSet15.Attendance' table. You can move, or remove it, as needed.
            this.attendanceTableAdapter1.Fill(this.sENTRYDataSet15.Attendance);
            // TODO: This line of code loads data into the 'sENTRYDataSet14.Attendance' table. You can move, or remove it, as needed.
            this.attendanceTableAdapter.Fill(this.sENTRYDataSet14.Attendance);
            // TODO: This line of code loads data into the 'sENTRYDataSet8.AttendanceLogs' table. You can move, or remove it, as needed.
            // TODO: This line of code loads data into the 'sENTRYDataSet7.AttendanceLogs' table. You can move, or remove it, as needed.
            // TODO: This line of code loads data into the 'sENTRYDataSet6.AttendanceLogs' table. You can move, or remove it, as needed.
            // TODO: This line of code loads data into the 'sENTRYDataSet5.AttendanceLogs' table. You can move, or remove it, as needed.            // TODO: This line of code loads data into the 'sENTRYDataSet4.AttendanceLogs' table. You can move, or remove it, as needed.
            TryConnectRfid();
            StartWatchdog();

            UpdateTimeInOutFromDatabase();
            LoadAttendanceLog2();
            CenterDataGridViewText(dgvAttendanceLog);
            UpdateAttendanceCount();      // Recalculate attendance count column
        }

        private void CenterDataGridViewText(DataGridView dgv)
        {
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        //private void LogAttendance(string tag)
        //{
        //    try
        //    {SDADA
        //        using (SqlConnection conn = new SqlConnection(connectionString))
        //        {
        //            conn.Open();

        //            SqlCommand checkCmd = new SqlCommand("SELECT * FROM Attendance WHERE RfidTag = @Tag", conn);
        //            checkCmd.Parameters.AddWithValue("@Tag", tag);

        //            using (SqlDataReader reader = checkCmd.ExecuteReader())
        //            {
        //                if (!reader.HasRows)
        //                {
        //                    MessageBox.Show("⚠ Unregistered RFID Tag.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //                    return;
        //                }

        //                if (!reader.Read())
        //                {
        //                    MessageBox.Show("⚠ Error reading tag details.", "Read Error");
        //                    return;
        //                }

        //                int id = Convert.ToInt32(reader["Id"]);
        //                string fullName = reader["FullName"].ToString();
        //                string studentId = reader["StudentId"].ToString();
        //                string program = reader["Program"].ToString();
        //                string year = reader["YearLevel"].ToString();
        //                string dept = reader["Department"].ToString();

        //                DateTime? AmIN = reader["AmIN"] == DBNull.Value ? null : (DateTime?)reader["AmIN"];
        //                DateTime? AmOut = reader["AmOut"] == DBNull.Value ? null : (DateTime?)reader["AmOut"];
        //                DateTime? PmIn = reader["PmIn"] == DBNull.Value ? null : (DateTime?)reader["PmIn"];
        //                DateTime? PmOut = reader["PmOut"] == DBNull.Value ? null : (DateTime?)reader["PmOut"];

        //                reader.Close();

        //                lblStudentName.Text = fullName;
        //                lblIdNumber.Text = studentId;
        //                lblStudentProgram.Text = program;
        //                lblStudentYear.Text = year;
        //                lblDepartment.Text = dept;

        //                string columnToUpdate = null;
        //                DateTime? lastLog = null;

        //                if (rbAm.Checked && rbTimeIn.Checked) { columnToUpdate = "AmIN"; lastLog = AmIN; }
        //                else if (rbAm.Checked && rbTimeOut.Checked) { columnToUpdate = "AmOut"; lastLog = AmOut; }
        //                else if (rbPm.Checked && rbTimeIn.Checked) { columnToUpdate = "PmIn"; lastLog = PmIn; }
        //                else if (rbPm.Checked && rbTimeOut.Checked) { columnToUpdate = "PmOut"; lastLog = PmOut; }

        //                if (string.IsNullOrEmpty(columnToUpdate))
        //                {
        //                    MessageBox.Show("⚠ Please select AM/PM and Time In/Out.");
        //                    return;
        //                }

        //                if (lastLog != null && lastLog.Value.Date == DateTime.Now.Date)
        //                {
        //                    MessageBox.Show($"⚠ {columnToUpdate} already logged at {lastLog:hh:mm tt}");
        //                    return;
        //                }

        //                SqlCommand updateCmd = new SqlCommand($"UPDATE Attendance SET {columnToUpdate} = @Now WHERE Id = @ID", conn);
        //                updateCmd.Parameters.AddWithValue("@Now", DateTime.Now);
        //                updateCmd.Parameters.AddWithValue("@ID", id);
        //                updateCmd.ExecuteNonQuery();

        //                MessageBox.Show($"✅ Time logged for {fullName} at {DateTime.Now:hh:mm tt}");

        //                LoadAttendanceLog2();
        //                UpdateAttendanceCount();
        //                UpdateTimeInOutFromDatabase();

        //                Task.Delay(5000).ContinueWith(_ =>
        //                {
        //                    if (this.IsHandleCreated && !this.IsDisposed)
        //                    {
        //                        this.BeginInvoke((MethodInvoker)(() =>
        //                        {
        //                            lblStudentName.Text = "";
        //                            lblIdNumber.Text = "";
        //                            lblStudentProgram.Text = "";
        //                            lblStudentYear.Text = "";
        //                            lblDepartment.Text = "";
        //                        }));
        //                    }
        //                });
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show("❌ Error logging attendance:\n" + ex.Message);
        //    }
        //}

        private void LogAttendance(string tag)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    SqlCommand checkCmd = new SqlCommand("SELECT * FROM Attendance WHERE RfidTag = @Tag", conn);
                    checkCmd.Parameters.AddWithValue("@Tag", tag);

                    SqlDataReader reader = checkCmd.ExecuteReader();

                    if (!reader.HasRows)
                    {
                        reader.Close();
                        MessageBox.Show("⚠ Unregistered RFID Tag.", "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    reader.Read();

                    int id = Convert.ToInt32(reader["Id"]);
                    string fullName = reader["FullName"].ToString();
                    string studentId = reader["StudentId"].ToString();
                    string program = reader["Program"].ToString();
                    string year = reader["YearLevel"].ToString();
                    string dept = reader["Department"].ToString();

                    DateTime? AmIN = reader["AmIN"] == DBNull.Value ? null : (DateTime?)reader["AmIN"];
                    DateTime? AmOut = reader["AmOut"] == DBNull.Value ? null : (DateTime?)reader["AmOut"];
                    DateTime? PmIn = reader["PmIn"] == DBNull.Value ? null : (DateTime?)reader["PmIn"];
                    DateTime? PmOut = reader["PmOut"] == DBNull.Value ? null : (DateTime?)reader["PmOut"];

                    reader.Close();

                    lblStudentName.Text = fullName;
                    lblIdNumber.Text = studentId;
                    lblStudentProgram.Text = program;
                    lblStudentYear.Text = year;
                    lblDepartment.Text = dept;

                    string columnToUpdate = null;
                    DateTime? lastLog = null;

                    if (rbAm.Checked && rbTimeIn.Checked) { columnToUpdate = "AmIN"; lastLog = AmIN; }
                    else if (rbAm.Checked && rbTimeOut.Checked) { columnToUpdate = "AmOut"; lastLog = AmOut; }
                    else if (rbPm.Checked && rbTimeIn.Checked) { columnToUpdate = "PmIn"; lastLog = PmIn; }
                    else if (rbPm.Checked && rbTimeOut.Checked) { columnToUpdate = "PmOut"; lastLog = PmOut; }

                    if (string.IsNullOrEmpty(columnToUpdate))
                    {
                        MessageBox.Show("⚠ Please select AM/PM and Time In/Out.");
                        return;
                    }

                    if (lastLog != null && lastLog.Value.Date == DateTime.Now.Date)
                    {
                        MessageBox.Show($"⚠ {columnToUpdate} already logged at {lastLog:hh:mm tt}");
                        return;
                    }

                    SqlCommand updateCmd = new SqlCommand($"UPDATE Attendance SET {columnToUpdate} = @Now WHERE Id = @ID", conn);
                    updateCmd.Parameters.AddWithValue("@Now", DateTime.Now);
                    updateCmd.Parameters.AddWithValue("@ID", id);
                    updateCmd.ExecuteNonQuery();

                    MessageBox.Show($"✅ Time logged for {fullName} at {DateTime.Now:hh:mm tt}");

                    LoadAttendanceLog2();
                    UpdateAttendanceCount();
                    UpdateTimeInOutFromDatabase();

                    Task.Delay(5000).ContinueWith(_ =>
                    {
                        if (this.IsHandleCreated && !this.IsDisposed)
                        {
                            this.BeginInvoke((MethodInvoker)(() =>
                            {
                                lblStudentName.Text = "";
                                lblIdNumber.Text = "";
                                lblStudentProgram.Text = "";
                                lblStudentYear.Text = "";
                                lblDepartment.Text = "";
                            }));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Error logging attendance:\n" + ex.Message);
            }
        }


        private void LoadAttendanceLogLatestFirst()
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT * FROM Attendance ORDER BY Id DESC"; // Use DateTime if preferred
                SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                DataTable dt = new DataTable();
                adapter.Fill(dt);
                dgvAttendanceLog.DataSource = dt;
            }
        }

        private void LogAttendanceById(string studentId)
        {
            if (!rbTimeIn.Checked && !rbTimeOut.Checked)
            {
                MessageBox.Show("Please select Time In or Time Out.");
                return;
            }

            if (!rbAm.Checked && !rbPm.Checked)
            {
                MessageBox.Show("Please select AM or PM.");
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                SqlCommand checkCmd = new SqlCommand("SELECT * FROM Attendance WHERE StudentId = @StudentId", conn);
                checkCmd.Parameters.AddWithValue("@StudentId", studentId);

                SqlDataReader reader = checkCmd.ExecuteReader();
                if (!reader.Read())
                {
                    MessageBox.Show("❌ Student not found in Attendance table.");
                    txtIdInput.Clear();
                    return;
                }

                int attId = Convert.ToInt32(reader["Id"]);
                string name = reader["FullName"].ToString();
                string tag = reader["RfidTag"].ToString();
                string program = reader["Program"].ToString();
                string year = reader["YearLevel"].ToString();
                string dept = reader["Department"].ToString();

                lblStudentName.Text = name;
                lblIdNumber.Text = studentId;
                lblStudentProgram.Text = program;
                lblStudentYear.Text = year;
                lblDepartment.Text = dept;

                string columnToUpdate = null;

                if (rbAm.Checked && rbTimeIn.Checked) columnToUpdate = "AmIN";
                else if (rbAm.Checked && rbTimeOut.Checked) columnToUpdate = "AmOut";
                else if (rbPm.Checked && rbTimeIn.Checked) columnToUpdate = "PmIn";
                else if (rbPm.Checked && rbTimeOut.Checked) columnToUpdate = "PmOut";

                if (string.IsNullOrEmpty(columnToUpdate))
                {
                    MessageBox.Show("⚠ Please select a valid AM/PM and In/Out combination.");
                    return;
                }

                // Prevent double scan
                if (!Convert.IsDBNull(reader[columnToUpdate]))
                {
                    MessageBox.Show($"⛔ Already logged {columnToUpdate} for {name}.");
                    txtIdInput.Clear();
                    return;
                }

                reader.Close();

                SqlCommand updateCmd = new SqlCommand($"UPDATE Attendance SET {columnToUpdate} = @Now WHERE Id = @ID", conn);
                updateCmd.Parameters.AddWithValue("@Now", DateTime.Now);
                updateCmd.Parameters.AddWithValue("@ID", attId);
                updateCmd.ExecuteNonQuery();

                UpdateAttendanceCount();

                MessageBox.Show($"✅ {columnToUpdate} recorded for {name} at {DateTime.Now:hh:mm tt}");
                LoadAttendanceLog2();
                UpdateTimeInOutFromDatabase();
                txtIdInput.Clear();
            }
        }




        private void guna2Button2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtIdInput.Text))
            {
                MessageBox.Show("Please enter a Student ID.");
                return;
            }

            LogAttendanceById(txtIdInput.Text.Trim());
        }

        
        private void btnConfirm_Click(object sender, EventArgs e)
        {
            lblEvent.Text = txtEventName.Text ;
            lblPlace.Text = txtEventPlace.Text;
            lblTime.Text = txtEventTime.Text  ;
            lblDate.Text = dtpEventTime.Value.ToString("hh:mm tt");

            pnlEnterEVent.Visible = false;
            pnlEnterEVent.Dock = DockStyle.None;
            pnlEnterEVent.Enabled = false;
        }

        private void guna2Button3_Click(object sender, EventArgs e)
        {
                string eventName = txtEventName.Text.Trim();
                string fileNameDate = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                if (string.IsNullOrWhiteSpace(eventName))
                {
                    MessageBox.Show("Please enter an Event Name.");
                    return;
                }

                DialogResult confirm = MessageBox.Show(
                    "Export all records from Attendance table and delete them after export?",
                    "Confirm Export & Delete",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (confirm != DialogResult.Yes) return;

                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        string query = @"
                            SELECT StudentId, RfidTag, FullName, Department, Program, YearLevel,
                                   AmIN, AmOut, PmIn, PmOut, [Count]
                            FROM Attendance
                            ORDER BY FullName ASC";

                        SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                        DataTable table = new DataTable();
                        adapter.Fill(table);

                        if (table.Rows.Count == 0)
                        {
                            MessageBox.Show("No records found in Attendance.");
                            return;
                        }

                        using (var workbook = new XLWorkbook())
                        {
                            var worksheet = workbook.Worksheets.Add("Attendance");
                            worksheet.Cell(1, 1).InsertTable(table);
                            worksheet.Table(0).Theme = XLTableTheme.TableStyleMedium9;
                            worksheet.Columns().AdjustToContents();

                            int lastRow = worksheet.LastRowUsed().RowNumber() + 2;
                            worksheet.Cell(lastRow, 1).Value = "Total Records:";
                            worksheet.Cell(lastRow, 2).Value = table.Rows.Count;
                            worksheet.Cell(lastRow, 1).Style.Font.Bold = true;

                            worksheet.Cell(lastRow + 1, 1).Value = "Exported by:";
                            worksheet.Cell(lastRow + 1, 2).Value = lblFullName.Text.Trim();

                            string fileName = $"{eventName}_{fileNameDate}.xlsx";

                            using (SaveFileDialog saveDialog = new SaveFileDialog())
                            {
                                saveDialog.Filter = "Excel Workbook|*.xlsx";
                                saveDialog.FileName = fileName;

                                if (saveDialog.ShowDialog() == DialogResult.OK)
                                {
                                    workbook.SaveAs(saveDialog.FileName);
                                    MessageBox.Show("✅ Exported to: " + saveDialog.FileName);

                                    SqlCommand deleteCmd = new SqlCommand("DELETE FROM Attendance", conn);
                                    deleteCmd.ExecuteNonQuery();

                                    dgvAttendanceLog.DataSource = null;
                                    btnExportExcel.Enabled = false;
                                    UpdateTimeInOutFromDatabase();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("❌ Export failed:\n" + ex.Message);
                }
        }

        private void UpdateAttendanceCount()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                UPDATE Attendance
                SET Count = 
                    (CASE WHEN AmIN IS NULL THEN 1 ELSE 0 END) +
                    (CASE WHEN AmOut IS NULL THEN 1 ELSE 0 END) +
                    (CASE WHEN PmIn IS NULL THEN 1 ELSE 0 END) +
                    (CASE WHEN PmOut IS NULL THEN 1 ELSE 0 END)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("⚠ Failed to update counts:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void UpdateTimeInOutFromDatabase()
        {
            int timeInCount = 0;
            int timeOutCount = 0;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = @"
            SELECT
                SUM(CASE WHEN AmIN IS NOT NULL OR PmIn IS NOT NULL THEN 1 ELSE 0 END) AS TimeInTotal,
                SUM(CASE WHEN AmOut IS NOT NULL OR PmOut IS NOT NULL THEN 1 ELSE 0 END) AS TimeOutTotal
            FROM Attendance";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        timeInCount = reader["TimeInTotal"] != DBNull.Value ? Convert.ToInt32(reader["TimeInTotal"]) : 0;
                        timeOutCount = reader["TimeOutTotal"] != DBNull.Value ? Convert.ToInt32(reader["TimeOutTotal"]) : 0;
                    }
                }
            }

            lblTimeIn.Text = timeInCount.ToString();
            lblTimeOut.Text = timeOutCount.ToString();
        }




        private void LoadAttendanceLog2()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
            SELECT StudentId, FullName, Department, Program, YearLevel,
                   AmIN, AmOut, PmIn, PmOut
            FROM Attendance
            WHERE CAST(AmIN AS DATE) = CAST(GETDATE() AS DATE)
               OR CAST(AmOut AS DATE) = CAST(GETDATE() AS DATE)
               OR CAST(PmIn AS DATE) = CAST(GETDATE() AS DATE)
               OR CAST(PmOut AS DATE) = CAST(GETDATE() AS DATE)
            ORDER BY 
                (SELECT MAX(dt) FROM (VALUES (AmIN), (AmOut), (PmIn), (PmOut)) AS AllTimes(dt)) DESC";

                    SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    dgvAttendanceLog.DataSource = table;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Failed to load today's attendance:\n" + ex.Message);
            }
        }



        private void dgvAtttendanceLog_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void rbTimeIn_CheckedChanged(object sender, EventArgs e)
        {
            if (rbTimeIn.Checked) LoadAttendanceLog2();
        }

        private void rbTimeOut_CheckedChanged(object sender, EventArgs e)
        {
            if (rbTimeOut.Checked) LoadAttendanceLog2();
        }

        private void rbAm_CheckedChanged(object sender, EventArgs e)
        {
            if (rbAm.Checked) LoadAttendanceLog2();
        }

        private void rbPm_CheckedChanged(object sender, EventArgs e)
        {
            if (rbPm.Checked) LoadAttendanceLog2();
        }

        private void btnSavetoDb_Click(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                    INSERT INTO History (
                        StudentId, RfidTag, FullName, Program, YearLevel, Department,
                        AmIN, AmOut, PmIn, PmOut, Count
                    )
                    SELECT 
                        StudentId, RfidTag, FullName, Program, YearLevel, Department,
                        AmIN, AmOut, PmIn, PmOut, Count
                    FROM Attendance
                    WHERE AmIN IS NOT NULL OR AmOut IS NOT NULL OR PmIn IS NOT NULL OR PmOut IS NOT NULL";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    int rows = cmd.ExecuteNonQuery();

                    if (rows > 0)
                    {
                        MessageBox.Show($"✅ {rows} records saved to History.");
                        btnExportExcel.Enabled = true;
                    }
                    else
                    {
                        MessageBox.Show("⚠ No completed attendance to save.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Error saving to History:\n" + ex.Message);
            }
        }

        private void lblStatus_Click(object sender, EventArgs e)
        {

        }

        private void btnRetry_Click(object sender, EventArgs e)
        {
            TryConnectRfid();
        }
    }
}
