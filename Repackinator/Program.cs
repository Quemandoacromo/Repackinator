﻿using Repackinator;

var version = "v1.1.2";

if (OperatingSystem.IsWindows())
{
    if (args.Length == 1 && args[0].Equals("Unregister", StringComparison.CurrentCultureIgnoreCase))
    {        
        var result = ShellExtension.UnregisterContext();
        if (result)
        {
            Console.WriteLine("Context menu removed.");
        }
        else
        {
            Console.WriteLine("Failed to remove context menu (need to run as admin).");
        }
        return;
    }
}

ShellExtension.RegisterContext();

if (args.Length > 0)
{
    ConsoleStartup.Start(version, args);
}
else
{
    ApplicationUIStartup.Start(version);    
}
