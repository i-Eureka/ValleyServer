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

            helper.Events.Display.MenuChanged += this.OnMenuChanged;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;

            this.Monitor.Log($"AutoPause 已启动。目标: ws://{this.Config.ServerIP}:{this.Config.ServerPort}/ws", LogLevel.Info);
        }

        private bool IsMenuValidForPause(IClickableMenu menu)
        {
            if (menu == null) return false;

            // 1. 黑名单
            if (menu is ReadyCheckDialog) return false; 
            if (menu is ChatBox) return false;          
            if (menu is ShippingMenu) return false;     

            // 吃东西过滤
            if (menu is DialogueBox)
            {
                if (Game1.player.itemToEat != null) return false;
                return true;
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
                   menu is CarpenterMenu;           
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            bool isValidClient = Context.IsMultiplayer && !Context.IsMainPlayer;
            bool shouldPauseNow = isValidClient && IsMenuValidForPause(e.NewMenu);

            if (shouldPauseNow && !_isPausedByMod)
            {
                // 发送暂停指令 (alos.pause)
                _ = this.TriggerWebSocketCommand("暂停游戏", this.Config.PauseCommand);
                _isPausedByMod = true;
            }
            else if (!shouldPauseNow && _isPausedByMod)
            {
                // 发送恢复指令 (alos.start)
                _ = this.TriggerWebSocketCommand("恢复游戏", this.Config.ResumeCommand);
                _isPausedByMod = false;
            }
        }

        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            if (_isPausedByMod)
            {
                // 退出游戏时发送一次恢复指令，避免客机在暂停页面退出游戏时，游戏仍处于暂停状态
                _ = this.TriggerWebSocketCommand("退回主界面发送恢复游戏指令", this.Config.ResumeCommand);
                _isPausedByMod = false;
            }
        }

        // 新增了一个参数 commandStr，用来动态接收要发送的指令文本
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
                    this.Monitor.Log($"[AutoPause] {action} 指令 ({commandStr}) 已发送", LogLevel.Info);

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
