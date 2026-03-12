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

            // 监听菜单变化 (核心暂停/恢复)
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;

            this.Monitor.Log($"AutoPause 已启动。目标: ws://{this.Config.ServerIP}:{this.Config.ServerPort}/ws", LogLevel.Info);
        }

        private bool IsMenuValidForPause(IClickableMenu menu)
        {
            if (menu == null) return false;

            // 1. 黑名单
            if (menu is ReadyCheckDialog) return false; 
            if (menu is ChatBox) return false;          
            if (menu is ShippingMenu) return false;     

            // 针对吃东西确认框的拦截
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
                   menu is CraftingPage;
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            bool isValidClient = Context.IsMultiplayer && !Context.IsMainPlayer;
            bool shouldPauseNow = isValidClient && IsMenuValidForPause(e.NewMenu);

            if (shouldPauseNow && !_isPausedByMod)
            {
                _ = this.TriggerWebSocketCommand("打开菜单暂停", this.Config.PauseCommand);
                _isPausedByMod = true;
            }
            else if (!shouldPauseNow && _isPausedByMod)
            {
                _ = this.TriggerWebSocketCommand("关闭菜单恢复", this.Config.ResumeCommand);
                _isPausedByMod = false;
            }
        }

        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            if (_isPausedByMod)
            {
                _ = this.TriggerWebSocketCommand("退回标题恢复", this.Config.ResumeCommand);
                _isPausedByMod = false;
            }
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (Context.IsMultiplayer && !Context.IsMainPlayer)
            {
                // 重置本地状态，并强制发送一次恢复命令
                _isPausedByMod = false;
                _ = this.TriggerWebSocketCommand("重新连接发送恢复", this.Config.ResumeCommand);
                this.Monitor.Log("[AutoPause] 玩家刚连入服务器，已执行强制恢复操作。", LogLevel.Info);
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
                    this.Monitor.Log($"[AutoPause] {action} - 发送了指令: {commandStr}", LogLevel.Info);

                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Command Sent", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"[AutoPause] WebSocket 发送失败: {ex.Message}", LogLevel.Error);
                }
            }
        }
    }
}
