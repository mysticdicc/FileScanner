using FileScanner.Models;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
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

namespace FileScanner.Windows
{
    /// <summary>
    /// Interaction logic for PermissionsWindow.xaml
    /// </summary>
    public partial class PermissionsWindow : Window
    {
        public View View { get; set; }
        public Settings AppSettings { get; set; }

        public PermissionsWindow(View view, Settings appSettings)
        {
            InitializeComponent();

            this.View = view;
            this.AppSettings = appSettings;

            foreach (var acl in View.Permissions)
            {
                grdPermissions.Items.Add(acl);
            }

            txtFile.Text = $"File: {View.Name}";
            txtOwner.Text = $"Owner: {View.Owner.Name}";
            AppSettings = appSettings;
        }

        public void BtnOwner_Click(Object sender, RoutedEventArgs e)
        {
            try
            {
                Security.AddViewPermissions(View, AppSettings.AdminGroupName);
            }
            catch(UnauthorizedAccessException)
            {
                var window = new PopupWindow("Access Denied", $"Access denied on folder {View.Path}");

                window.Show();
            }
            catch { }
        }
    }
}
