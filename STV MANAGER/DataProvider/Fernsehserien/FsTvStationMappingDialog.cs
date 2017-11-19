using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace STVM.DataProvider.Fernsehserien
{
    public partial class FsTvStationDialog : Form
    {
        FsTvStationMapping Mapping;

        public FsTvStationDialog()
        {
            InitializeComponent();
            this.Font = SystemFonts.MessageBoxFont;
        }

        private void WunschlisteMapping_Load(object sender, EventArgs e)
        {
            Mapping = FsTvStationMapping.ReadFromFile();
            olvWlMapping.SetObjects(Mapping);
        }

        private void WunschlisteMapping_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                Mapping.SaveToFile();
            }
        }
    }

}
