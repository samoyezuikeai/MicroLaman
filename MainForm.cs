using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Management;

namespace MicroLaman
{
    public partial class MainForm : Form
    {
        private CameraShowForm cameraShowForm;
        private CancellationTokenSource scanCancellation;
        private readonly StageScanController stageScanController = new StageScanController();

        public MainForm()
        {
            InitializeComponent();
            RefreshComList();
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
                    int start = name.LastIndexOf("(COM");
                    int end = name.LastIndexOf(")");
                    string com = name.Substring(start + 1, end - start - 1);
                    comboBoxCom.Items.Add(com);
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
                string com = comboBoxCom.SelectedItem.ToString();
                SerialPortManager.Open(com);
                stageScanController.ResetOrigin();
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

        private async void ScanSelection_Click(object sender, EventArgs e)
        {
            if (scanCancellation != null)
            {
                ScanSelection.Text = "停止中…";
                scanCancellation.Cancel();
                return;
            }

            if (cameraShowForm == null || cameraShowForm.IsDisposed)
            {
                MessageBox.Show(this, "请先打开相机窗口并框选扫描区域。", "蛇形扫描",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!SerialPortManager.IsOpen)
            {
                MessageBox.Show(this, "请先连接 TANGO 串口。", "蛇形扫描",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<PointF> scanPoints;
            string errorMessage;
            if (!cameraShowForm.TryGetSnakeScanPoints(out scanPoints, out errorMessage))
            {
                MessageBox.Show(this, errorMessage, "蛇形扫描",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            scanCancellation = new CancellationTokenSource();
            CancellationToken token = scanCancellation.Token;
            ScanSelection.Text = "标定中…";
            IProgress<string> progress = new Progress<string>(text => ScanSelection.Text = text);
            try
            {
                await Task.Run(() => stageScanController.CalibrateAndScan(cameraShowForm, scanPoints, progress, token), token);
                MessageBox.Show(this,
                    string.Format("已完成 {0} 个网格点的蛇形遍历。", scanPoints.Count),
                    "蛇形扫描",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show(this, "扫描已停止。", "蛇形扫描",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "蛇形扫描失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                scanCancellation.Dispose();
                scanCancellation = null;
                ScanSelection.Text = "蛇形扫描";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (scanCancellation != null)
                scanCancellation.Cancel();
            base.OnFormClosing(e);
        }
    }
}
