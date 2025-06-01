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
            progressBar.Value = progressInt;
        }


        private string SelectExplorerFolder()
        {
            using var folderDialog = new FolderBrowserDialog();
            // Настройки диалога
            folderDialog.Description = "Выберите папку";
            folderDialog.RootFolder = Environment.SpecialFolder.MyComputer;
            folderDialog.ShowNewFolderButton = true; // Разрешить создание новой папки

            DialogResult result = folderDialog.ShowDialog();

            // Открываем диалог и проверяем, нажал ли пользователь OK
            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
            {
                return folderDialog.SelectedPath; // Возвращаем путь к выбранной папке
            }
            return string.Empty;
        }
    }
}