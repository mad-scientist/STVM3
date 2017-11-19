using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HtmlAgilityPack;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using STVM.Data;
using STVM.Stv.Data;
using System.Web.Script.Serialization;
using STVM.Wrapper.Http;
using STVM.DataProvider.Fernsehserien;

namespace STVM.DataProvider.Fernsehserien
{
    public partial class FsFindSeriesDialog: Form
    {
        const string fsSearchURL = "https://www.fernsehserien.de/fastsearch";
        const string fsServerURL = "https://www.fernsehserien.de/";

        public FsFindSeriesDialog()
        {
            InitializeComponent();
            this.Font = SystemFonts.MessageBoxFont;
        }

        public FsFindSeriesDialog(List<FsSeries> foundShows) : this()
        {
            FoundShows = foundShows;
        }

        public string SearchString
        {
            get { return tbSearchShowTitle.Text; }
            set { tbSearchShowTitle.Text = value; }
        }

        private List<FsSeries> FoundShows;

        public FsSeries Result;

        private void wlResultUpdate(List<FsSeries> Shows)
        {
            lvFoundSeries.SetObjects(FoundShows);
            lvFoundSeries.SelectedIndex = 0;
            lbSeriesFound.Text = Shows.Count.ToString() + " Serien gefunden auf Fernsehserien.de";
        }

        private string GetValue(HtmlNode item, string tag)
        {
            IEnumerable<HtmlNode> classNodes = item.Descendants("span").Where(node => node.Attributes["class"] != null);
            HtmlNode tagNode = classNodes.FirstOrDefault(node => node.Attributes["class"].Value == tag);
            if (tagNode != null)
            {
                return tagNode.InnerText;
            }
            else
            {
                return String.Empty;
            }
        }

        private string GetTitle(HtmlNode item)
        {
            try
            {
                HtmlNode titleItem = item.Descendants("span").First(node => node.Attributes["class"].Value == "l4");
                HtmlNode removeChild = titleItem.Descendants("span").FirstOrDefault(node => node.Attributes["class"] != null);
                if (removeChild != null) removeChild.Remove();
                return titleItem.InnerText.Trim();
            }
            catch
            {
                return "";
            }
        }

        private async Task SearchTitle(string Text)
        {
            if (Text != "")
            {
                FsClient client = new FsClient();
                FoundShows = await client.GetSeriesAsync(Text);

                switch (FoundShows.Count)
                {
                    case 0:
                        break;

                    case 1:
                        Result = FoundShows.First();
                        this.DialogResult = System.Windows.Forms.DialogResult.OK;
                        this.Close();
                        break;

                    default:
                        wlResultUpdate(FoundShows);
                        break;
                }
            }
        }

        private async void btSearchTitle_Click(object sender, EventArgs e)
        {
            await SearchTitle(tbSearchShowTitle.Text);
        }

        private async void tbSearchShowTitle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                await SearchTitle(tbSearchShowTitle.Text);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Cancel();
            }

        }

        private void btLoad_Click(object sender, EventArgs e)
        {
            if (Result == null)
            {
                MessageBox.Show("Keine Serie ausgewählt", "STV MANAGER", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else
            {
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            }
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            Cancel();
        }

        private void Cancel()
        {
            this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Close();
        }

        private void FsFindSeriesDialog_Load(object sender, EventArgs e)
        {
            lvFoundSeries.SetObjects(FoundShows);
        }

        private void lvFoundSeries_SelectionChanged(object sender, EventArgs e)
        {
            Result = (FsSeries)lvFoundSeries.SelectedObject;
        }
    }

}
