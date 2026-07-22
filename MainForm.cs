using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;

namespace MicroLaman
{
    public partial class MainForm : Form
    {
        private CameraShowForm cameraShowForm;

        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 刷新更新串口，方便热插拔
        /// </summary>
        private void RefreshComList()
        {
            comboBoxCom.Items.Clear();

            using (ManagementObjectSearcher searcher =
                new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    string name = obj["Name"].ToString();
                    if (name[0] == '蓝' && name[1] == '牙') continue;
                    comboBoxCom.Items.Add(name);
                }
            }
        }

        private void RefreshMyComList_Click(object sender, EventArgs e)
        {
            RefreshComList();
        }

        /// <summary>
        /// 连接串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectCom_Click(object sender, EventArgs e)
        {
            try
            {
                if (comboBoxCom.SelectedItem == null) return;
                string port = comboBoxCom.SelectedItem.ToString();

                int start = port.LastIndexOf("(COM");
                int end = port.LastIndexOf(")");
                string com = port.Substring(start + 1, end - start - 1);
                SerialPortManager.Open(com);
            }
            catch { }
        }

        private void CameraShow_Click(object sender, EventArgs e)
        {
            if (cameraShowForm == null || cameraShowForm.IsDisposed)
            {
                cameraShowForm = new CameraShowForm();
                cameraShowForm.FormClosed += CameraShowForm_FormClosed;
                cameraShowForm.Show(this);
                return;
            }

            if (cameraShowForm.WindowState == FormWindowState.Minimized)
            {
                cameraShowForm.WindowState = FormWindowState.Normal;
            }

            cameraShowForm.Activate();
        }

        private void CameraShowForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            cameraShowForm = null;
        }
    }
}
