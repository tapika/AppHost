using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace TestWpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, IConsoleViewModel
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

        public void Clear()
        {
            LogText = String.Empty;
        }

        public void AppendError(string line)
        {
            LogText = LogText + Environment.NewLine + line;
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
