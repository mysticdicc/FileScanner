using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Wpf.Ui.Controls;
using FileScanner.Models;

namespace FileScanner.Windows
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        Settings AppSettings { get; set; }
        Settings OriginalSettings { get; set; }

        public SettingsWindow(Settings appSettings)
        {
            InitializeComponent();
            this.OriginalSettings = (Settings)appSettings.Clone();
            this.AppSettings = appSettings;
            this.DataContext = AppSettings;
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (txtFilterAdd.Text != string.Empty)
            {
                AppSettings.AdminGroupsFilter.Add(txtFilterAdd.Text);
                txtFilterAdd.Text = string.Empty;
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e) 
        {
            AppSettings.AdminGroupsFilter.Remove((string)lstFilters.SelectedItem);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) 
        {
            AppSettings = OriginalSettings;
            this.Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
