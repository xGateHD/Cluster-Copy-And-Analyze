namespace ClustersCopyAndAnalyze
{
    partial class TableView
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
            ClusterInChainDataGrid = new DataGridView();
            CurrentCluster = new DataGridViewTextBoxColumn();
            NextCluster = new DataGridViewTextBoxColumn();
            HexNextCluster = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)ClusterInChainDataGrid).BeginInit();
            SuspendLayout();
            // 
            // ClusterInChainDataGrid
            // 
            ClusterInChainDataGrid.Anchor = AnchorStyles.None;
            ClusterInChainDataGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            ClusterInChainDataGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            ClusterInChainDataGrid.Columns.AddRange(new DataGridViewColumn[] { CurrentCluster, NextCluster, HexNextCluster });
            ClusterInChainDataGrid.Location = new Point(12, 12);
            ClusterInChainDataGrid.Name = "ClusterInChainDataGrid";
            ClusterInChainDataGrid.ReadOnly = true;
            ClusterInChainDataGrid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
            ClusterInChainDataGrid.Size = new Size(748, 598);
            ClusterInChainDataGrid.TabIndex = 4;
            // 
            // CurrentCluster
            // 
            CurrentCluster.HeaderText = "CurrentCluster";
            CurrentCluster.Name = "CurrentCluster";
            CurrentCluster.ReadOnly = true;
            // 
            // NextCluster
            // 
            NextCluster.HeaderText = "NextCluster";
            NextCluster.Name = "NextCluster";
            NextCluster.ReadOnly = true;
            // 
            // HexNextCluster
            // 
            HexNextCluster.HeaderText = "HexNextClusterInChain";
            HexNextCluster.Name = "HexNextCluster";
            HexNextCluster.ReadOnly = true;
            // 
            // TableView
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(772, 622);
            Controls.Add(ClusterInChainDataGrid);
            Name = "TableView";
            Text = "TableView";
            ((System.ComponentModel.ISupportInitialize)ClusterInChainDataGrid).EndInit();
            ResumeLayout(false);
        }

        #endregion
        private DataGridView ClusterInChainDataGrid;
        private DataGridViewTextBoxColumn CurrentCluster;
        private DataGridViewTextBoxColumn NextCluster;
        private DataGridViewTextBoxColumn HexNextCluster;
    }
}