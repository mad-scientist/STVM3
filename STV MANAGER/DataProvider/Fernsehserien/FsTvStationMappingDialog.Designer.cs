namespace STVM.DataProvider.Fernsehserien
{
    partial class FsTvStationDialog
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
            this.olvWlMapping = new BrightIdeasSoftware.ObjectListView();
            this.olvColumnWlName = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.olvColumnStvName = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.btOK = new System.Windows.Forms.Button();
            this.btCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.olvWlMapping)).BeginInit();
            this.SuspendLayout();
            // 
            // olvWlMapping
            // 
            this.olvWlMapping.AllColumns.Add(this.olvColumnWlName);
            this.olvWlMapping.AllColumns.Add(this.olvColumnStvName);
            this.olvWlMapping.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.olvWlMapping.CellEditUseWholeCell = false;
            this.olvWlMapping.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.olvColumnWlName,
            this.olvColumnStvName});
            this.olvWlMapping.Cursor = System.Windows.Forms.Cursors.Default;
            this.olvWlMapping.GridLines = true;
            this.olvWlMapping.HighlightBackgroundColor = System.Drawing.Color.Empty;
            this.olvWlMapping.HighlightForegroundColor = System.Drawing.Color.Empty;
            this.olvWlMapping.Location = new System.Drawing.Point(12, 12);
            this.olvWlMapping.Name = "olvWlMapping";
            this.olvWlMapping.ShowGroups = false;
            this.olvWlMapping.Size = new System.Drawing.Size(391, 227);
            this.olvWlMapping.TabIndex = 0;
            this.olvWlMapping.UseCompatibleStateImageBehavior = false;
            this.olvWlMapping.View = System.Windows.Forms.View.Details;
            // 
            // olvColumnWlName
            // 
            this.olvColumnWlName.AspectName = "Key";
            this.olvColumnWlName.Text = "Sendername Wunschliste.de";
            this.olvColumnWlName.Width = 180;
            // 
            // olvColumnStvName
            // 
            this.olvColumnStvName.AspectName = "Value";
            this.olvColumnStvName.IsEditable = false;
            this.olvColumnStvName.Text = "Sendername Save.TV";
            this.olvColumnStvName.Width = 180;
            // 
            // btOK
            // 
            this.btOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btOK.Location = new System.Drawing.Point(328, 249);
            this.btOK.Name = "btOK";
            this.btOK.Size = new System.Drawing.Size(75, 23);
            this.btOK.TabIndex = 1;
            this.btOK.Text = "OK";
            this.btOK.UseVisualStyleBackColor = true;
            // 
            // btCancel
            // 
            this.btCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btCancel.Location = new System.Drawing.Point(247, 249);
            this.btCancel.Name = "btCancel";
            this.btCancel.Size = new System.Drawing.Size(75, 23);
            this.btCancel.TabIndex = 2;
            this.btCancel.Text = "Abbrechen";
            this.btCancel.UseVisualStyleBackColor = true;
            // 
            // WunschlisteMapping
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(415, 284);
            this.Controls.Add(this.btCancel);
            this.Controls.Add(this.btOK);
            this.Controls.Add(this.olvWlMapping);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "WunschlisteMapping";
            this.Text = "WunschlisteMapping";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.WunschlisteMapping_FormClosed);
            this.Load += new System.EventHandler(this.WunschlisteMapping_Load);
            ((System.ComponentModel.ISupportInitialize)(this.olvWlMapping)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private BrightIdeasSoftware.ObjectListView olvWlMapping;
        private BrightIdeasSoftware.OLVColumn olvColumnStvName;
        private BrightIdeasSoftware.OLVColumn olvColumnWlName;
        private System.Windows.Forms.Button btOK;
        private System.Windows.Forms.Button btCancel;
    }
}