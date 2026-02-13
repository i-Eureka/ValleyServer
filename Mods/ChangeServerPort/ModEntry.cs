
using HarmonyLib;
using Lidgren.Network;
using StardewValley;



namespace StardewModdingAPI;



public class ModEntry: Mod
{   
    static ModEntry instance;
    public ModConfig config;
    public override void Entry(IModHelper helper)

    {   
        instance = this;
        config = this.Helper.ReadConfig<ModConfig>();

        //int oldPort = this.Helper.Reflection.GetField<int>(typeof(LidgrenServer),"defaultPort").GetValue();
        //Monitor.Log($"Old port is {oldPort}", LogLevel.Info);

        var harmony = new Harmony("bilibili.Lixeer.changeserverport");
        harmony.Patch(
                original: AccessTools.Method(typeof(NetPeerConfiguration), "set_Port"), 
                prefix: new HarmonyMethod(typeof(ModEntry), nameof(SetPortPrefix))     
                
        );       

    }
    public static bool SetPortPrefix(ref int value)
    {
        value = instance.config.port;           
        instance.Monitor.Log($"New port is {value}", LogLevel.Info);
        return true; // 继续执行原始方法
    }

}
