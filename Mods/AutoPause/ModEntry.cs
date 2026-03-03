using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus; // 必须引入这个，用于识别菜单类型

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

            this.Monitor.Log($"AutoPause (WebSocket版) 已启动。目标: ws://{this.Config.ServerIP}:{this.Config.ServerPort}/ws", LogLevel.Info);
        }

        /// <summary>
        /// 核心修复：精准判断哪些界面才允许触发暂停
        /// </summary>
        private bool IsMenuValidForPause(IClickableMenu menu)
        {
            if (menu == null) return false;

            // 1. 黑名单：绝对不能暂停的界面
            if (menu is ReadyCheckDialog) return false; // 联机等待界面（如上床睡觉、进节日）
            if (menu is ChatBox) return false;          // 聊天框
            if (menu is ShippingMenu) return false;     // 每日结算出货界面

            // 2. 白名单：只有打开这些界面才触发暂停
            return menu is GameMenu ||           // 主菜单（背包、技能、社交、制作等）
                   menu is ItemGrabMenu ||       // 箱子、物流等物品交互界面
                   menu is ShopMenu ||           // 商店界面
                   menu is DialogueBox ||        // 对话界面（包括 NPC 聊天）
                   menu is QuestLog ||           // 任务日志
                   menu is Billboard ||          // 告示板
                   menu is LetterViewerMenu ||   // 阅读信件
                   menu is CarpenterMenu;        // 罗宾的建筑界面
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (!Context.IsMultiplayer || Context.IsMainPlayer)
                return;

            // 使用新的白名单验证逻辑
            bool shouldPauseNow = IsMenuValidForPause(e.NewMenu);

            if (shouldPauseNow && !_isPausedByMod)
            {
                _ = this.TriggerWebSocketCommand("暂停 (打开有效界面)");
                _isPausedByMod = true;
            }
            // 重点修复：如果切换到了无效界面（如睡觉等待界面），立即恢复游戏
            else if (!shouldPauseNow && _isPausedByMod)
            {
                _ = this.TriggerWebSocketCommand("恢复 (关闭或切换界面)");
                _isPausedByMod = false;
            }
        }

        private async Task TriggerWebSocketCommand(string action)
        {
            using (var ws = new ClientWebSocket())
            {
                try
                {
                    Uri serverUri = new Uri($"ws://{this.Config.ServerIP}:{this.Config.ServerPort}/ws");
                    await ws.ConnectAsync(serverUri, CancellationToken.None);

                    byte[] commandBytes = Encoding.UTF8.GetBytes(this.Config.Command);
                    ArraySegment<byte> bytesToSend = new ArraySegment<byte>(commandBytes);

                    await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);
                    this.Monitor.Log($"[AutoPause] {action} 指令已发送", LogLevel.Info);

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