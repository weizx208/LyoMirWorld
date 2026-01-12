using MirCommon;
using MirCommon.Network;
using MirCommon.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Player = GameServer.HumanPlayer;

namespace GameServer
{
    public partial class GameClient
    {
        private async Task HandleWalkMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 走路2");
            int x = (int)(msg.dwFlag & 0xFFFF);
            int y = (int)((msg.dwFlag >> 16) & 0xFFFF);
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            bool success = _player.WalkXY((ushort)x, (ushort)y);
            if (!success)
            {
                success = _player.Walk((Direction)dir);
            }

            if (success)
            {
                byte[] viewPayload = new byte[8];
                Buffer.BlockCopy(BitConverter.GetBytes(_player.GetFeather()), 0, viewPayload, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(_player.GetStatus()), 0, viewPayload, 4, 4);

                var walkMsg = new MirCommon.MirMsgOrign
                {
                    dwFlag = _player.ObjectId,
                    wCmd = MirCommon.ProtocolCmd.SM_WALK,
                    wParam = new ushort[3] { (ushort)_player.X, (ushort)_player.Y, (ushort)_player.Direction },
                };
                byte[] encodedWalk = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(walkMsg, viewPayload);
                _player.CurrentMap?.SendToNearbyPlayers(_player.X, _player.Y, 18, encodedWalk, _player.ObjectId);
            }

            
            int ackX = success ? _player.ActionX : _player.X;
            int ackY = success ? _player.ActionY : _player.Y;
            if (ackX == 0 && ackY == 0)
            {
                ackX = _player.X;
                ackY = _player.Y;
            }
            SendActionResult(ackX, ackY, success);

            await Task.CompletedTask;
        }

        private async Task HandleRunMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 跑步");
            int x = (int)(msg.dwFlag & 0xFFFF);
            int y = (int)((msg.dwFlag >> 16) & 0xFFFF);
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            
            bool success = _player.Run((Direction)dir);
            if (!success)
            {
                success = _player.RunXY((ushort)x, (ushort)y);
            }
            if (!success)
            {
                success = _player.Walk((Direction)dir);
            }

            if (success)
            {
                byte[] viewPayload = new byte[8];
                Buffer.BlockCopy(BitConverter.GetBytes(_player.GetFeather()), 0, viewPayload, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(_player.GetStatus()), 0, viewPayload, 4, 4);

                var runMsg = new MirCommon.MirMsgOrign
                {
                    dwFlag = _player.ObjectId,
                    wCmd = MirCommon.ProtocolCmd.SM_RUN,
                    wParam = new ushort[3] { (ushort)_player.X, (ushort)_player.Y, (ushort)_player.Direction },
                };
                byte[] encodedRun = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(runMsg, viewPayload);
                _player.CurrentMap?.SendToNearbyPlayers(_player.X, _player.Y, 18, encodedRun, _player.ObjectId);
            }

            int ackX = success ? _player.ActionX : _player.X;
            int ackY = success ? _player.ActionY : _player.Y;
            if (ackX == 0 && ackY == 0)
            {
                ackX = _player.X;
                ackY = _player.Y;
            }
            SendActionResult(ackX, ackY, success);

            await Task.CompletedTask;
        }

        private async Task HandleSayMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            try
            {
                string message = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
                LogManager.Default.Info($"处理客户端[{_player.Name}] 说话: {message}");

                if (!string.IsNullOrWhiteSpace(message) && message[0] == '@')
                {
                    string cmdLine = message.Length > 1 ? message.Substring(1).Trim() : string.Empty;
                    if (cmdLine.Length == 0)
                        return;

                    GmManager.Instance.ExecGameCmd(cmdLine, _player);
                    return;
                }

                
                var map = _world.GetMap(_player.MapId);
                if (map != null)
                {
                    var nearbyPlayers = map.GetPlayersInRange(_player.X, _player.Y, 15);
                    foreach (var nearbyPlayer in nearbyPlayers)
                    {
                        if (nearbyPlayer != null)
                        {
                            
                            SendChatMessage(nearbyPlayer, _player.Name, message);
                        }
                    }
                }
            }
            catch { }

            await Task.CompletedTask;
        }

        private async Task HandleTurnMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 转向");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            
            bool success = _player.Turn((Direction)dir);

            if (success)
            {
                
                
                byte[] appearPayload = new byte[12];
                Buffer.BlockCopy(BitConverter.GetBytes(_player.GetFeather()), 0, appearPayload, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(_player.GetStatus()), 0, appearPayload, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(_player.GetHealth()), 0, appearPayload, 8, 4);

                var turnMsg = new MirCommon.MirMsgOrign
                {
                    dwFlag = _player.ObjectId,
                    wCmd = MirCommon.ProtocolCmd.SM_APPEAR,
                    wParam = new ushort[3] { (ushort)_player.X, (ushort)_player.Y, (ushort)_player.Direction },
                };
                byte[] encodedTurn = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(turnMsg, appearPayload);
                _player.CurrentMap?.SendToNearbyPlayers(_player.X, _player.Y, 18, encodedTurn, _player.ObjectId);

            }

            
            int ackX = success ? _player.ActionX : _player.X;
            int ackY = success ? _player.ActionY : _player.Y;
            if (ackX == 0 && ackY == 0)
            {
                ackX = _player.X;
                ackY = _player.Y;
            }
            SendActionResult(ackX, ackY, success);

            await Task.CompletedTask;
        }

        private async Task HandleAttackMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 攻击");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            
            bool success = _player.Attack(dir);

            if (success)
            {
                
                
                byte[] attackPayload = new byte[8];
                Buffer.BlockCopy(BitConverter.GetBytes(_player.GetFeather()), 0, attackPayload, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(_player.GetStatus()), 0, attackPayload, 4, 4);

                var attackMsg = new MirCommon.MirMsgOrign
                {
                    dwFlag = _player.ObjectId,
                    wCmd = MirCommon.ProtocolCmd.SM_ATTACK,
                    wParam = new ushort[3] { (ushort)_player.X, (ushort)_player.Y, (ushort)_player.Direction },
                };
                byte[] encodedAttack = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(attackMsg, attackPayload);
                _player.CurrentMap?.SendToNearbyPlayers(_player.X, _player.Y, 18, encodedAttack, _player.ObjectId);

            }

            
            SendActionResult(_player.X, _player.Y, success);

            await Task.CompletedTask;
        }


        private async Task HandleGetMealMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 挖肉");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            
            bool success = _player.GetMeal(dir);

            
            SendActionResult(_player.X, _player.Y, success);

            await Task.CompletedTask;
        }

        private async Task HandleStopMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 停止移动");

            
            var stopMsg = new MirCommon.MirMsgOrign
            {
                dwFlag = _player.ObjectId,
                wCmd = MirCommon.ProtocolCmd.SM_STOP,
                wParam = new ushort[3] { (ushort)_player.X, (ushort)_player.Y, (ushort)_player.Direction },
            };
            byte[] encodedStop = MirCommon.Network.GameMessageHandler.EncodeGameMessageOrign(stopMsg);
            _player.CurrentMap?.SendToNearbyPlayers(_player.X, _player.Y, 18, encodedStop, _player.ObjectId);

            

            await Task.CompletedTask;
        }


        private async Task HandleConfirmFirstDialog(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 确认第一个对话框");

            
            _state = ClientState.GSUM_VERIFIED;
            LogManager.Default.Info($"已设置状态为GSUM_VERIFIED");

            
            if (!GameWorld.Instance.AddMapObject(_player))
            {
                
                return;
            }

            
            var dbClient = _server.GetDbServerClient();
            if (dbClient == null)
            {
                SendMsg(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "数据库连接未初始化！");
                return;
            }

            
            uint serverId = 1;

            
            LogManager.Default.Info($"向DBServer发送 DM_QUERYMAGIC 查询技能数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryMagic(serverId, _clientKey, _player.GetDBId());

            
            LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询装备数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_EQUIPMENT, 20);

            
            LogManager.Default.Info($"向DBServer发送 DM_QUERYUPGRADEITEM 查询升级物品数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryUpgradeItem(serverId, _clientKey, _player.GetDBId());

            
            
            int bagLimit = 40; 
            if (_player.GetBag() != null)
            {
                
                
                bagLimit = 40; 
            }
            LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询背包数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_BAG, bagLimit);

            
            LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询仓库数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_BANK, 100);

            
            LogManager.Default.Info($"向DBServer发送 DM_QUERYITEMS 查询宠物仓库数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.SendQueryItem(serverId, _clientKey, _player.GetDBId(), (byte)ItemDataFlag.IDF_PETBANK, 10);

            
            LogManager.Default.Info($"向DBServer发送 DM_QUERYTASKINFO 查询任务数据：{serverId}-{_clientKey}-{_player.GetDBId()}");
            await dbClient.QueryTaskInfo(serverId, _clientKey, _player.GetDBId());

            
            if (_player.IsFirstLogin)
            {
                LogManager.Default.Info($"向客户端发送 EP_FIRSTLOGINPROCESS");
                _player.AddProcess(EP_FIRSTLOGINPROCESS, 0, 0, 0, 0, 20, 1, null);
            }

            
            
            
            
            LogManager.Default.Info($"已处理确认第一个对话框，等待数据库响应: {_player.Name}");

            await Task.CompletedTask;
        }

        private async Task HandleSelectLink(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            string link = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 选择链接: {link}");

            
            if (!NpcScriptEngine.TryHandleSelectLink(_player, (uint)msg.dwFlag, link))
            {
                
                NPCMessageHandler.HandleSelectLink(_player, (uint)msg.dwFlag, link);
            }

            await Task.CompletedTask;
        }

        private async Task HandleTakeOnItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            int pos = msg.wParam[0];
            uint makeIndex = msg.dwFlag;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 穿戴物品 位置:{pos} 物品ID:{makeIndex}");

            bool ok = false;
            try
            {
                if (pos < 0 || pos >= (int)EquipSlot.Max)
                {
                    ok = false;
                }
                else
                {
                    var oldEquipped = _player.Equipment.GetItem((EquipSlot)pos);
                    var item = _player.Inventory.GetItemByMakeIndex(makeIndex);
                    if (item == null)
                    {
                        ok = false;
                    }
                    else
                    {
                        ok = _player.Equipment.Equip((EquipSlot)pos, item);
                        if (ok)
                        {
                            
                            _player.Inventory.RemoveItemByMakeIndex(makeIndex, 1);

                            
                            
                            if (oldEquipped != null && oldEquipped.GetMakeIndex() != makeIndex)
                            {
                                _player.SendAddBagItem(oldEquipped);
                            }
                            SendWeightChangedMessage();

                            
                            var dbClient = _server.GetDbServerClient();
                            if (dbClient != null)
                            {
                                await dbClient.SendUpdateItemPos(makeIndex, (byte)ItemDataFlag.IDF_EQUIPMENT, (ushort)pos);

                                if (oldEquipped != null && oldEquipped.GetMakeIndex() != makeIndex)
                                {
                                    
                                    int oldSlot = _player.Inventory.FindSlotByMakeIndex(oldEquipped.GetMakeIndex());
                                    ushort bagPos = (ushort)(oldSlot >= 0 ? oldSlot : 0);
                                    await dbClient.SendUpdateItemPos(oldEquipped.GetMakeIndex(), (byte)ItemDataFlag.IDF_BAG, bagPos);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理穿戴物品失败: player={_player.Name}, pos={pos}, item={makeIndex} - {ex.Message}");
                ok = false;
            }

            SendEquipItemResult(ok, pos, makeIndex);
            await Task.CompletedTask;
        }

        private async Task HandleTakeOffItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            int pos = msg.wParam[0];
            uint makeIndex = msg.dwFlag;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 脱下物品 位置:{pos} 物品ID:{makeIndex}");

            bool ok = false;
            try
            {
                if (pos < 0 || pos >= (int)EquipSlot.Max)
                {
                    ok = false;
                }
                else
                {
                    var equipped = _player.Equipment.GetItem((EquipSlot)pos);
                    if (equipped == null)
                    {
                        ok = false;
                    }
                    else
                    {
                        
                        if (makeIndex != 0 && equipped.GetMakeIndex() != makeIndex)
                        {
                            ok = false;
                        }
                        else
                        {
                            var unEquipped = _player.Equipment.Unequip((EquipSlot)pos);
                            ok = unEquipped != null;
                            if (ok)
                            {
                                
                                _player.SendAddBagItem(unEquipped!);
                                SendWeightChangedMessage();

                                
                                uint realMakeIndex = unEquipped!.GetMakeIndex();
                                var dbClient = _server.GetDbServerClient();
                                if (dbClient != null && realMakeIndex != 0)
                                {
                                    int bagSlot = _player.Inventory.FindSlotByMakeIndex(realMakeIndex);
                                    ushort bagPos = (ushort)(bagSlot >= 0 ? bagSlot : 0);
                                    await dbClient.SendUpdateItemPos(realMakeIndex, (byte)ItemDataFlag.IDF_BAG, bagPos);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理脱下物品失败: player={_player.Name}, pos={pos}, item={makeIndex} - {ex.Message}");
                ok = false;
            }

            SendUnEquipItemResult(ok, pos, makeIndex);
            await Task.CompletedTask;
        }

        private async Task HandleDropItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            uint makeIndex = msg.dwFlag;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 丢弃物品 物品ID:{makeIndex}");

            bool ok = false;
            try
            {
                var map = _player.CurrentMap;
                if (map == null)
                {
                    ok = false;
                }
                else
                {
                    var item = _player.Inventory.GetItemByMakeIndex(makeIndex);
                    if (item == null)
                    {
                        ok = false;
                    }
                    else
                    {
                        
                        if (!_player.Inventory.RemoveItemByMakeIndex(makeIndex, 1))
                        {
                            ok = false;
                        }
                        else
                        {
                            ok = DownItemMgr.Instance.HumanDropItem(map, item, (ushort)_player.X, (ushort)_player.Y, _player);
                            if (!ok)
                            {
                                _player.Inventory.AddItem(item);
                            }
                            else
                            {
                                SendWeightChangedMessage();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理丢弃物品失败: player={_player.Name}, item={makeIndex} - {ex.Message}");
                ok = false;
            }

            SendDropItemResult(ok, makeIndex);
            await Task.CompletedTask;
        }

        private async Task HandlePickupItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            uint makeIndex = msg.dwFlag;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 拾取物品 makeIndex:{makeIndex}");

            bool ok = false;
            try
            {
                var map = _player.CurrentMap;
                if (map != null)
                {
                    DownItemObject? target = null;

                    
                    var objects = map.GetObjectsInRange(_player.X, _player.Y, 2);
                    foreach (var obj in objects)
                    {
                        if (obj is not DownItemObject down)
                            continue;

                        if (makeIndex != 0)
                        {
                            if (down.GetItem()?.GetMakeIndex() == makeIndex)
                            {
                                target = down;
                                break;
                            }
                        }
                        else
                        {
                            if (down.X == _player.X && down.Y == _player.Y)
                            {
                                target = down;
                                break;
                            }
                            target ??= down;
                        }
                    }

                    if (target != null)
                    {
                        bool isGold = target.IsGold();
                        var pickedItem = target.GetItem();

                        ok = DownItemMgr.Instance.PickupItem(map, target, _player);
                        if (ok)
                        {
                            
                            if (!isGold && pickedItem != null)
                            {
                                _player.SendAddBagItem(pickedItem);
                            }
                            SendWeightChangedMessage();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理拾取物品失败: player={_player.Name}, item={makeIndex} - {ex.Message}");
                ok = false;
            }

            SendPickupItemResult(ok);
            await Task.CompletedTask;
        }

        
        private async Task HandleSpellSkill(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 技能施放");
            
            int x = (int)(msg.dwFlag & 0xFFFF);
            int y = (int)((msg.dwFlag >> 16) & 0xFFFF);
            uint magicId = (uint)msg.wParam[0];
            ushort targetId = (ushort)msg.wParam[1];

            
            bool success = _player.SpellCast(x, y, magicId, targetId);

            
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleQueryTrade(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            
            uint targetId = msg.dwFlag;
            var target = HumanPlayerMgr.Instance.FindById(targetId);
            if (target == null)
            {
                _player.SaySystem("对方不在线，无法交易！");
                return;
            }

            
            int dx = Math.Abs(_player.X - target.X);
            int dy = Math.Abs(_player.Y - target.Y);
            if (dx > 1 || dy > 1)
            {
                _player.SaySystem("对方距离太远，无法交易！");
                return;
            }

            if (!TradeManager.Instance.StartTrade(_player, target))
            {
                _player.SaySystem("无法开始交易（可能已在交易中）。");
            }

            await Task.CompletedTask;
        }

        private async Task HandlePutTradeItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            
            ulong instanceId = msg.dwFlag;
            var trade = TradeManager.Instance.GetPlayerTrade(_player);
            if (trade == null)
            {
                _player.SaySystem("您现在不在交易状态！");
                return;
            }

            
            var item = _player.Inventory?.GetAllItems()?.Values?.FirstOrDefault(i => (ulong)i.InstanceId == instanceId);
            if (item == null)
            {
                
                
                _player.SaySystem("物品不存在，无法放入交易。");
                _player.SendTradePutItemFail();

                
                SendMsg(0, GameMessageHandler.ServerCommands.SM_PUTTRADEITEMOK, 0, 0, 0);
                return;
            }

            if (!trade.PutItem(_player, item))
            {
                _player.SaySystem(trade.ErrorMessage);
                
            }

            
            SendMsg(0, GameMessageHandler.ServerCommands.SM_PUTTRADEITEMOK, 0, 0, 0);
            await Task.CompletedTask;
        }

        private async Task HandlePutTradeGold(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            
            uint amount = msg.dwFlag;
            var type = (MoneyType)msg.wParam[0];

            var trade = TradeManager.Instance.GetPlayerTrade(_player);
            if (trade == null)
            {
                _player.SaySystem("您现在不在交易状态！");
                return;
            }

            bool ok = trade.PutMoney(_player, type, amount);
            if (!ok)
            {
                _player.SaySystem(trade.ErrorMessage);
                SendMsg(0, GameMessageHandler.ServerCommands.SM_PUTTRADEGOLDFAIL, 0, 0, (ushort)type);
                return;
            }

            
            
            
            
            uint curMoney = type == MoneyType.Gold ? _player.Gold : _player.Yuanbao;
            SendMsg(amount,
                GameMessageHandler.ServerCommands.SM_PUTTRADEGOLDOK,
                (ushort)(curMoney & 0xffff),
                (ushort)(curMoney >> 16),
                (ushort)type);

            await Task.CompletedTask;
        }

        private async Task HandleQueryTradeEnd(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            var trade = TradeManager.Instance.GetPlayerTrade(_player);
            if (trade == null)
            {
                _player.SaySystem("您现在不在交易状态！");
                return;
            }

            trade.End(_player, TradeEndType.Confirm);
            await Task.CompletedTask;
        }

        private async Task HandleCancelTrade(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            var trade = TradeManager.Instance.GetPlayerTrade(_player);
            if (trade == null)
            {
                _player.SaySystem("您现在不在交易状态！");
                return;
            }

            trade.End(_player, TradeEndType.Cancel);
            await Task.CompletedTask;
        }

        private async Task HandlePing(MirMsgOrign msg, byte[] payload)
        {
            
            LogManager.Default.Info($"处理客户端Ping: {msg.dwFlag}");
            GameMessageHandler.SendSimpleMessage2(_stream, msg.dwFlag, 0x3d4, 0, 0, 0);
            await Task.CompletedTask;
        }

        private async Task HandlePingResponse(MirMsgOrign msg, byte[] payload)
        {
            
            if (_player != null)
                LogManager.Default.Debug($"收到PingResponse/keepalive: player={_player.Name}, flag={msg.dwFlag}");
            await Task.CompletedTask;
        }

        private async Task HandleRideHorse(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 骑马");
            
            GameMessageHandler.SendSimpleMessage2(_stream, 1,
                GameMessageHandler.ServerCommands.SM_RIDEHORSERESPONSE, 0, 0, 0);
            await Task.CompletedTask;
        }

        private async Task HandleUseItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 使用物品");
            uint makeIndex = msg.dwFlag;

            
            _player.UseItem(makeIndex);
            await Task.CompletedTask;
        }

        private async Task HandleDropGold(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 丢弃金币");
            uint amount = msg.dwFlag;

            
            bool success = _player.DropGold(amount);

            
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleNPCTalk(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] NPC对话");
            await Task.CompletedTask;
        }

        private async Task HandleBuyItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            
            uint npcId = msg.dwFlag;
            uint reqMakeIndex = (uint)(msg.wParam[0] | ((uint)msg.wParam[1] << 16));
            string templateName = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0').Trim();

            uint error = 0;
            try
            {
                var target = GameWorld.Instance.GetAliveObjectById(npcId);
                if (target is not Npc npc)
                {
                    error = 0;
                    _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                var scriptObject = NpcScriptEngine.TryGetScriptObject(npc);
                if (scriptObject == null)
                {
                    error = 0;
                    _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                float buyPercent = 1.0f;
                if (npc is NpcInstanceEx ex)
                    buyPercent = ex.Definition.BuyPercent;

                var goods = ScriptNpcShopHelper.ExtractGoods(scriptObject);
                if (!goods.Any(g => string.Equals(g.TemplateName, templateName, StringComparison.OrdinalIgnoreCase)))
                {
                    error = 0;
                    _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                var def = ItemManager.Instance.GetDefinitionByName(templateName);
                if (def == null)
                {
                    error = 0;
                    _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                
                if (_player.Inventory.GetUsedSlots() >= _player.Inventory.MaxSlots)
                {
                    error = 2;
                    _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                uint price = ScriptNpcShopHelper.CalcBuyPrice(def, buyPercent);
                if (_player.Gold < price)
                {
                    
                    error = 3;
                    _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                var item = ItemManager.Instance.CreateItem(def.ItemId, 1);
                if (item == null)
                {
                    error = 0;
                    _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                if (!_player.Inventory.TryAddItemNoStack(item, out _))
                {
                    error = 2;
                    _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                if (!_player.TakeGold(price))
                {
                    error = 3;
                    _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                
                _player.SendAddBagItem(item);
                _player.SendWeightChanged();

                _player.SendMsg(_player.Gold, GameMessageHandler.ServerCommands.SM_BUYITEMOK,
                    (ushort)(reqMakeIndex & 0xFFFF),
                    (ushort)((reqMakeIndex >> 16) & 0xFFFF),
                    0,
                    null);

                LogManager.Default.Info($"购买成功: player={_player.Name}, npc={npc.Name}({npcId:X8}), item={templateName}, price={price}, reqMakeIndex={reqMakeIndex}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理购买物品失败: player={_player.Name}, npc={npcId:X8}, item={templateName}, err={ex.Message}");
                _player.SendMsg(error, GameMessageHandler.ServerCommands.SM_BUYITEMFAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleSellItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            
            uint npcId = msg.dwFlag;
            uint makeIndex = (uint)(msg.wParam[0] | ((uint)msg.wParam[1] << 16));

            try
            {
                var target = GameWorld.Instance.GetAliveObjectById(npcId);
                if (target is not Npc npc)
                {
                    _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_SELLITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                float sellPercent = 0.5f;
                if (npc is NpcInstanceEx ex)
                    sellPercent = ex.Definition.SellPercent;

                var item = _player.Inventory.GetItemByMakeIndex(makeIndex);
                if (item == null)
                {
                    _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_SELLITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                if (item.IsBound || item.Definition == null || !item.Definition.CanTrade)
                {
                    _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_SELLITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                uint price = ScriptNpcShopHelper.CalcSellPrice(item, sellPercent);
                if (!_player.CanAddGold(price))
                {
                    _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_SELLITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                int sellCount = item.Count > 0 ? item.Count : 1;
                if (!_player.Inventory.RemoveItemByMakeIndex(makeIndex, sellCount))
                {
                    _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_SELLITEMFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                _player.AddGold(price);
                _player.SendWeightChanged();

                _player.SendMsg(_player.Gold, GameMessageHandler.ServerCommands.SM_SELLITEMOK, 0, 0, 0, null);
                LogManager.Default.Info($"出售成功: player={_player.Name}, npc={npc.Name}({npcId:X8}), makeIndex={makeIndex}, price={price}");
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理出售物品失败: player={_player.Name}, npc={npcId:X8}, makeIndex={makeIndex}, err={ex.Message}");
                _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_SELLITEMFAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleQueryItemSellPrice(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            uint npcId = msg.dwFlag;
            uint makeIndex = (uint)(msg.wParam[0] | ((uint)msg.wParam[1] << 16));

            try
            {
                var target = GameWorld.Instance.GetAliveObjectById(npcId);
                if (target is not Npc npc)
                {
                    _player.SendMsg(0, GameMessageHandler.ServerCommands.SM_QUERYITEMSELLPRICE, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                float sellPercent = 0.5f;
                if (npc is NpcInstanceEx ex)
                    sellPercent = ex.Definition.SellPercent;

                var item = _player.Inventory.GetItemByMakeIndex(makeIndex);
                uint price = item != null ? ScriptNpcShopHelper.CalcSellPrice(item, sellPercent) : 0u;

                _player.SendMsg(price, GameMessageHandler.ServerCommands.SM_QUERYITEMSELLPRICE, 0, 0, 0, null);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"查询出售价格失败: player={_player.Name}, npc={npcId:X8}, makeIndex={makeIndex}, err={ex.Message}");
                _player.SendMsg(0, GameMessageHandler.ServerCommands.SM_QUERYITEMSELLPRICE, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleQueryItemList(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            
            uint npcId = msg.dwFlag;
            int ptr = msg.wParam[0];
            string templateName = Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0').Trim();

            try
            {
                var target = GameWorld.Instance.GetAliveObjectById(npcId);
                if (target is not Npc npc)
                {
                    _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_QUERYITEMLISTFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                var scriptObject = NpcScriptEngine.TryGetScriptObject(npc);
                if (scriptObject == null)
                {
                    _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_QUERYITEMLISTFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                float buyPercent = 1.0f;
                if (npc is NpcInstanceEx ex)
                    buyPercent = ex.Definition.BuyPercent;

                var goods = ScriptNpcShopHelper.ExtractGoods(scriptObject);
                if (!goods.Any(g => string.Equals(g.TemplateName, templateName, StringComparison.OrdinalIgnoreCase)))
                {
                    _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_QUERYITEMLISTFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                var def = ItemManager.Instance.GetDefinitionByName(templateName);
                if (def == null)
                {
                    _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_QUERYITEMLISTFAIL, 0, 0, 0, null);
                    await Task.CompletedTask;
                    return;
                }

                uint price = ScriptNpcShopHelper.CalcBuyPrice(def, buyPercent);

                
                var temp = new ItemInstance(def, 0)
                {
                    MaxDurability = def.MaxDura > 0 ? def.MaxDura : 100,
                    Durability = def.MaxDura > 0 ? def.MaxDura : 100,
                    Count = 1
                };

                var ic = ItemPacketBuilder.BuildITEMCLIENT(temp);
                ic.dwMakeIndex = 0;
                ic.baseitem.nPrice = (int)Math.Clamp((long)price, int.MinValue, int.MaxValue);

                var arr = new MirCommon.ITEMCLIENT[] { ic };
                byte[] data = StructArrayToBytes(arr);

                _player.SendMsg(npcId, 0x28c, 1, (ushort)Math.Clamp(ptr, 0, ushort.MaxValue), 0, data);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"查询商品列表失败: player={_player.Name}, npc={npcId:X8}, item={templateName}, err={ex.Message}");
                _player.SendMsg(unchecked((uint)-1), GameMessageHandler.ServerCommands.SM_QUERYITEMLISTFAIL, 0, 0, 0, null);
            }

            await Task.CompletedTask;
        }

        private async Task HandleRepairItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 修理");
            uint npcInstanceId = msg.dwFlag;
            int bagSlot = msg.wParam[0];

            
            bool success = _player.RepairItem(npcInstanceId, bagSlot);

            
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleQueryRepairPrice(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 查询修理价格");
            uint npcInstanceId = msg.dwFlag;
            int bagSlot = msg.wParam[0];

            
            bool success = _player.QueryRepairPrice(npcInstanceId, bagSlot);

            
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleQueryMinimap(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            
            LogManager.Default.Info($"处理客户端[{_player.Name}] 地图加载完成/查询小地图(0x409)");

            int miniMapId = 0;
            var map = LogicMapMgr.Instance?.GetLogicMapById((uint)_player.MapId);
            if (map != null)
            {
                miniMapId = map.GetMiniMapId();
            }

            GameMessageHandler.SendSimpleMessage2(
                _stream,
                _player.ObjectId,
                GameMessageHandler.ServerCommands.SM_MINIMAP,
                (ushort)(miniMapId & 0xffff),
                0,
                0);

            
                if (map != null)
                {
                    
                    _player.SendMsg(_player.ObjectId, MirCommon.ProtocolCmd.SM_CLEAROBJECTS, 0, 0, 0);

                    
                    _player.IsMapLoaded = true;

                    
                    try
                    {
                        MonsterGenManager.Instance.EnsureInitialSpawnForMap(_player.MapId);
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Warning($"地图初始刷怪补齐失败: mapId={_player.MapId}, err={ex.Message}");
                    }

                    
                    try
                    {
                        _player.UpdateProp();
                        _player.UpdateSubProp();
                        _player.SendStatusChanged();
                        _player.SendTimeWeatherChanged();

                        ushort curHp = (ushort)Math.Clamp(_player.CurrentHP, 0, ushort.MaxValue);
                        ushort curMp = (ushort)Math.Clamp(_player.CurrentMP, 0, ushort.MaxValue);
                        ushort maxHp = (ushort)Math.Clamp(_player.MaxHP, 0, ushort.MaxValue);
                        _player.SendMsg(_player.ObjectId, MirCommon.ProtocolCmd.SM_HPMPCHANGED, curHp, curMp, maxHp);

                        LogManager.Default.Info($"0x409补发属性完成：hp={curHp}/{maxHp}, mp={curMp}/{_player.MaxMP}");
                    }
                    catch (Exception ex)
                    {
                        LogManager.Default.Error($"0x409补发属性/状态失败: {ex.Message}");
                    }

                    
                    _player.CleanVisibleList();
                    _player.SearchViewRange();

                var objects = map.GetObjectsInRange(_player.X, _player.Y, 18);
                int playerCount = 0, npcCount = 0, monsterCount = 0, itemCount = 0, eventCount = 0, visibleEventCount = 0;
                foreach (var o in objects)
                {
                    switch (o.GetObjectType())
                    {
                        case ObjectType.Player: playerCount++; break;
                        case ObjectType.NPC: npcCount++; break;
                        case ObjectType.Monster: monsterCount++; break;
                        case ObjectType.Item:
                        case ObjectType.DownItem: itemCount++; break;
                        case ObjectType.VisibleEvent: visibleEventCount++; break;
                        case ObjectType.Event: eventCount++; break;
                    }
                }
                LogManager.Default.Info($"0x409入场景对象刷新完成：mapId={_player.MapId}, count={objects.Count}, player={playerCount}, npc={npcCount}, monster={monsterCount}, item={itemCount}, vevent={visibleEventCount}, event={eventCount}");
            }

            await Task.CompletedTask;
        }

        private async Task HandleViewEquipment(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 查看装备");
            uint targetPlayerId = msg.dwFlag;

            
            bool success = _player.ViewEquipment(targetPlayerId);

            
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleMine(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 挖矿");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            
            bool success = _player.DoMine(dir);

            
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleTrainHorse(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 训练马匹");
            byte dir = (byte)(msg.wParam[1] & 0xFF);

            
            bool success = _player.DoTrainHorse(dir);

            
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleSpecialHit(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 特殊攻击");
            byte dir = (byte)(msg.wParam[1] & 0xFF);
            int skillType = msg.wCmd switch
            {
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_KILL => 7,      
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_ASSASSINATE => 12, 
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_HALFMOON => 25,   
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_FIRE => 26,       
                GameMessageHandler.ClientCommands.CM_SPECIALHIT_POJISHIELD => 0,  
                _ => 0
            };

            
            bool success = _player.SpecialHit(dir, skillType);

            
            SendActionResult(_player.X, _player.Y, success);
            await Task.CompletedTask;
        }

        private async Task HandleLeaveServer(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 离开服务器");

            
            Disconnect();

            await Task.CompletedTask;
        }

        private async Task HandleUnknown45(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;

            LogManager.Default.Info($"处理客户端[{_player.Name}] 处理未知命令 0x45");

            
            GameMessageHandler.SendSimpleMessage2(_stream, 0,
                GameMessageHandler.ServerCommands.SM_PINGRESPONSE, 0, 0, 0);

            await Task.CompletedTask;
        }

        
        private async Task HandlePutItemToPetBag(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 放入宠物仓库 物品ID:{msg.dwFlag}");
            
            
            _player.PutItemToPetBag(msg.dwFlag);
            
            await Task.CompletedTask;
        }

        private async Task HandleGetItemFromPetBag(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 从宠物仓库取出 物品ID:{msg.dwFlag}");
            
            
            _player.GetItemFromPetBag(msg.dwFlag);
            
            await Task.CompletedTask;
        }

        private async Task HandleDeleteTask(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 删除任务 任务ID:{msg.dwFlag}");
            
            
            _player.DeleteTask(msg.dwFlag);
            
            await Task.CompletedTask;
        }

        private async Task HandleGMTestCommand(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] GM命令测试");
            
            
            
            LogManager.Default.Info($"GM测试命令: Flag={msg.dwFlag}, wParam0={msg.wParam[0]}, wParam1={msg.wParam[1]}");
            
            await Task.CompletedTask;
        }

        private async Task HandleCompletelyQuit(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 完全退出");
            
            
            _competlyQuit = true;
            
            
            Disconnect();
            
            await Task.CompletedTask;
        }

        private async Task HandleCutBody(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 切割尸体 Flag:{msg.dwFlag}, wParam0:{msg.wParam[0]}, wParam1:{msg.wParam[1]}, wParam2:{msg.wParam[2]}");
            
            
            bool success = _player.CutBody(msg.dwFlag, msg.wParam[0], msg.wParam[1], msg.wParam[2]);
            
            if (success)
            {
                
                
                
                LogManager.Default.Info($"切割尸体成功，发送周围消息");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandlePutItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 放入物品 Flag:{msg.dwFlag}, wParam0:{msg.wParam[0]}");
            
            
            uint param = (uint)((msg.wParam[1] << 16) | msg.wParam[0]);
            _player.OnPutItem(msg.dwFlag, param);
            
            await Task.CompletedTask;
        }

        private async Task HandleShowPetInfo(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 显示宠物信息");
            
            
            _player.ShowPetInfo();
            
            await Task.CompletedTask;
        }

        private async Task HandleMarketMessage(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 市场消息 wParam0:{msg.wParam[0]}, wParam1:{msg.wParam[1]}, wParam2:{msg.wParam[2]}");
            
            
            MarketManager.Instance.OnClientMsg(_player, msg.wParam[0], msg.wParam[1], msg.wParam[2], payload, payload?.Length ?? 0);
            
            await Task.CompletedTask;
        }

        private async Task HandleDeleteFriend(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string friendName = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 删除好友: {friendName}");
            
            
            _player.DeleteFriend(friendName);
            
            await Task.CompletedTask;
        }

        private async Task HandleReplyAddFriendRequest(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string replyData = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 回复添加好友请求 Flag:{msg.dwFlag}, 数据:{replyData}");
            
            
            _player.ReplyAddFriendRequest(msg.dwFlag, replyData);
            
            await Task.CompletedTask;
        }

        private async Task HandleAddFriend(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string friendName = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 添加好友: {friendName}");
            
            
            var targetPlayer = HumanPlayerMgr.Instance.FindByName(friendName);
            if (targetPlayer != null)
            {
                
                targetPlayer.PostAddFriendRequest(_player);
                LogManager.Default.Info($"已向 {friendName} 发送添加好友请求");
            }
            else
            {
                
                _player.SendFriendSystemError(1, friendName); 
                LogManager.Default.Info($"玩家 {friendName} 不在线");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleCreateGuildOrInputConfirm(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string inputData = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 创建行会/输入确认: {inputData}");
            
            
            
            
            LogManager.Default.Info($"输入确认处理: {inputData}");
            
            await Task.CompletedTask;
        }

        private async Task HandleReplyAddToGuildRequest(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            bool accept = msg.wParam[0] != 0;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 回复加入行会请求: {(accept ? "接受" : "拒绝")}");
            
            
            _player.ReplyAddToGuildRequest(accept);
            
            await Task.CompletedTask;
        }

        private async Task HandleInviteToGuild(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string memberName = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 邀请加入行会: {memberName}");
            
            
            var guild = _player.Guild;
            if (guild != null && guild.IsMaster(_player))
            {
                var targetPlayer = HumanPlayerMgr.Instance.FindByName(memberName);
                if (targetPlayer != null)
                {
                    if (targetPlayer.Guild != null)
                    {
                        _player.SaySystem("对方已经是其他行会成员");
                    }
                    else
                    {
                        
                        targetPlayer.PostAddToGuildRequest(_player);
                        LogManager.Default.Info($"已向 {memberName} 发送加入行会请求");
                    }
                }
                else
                {
                    _player.SaySystem("玩家不存在");
                }
            }
            else
            {
                _player.SaySystem("只有行会会长才能邀请成员");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleTakeBankItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            uint npcId = msg.dwFlag;
            uint itemId = (uint)((msg.wParam[1] << 16) | msg.wParam[0]);
            LogManager.Default.Info($"处理客户端[{_player.Name}] 从仓库取出 NPC:{npcId}, 物品ID:{itemId}");
            
            
            
            bool success = _player.TakeBankItem(itemId);
            
            if (success)
            {
                SendMsg(itemId, GameMessageHandler.ServerCommands.SM_BANKTAKEOK, 0, 0, 0); 
            }
            else
            {
                SendMsg(itemId, GameMessageHandler.ServerCommands.SM_BANKTAKEFAIL, 0, 0, 0); 
            }
            
            await Task.CompletedTask;
        }

        private async Task HandlePutBankItem(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            uint npcId = msg.dwFlag;
            uint itemId = (uint)((msg.wParam[1] << 16) | msg.wParam[0]);
            LogManager.Default.Info($"处理客户端[{_player.Name}] 放入仓库 NPC:{npcId}, 物品ID:{itemId}");
            
            
            
            bool success = _player.PutBankItem(itemId);
            
            if (success)
            {
                SendMsg(itemId, GameMessageHandler.ServerCommands.SM_BANKPUTOK, 0, 0, 0); 
            }
            else
            {
                SendMsg(itemId, GameMessageHandler.ServerCommands.SM_BANKPUTFAIL, 0, 0, 0); 
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryCommunity(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 查询社区信息");

            
            
            


            
            var dbClient = _server.GetDbServerClient();
            if (dbClient != null)
            {
                
                LogManager.Default.Info($"查询社区信息: 玩家ID={_player.GetDBId()}");
                
            }
            else
            {
                SendMsg(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "数据库错误！");
                
            }

            await Task.CompletedTask;
        }

        private async Task HandleDeleteGuildMember(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string memberName = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 删除行会成员: {memberName}");
            
            
            var guild = _player.Guild;
            if (guild != null && guild.IsMaster(_player))
            {
                guild.RemoveMember(memberName);
                LogManager.Default.Info($"已删除行会成员: {memberName}");
            }
            else
            {
                _player.SaySystem("只有行会会长才能删除成员");
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleEditGuildNotice(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string notice = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 编辑行会公告: {notice}");
            
            
            var guild = _player.Guild;
            if (guild != null && guild.IsMaster(_player))
            {
                
                notice += "\r";
                guild.SetNotice(notice);
                
                
                string frontPage = guild.GetFrontPage();
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGE, 0, 0, 0, frontPage);
                LogManager.Default.Info($"已更新行会公告");
            }
            else
            {
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGEFAIL, 0, 0, 0); 
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleEditGuildTitle(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            string memberList = System.Text.Encoding.GetEncoding("GBK").GetString(payload).TrimEnd('\0');
            LogManager.Default.Info($"处理客户端[{_player.Name}] 编辑行会封号: {memberList}");
            
            
            var guild = _player.Guild;
            if (guild != null && guild.IsMaster(_player))
            {
                bool success = guild.ParseMemberList(_player, memberList);
                if (!success)
                {
                    _player.SaySystem(guild.GetErrorMsg());
                }
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryGuildExp(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 查询行会经验");
            
            
            var guild = _player.Guild;
            if (guild != null)
            {
                guild.SendExp(_player);
            }
            else
            {
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGEFAIL, 0, 0, 0); 
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryGuildInfo(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 请求行会信息");
            
            
            var guild = _player.Guild;
            if (guild != null)
            {
                guild.SendFirstPage(_player);
            }
            else
            {
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGEFAIL, 0, 0, 0); 
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryGuildMemberList(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 请求行会成员列表");
            
            
            var guild = _player.Guild;
            if (guild != null)
            {
                guild.SendMemberList(_player);
            }
            else
            {
                SendMsg(0, GameMessageHandler.ServerCommands.SM_GUILDFRONTPAGEFAIL, 0, 0, 0); 
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleQueryHistoryAddress(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 查询历史地址");
            
            
            
            

            
            var dbClient = _server.GetDbServerClient();
            if (dbClient != null)
            {
                
                LogManager.Default.Info($"查询历史地址: 玩家ID={_player.GetDBId()}");
                
            }
            else
            {
                SendMsg(0, ProtocolCmd.SM_ERRORDIALOG, 0, 0, 0, "数据库错误！");
                
            }
            
            await Task.CompletedTask;
        }

        private async Task HandleSetMagicKey(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 设置技能快捷键 Flag:{msg.dwFlag}, wParam0:{msg.wParam[0]}, wParam1:{msg.wParam[1]}");
            
            
            _player.SetMagicKey(msg.dwFlag, msg.wParam[0], msg.wParam[1]);
            
            await Task.CompletedTask;
        }

        private async Task HandleSetBagItemPos(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 设置背包物品位置");
            
            
            
            
            LogManager.Default.Info($"设置背包物品位置: 数据长度={payload.Length}");
            
            await Task.CompletedTask;
        }

        private async Task HandleNPCTalkOrViewPrivateShop(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            
            
            
            uint targetId = msg.dwFlag;
            LogManager.Default.Info($"处理客户端[{_player.Name}] NPC对话/查看个人商店 TargetId:{targetId}");

            var target = GameWorld.Instance.GetAliveObjectById(targetId);
            if (target == null)
            {
                LogManager.Default.Warning($"NPC对话/查看商店失败：找不到目标对象 TargetId={targetId}");
                await Task.CompletedTask;
                return;
            }

            if (target is Npc npc)
            {
                
                if (NpcScriptEngine.TryHandleTalk(_player, npc))
                {
                    await Task.CompletedTask;
                    return;
                }

                
                if (target is NPCInstance npcSystem)
                {
                    npcSystem.OnInteract(_player);
                }
                else
                {
                    npc.OnTalk(_player);
                }
            }
            else if (target is HumanPlayer otherPlayer)
            {
                
                LogManager.Default.Info($"查看个人商店：目标玩家={otherPlayer.Name}({otherPlayer.ObjectId})");
            }

            await Task.CompletedTask;
        }

        private async Task HandleRestartGame(MirMsgOrign msg, byte[] payload)
        {
            if (_player == null) return;
            LogManager.Default.Info($"处理客户端[{_player.Name}] 重启游戏");
             
            
            
            try
            {
                var dbClient = _server.GetDbServerClient();
                _player.UpdateToDB(dbClient);
            }
            catch (Exception ex)
            {
                LogManager.Default.Warning($"重启游戏保存数据失败: {_player.Name} - {ex.Message}");
            }

            
            try
            {
                var sc = _server.GetServerCenterClient();
                if (sc != null)
                {
                    byte[] enterBytes = StructToBytes(_enterInfo);
                    ushort targetIndex = (ushort)_enterInfo.dwSelectCharServerId;
                    byte sendType = (byte)MirCommon.ProtocolCmd.MST_SINGLE;

                    bool sent = await sc.SendMsgAcrossServerAsync(
                        clientId: 0,
                        cmd: MirCommon.ProtocolCmd.MAS_RESTARTGAME,
                        sendType: sendType,
                        targetIndex: targetIndex,
                        binaryData: enterBytes
                    );

                    if (sent)
                    {
                        _competlyQuit = true;
                        LogManager.Default.Info($"已发送MAS_RESTARTGAME: targetIndex={targetIndex}");
                    }
                    else
                    {
                        LogManager.Default.Warning($"发送MAS_RESTARTGAME失败: targetIndex={targetIndex}");
                    }
                }
                else
                {
                    LogManager.Default.Warning("ServerCenterClient为空，无法发送MAS_RESTARTGAME");
                }
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"处理重启游戏(MAS_RESTARTGAME)失败: {ex.Message}");
            }
             
            
            
            

            Disconnect();
            await Task.CompletedTask;
        }
    }
}
