using FileScanner.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace FileScanner.Models
{
    public class Security
    {
        public const int SE_FILE_OBJECT = 1;
        public const int OWNER_SECURITY_INFORMATION = 0x00000001;

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto, EntryPoint = "SetNamedSecurityInfoW")]
        public static extern uint SetNamedSecrutiyInfo(
            string pObjectName,
            int ObjectType,
            uint SecurityInfo,
            IntPtr psidOwner,
            IntPtr psidGroup,
            IntPtr pDacl,
            IntPtr pSacl
        );

        public static IntPtr GetSidFromUsername(string username)
        {
            var ntAccount = new NTAccount(username);
            var sid = (SecurityIdentifier)ntAccount.Translate(typeof(SecurityIdentifier));
            byte[]sidBytes = new byte[sid.BinaryLength];
            sid.GetBinaryForm(sidBytes, 0);

            IntPtr sidPtr = Marshal.AllocHGlobal(sidBytes.Length);
            Marshal.Copy(sidBytes, 0, sidPtr, sidBytes.Length);
            return sidPtr;
        }

        public static void SetFolderOwner(string folderpath, string username)
        {
            PrivilegeEnabler.EnableTakeOwnershipPrivilege();
            var sid = GetSidFromUsername(username);

            var result = Security.SetNamedSecrutiyInfo(
                    folderpath,
                    SE_FILE_OBJECT,
                    OWNER_SECURITY_INFORMATION,
                    sid,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero
                );

            if (result != 0)
            {
                throw new System.ComponentModel.Win32Exception((int)result);
            }

            Marshal.FreeHGlobal(sid);
        }

        public static void AddViewPermissions(View view, string groupName)
        {
            var group = new NTAccount(groupName);

            var fileACL = new FileSystemAccessRule(
                group,
                FileSystemRights.FullControl,
                AccessControlType.Allow
            );

            var folderACL_1 = new FileSystemAccessRule(
                group,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit,
                PropagationFlags.None,
                AccessControlType.Allow
            );

            var folderACL_2 = new FileSystemAccessRule(
               group,
               FileSystemRights.FullControl,
               InheritanceFlags.ObjectInherit,
               PropagationFlags.None,
               AccessControlType.Allow
            );

            var username = WindowsIdentity.GetCurrent().Name;
            Security.SetFolderOwner(view.Path, username);

            if (view.GetType() == typeof(FolderView))
            {
                var info = new DirectoryInfo(view.Path);
                var security = info.GetAccessControl();

                security.AddAccessRule(folderACL_1);
                security.AddAccessRule(folderACL_2);
                info.SetAccessControl(security);
            }
            else if (view.GetType() == typeof(FileView))
            {
                var info = new FileInfo(view.Path);
                var security = info.GetAccessControl();

                security.AddAccessRule(fileACL);
                info.SetAccessControl(security);
            }
        }

        public static void AddFolderPermissions(string path, string groupName)
        {
            var group = new NTAccount(groupName);

            var folderACL_1 = new FileSystemAccessRule(
                group,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit,
                PropagationFlags.None,
                AccessControlType.Allow
            );

            var folderACL_2 = new FileSystemAccessRule(
               group,
               FileSystemRights.FullControl,
               InheritanceFlags.ObjectInherit,
               PropagationFlags.None,
               AccessControlType.Allow
            );

            var username = WindowsIdentity.GetCurrent().Name;
            Security.SetFolderOwner(path, username);

            var info = new DirectoryInfo(path);
            var security = info.GetAccessControl();

            security.AddAccessRule(folderACL_1);
            security.AddAccessRule(folderACL_2);
            info.SetAccessControl(security);
        }

        public static void AddFilePermissions(string path, string groupName)
        {
            var group = new NTAccount(groupName);

            var fileACL = new FileSystemAccessRule(
                group,
                FileSystemRights.FullControl,
                AccessControlType.Allow
            );

            var username = WindowsIdentity.GetCurrent().Name;
            Security.SetFolderOwner(path, username);

            var info = new FileInfo(path);
            var security = info.GetAccessControl();

            security.AddAccessRule(fileACL);
            info.SetAccessControl(security);
        }
    }


    public class PrivilegeEnabler
    {
        private const int SE_PRIVILEGE_ENABLED = 0x00000002;
        private const string SE_TAKE_OWNERSHIP_NAME = "SeTakeOwnershipPrivilege";

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public LUID Luid;
            public int Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();

        const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        const uint TOKEN_QUERY = 0x0008;

        public static void EnableTakeOwnershipPrivilege()
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "OpenProcessToken failed");
            }

            if (!LookupPrivilegeValue(null, SE_TAKE_OWNERSHIP_NAME, out LUID luid))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "LookupPrivilegeValue failed");
            }

            TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SE_PRIVILEGE_ENABLED
            };

            if (!AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "AdjustTokenPrivileges failed");
            }
        }
    }
}
