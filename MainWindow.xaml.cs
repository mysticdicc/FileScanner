using FileScanner.Models;
using FileScanner.Windows;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.DirectoryServices;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xaml;
using Wpf.Ui.Controls;
using Newtonsoft.Json;
using System.Xml.Linq;

namespace FileScanner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        private DateTime ScanStartTime { get; set; }
        private int TotalItemCount { get; set; }
        private ObservableCollection<View> CurrentViewedItems { get; set; }
        private List<View> AllViewsList { get; set; }

        private string ReportPathRoot { get; set; }
        private string LastScannedPath { get; set; } = string.Empty;
        private long TotalSize { get; set; }
        private Settings AppSettings { get; set; }
        private static string SettingsPath = $"{AppDomain.CurrentDomain.BaseDirectory}/settings.json";

        public MainWindow()
        {
            InitializeComponent();
            CurrentViewedItems = [];
            AllViewsList = [];
            AppSettings = LoadSettings();
            ReportPathRoot = AppSettings.ReportPath;
            treeFolderView.SelectedItemChanged += TreeFolderView_SelectedItemChanged;
        }

        private Settings LoadSettings()
        {
            Settings? settings;

            if (File.Exists(SettingsPath))
            {
                var content = File.ReadAllText(SettingsPath);
                settings = JsonConvert.DeserializeObject<Settings>(content);

                if (null != settings)
                {
                    return settings;
                }
                else
                {
                    settings = new Settings();
                    var settingsContent = JsonConvert.SerializeObject(settings);
                    WriteToFile(settingsContent, SettingsPath);

                    return settings;
                }
            }
            else
            {
                settings = new Settings();
                var settingsContent = JsonConvert.SerializeObject(settings);
                WriteToFile(settingsContent, SettingsPath);

                return settings;
            }
        }

        private void TreeFolderView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var item = (View)treeFolderView.SelectedItem;
            lblCurrentFileHidden.Content = item.Hidden;
            lblCurrentPathLength.Content = item.PathLength;
        }

        async private void btnFileSearch_Click(object sender, RoutedEventArgs e)
        {
            if (txtFilePath.Text == string.Empty)
            {
                var window = new PopupWindow("No Path Entered", "Please enter a file path before starting scan");
                window.ShowDialog();
                return;
            }

            if (!Directory.Exists(txtFilePath.Text))
            {
                var window = new PopupWindow("File Path Not Valid", "Could not detect file path, please ensure it is a valid file path");
                window.ShowDialog();
                return;
            }

            ScanStartTime = DateTime.Now;
            TotalItemCount = 0;
            AllViewsList = [];
            CurrentViewedItems = [];
            LastScannedPath = txtFilePath.Text;
            TotalSize = 0;
            txtStatus.Text = "Scanning files...";

            var timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += new EventHandler(TimerTick);
            timer.Start();
            Mouse.OverrideCursor = Cursors.Wait;

            var path = txtFilePath.Text;
            ConcurrentBag<FolderView> fileList = [];
            var topFiles = GetFileInfo(path);

            await Task.Run(() =>
            {
                fileList = GetFolderInfo(path);
            });

            foreach (var view in fileList)
            {
                CurrentViewedItems.Add(view);
                TotalSize += view.Size;
            }

            foreach (var view in topFiles)
            {
                CurrentViewedItems.Add(view);
                TotalSize += view.Size;
            }

            lblTotalCount.Content = TotalItemCount.ToString();

            var size = Math.Round((decimal)TotalSize / (decimal)1073741824, 2);
            lblTotalSize.Content = $"{size} GB";

            treeFolderView.ItemsSource = CurrentViewedItems;
            timer.Stop();

            Mouse.OverrideCursor = null;
            txtStatus.Text = "";
        }

        private void TimerTick(object? sender, EventArgs e)
        {
            var currentTime = DateTime.Now;
            var timeSpan = currentTime - ScanStartTime;

            Application.Current.Dispatcher.Invoke(() => { lblRunTimer.Content = $"Scan Time: {timeSpan.Hours}:{timeSpan.Minutes}:{timeSpan.Seconds}"; });
            Application.Current.Dispatcher.Invoke(() => { lblTotalCount.Content = TotalItemCount.ToString(); });
        }

        private void btnPermissions_Click(object sender, RoutedEventArgs e)
        {
            var item = (View)treeFolderView.SelectedItem;

            if (null != item)
            {
                var window = new PermissionsWindow(item, AppSettings);
                window.View = item;
                window.Show();
            }
            else
            {
                var window = new PopupWindow("No file or folder selected", "Please select a file or folder from the scanned items list to view permissions");
                window.ShowDialog();
            }
        }

        private ConcurrentBag<FileView> GetFileInfo(string path)
        {
            ConcurrentBag<FileView> views = [];
            var rootFolder = new DirectoryInfo(path);
            FileInfo[] fileList = [];

            try
            {
                fileList = rootFolder.GetFiles();
            }
            catch { }

            var taskArray = new Task[fileList.Count()];

            for (int i = 0; i < taskArray.Length; i++)
            {
                int localIndex = i;

                taskArray[i] = Task.Factory.StartNew(() =>
                {
                    TotalItemCount++;

                    try
                    {
                        bool hidden = false;
                        string iconPath = "pack://application:,,,/Images/file.png";

                        if (fileList[localIndex].Attributes.HasFlag(FileAttributes.Hidden))
                        {
                            hidden = true;
                            iconPath = "pack://application:,,,/Images/grey-file.png";
                        }

                        if (fileList[localIndex].FullName.Length > 250)
                        {
                            iconPath = "pack://application:,,,/Images/red-file.png";
                        }

                        List<ACL> acl = [];
                        bool accessDenied = false;
                        var ownerAcl = new ACL();
                        var perms = new FileSecurity();

                        try
                        {
                            perms = fileList[localIndex].GetAccessControl();
                            var owner = perms.GetOwner(typeof(NTAccount));

                            if (owner != null)
                            {
                                ownerAcl = new ACL()
                                {
                                    AccessLevel = ACL.PermissionLevel.Owner,
                                    Name = owner.Value.ToString()
                                };
                            }

                        }
                        catch (UnauthorizedAccessException)
                        {
                            iconPath = "pack://application:,,,/Images/lock.png";
                            accessDenied = true;
                        }
                        catch (Exception)
                        {

                        }
;

                        if (!accessDenied)
                        {
                            if (null != perms)
                            {
                                acl = GetFilePermissions(perms);
                            }
                        }

                        var item = new FileView()
                        {
                            Name = fileList[localIndex].Name,
                            Path = fileList[localIndex].FullName,
                            IconPath = iconPath,
                            PathLength = fileList[localIndex].FullName.Length,
                            Hidden = hidden,
                            Permissions = acl,
                            Owner = ownerAcl,
                            AccessDenied = accessDenied,
                            Extension = fileList[localIndex].Extension,
                            Size = fileList[localIndex].Length
                        };

                        views.Add(item);
                        AllViewsList.Add(item);
                    }
                    catch (Exception)
                    {

                    };
                });
            }

            Task.WaitAll(taskArray);

            return views;
        }

        private ConcurrentBag<FolderView> GetFolderInfo(string path)
        {
            ConcurrentBag<FolderView> views = [];
            var rootFolder = new DirectoryInfo(path);
            DirectoryInfo[] folderList = [];

            try
            {
                folderList = rootFolder.GetDirectories();
            }
            catch { }

            var taskArray = new Task[folderList.Count()];

            for (int i = 0; i < taskArray.Length; i++)
            {
                int localIndex = i;

                taskArray[i] = Task.Factory.StartNew(() =>
                {
                    TotalItemCount++;

                    bool hidden = false;
                    string iconPath = "pack://application:,,,/Images/folder.png";

                    if (folderList[localIndex].Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        hidden = true;
                        iconPath = "pack://application:,,,/Images/grey-folder.png";
                    }

                    if (folderList[localIndex].FullName.Length > 250)
                    {
                        iconPath = "pack://application:,,,/Images/red-folder.png";
                    }

                    List<ACL> acl = [];
                    bool accessDenied = false;
                    var ownerAcl = new ACL();
                    var perms = new DirectorySecurity();

                    try
                    {
                        perms = folderList[localIndex].GetAccessControl();
                        var owner = perms.GetOwner(typeof(NTAccount));

                        if (owner != null)
                        {
                            ownerAcl = new ACL()
                            {
                                AccessLevel = ACL.PermissionLevel.Owner,
                                Name = owner.Value.ToString()
                            };
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        iconPath = "pack://application:,,,/Images/lock.png";
                        accessDenied = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Source.ToString());
                        Console.WriteLine(ex.StackTrace);
                        Console.WriteLine(ex.InnerException);
                        Console.WriteLine(ex.Message);
                    }
                    ;

                    if (!accessDenied)
                    {
                        if (null != perms)
                        {
                            acl = GetDirectoryPermissions(perms);
                        }
                    }

                    ObservableCollection<View> subItems = [];
                    var fileInfo = GetFileInfo(folderList[localIndex].FullName);
                    var folderInfo = GetFolderInfo(folderList[localIndex].FullName);
                    long size = 0;

                    foreach (var view in fileInfo)
                    {
                        subItems.Add(view);
                        size += view.Size;
                    }

                    foreach (var view in folderInfo)
                    {
                        subItems.Add(view);
                        size += view.Size;
                    }

                    var item = new FolderView()
                    {
                        Name = folderList[localIndex].Name,
                        Path = folderList[localIndex].FullName,
                        Views = subItems,
                        IconPath = iconPath,
                        PathLength = folderList[localIndex].FullName.Length,
                        Hidden = hidden,
                        Permissions = acl,
                        Owner = ownerAcl,
                        AccessDenied = accessDenied,
                        Size = size
                    };

                    views.Add(item);
                    AllViewsList.Add(item);
                });
            }

            Task.WaitAll(taskArray);

            return views;
        }

        private List<ACL> GetDirectoryPermissions(DirectorySecurity permissions)
        {
            var aclList = new List<ACL>();

            foreach (FileSystemAccessRule permission in permissions.GetAccessRules(true, true, typeof(NTAccount)))
            {
                var level = ACL.PermissionLevel.Other;

                if (permission.FileSystemRights == FileSystemRights.Read)
                {
                    level = ACL.PermissionLevel.Read;
                }
                else if (permission.FileSystemRights == FileSystemRights.Modify)
                {
                    level = ACL.PermissionLevel.Modify;
                }
                else if (permission.FileSystemRights == FileSystemRights.FullControl)
                {
                    level = ACL.PermissionLevel.FullControl;
                }

                var acl = new ACL()
                {
                    Name = permission.IdentityReference.ToString(),
                    AccessLevel = level
                };

                aclList.Add(acl);
            }

            return aclList;
        }

        private List<ACL> GetFilePermissions(FileSecurity permissions)
        {
            var aclList = new List<ACL>();

            foreach (FileSystemAccessRule permission in permissions.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            {
                var level = ACL.PermissionLevel.Other;

                if (permission.FileSystemRights == FileSystemRights.Read)
                {
                    level = ACL.PermissionLevel.Read;
                }
                else if (permission.FileSystemRights == FileSystemRights.Modify)
                {
                    level = ACL.PermissionLevel.Modify;
                }
                else if (permission.FileSystemRights == FileSystemRights.FullControl)
                {
                    level = ACL.PermissionLevel.FullControl;
                }

                var acl = new ACL()
                {
                    Name = permission.IdentityReference.ToString(),
                    AccessLevel = level
                };

                aclList.Add(acl);
            }

            return aclList;
        }

        private async void MenuGenerateClicked(object sender, RoutedEventArgs e)
        {
            if (AppSettings.ReportPath == string.Empty || ReportPathRoot == string.Empty)
            {
                var window = new PopupWindow("No Report Path Set", "Please open the settings window and set a report path to output files to");
                window.Show();
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            txtStatus.Text = "Generating report/s...";

            if (MenuReportPersonal.IsChecked == true)
            {
                MenuReportPersonal.IsChecked = false;

                await Task.Run(() => {
                    ReportsPersonal();
                });
            }

            if (MenuReportAccess.IsChecked == true)
            {
                MenuReportAccess.IsChecked = false;

                await Task.Run(() => {
                    ReportsAccessDenied();
                });
            }

            if (MenuReportHidden.IsChecked == true)
            {
                MenuReportHidden.IsChecked = false;

                await Task.Run(() => {
                    ReportsHidden();
                });
            }

            if (MenuReportLength.IsChecked == true)
            {
                MenuReportLength.IsChecked = false;

                await Task.Run(() => {
                    ReportsFileLength();
                });
            }

            if (MenuReportPerms.IsChecked == true)
            {
                MenuReportPerms.IsChecked = false;

                await Task.Run(() => {
                    ReportsPerms();
                });
            }

            if (MenuReportAll.IsChecked == true)
            {
                MenuReportAll.IsChecked = false;

                await Task.Run(() => {
                    ReportsAll();
                });
            }

            Mouse.OverrideCursor = null;
            txtStatus.Text = "";
        }

        private void ReportsPerms()
        {
            if (AppSettings.ReportPath == string.Empty || ReportPathRoot == string.Empty)
            {
                var window = new PopupWindow("No Report Path Set", "Please open the settings window and set a report path to output files to");
                window.Show();
                return;
            }

            string scanPath = LastScannedPath.Replace(@"\", "-").Replace(":", "").Replace(" ",  "");
            string reportName = $"{scanPath}_Permissions";

            var csv = new StringBuilder();
            string reportString = $"\"Name\",\"File Path\",\"Path Length\",\"File Extension\",\"Owner\",\"Permission\",\"Permission\",\"Permission\",\"Permission\",\"Permission\",\"Permission\",\"Permission\",\"Permission\",\"Permission\",\"Permission\"";
            csv.AppendLine(reportString);

            int lineCount = 0;
            int fileCount = 0;

            foreach (var view in AllViewsList)
            {

                if (lineCount == AppSettings.LinesPerReport)
                {
                    lineCount = 0;
                    WriteToFile(csv.ToString(), $"{ReportPathRoot}/{reportName}_{fileCount}.csv");
                    csv = new StringBuilder();
                    fileCount++;
                }

                var perms = view.Permissions.Select(x => x.Name).ToList();
                var permsString = string.Empty;

                foreach (var perm in perms)
                {
                    permsString = $",\"{perm}\"";
                }

                if (view.GetType() == typeof(FileView))
                {
                    var item = (FileView)view;

                    reportString = $"\"{item.Name}\",\"{item.Path}\",\"{item.PathLength}\",\"{item.Extension}\",\"{item.Owner.Name}\"{permsString}";
                    csv.AppendLine(reportString);
                }
                else
                {
                    reportString = $"\"{view.Name}\",\"{view.Path}\",\"{view.PathLength}\",\"Directory\",\"{view.Owner.Name}\"{permsString}";
                    csv.AppendLine(reportString);
                }

                lineCount++;
            }

            if (fileCount != 0) 
            {
                fileCount++; 
            }

            WriteToFile(csv.ToString(), $"{ReportPathRoot}/{reportName}_{fileCount}.csv");
        }

        private void ReportsPersonal() 
        {
            if (AppSettings.LdapString == string.Empty || AppSettings.AdminGroupsFilter.Count == 0 || AppSettings.EmailAttribute == string.Empty || AppSettings.EmailFilter == string.Empty)
            {
                var window = new PopupWindow("Required Settings Missing", "Please ensure you have set all the required settings in the settings menu before trying to run a personal drives report");
                window.ShowDialog();
                return;
            }

            string reportName = $"{Environment.MachineName}_PersonalDrivesReport.csv";
            string reportPath = $"{ReportPathRoot}{reportName}";

            string mappingName = $"{Environment.MachineName}_odMap.csv";
            string mappingPath = $"{ReportPathRoot}{mappingName}";

            string userListName = $"{Environment.MachineName}_AllUsers.txt";
            string userPath = $"{ReportPathRoot}{userListName}";

            List<string> allUsersList = [];
            var allUsers = new StringBuilder();

            var reportCsv = new StringBuilder();
            string line = "\"File Path\",\"Folder Name\",\"User Email\",\"One Drive URL\",\"Status\"";
            reportCsv.AppendLine(line);

            var mapCsv = new StringBuilder();

            var searcher = new DirectorySearcher(AppSettings.LdapString);

            var mailPattern = "[^a-zA-Z_0-9]";

            List<Regex> ignoreGroups = [];

            foreach (string filter in AppSettings.AdminGroupsFilter)
            {
                ignoreGroups.Add(new Regex(filter));
            }

            foreach (var view in CurrentViewedItems)
            {
                if (!view.AccessDenied)
                {
                    List<string> perms = view.Permissions.Select(x => x.Name).Distinct().Where(name => !ignoreGroups.Any(x => x.IsMatch(name))).ToList();
                    var permsString = string.Join(",", perms);
                    string userName = string.Empty;

                    if (perms.Count == 1)
                    {
                        userName = perms.First().Replace(AppSettings.DomainPrefix, "");
                        searcher.Filter = $"(&(objectClass=user)(sAMAccountName={userName}))";
                        var ldapUser = searcher.FindOne();

                        if (ldapUser != null)
                        {
                            var entry = ldapUser.GetDirectoryEntry();
                            var property = entry.Properties[AppSettings.EmailAttribute][0];

                            if (property != null)
                            {
                                var email = property.ToString();

                                if (Regex.IsMatch(email, AppSettings.EmailFilter))
                                {
                                    var emailReg = Regex.Replace(email, mailPattern, "_");
                                    var odPath = $"https://gxologistics-my.sharepoint.com/personal/{emailReg}";

                                    line = $"\"{view.Path}\",{null},{null},\"{odPath}\",\"Documents\"";
                                    mapCsv.AppendLine(line);

                                    line = $"\"{view.Path}\",\"{view.Name}\",\"{email}\",\"{odPath}\",\"Valid email found for user {perms.First()}\"";
                                    reportCsv.AppendLine(line);
                                    allUsersList.Add(email);
                                }
                                else
                                {
                                    line = $"\"{view.Path}\",\"{view.Name}\",\"No {AppSettings.EmailFilter} email\",\"Not found\",\"User account found but no {AppSettings.EmailFilter} email: {permsString}\"";
                                    reportCsv.AppendLine(line);
                                }
                            }
                            else
                            {
                                line = $"\"{view.Path}\",\"{view.Name}\",\"No {AppSettings.EmailFilter} email\",\"Not found\",\"LDAP result missing msDS-cloudExtensionAttribute1\"";
                                reportCsv.AppendLine(line);
                            }
                        }
                        else
                        {
                            line = $"\"{view.Path}\",\"{view.Name}\",\"No GXO email\",\"Not found\",\"No LDAP results for user: {userName}\"";
                            reportCsv.AppendLine(line);
                        }
                    }
                    else if (perms.Count > 1)
                    {
                        line = $"\"{view.Path}\",\"{view.Name}\",\"Too many matches\",\"Not found\",\"Too many potential users on folder: {permsString}\"";
                        reportCsv.AppendLine(line);
                    }
                    else if (perms.Count == 0)
                    {
                        line = $"\"{view.Path}\",\"{view.Name}\",\"No matches\",\"Not found\",\"No potential users on folder\"";
                        reportCsv.AppendLine(line);
                    }
                    else
                    {
                        line = $"\"{view.Path}\",\"{view.Name}\",\"Not {AppSettings.DomainPrefix} user\",\"Not Found\",\"Potential user detected but not {AppSettings.DomainPrefix} user: {permsString}\"";
                        reportCsv.AppendLine(line);
                    }
                }
                else
                {
                    line = $"\"{view.Path}\",\"{view.Name}\",\"Access Denied\",\"Not Found\",\"No access to folder to get permissions\"";
                    reportCsv.AppendLine(line);
                }
            }

            WriteToFile(reportCsv.ToString(), reportPath);
            WriteToFile(mapCsv.ToString(), mappingPath);

            var userString = string.Join(",", allUsersList);
            WriteToFile(userString, userPath);
        }

        private void ReportsFileLength()
        {
            string scanPath = LastScannedPath.Replace(@"\", "-").Replace(":", "").Replace(" ",  "");
            string reportName = $"{scanPath}_FileLength";
            string reportPath = $"{ReportPathRoot}{reportName}";
            var list = AllViewsList.Where(x => x.PathLength > 250).ToList();

            GenerateReport(reportName, list);
        }

        private void ReportsAll()
        {
            string scanPath = LastScannedPath.Replace(@"\", "-").Replace(":", "").Replace(" ",  "");
            string reportName = $"{scanPath}_AllFiles";

            GenerateReport(reportName, AllViewsList);
        }

        private void ReportsHidden()
        {
            string scanPath = LastScannedPath.Replace(@"\", "-").Replace(":", "").Replace(" ",  "");
            string reportName = $"{scanPath}_HiddenFiles";
            var list = AllViewsList.Where(x => x.Hidden == true).ToList();

            GenerateReport(reportName, list);
        }

        private void ReportsAccessDenied()
        {
            string scanPath = LastScannedPath.Replace(@"\", "-").Replace(":", "").Replace(" ",  "");
            string reportName = $"{scanPath}_AccessDenied";
            var list = AllViewsList.Where(x => x.AccessDenied == true).ToList();

            GenerateReport(reportName, list);
        }

        private async void GenerateReport(string name, List<View> list)
        {
            var csv = new StringBuilder();
            string reportString = $"\"Name\",\"File Path\",\"Path Length\",\"File Extension\",\"Size (KB)\",\"Size (MB)\",\"Size (GB)\"";
            csv.AppendLine(reportString);

            var taskArray = new Task<string>[list.Count];

            for (int i = 0; i < taskArray.Length; i++)
            {
                int localIndex = i;

                taskArray[i] = Task<string>.Factory.StartNew(() =>
                {
                    var view = list[localIndex];
                    var csvString = string.Empty;
                    var sizeKb = Math.Round((decimal)view.Size / (decimal)1024, 2);
                    var sizeMb = Math.Round((decimal)view.Size / (decimal)1048576, 2);
                    var sizeGb = Math.Round((decimal)view.Size / (decimal)1073741824, 2);

                    if (view.GetType() == typeof(FileView))
                    {
                        var item = (FileView)view;
                        csvString = $"\"{item.Name}\",\"{item.Path}\",\"{item.PathLength}\",\"{item.Extension}\",\"{sizeKb}\",\"{sizeMb}\",\"{sizeGb}\"";
                    }
                    else
                    {
                        csvString = $"\"{view.Name}\",\"{view.Path}\",\"{view.PathLength}\",\"Directory\",\"{sizeKb}\",\"{sizeMb}\",\"{sizeGb}\"";
                    }

                    return csvString;
                });
            }

            var tasks = await Task.WhenAll(taskArray);

            int lineCount = 0;
            int fileCount = 0;

            foreach (var line in tasks)
            {
                if (lineCount == AppSettings.LinesPerReport)
                {
                    lineCount = 0;
                    WriteToFile(csv.ToString(), $"{ReportPathRoot}/{name}_{fileCount}.csv");
                    csv = new StringBuilder();
                    csv.AppendLine(reportString);
                    fileCount++;
                }

                csv.AppendLine(line);
                lineCount++;
            }

            if (fileCount != 0)
            {
                fileCount++;
            }

            WriteToFile(csv.ToString(), $"{ReportPathRoot}/{name}_{fileCount}.csv");
        }

        private async void FixesPermissions(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "Fixing file permissions....";
            Mouse.OverrideCursor = Cursors.Wait;

            await Task.Run(() => 
            {
                string scanPath = LastScannedPath.Replace(@"\", "-").Replace(":", "").Replace(" ",  "");
                string reportName = $"{Environment.MachineName}_{scanPath}_FixesPermissionsLog.csv";
                string reportPath = $"{ReportPathRoot}{reportName}";

                var views = AllViewsList.Where(x => x.AccessDenied == true);

                if (views.Any()) 
                {
                    var list = views.ToList();

                    if (list != null)
                    {
                        var csv = new StringBuilder();
                        string reportString = $"\"Name\",\"File Path\",\"Access Changed\",\"Error\"";
                        csv.AppendLine(reportString);

                        foreach (var view in list)
                        {
                            if (view.GetType() == typeof(FolderView))
                            {
                                try
                                {
                                    if (Directory.Exists(view.Path))
                                    {
                                        RecursiveFolderPermissions(csv, view.Path, view.Name);
                                    }
                                    else
                                    {
                                        reportString = $"\"{view.Name}\",\"{view.Path}\",\"FALSE\",\"Folder does not exist\"";
                                        csv.AppendLine(reportString);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    reportString = $"\"{view.Name}\",\"{view.Path}\",\"FALSE\",\"{ex.Message}\"";
                                    csv.AppendLine(reportString);
                                }
                            }
                            else
                            {
                                try
                                {
                                    if (File.Exists(view.Path))
                                    {
                                        Security.AddViewPermissions(view, AppSettings.AdminGroupName);
                                        reportString = $"\"{view.Name}\",\"{view.Path}\",\"TRUE\",\"NONE\"";
                                        csv.AppendLine(reportString);
                                    }

                                }
                                catch (Exception ex)
                                {
                                    reportString = $"\"{view.Name}\",\"{view.Path}\",\"FALSE\",\"{ex.Message}\"";
                                    csv.AppendLine(reportString);
                                }
                            }
                        }

                        WriteToFile(csv.ToString(), reportPath);
                    }
                }
            });

            txtStatus.Text = "";
            Mouse.OverrideCursor = null;
        }

        private void RecursiveFolderPermissions(StringBuilder csv, string path, string name)
        {
            var reportString = string.Empty;

            try
            {
                Security.AddFolderPermissions(path, AppSettings.AdminGroupName);
                reportString = $"\"{name}\",\"{path}\",\"TRUE\",\"NONE\"";
                csv.AppendLine(reportString);
            }
            catch(Exception ex)
            {
                reportString = $"\"{name}\",\"{path}\",\"FALSE\",\"{ex.Message}\"";
                csv.AppendLine(reportString);
            }
            
            try
            {
                var info = new DirectoryInfo(path);
                var directories = info.EnumerateDirectories();

                if (null != directories)
                {
                    foreach (var directory in directories)
                    {
                        try
                        {
                            RecursiveFolderPermissions(csv, directory.FullName, directory.Name);
                            reportString = $"\"{directory.Name}\",\"{path}\",\"TRUE\",\"NONE\"";
                            csv.AppendLine(reportString);
                        }
                        catch (Exception ex)
                        {
                            reportString = $"\"{directory.Name}\",\"{path}\",\"FALSE\",\"{ex.Message}\"";
                            csv.AppendLine(reportString);
                        }
                    }
                }

                var files = info.EnumerateFiles();

                if (null != files) 
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            Security.AddFilePermissions(file.FullName, AppSettings.AdminGroupName);
                            reportString = $"\"{file.Name}\",\"{file.FullName}\",\"TRUE\",\"NONE\"";
                            csv.AppendLine(reportString);
                        }
                        catch(Exception ex)
                        {
                            reportString = $"\"{file.Name}\",\"{file.FullName}\",\"FALSE\",\"{ex.Message}\"";
                            csv.AppendLine(reportString);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                reportString = $"\"{name}\",\"{path}\",\"FALSE\",\"{ex.Message}\"";
                csv.AppendLine(reportString);
            }
        }

        private async void FixesHidden(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "Fixing hidden files....";
            Mouse.OverrideCursor = Cursors.Wait;

            await Task.Run(() => 
            {
                string scanPath = LastScannedPath.Replace(@"\", "-").Replace(":", "").Replace(" ",  "");
                string reportName = $"{Environment.MachineName}_{scanPath}_FixesHidenFilesLog.csv";
                string reportPath = $"{ReportPathRoot}{reportName}";
                var list = AllViewsList.Where(x => x.Hidden == true).ToList();

                var csv = new StringBuilder();
                string reportString = $"\"Name\",\"File Path\",\"Hidden Changed\",\"Error\"";
                csv.AppendLine(reportString);

                foreach (var view in list)
                {
                    if (view.GetType() == typeof(FileView))
                    {
                        try
                        {
                            var info = new FileInfo(view.Path);
                            info.Attributes = info.Attributes & ~FileAttributes.Hidden;

                            reportString = $"\"{view.Name}\",\"{view.Path}\",\"TRUE\",\"NONE\"";
                            csv.AppendLine(reportString);
                        }
                        catch (Exception ex)
                        {
                            reportString = $"\"{view.Name}\",\"{view.Path}\",\"FALSE\",\"{ex.Message}\"";
                            csv.AppendLine(reportString);
                        }
                    }
                    else if (view.GetType() == typeof(FolderView))
                    {
                        try
                        {
                            var info = new DirectoryInfo(view.Path);
                            info.Attributes = info.Attributes & ~FileAttributes.Hidden;

                            reportString = $"\"{view.Name}\",\"{view.Path}\",\"TRUE\",\"NONE\"";
                            csv.AppendLine(reportString);
                        }
                        catch (Exception ex)
                        {
                            reportString = $"\"{view.Name}\",\"{view.Path}\",\"FALSE\",\"{ex.Message}\"";
                            csv.AppendLine(reportString);
                        }
                    }
                }

                WriteToFile(csv.ToString(), reportPath);
            });

            txtStatus.Text = "";
            Mouse.OverrideCursor = null;
        }

        private void WriteToFile(string content, string path)
        {
            if (!File.Exists(path)) 
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var window = new PopupWindow("Cant Delete Existing Report", $"Path is locked {path}");

                        window.ShowDialog();
                    });
                }
                catch (UnauthorizedAccessException ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var window = new PopupWindow("Cant Delete Existing Report", $"Access denied to: {path}");

                        window.ShowDialog();
                    });
                }
                catch
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var window = new PopupWindow("Cant Delete Existing Report", $"Unhandled exception occurred, please check you can access the reports folder and it is not locked");

                        window.ShowDialog();
                    });
                }
                finally
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch { }
                }
            }

            try
            {
                File.WriteAllText(path, content);
            }
            catch (IOException ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var window = new PopupWindow("Cant Save Report File", $"Path is locked: {path}");

                    window.ShowDialog();
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var window = new PopupWindow("Cant Save Report File", $"The following file path is read only: {path}");

                    window.ShowDialog();
                });
            }
            catch (SecurityException ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var window = new PopupWindow("Cant Save Report File", $"Access denied to: {path}");

                    window.ShowDialog();
                });
            }
            catch 
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var window = new PopupWindow("Cant Save Report File", $"Unhandled exception occurred, please check you can access the reports folder and it is not locked");

                    window.ShowDialog();
                });
            }
            finally
            {
                try
                {
                    File.WriteAllText(path, content);
                }
                catch { }
            }
        }

        private void BtnReports_Click(object sender, EventArgs e)
        {
            btnReports.ContextMenu.IsOpen = !btnReports.ContextMenu.IsOpen;
        }

        private void BtnFixes_Click(object sender, EventArgs e) 
        { 
            btnFixes.ContextMenu.IsOpen = !btnFixes.ContextMenu.IsOpen;
        }

        private void BtnSettings_Click(object sender, EventArgs e) 
        {
            var window = new SettingsWindow(AppSettings);
            window.ShowDialog();

            var settingsContent = JsonConvert.SerializeObject(AppSettings);
            WriteToFile(settingsContent, SettingsPath);
        }
    }
}

 