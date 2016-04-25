# SysLogToElmahIo

This program will send your IIS ASP.NET System Log to Elmah.Io
1) Setup a log in Elmah.Io, take the guid and put it in the .config files as applicable
2) Change the values in Application_ASP.NET 4.0.30319.0.xml to your specific paths and usernames, drop the complied application on your server
3) Import Application_ASP.NET 4.0.30319.0.xml in scheduled tasks

This can be used as a starting point to send any events in Windows Event Viewer to Elmah.Io
