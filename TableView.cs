using System.Data;

namespace ClustersCopyAndAnalyze
{
    public partial class TableView : Form
    {
        public TableView(DataTable table, string formName)
        {
            InitializeComponent();
            ClusterInChainDataGrid.Columns.Clear();
            ClusterInChainDataGrid.DataSource = table;
            Text = formName;
            Show();
        }
    }
}
