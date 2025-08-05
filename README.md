# File Scanner
This is a WPF tool created to assist with various file server maintenence issues or projects such as migrations to SharePoint. When given a directory to scan it will inventory a number of useful facts about the folder tree like the size of files, if they are hidden and what permissions they currently have. Once a file scan is completed you can generate a number of reports into csv files about the data, explore it in the tree view or run some automated fixes. It is recommended to run this tool as Administrator as this allows it to check file permissions where the Administrators group has access. The tool was designed to be quite aggressive on resources in exchange for speed, so be aware that high RAM, CPU and disk usage is expected. 

<br/>
<img width="900" height="550" alt="image" src="https://github.com/user-attachments/assets/9c4e2ff7-1ade-400f-9a98-710215285101" />
<br/><br/>

## Reports
- Personal Drives Report: It is quite common in file server/Active Directory style environments for users to have a personal drive where they store their data, everytime I have experienced this they have been under a folder on the server. This report will attempt to identify the owner of the shared drive by using the Regex filters set in the settings and then identify their mail address via LDAP query. This is then used to generate a mapping file which can be used with Microsoft's SharePoint Migration Tool to migrate this data into OneDrive.

<br/>
<img width="900" height="550" alt="image" src="https://github.com/user-attachments/assets/317a6fa2-79e4-47dc-85cf-25c364d00a0d" />
<br/><br/>

- File Length: This will create a csv file of all of the file paths which are longer than 250 characters in the scanned file strcuture, which can cause issues on Windows file shares and impede SharePoint migrations.
- Hidden Files: This will create a list of all hidden files on the server which again can cause issues with SharePoint migrations.
- Access Denied: A list of all folders where the user running the tool does not have access.
- Permissions: A list of all files and folders that were scanned and the groups or users that appear in the access control table for them.
- All Files: A list of all folders and files scanned.

These reports can all be ran simultanously, select any you require and then press the generate button at the bottom of the menu.

## Fixes
Reports of both of these activities are stored in the report path set in the settings of the application.

<br/>
<img width="900" height="550" alt="image" src="https://github.com/user-attachments/assets/fbf75e8f-42bf-4924-8ca2-72adf719a9f3" />
<br/><br/>

- Access Denied: This will attempt to recursively set the user running the tool as owner, and then add a group specified in the settings as having full control to the files/folders that were marked as access denied from the scanning.
- Hidden Files: This will attempt to unhide any hidden files the scan has picked up.

## Settings
The application has a settings Window to allow it to be used in different environments, if you try to run any of the features that require these settings without filling them in you will get an error. The settings are stored in a settings.json file in the same directory as the applications executable. In the below examples I will be using my home domain as an example for the values, which is dank.net or DANKNET depending on where it is featured. All Regex filters should support [Microsoft's standard library](https://learn.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference) for Regex strings. The settings.json will be created automatically if you open the application in a folder where one does not exist, and can be edited directly if you properly format it. 

<br/>
<img width="400" height="600" alt="image" src="https://github.com/user-attachments/assets/fa075f2e-718e-4676-a2b6-47ff924fc6e7" />
<br/><br/>

- Admin Group: The group which you wish to provide with administrator access if you run the access denied fix menu item. Eg: DANKNET\Domain Admins
- Report Path: The file path or share path where you wish to store the logs and reports generated. Eg: \\dankfp01\reportshare$
- LDAP String: The LDAP connection string for your environment. Eg: LDAP://dank.net/DC=dank,DC=net
- Lines Per Report: The number of lines you wish to generate per csv file for the reports, which can be important as Excel has a line limit at which it will stop displaying data.
- Email Filter: Regex string that filters for valid email addresses on the Personal Drives Report. Eg: .*@danknet.uk
- Domain Prefix: Regex string that filters for valid domain users on the Personal Drives Report. Eg: DANKNET\
- Email Attribute: The name of the Active Directory attribute in which your email addresses are stored. Eg: msDS-cloudExtensionAttribute1 or mail
- Admin Group Filter: A list of Regex strings that are used to filter out groups that are on personal drives for the Personal Drive Reports. Some commons ones will be shown below, but you will also want to add ones that are unique to your environment like administrative groups:
  - Domain Admins: (?i).*Domain Admins
  - Any Group With Administrators in the Name: (?i).*Administrators
  - Local System Accounts: (?i)NT AUTHORITY.*
  - The Everyone Group: (?i)Everyone
  - BuiltIn Accounts: (?i)BUILTIN.*
  - Unmapped SIDs: (?i)S-1-.*
  - Creator Owner: (?i)CREATOR OWNER
