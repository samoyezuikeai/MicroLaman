
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
            this.comboBoxCom = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.RefreshMyComList = new System.Windows.Forms.Button();
            this.ConnectCom = new System.Windows.Forms.Button();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.CameraShow = new System.Windows.Forms.ToolStripButton();
            this.panel1 = new System.Windows.Forms.Panel();
            this.formsPlot1 = new ScottPlot.WinForms.FormsPlot();
            this.toolStrip1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // comboBoxCom
            // 
            this.comboBoxCom.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.comboBoxCom.FormattingEnabled = true;
            this.comboBoxCom.Location = new System.Drawing.Point(52, 13);
            this.comboBoxCom.Name = "comboBoxCom";
            this.comboBoxCom.Size = new System.Drawing.Size(178, 27);
            this.comboBoxCom.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.label1.Location = new System.Drawing.Point(7, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(51, 20);
            this.label1.TabIndex = 1;
            this.label1.Text = "串口：";
            // 
            // RefreshMyComList
            // 
            this.RefreshMyComList.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.RefreshMyComList.Location = new System.Drawing.Point(52, 47);
            this.RefreshMyComList.Name = "RefreshMyComList";
            this.RefreshMyComList.Size = new System.Drawing.Size(53, 29);
            this.RefreshMyComList.TabIndex = 2;
            this.RefreshMyComList.Text = "刷新";
            this.RefreshMyComList.UseVisualStyleBackColor = true;
            this.RefreshMyComList.Click += new System.EventHandler(this.RefreshMyComList_Click);
            // 
            // ConnectCom
            // 
            this.ConnectCom.Font = new System.Drawing.Font("Microsoft YaHei UI", 10F);
            this.ConnectCom.Location = new System.Drawing.Point(122, 47);
            this.ConnectCom.Name = "ConnectCom";
            this.ConnectCom.Size = new System.Drawing.Size(53, 29);
            this.ConnectCom.TabIndex = 3;
            this.ConnectCom.Text = "连接";
            this.ConnectCom.UseVisualStyleBackColor = true;
            this.ConnectCom.Click += new System.EventHandler(this.ConnectCom_Click);
            // 
            // toolStrip1
            // 
            this.toolStrip1.BackColor = System.Drawing.Color.White;
            this.toolStrip1.ImageScalingSize = new System.Drawing.Size(25, 25);
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.CameraShow});
            this.toolStrip1.Location = new System.Drawing.Point(0, 0);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Padding = new System.Windows.Forms.Padding(0);
            this.toolStrip1.Size = new System.Drawing.Size(1114, 32);
            this.toolStrip1.TabIndex = 4;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // CameraShow
            // 
            this.CameraShow.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.CameraShow.Image = ((System.Drawing.Image)(resources.GetObject("CameraShow.Image")));
            this.CameraShow.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.CameraShow.Name = "CameraShow";
            this.CameraShow.Size = new System.Drawing.Size(29, 29);
            this.CameraShow.Click += new System.EventHandler(this.CameraShow_Click);
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.Controls.Add(this.comboBoxCom);
            this.panel1.Controls.Add(this.label1);
            this.panel1.Controls.Add(this.ConnectCom);
            this.panel1.Controls.Add(this.RefreshMyComList);
            this.panel1.Location = new System.Drawing.Point(0, 35);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(235, 640);
            this.panel1.TabIndex = 5;
            // 
            // formsPlot1
            // 
            this.formsPlot1.Location = new System.Drawing.Point(236, 136);
            this.formsPlot1.Name = "formsPlot1";
            this.formsPlot1.Size = new System.Drawing.Size(507, 539);
            this.formsPlot1.TabIndex = 6;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1114, 675);
            this.Controls.Add(this.formsPlot1);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.toolStrip1);
            this.Name = "MainForm";
            this.Text = "MicroLaman";
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ComboBox comboBoxCom;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button RefreshMyComList;
        private System.Windows.Forms.Button ConnectCom;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton CameraShow;
        private System.Windows.Forms.Panel panel1;
        private ScottPlot.WinForms.FormsPlot formsPlot1;
    }
}

