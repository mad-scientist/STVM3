using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace STVM.Dialogs
{
    public partial class fmSplash : Form
    {

        private List<string> lines;

        public void AddLine(string Text)
        {
            lines.Add(Text);
            lbLoading.Text = String.Join("\r\n", lines);
        }

        public fmSplash()
        {
            InitializeComponent();
            this.Font = SystemFonts.MessageBoxFont;
            lines = new List<string>();
        }
    }
}
