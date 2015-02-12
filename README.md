SharePoint Migrator
=========
This a tool was built to migrate all data from a local folder, into the Shared Documents folder of a SharePoint team site. 

It is not limited to a number of files or folders like SkyDrive is. Bear in mind that you are still limited by your SharePoint server.

It will also remove around any characters that are not allowed by SharePoint for example #%*:<>?|/. These are removed for the online copy only, the local copy should remain unchanged. 

Warnings
=========
* Please backup your data; both on the SharePoint server and your local server. I am not responsible for any data that may be lost using this tool.
* The tool was designed to migrate data assuming there isn't any data in the team site. It may be better to clear out existing data.

Requirements
========
[SharePoint 2013 SDK](http://www.microsoft.com/en-us/download/details.aspx?id=30722)