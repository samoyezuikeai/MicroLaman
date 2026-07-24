
namespace MicroLaman
{
    partial class MainForm
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.comboBoxController = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.RefreshMyComList = new System.Windows.Forms.Button();
            this.ConnectCom = new System.Windows.Forms.Button();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.CameraShow = new System.Windows.Forms.ToolStripButton();
            this.CalibrateStage = new System.Windows.Forms.ToolStripButton();
            this.ScanSelection = new System.Windows.Forms.ToolStripButton();
            this.panel1 = new System.Windows.Forms.Panel();
            this.LaserSettings = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.formsPlot1 = new ScottPlot.WinForms.FormsPlot();
            this.toolStrip1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // comboBoxController
            // 
            this.comboBoxController.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.comboBoxController.FormattingEnabled = true;
            this.comboBoxController.Location = new System.Drawing.Point(176, 31);
            this.comboBoxController.Margin = new System.Windows.Forms.Padding(6);
            this.comboBoxController.Name = "comboBoxController";
            this.comboBoxController.Size = new System.Drawing.Size(146, 43);
            this.comboBoxController.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label1.Location = new System.Drawing.Point(14, 34);
            this.label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(177, 35);
            this.label1.TabIndex = 1;
            this.label1.Text = "控制台串口：";
            // 
            // RefreshMyComList
            // 
            this.RefreshMyComList.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.RefreshMyComList.Location = new System.Drawing.Point(20, 143);
            this.RefreshMyComList.Margin = new System.Windows.Forms.Padding(6);
            this.RefreshMyComList.Name = "RefreshMyComList";
            this.RefreshMyComList.Size = new System.Drawing.Size(106, 58);
            this.RefreshMyComList.TabIndex = 2;
            this.RefreshMyComList.Text = "刷新";
            this.RefreshMyComList.UseVisualStyleBackColor = true;
            this.RefreshMyComList.Click += new System.EventHandler(this.RefreshMyComList_Click);
            // 
            // ConnectCom
            // 
            this.ConnectCom.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.ConnectCom.Location = new System.Drawing.Point(138, 143);
            this.ConnectCom.Margin = new System.Windows.Forms.Padding(6);
            this.ConnectCom.Name = "ConnectCom";
            this.ConnectCom.Size = new System.Drawing.Size(106, 58);
            this.ConnectCom.TabIndex = 3;
            this.ConnectCom.Text = "连接";
            this.ConnectCom.UseVisualStyleBackColor = true;
            this.ConnectCom.Click += new System.EventHandler(this.ConnectCom_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.AutoSize = false;
            this.toolStrip1.BackColor = System.Drawing.Color.White;
            this.toolStrip1.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(44, 44);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.CameraShow,
            this.CalibrateStage,
            this.ScanSelection});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(4);
            this.toolStrip1.Size = new System.Drawing.Size(2228, 80);
            this.toolStrip1.TabIndex = 4;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // CameraShow
            // 
            this.CameraShow.AutoSize = false;
            this.CameraShow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.CameraShow.Image = ((System.Drawing.Image)(resources.GetObject("CameraShow.Image")));
            this.CameraShow.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.CameraShow.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.CameraShow.Name = "CameraShow";
            this.CameraShow.Size = new System.Drawing.Size(56, 56);
            this.CameraShow.Click += new System.EventHandler(this.CameraShow_Click);
            // 
            // CalibrateStage
            // 
            this.CalibrateStage.AutoSize = false;
            this.CalibrateStage.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.CalibrateStage.Image = ((System.Drawing.Image)(resources.GetObject("CalibrateStage.Image")));
            this.CalibrateStage.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.CalibrateStage.Name = "CalibrateStage";
            this.CalibrateStage.Size = new System.Drawing.Size(56, 56);
            this.CalibrateStage.ToolTipText = "在关闭激光、打开明场照明后计算像素与平台坐标比例";
            this.CalibrateStage.Click += new System.EventHandler(this.CalibrateStage_Click);
            // 
            // ScanSelection
            // 
            this.ScanSelection.AutoSize = false;
            this.ScanSelection.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.ScanSelection.Image = ((System.Drawing.Image)(resources.GetObject("ScanSelection.Image")));
            this.ScanSelection.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ScanSelection.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.ScanSelection.Name = "ScanSelection";
            this.ScanSelection.Size = new System.Drawing.Size(56, 56);
            this.ScanSelection.ToolTipText = "按蛇形顺序遍历框选区域内的全部网格点";
            this.ScanSelection.Click += new System.EventHandler(this.ScanSelection_Click);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.Controls.Add(this.LaserSettings);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.comboBoxController);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.ConnectCom);
            this.panel1.Controls.Add(this.RefreshMyComList);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel1.Location = new System.Drawing.Point(0, 80);
            this.panel1.Margin = new System.Windows.Forms.Padding(6);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(348, 1270);
            this.panel1.TabIndex = 5;
            // 
            // LaserSettings
            // 
            this.LaserSettings.Enabled = false;
            this.LaserSettings.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.LaserSettings.Location = new System.Drawing.Point(20, 213);
            this.LaserSettings.Margin = new System.Windows.Forms.Padding(6);
            this.LaserSettings.Name = "LaserSettings";
            this.LaserSettings.Size = new System.Drawing.Size(314, 58);
            this.LaserSettings.TabIndex = 6;
            this.LaserSettings.Text = "激光器设置";
            this.LaserSettings.UseVisualStyleBackColor = true;
            this.LaserSettings.Click += new System.EventHandler(this.LaserSettings_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label2.Location = new System.Drawing.Point(14, 91);
            this.label2.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(204, 35);
            this.label2.TabIndex = 5;
            this.label2.Text = "激光器：未连接";
            // 
            // formsPlot1
            // 
            this.formsPlot1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.formsPlot1.Location = new System.Drawing.Point(348, 80);
            this.formsPlot1.Margin = new System.Windows.Forms.Padding(6);
            this.formsPlot1.Name = "formsPlot1";
            this.formsPlot1.Size = new System.Drawing.Size(1880, 1270);
            this.formsPlot1.TabIndex = 6;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(2228, 1350);
            this.Controls.Add(this.formsPlot1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.toolStrip1);
            this.Margin = new System.Windows.Forms.Padding(6);
            this.Name = "MainForm";
            this.Text = "MicroLaman";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ComboBox comboBoxController;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button RefreshMyComList;
        private System.Windows.Forms.Button ConnectCom;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton CameraShow;
        private System.Windows.Forms.ToolStripButton CalibrateStage;
        private System.Windows.Forms.ToolStripButton ScanSelection;
        private System.Windows.Forms.Panel panel1;
        private ScottPlot.WinForms.FormsPlot formsPlot1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button LaserSettings;
    }
}

