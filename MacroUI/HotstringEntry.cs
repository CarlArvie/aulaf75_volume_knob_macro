using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MacroUI
{
    public class HotstringEntry : INotifyPropertyChanged
    {
        private string _trigger = "";
        public string Trigger
        {
            get => _trigger;
            set { _trigger = value; OnPropertyChanged(); }
        }

        private string _replacement = "";
        public string Replacement
        {
            get => _replacement;
            set { _replacement = value; OnPropertyChanged(); }
        }

        private string _imagePath;
        public string ImagePath
        {
            get => _imagePath;
            set { _imagePath = value; OnPropertyChanged(); }
        }

        private bool _matchTypedCase = true;
        public bool MatchTypedCase
        {
            get => _matchTypedCase;
            set { _matchTypedCase = value; OnPropertyChanged(); }
        }

        private bool _isSecure;
        public bool IsSecure
        {
            get => _isSecure;
            set { _isSecure = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
