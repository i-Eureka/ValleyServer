namespace ServerCMD;

using System;
using System.IO;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        helper.ConsoleCommands.Add("ServerCMD.load_save", "加载存档\n\nUsage: load_save <save_name>\n- value: 字符串，存档名称", this.LoadSave);
        helper.ConsoleCommands.Add("ServerCMD.set_multiplayermode", "设置多人联机模式\n\nUsage: set_multiplayermode <mode_id>\n- mode_id: 整型[0,1,2] 0:关闭，1:本地联机，2:网络联机", this.SetMultiplayerMode);
        helper.ConsoleCommands.Add("ServerCMD.get_save_list", "获取存档列表\n\nUsage: get_save_list", this.GetSaveList);
        helper.ConsoleCommands.Add("ServerCMD.get_save_path", "获取存档路径\n\nUsage: new_save", this.GetSavePath);
        helper.ConsoleCommands.Add("ServerCMD.new_save", "创建新存档\n\nUsage: new_save", this.NewSave);
        helper.ConsoleCommands.Add("ServerCMD.tp", "传送主机", this.HandleTPCommand);
    }
    private void HandleTPCommand(string command, string[] args)
    {
        Monitor.Log("未完成的功能", LogLevel.Info);
    }

    private void NewSave(string arg1, string[] arg2)
    { 
        new TitleMenu().createdNewCharacter(true);
    }

    private void GetSavePath(string arg1, string[] arg2)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Saves");
        Monitor.Log("Save Path: " + path, LogLevel.Info);

    }

    private void GetSaveList(string command, string[] arg2)
    {   
        
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Saves");
        var directories = Directory.GetDirectories(path);
        Monitor.Log("\nSave List:",LogLevel.Info);
        foreach (string directory in directories)
        {
            string save_name = Path.GetFileName(directory);
            Console.WriteLine(save_name);
        }
    }

    private void SetMultiplayerMode(string command, string[] arg2)
    {
        if (arg2.Length < 1)
        {
            Monitor.Log("Invalid arguments. Usage: set_multiplayermode <mode_id>", LogLevel.Info);
            return;
        }
        int mode_id = int.Parse(arg2[0]);
        if (mode_id < 0 || mode_id > 2)
        {
            Monitor.Log("Invalid mode_id. Valid values are 0, 1 or 2.", LogLevel.Info);
            return;
        }
        Game1.multiplayerMode = byte.Parse(arg2[0]);
        
    }

    private void LoadSave(string command, string[] args)
    {   
        if (args.Length < 1)
        {
            Monitor.Log("Invalid arguments. Usage: load_save <save_name>", LogLevel.Info);
            return;
        }
        string save_name = args[0];
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StardewValley", "Saves", save_name);
        

        if (!Directory.Exists(path))
        {
            Monitor.Log("Attempted to load a save that doesn't seem to exist.", LogLevel.Warn);
            return;
        }

        try
		{   
            //Game1.multiplayerMode = 2;
			base.Monitor.Log("Loading Save: " + save_name, LogLevel.Info);
			SaveGame.Load(save_name);
            
			TitleMenu titleMenu;
			if ((titleMenu = Game1.activeClickableMenu as TitleMenu) != null)
			{
				titleMenu.exitThisMenu(false);
			}
		}
		catch (Exception ex)
		{
			base.Monitor.Log("Load Failed", LogLevel.Error);
			base.Monitor.Log(ex.Message, LogLevel.Debug);
		}
    }
}