using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileScanner.Models
{
    public class Settings : ICloneable, INotifyPropertyChanged
    {
        private string _adminGroupName;
        public string AdminGroupName 
        { 
            get => _adminGroupName; 
            set 
            {
                if (_adminGroupName != value)
                {
                    _adminGroupName = value;
                    OnPropertyChanged(nameof(AdminGroupName));
                }
            } 
        }

        private ObservableCollection<string> _adminGroupsFilter;
        public ObservableCollection<string> AdminGroupsFilter { 
            get => _adminGroupsFilter;
            set
            {
                if (_adminGroupsFilter != value)
                {
                    _adminGroupsFilter = value;
                    OnPropertyChanged(nameof(AdminGroupsFilter));
                }
            }
        }

        private string _reportPath;
        public string ReportPath 
        { 
            get => _reportPath;
            set
            {
                if (_reportPath != value)
                {
                    _reportPath = value;
                    OnPropertyChanged(nameof(ReportPath));
                }
            }
        }

        private string _ldapString;
        public string LdapString 
        { 
            get => _ldapString; 
            set
            {
                if(_ldapString != value)
                {
                    _ldapString = value;
                    OnPropertyChanged(nameof(LdapString));
                }
            } 
        }

        private int _linesPerReport;
        public int LinesPerReport 
        { 
            get => _linesPerReport; 
            set
            {
                if(_linesPerReport != value)
                {
                    _linesPerReport = value;
                    OnPropertyChanged(nameof(LinesPerReport));
                }
            }
        }

        private string _emailFilter;
        public string EmailFilter
        {
            get => _emailFilter;
            set
            {
                if (_emailFilter != value)
                {
                    _emailFilter = value;
                    OnPropertyChanged(nameof(EmailFilter));
                }
            }
        }

        private string _domainPrefix;
        public string DomainPrefix
        {
            get => _domainPrefix;
            set
            {
                if (_domainPrefix != value)
                {
                    _domainPrefix = value;
                    OnPropertyChanged(nameof(DomainPrefix));
                }
            }
        }

        private string _emailAttribute;
        public string EmailAttribute
        {
            get => _emailAttribute;
            set
            {
                if (_emailAttribute != value)
                {
                    _emailAttribute = value;
                    OnPropertyChanged(nameof(EmailAttribute));
                }
            }
        }

        public Settings()
        {
            _linesPerReport = 950000;
            _adminGroupName = string.Empty;
            _adminGroupsFilter = [];
            _reportPath = string.Empty;
            _ldapString = string.Empty;
            _emailFilter = string.Empty;
            _domainPrefix = string.Empty;
            _emailAttribute = string.Empty;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)  => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
