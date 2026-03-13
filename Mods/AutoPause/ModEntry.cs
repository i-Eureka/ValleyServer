using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace AutoPause
{
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private bool _isPausedByMod = false;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            helper.WriteConfig(this.Config);

            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;

            this.Monitor.Log($"AutoPause 已启动。目标: ws://{this.Config.ServerIP}:{this.Config.ServerPort}/ws", LogLevel.Info);
        }

        private bool IsMenuValidForPause(IClickableMenu menu)
        {
            if (menu == null) return false;
            if (Game1.fadeToBlack) return false;

            // 1. 黑名单
            if (menu is ReadyCheckDialog) return false; 
            if (menu is ChatBox) return false;          
            if (menu is ShippingMenu) return false;     

            if (menu is DialogueBox)
            {
                if (Game1.player.itemToEat != null) return false;
                return true;
            }

            // 修复在建筑界面或动物放置界面不能恢复游戏的问题
            if (menu is CarpenterMenu carpenterMenu)
            {
                bool isFrozen = this.Helper.Reflection.GetField<bool>(carpenterMenu, "freeze", false)?.GetValue() ?? false;
                if (isFrozen) return false; 
            }
            if (menu is PurchaseAnimalsMenu animalMenu)
            {
                bool isFrozen = this.Helper.Reflection.GetField<bool>(animalMenu, "freeze", false)?.GetValue() ?? false;
                if (isFrozen) return false; 
            }
            
            // 新增：第三方 Mod 界面兼容
            string menuFullName = menu.GetType().FullName;
            if (!string.IsNullOrEmpty(menuFullName))
            {
                // 兼容 Lookup Anything
                if (menuFullName.StartsWith("Pathoschild.Stardew.LookupAnything"))
                    return true;
                //  Generic Mod Config Menu
                // if (menuFullName.StartsWith("GenericModConfigMenu")) return true;
            }

            // 2. 白名单
            return menu is GameMenu ||              
                   menu is ItemGrabMenu ||          
                   menu is ShopMenu ||              
                   menu is PurchaseAnimalsMenu ||   
                   menu is GeodeMenu ||             
                   menu is MuseumMenu ||            
                   menu is QuestLog ||              
                   menu is Billboard ||             
                   menu is LetterViewerMenu ||      
                   menu is CarpenterMenu ||         
                   menu is JunimoNoteMenu ||        
                   menu is CraftingPage ||
                   menu is NamingMenu ||
                   menu is TailoringMenu ||
                   menu is ForgeMenu;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            bool isValidClient = Context.IsMultiplayer && !Context.IsMainPlayer;
            bool shouldPauseNow = isValidClient && IsMenuValidForPause(Game1.activeClickableMenu);

            if (shouldPauseNow && !_isPausedByMod)
            {
                _ = this.TriggerWebSocketCommand("打开菜单", this.Config.PauseCommand);
                _isPausedByMod = true;
            }
            else if (!shouldPauseNow && _isPausedByMod)
            {
                _ = this.TriggerWebSocketCommand("关闭或过渡", this.Config.ResumeCommand);
                _isPausedByMod = false;
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                _isPausedByMod = false;
                _ = this.TriggerWebSocketCommand("重连恢复游戏", this.Config.ResumeCommand);
            }
        }

        private async Task TriggerWebSocketCommand(string action, string commandStr)
        {
            using (var ws = new ClientWebSocket())
            {
                try
                {
                    Uri serverUri = new Uri($"ws://{this.Config.ServerIP}:{this.Config.ServerPort}/ws");
                    await ws.ConnectAsync(serverUri, CancellationToken.None);

                    byte[] commandBytes = Encoding.UTF8.GetBytes(commandStr);
                    ArraySegment<byte> bytesToSend = new ArraySegment<byte>(commandBytes);

                    await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);
                    this.Monitor.Log($"[AutoPause] {action} - 指令: {commandStr}", LogLevel.Info);

                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Command Sent", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"[AutoPause] WebSocket 失败: {ex.Message}", LogLevel.Error);
                }
            }
        }
    }
}
