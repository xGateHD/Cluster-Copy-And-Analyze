namespace CCAA
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            contextMenuStrip1 = new ContextMenuStrip(components);
            menuStrip2 = new MenuStrip();
            fileOperationsToolStripMenuItem = new ToolStripMenuItem();
            selectPathsToolStripMenuItem = new ToolStripMenuItem();
            tableBeforeCopyToolStripMenuItem = new ToolStripMenuItem();
            tableAfterCopyToolStripMenuItem = new ToolStripMenuItem();
            infoToolStripMenuItem = new ToolStripMenuItem();
            contextMenuStrip2 = new ContextMenuStrip(components);
            OriginPathField = new TextBox();
            TargetPathField = new TextBox();
            label1 = new Label();
            label2 = new Label();
            analyzeButton = new Button();
            progressBar = new ProgressBar();
            progressLabel = new Label();
            tableBeforeCopyToolStripMenuItem1 = new ToolStripMenuItem();
            menuStrip2.SuspendLayout();
            SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(61, 4);
            // 
            // menuStrip2
            // 
            menuStrip2.Items.AddRange(new ToolStripItem[] { fileOperationsToolStripMenuItem, selectPathsToolStripMenuItem, infoToolStripMenuItem });
            menuStrip2.Location = new Point(0, 0);
            menuStrip2.Name = "menuStrip2";
            menuStrip2.Size = new Size(804, 24);
            menuStrip2.TabIndex = 6;
            menuStrip2.Text = "menuStrip2";
            // 
            // fileOperationsToolStripMenuItem
            // 
            fileOperationsToolStripMenuItem.Name = "fileOperationsToolStripMenuItem";
            fileOperationsToolStripMenuItem.Size = new Size(96, 20);
            fileOperationsToolStripMenuItem.Text = "File operations";
            // 
            // selectPathsToolStripMenuItem
            // 
            selectPathsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { tableBeforeCopyToolStripMenuItem, tableAfterCopyToolStripMenuItem, tableBeforeCopyToolStripMenuItem1 });
            selectPathsToolStripMenuItem.Name = "selectPathsToolStripMenuItem";
            selectPathsToolStripMenuItem.Size = new Size(46, 20);
            selectPathsToolStripMenuItem.Text = "Table";
            // 
            // tableBeforeCopyToolStripMenuItem
            // 
            tableBeforeCopyToolStripMenuItem.Name = "tableBeforeCopyToolStripMenuItem";
            tableBeforeCopyToolStripMenuItem.Size = new Size(167, 22);
            tableBeforeCopyToolStripMenuItem.Text = "Table before copy";
            // 
            // tableAfterCopyToolStripMenuItem
            // 
            tableAfterCopyToolStripMenuItem.Name = "tableAfterCopyToolStripMenuItem";
            tableAfterCopyToolStripMenuItem.Size = new Size(167, 22);
            tableAfterCopyToolStripMenuItem.Text = "Table after copy";
            // 
            // infoToolStripMenuItem
            // 
            infoToolStripMenuItem.Name = "infoToolStripMenuItem";
            infoToolStripMenuItem.Size = new Size(40, 20);
            infoToolStripMenuItem.Text = "Info";
            // 
            // contextMenuStrip2
            // 
            contextMenuStrip2.Name = "contextMenuStrip2";
            contextMenuStrip2.Size = new Size(61, 4);
            // 
            // OriginPathField
            // 
            OriginPathField.Location = new Point(12, 106);
            OriginPathField.Name = "OriginPathField";
            OriginPathField.Size = new Size(181, 23);
            OriginPathField.TabIndex = 8;
            OriginPathField.MouseDoubleClick += OriginPathField_MouseDoubleClickAsync;
            // 
            // TargetPathField
            // 
            TargetPathField.Location = new Point(527, 106);
            TargetPathField.Name = "TargetPathField";
            TargetPathField.Size = new Size(181, 23);
            TargetPathField.TabIndex = 9;
            TargetPathField.MouseDoubleClick += TargetPathField_MouseDoubleClick;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 54);
            label1.Name = "label1";
            label1.Size = new Size(64, 15);
            label1.TabIndex = 10;
            label1.Text = "OriginPath";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(527, 54);
            label2.Name = "label2";
            label2.Size = new Size(63, 15);
            label2.TabIndex = 11;
            label2.Text = "TargetPath";
            // 
            // analyzeButton
            // 
            analyzeButton.Location = new Point(12, 312);
            analyzeButton.Name = "analyzeButton";
            analyzeButton.Size = new Size(181, 83);
            analyzeButton.TabIndex = 12;
            analyzeButton.Text = "Start operations";
            analyzeButton.UseVisualStyleBackColor = true;
            analyzeButton.Click += analyzeButton_Click;
            // 
            // progressBar
            // 
            progressBar.BackColor = SystemColors.Control;
            progressBar.Location = new Point(232, 106);
            progressBar.MarqueeAnimationSpeed = 1000;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(254, 23);
            progressBar.TabIndex = 13;
            // 
            // progressLabel
            // 
            progressLabel.Anchor = AnchorStyles.None;
            progressLabel.BackColor = Color.White;
            progressLabel.ForeColor = SystemColors.ControlText;
            progressLabel.Location = new Point(232, 132);
            progressLabel.Name = "progressLabel";
            progressLabel.Size = new Size(254, 23);
            progressLabel.TabIndex = 14;
            progressLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // tableBeforeCopyToolStripMenuItem1
            // 
            tableBeforeCopyToolStripMenuItem1.Enabled = false;
            tableBeforeCopyToolStripMenuItem1.Name = "tableBeforeCopyToolStripMenuItem1";
            tableBeforeCopyToolStripMenuItem1.Size = new Size(167, 22);
            tableBeforeCopyToolStripMenuItem1.Text = "Table before copy";
            // 
            // Form1
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(804, 455);
            Controls.Add(progressLabel);
            Controls.Add(progressBar);
            Controls.Add(analyzeButton);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(TargetPathField);
            Controls.Add(OriginPathField);
            Controls.Add(menuStrip2);
            Name = "Form1";
            Text = "Form1";
            menuStrip2.ResumeLayout(false);
            menuStrip2.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private ContextMenuStrip contextMenuStrip1;
        private MenuStrip menuStrip2;
        private ToolStripMenuItem fileOperationsToolStripMenuItem;
        private ToolStripMenuItem selectPathsToolStripMenuItem;
        private ToolStripMenuItem infoToolStripMenuItem;
        private ToolStripMenuItem tableBeforeCopyToolStripMenuItem;
        private ToolStripMenuItem tableAfterCopyToolStripMenuItem;
        private ContextMenuStrip contextMenuStrip2;
        private TextBox OriginPathField;
        private TextBox TargetPathField;
        private Label label1;
        private Label label2;
        private Button analyzeButton;
        private ProgressBar progressBar;
        private Label progressLabel;
        private ToolStripMenuItem tableBeforeCopyToolStripMenuItem1;
    }
}
