using System.Globalization;
using System.Windows;

namespace RemnantSaveManager
{
    /// <summary>
    /// Interaction logic for RestoreDialog.xaml
    /// </summary>
    public partial class RestoreDialog : Window
    {
        private SaveBackup _saveBackup;
        private RemnantSave _activeSave;
        public string Result { get; set; }
        public RestoreDialog(MainWindow @mw, SaveBackup @sb, RemnantSave @as)
        {
            InitializeComponent();
            this.txtSave.Content = $"Название:\t{sb.Name}\nДата:\t\t{sb.SaveDate.ToString(CultureInfo.CurrentCulture)}";
            this._saveBackup = sb;
            this._activeSave = @as;
        }

        private void btnCharacter_Click(object sender, RoutedEventArgs e)
        {
            Result = "Character";
            DialogResult = true;
            this.Close();
        }

        private void btnWorld_Click(object sender, RoutedEventArgs e)
        {

            if (this._saveBackup.Save.Characters.Count != this._activeSave.Characters.Count)
            {
                MessageBoxResult confirmResult = MessageBox.Show("Число персонажей в текущем сохранении отличается от числа миров в резервной копии. Возможны непредвиденные последствия. Продолжить?",
                                     "Число персонажей не совпадает", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (confirmResult == MessageBoxResult.No)
                {
                    DialogResult = false;
                    this.Close();
                    return;
                }
            }

            Result = "World";
            DialogResult = true;
            this.Close();
        }

        private void btnAll_Click(object sender, RoutedEventArgs e)
        {
            Result = "All";
            DialogResult = true;
            this.Close();
        }
    }
}
