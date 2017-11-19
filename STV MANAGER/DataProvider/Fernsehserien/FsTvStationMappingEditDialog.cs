using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using STVM.Stv.Data;

namespace STVM.DataProvider.Fernsehserien
{
    public partial class FsTvStationMappingEditDialog : Form
    {
        public string StvTvStation
        {
            get { return cbStvTvStation.Text; }
        }

        public FsTvStationMappingEditDialog(tTVStationList TvStations, string FsTvStation)
        {
            InitializeComponent();
            this.Font = SystemFonts.MessageBoxFont;
            foreach(tTVStation tvStation in TvStations)
            {
                cbStvTvStation.Items.Add(tvStation.Name);
            }
            cbStvTvStation.SelectedItem = TvStations.First().Name;

            boxFsTvStation.Text = FsTvStation;
        }
    }
}
