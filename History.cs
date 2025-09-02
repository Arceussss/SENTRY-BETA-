using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using ClosedXML.Excel;
using System.IO;

namespace SENTRY_BETA_
{
    public partial class History : Form
    {
        private string connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\BACKUP\\SENTRY(BETA)\\SENTRY.mdf;Integrated Security=True";

        public History()
        {
            InitializeComponent();
        }

        private void History_Load(object sender, EventArgs e)
        {
            // TODO: This line of code loads data into the 'sENTRYDataSet16.History' table. You can move, or remove it, as needed.
            this.historyTableAdapter1.Fill(this.sENTRYDataSet16.History);
            // TODO: This line of code loads data into the 'sENTRYDataSet13.History' table. You can move, or remove it, as needed.

            CenterDataGridViewText(dgvHistory);

            cbSort.Items.AddRange(new string[]
{
                "Full Name A-Z",
                "Full Name Z-A",
                "Newest First",
                "Oldest First"
            });
            cbSort.SelectedIndex = 0;
        }

        private void CenterDataGridViewText(DataGridView dgv)
        {
            dgv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgv.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

       
        private void cbSort_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSortedHistory();
        }

        private void LoadSortedHistory()
        {
            if (cbSort.SelectedItem == null) return;

            string selected = cbSort.SelectedItem.ToString();
            string sortClause = "ORDER BY Id DESC"; // fallback

            // Determine sorting based on selected value
            switch (selected)
            {
                case "Full Name A-Z":
                    sortClause = "ORDER BY FullName ASC";
                    break;
                case "Full Name Z-A":
                    sortClause = "ORDER BY FullName DESC";
                    break;
                case "Recent AM In":
                    sortClause = "ORDER BY AmIN DESC";
                    break;
                case "Recent PM In":
                    sortClause = "ORDER BY PmIn DESC";
                    break;
                case "Recent AM Out":
                    sortClause = "ORDER BY AmOut DESC";
                    break;
                case "Recent PM Out":
                    sortClause = "ORDER BY PmOut DESC";
                    break;
                case "Oldest AM In":
                    sortClause = "ORDER BY AmIN ASC";
                    break;
                case "Oldest PM In":
                    sortClause = "ORDER BY PmIn ASC";
                    break;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT * FROM History WHERE 1=1 " + sortClause;

                SqlDataAdapter adapter = new SqlDataAdapter(query, conn);
                DataTable dt = new DataTable();
                adapter.Fill(dt);
                dgvHistory.DataSource = dt;
            }

            UpdateHistoryCounts();
        }



        private void btnExportExcel_Click(object sender, EventArgs e)
        {
            if (dgvHistory.DataSource == null || dgvHistory.Rows.Count == 0)
            { 
                MessageBox.Show("No records to export.");
                return;
            }

            DataTable dt = ((DataTable)dgvHistory.DataSource);

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Filtered History");
                ws.Cell(1, 1).InsertTable(dt);
                ws.Columns().AdjustToContents();

                string fileName = $"History_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "Excel Workbook|*.xlsx";
                    saveDialog.FileName = fileName;

                    if (saveDialog.ShowDialog() == DialogResult.OK)
                    {
                        workbook.SaveAs(saveDialog.FileName);
                        MessageBox.Show("✅ Exported to: " + saveDialog.FileName);
                    }
                }
            }
        }

        private void UpdateHistoryCounts()
        {
            int timeIn = 0;
            int timeOut = 0;
            int total = 0;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = @"
            SELECT
                SUM(CASE WHEN AmIN IS NOT NULL OR PmIn IS NOT NULL THEN 1 ELSE 0 END) AS TimeInTotal,
                SUM(CASE WHEN AmOut IS NOT NULL OR PmOut IS NOT NULL THEN 1 ELSE 0 END) AS TimeOutTotal,
                COUNT(*) AS TotalLogs
            FROM History";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        timeIn = reader["TimeInTotal"] != DBNull.Value ? Convert.ToInt32(reader["TimeInTotal"]) : 0;
                        timeOut = reader["TimeOutTotal"] != DBNull.Value ? Convert.ToInt32(reader["TimeOutTotal"]) : 0;
                        total = reader["TotalLogs"] != DBNull.Value ? Convert.ToInt32(reader["TotalLogs"]) : 0;
                    }
                }
            }

            lblTimeIn.Text = timeIn.ToString();
            lblTimeOut.Text = timeOut.ToString();
            lblTotalLog.Text = total.ToString();
        }



        private void dgvHistory_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void chkTodayOnly_CheckedChanged(object sender, EventArgs e)
        {
            LoadSortedHistory();
        }

        private void dtpFrom_ValueChanged(object sender, EventArgs e)
        {

        }

        private void dtpTo_ValueChanged(object sender, EventArgs e)
        {

        }
    }
}
