namespace MicroLaman
{
    partial class CameraShowForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.previewBox = new System.Windows.Forms.PictureBox();
            this.titleLabel = new System.Windows.Forms.Label();
            this.statusLabel = new System.Windows.Forms.Label();
            this.captureButton = new System.Windows.Forms.Button();
            this.closeButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.previewBox)).BeginInit();
            this.SuspendLayout();
            // 
            // previewBox
            // 
            this.previewBox.BackColor = System.Drawing.Color.FromArgb(32, 32, 32);
            this.previewBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.previewBox.Location = new System.Drawing.Point(18, 48);
            this.previewBox.Name = "previewBox";
            this.previewBox.Size = new System.Drawing.Size(604, 340);
            this.previewBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.previewBox.TabIndex = 0;
            this.previewBox.TabStop = false;
            // 
            // titleLabel
            // 
            this.titleLabel.AutoSize = true;
            this.titleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold);
            this.titleLabel.Location = new System.Drawing.Point(14, 14);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(90, 22);
            this.titleLabel.TabIndex = 1;
            this.titleLabel.Text = "相机预览";
            // 
            // statusLabel
            // 
            this.statusLabel.AutoEllipsis = true;
            this.statusLabel.Location = new System.Drawing.Point(18, 405);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(360, 23);
            this.statusLabel.TabIndex = 2;
            this.statusLabel.Text = "相机尚未连接";
            this.statusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // captureButton
            // 
            this.captureButton.Location = new System.Drawing.Point(452, 400);
            this.captureButton.Name = "captureButton";
            this.captureButton.Size = new System.Drawing.Size(82, 29);
            this.captureButton.TabIndex = 3;
            this.captureButton.Text = "拍照";
            this.captureButton.UseVisualStyleBackColor = true;
            this.captureButton.Click += new System.EventHandler(this.CaptureButton_Click);
            // 
            // closeButton
            // 
            this.closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.closeButton.Location = new System.Drawing.Point(540, 400);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(82, 29);
            this.closeButton.TabIndex = 4;
            this.closeButton.Text = "关闭";
            this.closeButton.UseVisualStyleBackColor = true;
            this.closeButton.Click += new System.EventHandler(this.CloseButton_Click);
            // 
            // CameraShowForm
            // 
            this.AcceptButton = this.captureButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeButton;
            this.ClientSize = new System.Drawing.Size(640, 445);
            this.Controls.Add(this.closeButton);
            this.Controls.Add(this.captureButton);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.titleLabel);
            this.Controls.Add(this.previewBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CameraShowForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "相机预览";
            ((System.ComponentModel.ISupportInitialize)(this.previewBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.PictureBox previewBox;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Button captureButton;
        private System.Windows.Forms.Button closeButton;
    }
}
