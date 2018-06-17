using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace VkClient
{
    [DataContract]
    public class Proxy : INotifyPropertyChanged
    {
        [field:NonSerialized]
        public event PropertyChangedEventHandler PropertyChanged;

        private string _address;
        private string _login;
        private string _password;

        [DataMember]
        public string Address
        {
            get { return _address;}
            set { _address = value; OnPropertyChanged(); }
        }
        [DataMember]
        public string Login
        {
            get { return _login;}
            set { _login = value; OnPropertyChanged(); }
        }
        [DataMember]
        public string Password
        {
            get { return _password; }
            set { _password = value; OnPropertyChanged(); }
        }

        public Proxy(string addreess, string login = null, string password = null)
        {
            Address = addreess;
            Login = login;
            Password = password;
        }

        public Proxy()
        {
        }

        protected void OnPropertyChanged([CallerMemberName] string name = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString()
        {
            return $"{Address}////{Login}:{Password}";
        }
    }
}
