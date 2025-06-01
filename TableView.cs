using ClustersCopyAndAnalyze.Services.Clusters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ClustersCopyAndAnalyze
{
    public partial class TableView : Form
    {
        public TableView(DataTable table, string formName)
        {
            InitializeComponent();
            ClusterInChainDataGrid.DataSource = table;
            Name = formName;
            Show();
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}
