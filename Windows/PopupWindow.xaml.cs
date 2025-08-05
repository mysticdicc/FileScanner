using System;
using System.Collections.Generic;
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
using System.Windows.Shell;

namespace FileScanner.Windows
{
    /// <summary>
    /// Interaction logic for PopupWindow.xaml
    /// </summary>
    public partial class PopupWindow : Window
    {
        public string ErrorTitle { get; set; }
        public string ErrorMessage { get; set; }

        public PopupWindow(string errorTitle, string errorMessage)
        {
            InitializeComponent();
            this.ErrorMessage = errorMessage;
            this.ErrorTitle = errorTitle;

            txtTitle.Text = ErrorTitle;
            txtError.Text = ErrorMessage;
        }

        void BtnOk_Click( object sender, RoutedEventArgs e )
        {
            Close();
        }
    }
}
