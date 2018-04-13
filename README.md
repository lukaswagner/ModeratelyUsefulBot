# ModeratelyUsefulBot

Just a simple telegram bot. Well, at this point, it's more of a bot framework.

## Setup

If you want to run this bot (why would you, there's pretty much no functionality), you need to set up the dotnet SDK (on windows, install Visual Studio with the C# packages, on linux you'll have to help yourself). Next, add a credentials.xml in the data folder (see exampleCredentials.xml), edit the config.xml and run `dotnet publish -r [platform] -c "Release"`. Move the results to wherever you want to run the bot and start it.  
If you want to deploy the bot on a Raspberry Pi, you can use the `publish.bat` script, it builds the code for linux-arm and moves it to a given host using sftp. You'll need to have psftp installed (part of the PuTTY suite). Call the script like this: `.\publish.bat [host] [port] [username] [password]`.

Note: if you want to use the restart functionality, you have to start the bot using a script and check its return value. If it returns 1, it wants to be restarted. See the `start.sh` script for reference.

## About the config files

The config is split in two xml files: the config and the credentials. This was done in order to be able to share a working config file without sharing the credentials as well. It also means that the config can be tracked using git and updated frequently, while the credentials shouldn't change often. If you want to run multiple bots, their order in the config/credentials files has to be the same for the settings to be correctly loaded.

The config reader is set up to automatically choose a different config file while debugging, in order to not disturb an already running bot by using the same tokens while developing. It checks if the DEBUG symbol is defined (aka if you're debugging in Visual Studio) and uses the debugConfig.xml and the debugCredentials.xml instead of the regular ones.

## Contributing

You actually want to use this and add functionality, or just have ideas on how to make this thing better? Any issues, forks, pull requests, whatever are welcome.