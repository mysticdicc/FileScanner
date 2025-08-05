using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace FileScanner
{
    public class FolderView : View
    {
        public ObservableCollection<View> Views { get; set; } = [];
    }

    public class FileView : View
    {
        public string Extension { get; set; }
    }

    public class View
    {
        public string Name { get; set; }
        public string IconPath { get; set; }
        public string Path { get; set; }
        public int PathLength { get; set; }
        public bool AccessDenied { get; set; }
        public bool Hidden { get; set; }
        public List<ACL> Permissions { get; set; }
        public ACL Owner { get; set; }
        public long Size { get; set; }
    }

    public class ACL
    {
        public string Name { get; set; }
        public enum PermissionLevel 
        {
            FullControl,
            Modify,
            Read,
            Other,
            Owner
        }

        public PermissionLevel AccessLevel { get; set; }
    }
}
