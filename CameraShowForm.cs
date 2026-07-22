using System;
using System.Windows.Forms;

namespace MicroLaman
{
    public partial class CameraShowForm : Form
    {
        public CameraShowForm()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void CaptureButton_Click(object sender, EventArgs e)
        {
            statusLabel.Text = "已请求拍照（请在此处接入相机采集逻辑）";
        }
    }
}
