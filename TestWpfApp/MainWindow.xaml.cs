using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace TestWpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
        }

        string _logText;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string LogText
        {
            get
            {
                return _logText;
            }
            set
            {
                _logText = value;
                NotifyPropertyChanged();
            }
        }
    }
}
