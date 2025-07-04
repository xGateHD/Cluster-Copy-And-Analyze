namespace CCAA
{

    public partial class Form1 : Form
    {
        public delegate void TryStart(string sourcePath, string destPath);
        public TryStart OnTryStart;

        public Form1()
        {
            InitializeComponent();
        }

        #region Handlers

        private void OriginPathField_MouseDoubleClickAsync(object sender, MouseEventArgs e)
        {
            OriginPathField.Text = SelectExplorerFolder();
        }

        private void TargetPathField_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            TargetPathField.Text = SelectExplorerFolder();
        }

        private void analyzeButton_Click(object sender, EventArgs e)
        {
            OnTryStart?.Invoke(OriginPathField.Text, TargetPathField.Text);
        }

        #endregion

        public void ShowProgress(string progress)
        {
            progressLabel.Text = progress;
        }

        public void ShowProgress(double progress)
        {
            int progressInt = (int)(progress * 100);
            progressBar.Value = Math.Clamp(progressInt, 0, 100);
        }


        private string SelectExplorerFolder()
        {
            using var folderDialog = new FolderBrowserDialog();
            // Ќастройки диалога
            folderDialog.Description = "¬ыберите папку";
            folderDialog.RootFolder = Environment.SpecialFolder.MyComputer;
            folderDialog.ShowNewFolderButton = true; // –азрешить создание новой папки

            DialogResult result = folderDialog.ShowDialog();

            // ќткрываем диалог и провер€ем, нажал ли пользователь OK
            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
            {
                return folderDialog.SelectedPath; // ¬озвращаем путь к выбранной папке
            }
            return string.Empty;
        }

        private void infoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string helpText =
                "—правка по функци€м:\n\n" +
                "Х ƒвойной клик по полю 'OriginPathField' Ч открыть диалог выбора исходной папки.\n" +
                "Х ƒвойной клик по полю 'TargetPathField' Ч открыть диалог выбора целевой папки.\n" +
                "Х  нопка 'Analyze' Ч запускает анализ, вызыва€ метод OnTryStart(sourcePath, destPath).\n" +
                "Х ѕрогресс отображаетс€ в 'progressLabel' (текст) и 'progressBar' (графически).\n\n" +
                "Х ћеню '»нфо' Ч вызывает эту справку.";

            MessageBox.Show(helpText, "—правка", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}