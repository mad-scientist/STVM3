namespace STVM.DataProvider.Fernsehserien
{
    partial class FsTvStationMappingEditDialog
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.boxFsTvStation = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cbStvTvStation = new System.Windows.Forms.ComboBox();
            this.btOK = new System.Windows.Forms.Button();
            this.btCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 31);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(151, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Sendername Fernsehserien.de";
            // 
            // boxFsTvStation
            // 
            this.boxFsTvStation.Location = new System.Drawing.Point(169, 28);
            this.boxFsTvStation.Name = "boxFsTvStation";
            this.boxFsTvStation.ReadOnly = true;
            this.boxFsTvStation.Size = new System.Drawing.Size(141, 20);
            this.boxFsTvStation.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 62);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(112, 13);
            this.label2.TabIndex = 2;
            this.label2.Text = "Sendername Save.TV";
            // 
            // cbStvTvStation
            // 
            this.cbStvTvStation.FormattingEnabled = true;
            this.cbStvTvStation.Location = new System.Drawing.Point(169, 59);
            this.cbStvTvStation.Name = "cbStvTvStation";
            this.cbStvTvStation.Size = new System.Drawing.Size(121, 21);
            this.cbStvTvStation.TabIndex = 3;
            // 
            // btOK
            // 
            this.btOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btOK.Location = new System.Drawing.Point(235, 103);
            this.btOK.Name = "btOK";
            this.btOK.Size = new System.Drawing.Size(75, 23);
            this.btOK.TabIndex = 4;
            this.btOK.Text = "OK";
            this.btOK.UseVisualStyleBackColor = true;
            // 
            // btCancel
            // 
            this.btCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btCancel.Location = new System.Drawing.Point(151, 103);
            this.btCancel.Name = "btCancel";
            this.btCancel.Size = new System.Drawing.Size(75, 23);
            this.btCancel.TabIndex = 5;
            this.btCancel.Text = "Abbrechen";
            this.btCancel.UseVisualStyleBackColor = true;
            // 
            // fmWunschlisteEditTvStation
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(325, 140);
            this.Controls.Add(this.btCancel);
            this.Controls.Add(this.btOK);
            this.Controls.Add(this.cbStvTvStation);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.boxFsTvStation);
            this.Controls.Add(this.label1);
            this.Name = "fmWunschlisteEditTvStation";
            this.Text = "TV-Sender Zuordnung bearbeiten";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox boxFsTvStation;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cbStvTvStation;
        private System.Windows.Forms.Button btOK;
        private System.Windows.Forms.Button btCancel;
    }
}