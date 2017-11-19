namespace STVM.DataProvider.Fernsehserien
{
    partial class FsFindSeriesDialog
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
            this.tbSearchShowTitle = new System.Windows.Forms.TextBox();
            this.btSearchTitle = new System.Windows.Forms.Button();
            this.lbSeriesFound = new System.Windows.Forms.Label();
            this.btLoad = new System.Windows.Forms.Button();
            this.btCancel = new System.Windows.Forms.Button();
            this.lvFoundSeries = new BrightIdeasSoftware.ObjectListView();
            this.olvColumn1 = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.olvColumn2 = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            this.olvColumn3 = ((BrightIdeasSoftware.OLVColumn)(new BrightIdeasSoftware.OLVColumn()));
            ((System.ComponentModel.ISupportInitialize)(this.lvFoundSeries)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(114, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Suche nach Serientitel";
            // 
            // tbSearchShowTitle
            // 
            this.tbSearchShowTitle.Location = new System.Drawing.Point(132, 14);
            this.tbSearchShowTitle.Name = "tbSearchShowTitle";
            this.tbSearchShowTitle.Size = new System.Drawing.Size(182, 20);
            this.tbSearchShowTitle.TabIndex = 1;
            this.tbSearchShowTitle.KeyDown += new System.Windows.Forms.KeyEventHandler(this.tbSearchShowTitle_KeyDown);
            // 
            // btSearchTitle
            // 
            this.btSearchTitle.Location = new System.Drawing.Point(320, 12);
            this.btSearchTitle.Name = "btSearchTitle";
            this.btSearchTitle.Size = new System.Drawing.Size(75, 23);
            this.btSearchTitle.TabIndex = 2;
            this.btSearchTitle.Text = "Suchen";
            this.btSearchTitle.UseVisualStyleBackColor = true;
            this.btSearchTitle.Click += new System.EventHandler(this.btSearchTitle_Click);
            // 
            // lbSeriesFound
            // 
            this.lbSeriesFound.AutoSize = true;
            this.lbSeriesFound.Location = new System.Drawing.Point(9, 200);
            this.lbSeriesFound.Name = "lbSeriesFound";
            this.lbSeriesFound.Size = new System.Drawing.Size(196, 13);
            this.lbSeriesFound.TabIndex = 35;
            this.lbSeriesFound.Text = "0 Serien gefunden auf Fernsehserien.de";
            // 
            // btLoad
            // 
            this.btLoad.Location = new System.Drawing.Point(239, 226);
            this.btLoad.Name = "btLoad";
            this.btLoad.Size = new System.Drawing.Size(75, 23);
            this.btLoad.TabIndex = 36;
            this.btLoad.Text = "Auswählen";
            this.btLoad.UseVisualStyleBackColor = true;
            this.btLoad.Click += new System.EventHandler(this.btLoad_Click);
            // 
            // btCancel
            // 
            this.btCancel.Location = new System.Drawing.Point(320, 226);
            this.btCancel.Name = "btCancel";
            this.btCancel.Size = new System.Drawing.Size(75, 23);
            this.btCancel.TabIndex = 37;
            this.btCancel.Text = "Abbrechen";
            this.btCancel.UseVisualStyleBackColor = true;
            this.btCancel.Click += new System.EventHandler(this.btCancel_Click);
            // 
            // lvFoundSeries
            // 
            this.lvFoundSeries.AllColumns.Add(this.olvColumn1);
            this.lvFoundSeries.AllColumns.Add(this.olvColumn2);
            this.lvFoundSeries.AllColumns.Add(this.olvColumn3);
            this.lvFoundSeries.CellEditUseWholeCell = false;
            this.lvFoundSeries.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.olvColumn1,
            this.olvColumn2,
            this.olvColumn3});
            this.lvFoundSeries.Cursor = System.Windows.Forms.Cursors.Default;
            this.lvFoundSeries.FullRowSelect = true;
            this.lvFoundSeries.HideSelection = false;
            this.lvFoundSeries.HighlightBackgroundColor = System.Drawing.Color.Empty;
            this.lvFoundSeries.HighlightForegroundColor = System.Drawing.Color.Empty;
            this.lvFoundSeries.Location = new System.Drawing.Point(12, 41);
            this.lvFoundSeries.MultiSelect = false;
            this.lvFoundSeries.Name = "lvFoundSeries";
            this.lvFoundSeries.ShowGroups = false;
            this.lvFoundSeries.Size = new System.Drawing.Size(382, 155);
            this.lvFoundSeries.TabIndex = 38;
            this.lvFoundSeries.UseCompatibleStateImageBehavior = false;
            this.lvFoundSeries.View = System.Windows.Forms.View.Details;
            this.lvFoundSeries.SelectionChanged += new System.EventHandler(this.lvFoundSeries_SelectionChanged);
            // 
            // olvColumn1
            // 
            this.olvColumn1.AspectName = "Title";
            this.olvColumn1.Text = "Titel";
            this.olvColumn1.Width = 260;
            // 
            // olvColumn2
            // 
            this.olvColumn2.AspectName = "Country";
            this.olvColumn2.Text = "Land";
            // 
            // olvColumn3
            // 
            this.olvColumn3.AspectName = "Years";
            this.olvColumn3.Text = "Jahr";
            // 
            // FsFindSeriesDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(408, 261);
            this.Controls.Add(this.lvFoundSeries);
            this.Controls.Add(this.btCancel);
            this.Controls.Add(this.btLoad);
            this.Controls.Add(this.lbSeriesFound);
            this.Controls.Add(this.btSearchTitle);
            this.Controls.Add(this.tbSearchShowTitle);
            this.Controls.Add(this.label1);
            this.Name = "FsFindSeriesDialog";
            this.Text = "Import von Fernsehserien.de";
            this.Load += new System.EventHandler(this.FsFindSeriesDialog_Load);
            ((System.ComponentModel.ISupportInitialize)(this.lvFoundSeries)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbSearchShowTitle;
        private System.Windows.Forms.Button btSearchTitle;
        private System.Windows.Forms.Label lbSeriesFound;
        private System.Windows.Forms.Button btLoad;
        private System.Windows.Forms.Button btCancel;
        private BrightIdeasSoftware.ObjectListView lvFoundSeries;
        private BrightIdeasSoftware.OLVColumn olvColumn1;
        private BrightIdeasSoftware.OLVColumn olvColumn2;
        private BrightIdeasSoftware.OLVColumn olvColumn3;
    }
}