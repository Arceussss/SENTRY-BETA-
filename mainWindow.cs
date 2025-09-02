using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SENTRY_BETA_
{
    public partial class mainWindow : Form
    {
        private int childFormNumber = 0;
        private Guna2Button activeButton = null;
        private string currentUser;
        private string currentFullName;
        private Image currentProfileImage;
        Log_In Log_In;

        public mainWindow(string username, string fullName, Image profileImg)
        {
            InitializeComponent();

            pnlAccount.Visible = false;  

            currentUser = username;
            currentFullName = fullName;
            currentProfileImage = profileImg;

            lblAdminName.Text = currentUser;
            lblFullName.Text = currentFullName;

            if (currentProfileImage != null)
                btnAccount.Image = currentProfileImage; btnAdminPic.Image = currentProfileImage;

            this.Shown += (s, e) => ApplyRoundedCorners();
        }

        private void ShowNewForm(object sender, EventArgs e)
        {
            Form childForm = new Form();
            childForm.MdiParent = this;
            childForm.Text = "Window " + childFormNumber++;
            childForm.Show();
        }

        private void OpenFile(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            openFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                string FileName = openFileDialog.FileName;
            }
        }

        private void SaveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                string FileName = saveFileDialog.FileName;
            }
        }

        private void ExitToolsStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void ToolBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void StatusBarToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void CascadeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.Cascade);
        }

        private void TileVerticalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.TileVertical);
        }

        private void TileHorizontalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.TileHorizontal);
        }

        private void ArrangeIconsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LayoutMdi(MdiLayout.ArrangeIcons);
        }

        private void CloseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (Form childForm in MdiChildren)
            {
                childForm.Close();
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void mainWindow_Load(object sender, EventArgs e)
        {
            ApplyRoundedCorners();
            OpenMdiChild(typeof(Dashboard));
            HighlightActiveButton(btnDashboard);
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
            this.Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 20, 20));
        }

        private void btnAccount_Click(object sender, EventArgs e)
        {
        }

        private void btnAdminPic_Click(object sender, EventArgs e)
        {
            pnlAccount.Visible = false;
        }

        private void btnAccount_Click_1(object sender, EventArgs e)
        {
            pnlAccount.Visible = true;

        }

        private void btnAccount_Click_2(object sender, EventArgs e)
        {
            pnlAccount.Visible = true;

        }

        private void OpenMdiChild(Type formType)
        {
            foreach (Form child in MdiChildren)
            {
                if (child.GetType() == formType)
                {
                    child.BringToFront();
                    return;
                }
                child.Close();
            }

            Form formInstance = (Form)Activator.CreateInstance(formType);
            formInstance.MdiParent = this;
            formInstance.Dock = DockStyle.Fill;
            formInstance.Show();
        }


        private void HighlightActiveButton(Guna2Button clickedButton)
        {
            Color hoverColor = ColorTranslator.FromHtml("#cdd3fd");
            Color clickedColor = Color.FromArgb(128, hoverColor.R, hoverColor.G, hoverColor.B);

            if (activeButton != null && activeButton != clickedButton)
            {
                activeButton.FillColor = Color.Transparent;
                activeButton.ForeColor = Color.White;
            }

            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is Guna2Button btn)
                {
                    btn.HoverState.FillColor = hoverColor;
                    btn.PressedColor = hoverColor;
                    btn.ForeColor = Color.White;
                }
            }

            if (clickedButton != null)
            {
                clickedButton.FillColor = clickedColor;
                clickedButton.ForeColor = Color.White;
                activeButton = clickedButton;
            }
        }

        private void btnDashboard_Click(object sender, EventArgs e)
        {
            OpenMdiChild(typeof(Dashboard));
            HighlightActiveButton(sender as Guna2Button);        
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            OpenMdiChild(typeof(About_us));
            HighlightActiveButton(sender as Guna2Button);
        }

        private void btnSignOut_Click(object sender, EventArgs e)
        {
            HighlightActiveButton(sender as Guna2Button);
            Log_In log_In = new Log_In();
            log_In.Show();
            this.Hide();
        }

        private void btnAdmin_Click(object sender, EventArgs e)
        {
            OpenMdiChild(typeof (Admin));
            HighlightActiveButton(sender as Guna2Button);
        }

        private void guna2Button1_Click(object sender, EventArgs e)
        {
            OpenMdiChild(typeof(History));
            HighlightActiveButton(sender as Guna2Button);
        }
    }
}
