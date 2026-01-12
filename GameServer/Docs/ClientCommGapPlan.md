# GameServer ? Client 通讯逻辑对照 C++（`MirWorld_Server`）差异清单与补齐计划

> 目的：对照父目录 C++ 工程 `MIRWORLDSERVER_xLib2\\MirWorld_Server`，梳理 `MirWorld_CSharp\\GameServer` 中与游戏客户端通讯相关逻辑的缺失/简化点，并形成可执行的补齐计划。
>
> 对照依据：
> - C++：`MirWorld_Server\\GameServer\\gsClientObj.cpp` 中 `CClientObj::ProcClientMsg`、`OnCodedMsg`、`OnVerifyString`、`OnDBMsg`、`OnDisconnect`
> - C#：`GameServer\\GameClient.cs` / `GameServer\\GameClientEx.cs` 及相关系统

---

## 1. 范围与现状概述

### 1.1 C# 消息分发入口

- `GameServer\\GameClient.cs`：
  - `ProcessAsync()` + `ProcessMessageWithErrorHandling()`：处理状态机（`GSUM_NOTVERIFIED/WAITINGDBINFO/WAITINGCONFIRM/VERIFIED`）
  - `HandleGameMessage(MirMsgOrign msg, byte[] payload)`：已验证状态下的消息分发（大量 `switch`）
- `GameServer\\GameClientEx.cs`：实现绝大多数 `HandleXxx` 处理函数

### 1.2 C++ 对照入口

- `MirWorld_Server\\GameServer\\gsClientObj.cpp`
  - 登录/状态机：`OnVerifyString`、`OnCodedMsg`、`OnDBMsg`
  - 已验证后消息分发：`ProcClientMsg`
  - 断线：`OnDisconnect`

---

## 2. 基于 C++ `ProcClientMsg` 的逐条差异与补齐计划（第一轮）

> 说明：本节以 C++ `ProcClientMsg` 中的 `case ...` 为索引，逐条对照当前 C# 实现。

### 2.1 市场消息 `0x1000 (CM_MARKET)`

- **C++ 行为**：
  - `CMarketManager::GetInstance()->OnClientMsg(m_pPlayer, w1, w2, w3, data, datasize)`
- **C# 现状**：
  - `HandleMarketMessage`：仅日志
- **缺失**：
  - 没有对接 `MarketManager` 业务处理与回包
- **补齐任务**：
  1. 在 `GameServer\\MarketManager.cs` 墩加 `OnClientMsg(HumanPlayer player, ushort w1, ushort w2, ushort w3, byte[] data, int size)`
  2. `HandleMarketMessage` 真实调用 `MarketManager` 并输出协议回包（目录、列表、购买、刷新、滚动文本等）
- **涉及文件**：`GameClientEx.cs`、`MarketManager.cs`

---

### 2.2 私人商店 `CM_QUERYSTARTPRIVATESHOP`

- **C++ 行为**：
  - `PRIVATESHOPQUERY* pQuery = (PRIVATESHOPQUERY*)pMsg->data;`
  - `SetPrivateShop(count,pQuery)`，并根据当前动作类型决定 `UpdatePrivateShopToAround()` 或 `SendStartPrivateShop()`
  - 失败/关闭：`StopPrivateShop()` + `SendStopPrivateShop()`
- **C# 现状**：
  - `HandleQueryStartPrivateShop`：仅日志
- **缺失**：
  - payload 结构未解析；玩家动作/店铺状态机未对齐；缺少广播/回包
- **补齐任务**：
  1. 定义并反序列化 `PRIVATESHOPQUERY`/条目结构
  2. 对接玩家状态机：`SetPrivateShop/StopPrivateShop/UpdatePrivateShopToAround/SendStartPrivateShop/SendStopPrivateShop`
- **涉及文件**：`GameClientEx.cs`、`HumanPlayer`、摆摊/店铺系统（如 `StallSystem.cs`）

---

### 2.3 输入确认/创建行会 `0x6891`

- **C++ 行为**：`m_pPlayer->GetScriptTarget()->OnInputConfirm(pMsg->data);`
- **C# 现状**：`HandleCreateGuildOrInputConfirm` 仅日志
- **缺失**：脚本输入确认链路
- **补齐任务**：
  1. 在 `HumanPlayer` 暴露/实现 `GetScriptTarget()` 或等价对象
  2. 调用脚本目标 `OnInputConfirm`，并补齐失败/关闭处理
- **涉及文件**：`GameClientEx.cs`、`ScriptTarget`/`SystemScript`/`ScriptView`

---

### 2.4 NPC 对话/查看别人商店 `0x3f2`

- **C++ 行为**：
  - `GetAliveObjectById(dwFlag)`
  - `OBJ_NPC`：`QueryTalk(m_pPlayer)`
  - `OBJ_PLAYER`：`SendPrivateShopPage(m_pPlayer,1)`
- **C# 现状**：`HandleNPCTalkOrViewPrivateShop` 仅按 `dwFlag==0` 猜测分支
- **缺失**：按对象类型路由；NPC/玩家私店页面对齐
- **补齐任务**：
  1. 加入对象查询：按 `dwFlag` 查 `GameWorld` 活体对象
  2. NPC：调用脚本 NPC 的 `QueryTalk`
  3. 玩家：发送私店页面（依赖私店系统实现）
- **涉及文件**：`GameClientEx.cs`、`GameWorld`、`NpcManagerEx`、私店系统

---

### 2.5 选择链接 `0x3f3 (CM_SELECTLINK)`

- **C++ 行为**：
  - `dwFlag == 0xffffffff`：执行系统脚本 `CSystemScript::Execute(..., TRUE)`，失败则 `SendCloseScriptPage(0xffffffff)`
  - 否则按 npcId 获取 `CScriptNpc`，执行 `QuerySelectLink(m_pPlayer, data)`
- **C# 现状**：`HandleSelectLink` 仅调用 `NPCMessageHandler.HandleSelectLink`，未覆盖系统脚本分支
- **缺失**：`dwFlag==0xffffffff` 分支与失败关闭页面
- **补齐任务**：
  1. 增加系统脚本分支处理
  2. 对齐失败后关闭脚本页的协议行为
- **涉及文件**：`GameClientEx.cs`、`SystemScript.cs`、`ScriptView.cs`

---

### 2.6 重启游戏 `0x3f1`

- **C++ 行为**：
  - 通过 `SCClientObj` 跨服发送 `MAS_RESTARTGAME`，并设置 `m_bCompetlyQuit = TRUE`
- **C# 现状**：`HandleRestartGame` 仅 `UpdateToDB()`
- **缺失**：跨服消息、完全退出标志与重登流程
- **补齐任务**：
  1. 使用 `GameServerApp`/`ServerCenterClient` 发送 `MAS_RESTARTGAME`
  2. 设置 `_competlyQuit=true` 并走统一退出
- **涉及文件**：`GameClientEx.cs`、`GameServerApp.cs`

---

### 2.7 查看装备 `0x52 (CM_VIEWEQUIPMENT)`

- **C++ 行为**：
  - 构建 `VIEWDETAIL_EX` 并发送 `0x2ef`，且带固定 wParam（`0x8261, 0x0cd0, 0xff`）
- **C# 现状**：`_player.ViewEquipment` + `SendActionResult`
- **缺失**：协议回包结构/参数一致性
- **补齐任务**：
  1. 定义并发送 `VIEWDETAIL_EX`（或等价）
  2. 校验并对齐 wParam 与 payload 长度
- **涉及文件**：`GameClientEx.cs`、`MirCommon` 结构体、`HumanPlayer.ViewEquipment`

---

### 2.8 交易（`CM_QUERYTRADE*`）

- **C++ 行为**：
  - `Trade/PutTradeItem/PutTradeMoney/ConfirmTrade/CancelTrade` + 对应回包（特别是 `SM_PUTTRADEGOLDOK` 需携带余额拆分）
- **C# 现状**：全部仅日志
- **缺失**：交易状态机、校验、冻结与结算、回包
- **补齐任务**：
  1. 对接 `TradeSystem` 与 `HumanPlayer` 的交易 API
  2. 实现与 C++ 一致的回包命令与参数语义
- **涉及文件**：`GameClientEx.cs`、`TradeSystem.cs`、`HumanPlayer`

---

### 2.9 穿戴/脱下/丢弃/拾取（`CM_TAKEONITEM/CM_TAKEOFFITEM/CM_DROPITEM/CM_PICKUPITEM`）

- **C++ 行为**：
  - `EquipItem/UnEquipItem/DropBagItem/PickupItem`
  - `Drop` 成功回 `0x258` 失败回 `0x259`，并 `SendWeightChanged`
  - `SendEquipItemResult/SendUnEquipItemResult` 内部还会：刷新属性/状态并调用 `CItemManager::UpdateItemPos`
- **C# 现状**：
  - 直接回 OK/重量变化，不做真实容器变更，不刷新属性状态，不更新物品位置
- **缺失**：核心物品流转逻辑
- **补齐任务**：
  1. 调用 `_player.EquipItem/UnEquipItem/DropBagItem/PickupItem` 并按结果回包
  2. 补齐：`SendFeatureChanged/UpdateProp/UpdateSubProp/SendStatusChanged`
  3. 补齐物品位置同步（等价于 `UpdateItemPos`，可能经 DBServer）
- **涉及文件**：`GameClientEx.cs`、`GameClient.cs`（回包）、`ItemSystem`/`DownItemMgr`、`HumanPlayer`

---

### 2.10 说话 `CM_SAY`

- **C++ 行为（`gsClientObj.cpp` 中 `case CM_SAY`）**：
  1. 禁言判断：`SF_BANED` → `SaySystem(SD_YOUAREBANED)` 并返回
  2. 内容过滤：`FilterString(pMsg->data)`
  3. 按首字符分流：
     - `'@'`：`OnCommand(pMsg->data+1)`
     - `'/'`：私聊：解析目标名与内容 → `ChannelSay(CCH_WISPER, targetName, text)`
     - `'!'`：二级分流：
       - `'!!'`：组队频道 `CCH_GROUP`
       - `'!~'`：行会频道 `CCH_GUILD`
       - 否则：喊话频道 `CCH_CRY`
     - 默认：
       - `SF_SCROLLTEXTMODE`：`AddGlobeProcess(EP_SCROLLTEXT, ..., pMsg->data)`
       - `SF_NOTICEMODE`：`PostSystemMessage(pMsg->data)`
       - 否则：`ChannelSay(GetChatChannel(), NULL, pMsg->data)`

- **C# 现状**：
  - `HandleSayMessage`：只做“附近广播”。
  - ~~广播实现存在严重问题：`SendChatMessage(Player targetPlayer,...)` 始终用当前 `_stream` 发送，无法发给附近玩家连接。~~ ? **已修复**：`SendChatMessage` 已改为使用 `targetPlayer.SendMsg(...)`（通过玩家发送委托），不再使用当前客户端 `_stream`。
  - 未实现：禁言/过滤/命令/私聊/组队/行会/喊话/滚动字幕/公告模式等分支。

- **补齐任务（按 C++ 分支拆解）**：
  1. ~~修复发送通道：聊天必须通过 `targetPlayer` 的发送委托（或其 `GameClient`）发送，而不是当前连接的 `_stream`。~~ ?
  2. 对齐禁言：引入并检查 `SF_BANED`。
  3. 对齐过滤：对接 C# `ChatFilter` 或移植 `FilterString`。
  4. 对齐命令：实现 `OnCommand` 分支（至少 GM 命令入口）。
  5. 对齐频道：私聊/组队/行会/喊话/普通频道应调用 `HumanPlayer.ChannelSay(...)` 或等价层。
  6. 对齐滚动字幕/公告：`SF_SCROLLTEXTMODE`→世界进程 `EP_SCROLLTEXT`；`SF_NOTICEMODE`→世界公告。

- **涉及文件**：`GameClientEx.cs`、`GameClient.cs`（`SendChatMessage`）、`ChatSystem.cs`/`ChatFilter.cs`、`HumanPlayer`、`GlobeProcess.cs`/`GameWorld`

---

### 2.11 `CM_TURN/CM_WALK/CM_RUN` 的回执坐标差异

- **C++ 行为**：
  - `CM_TURN` 成功：`SendActionResult(m_pPlayer->GetActionX(), m_pPlayer->GetActionY(), TRUE)`；失败：`SendActionResult(getX(), getY(), FALSE)`
  - `CM_WALK`/`CM_RUN`：
    - 先 `WalkXY/RunXY`，失败再 `Walk/Run(dir)`
    - 成功回执使用 **动作目标坐标** `GetActionX/GetActionY`（不是当前 `getX/getY`）

- **C# 现状**：
  - ~~`HandleTurnMessage`/`HandleWalkMessage`/`HandleRunMessage` 成功后广播，但回执固定 `SendActionResult(_player.X,_player.Y,success)`，没有使用 `GetActionX/GetActionY`。~~ ? **已对齐**：成功回执使用 `ActionX/ActionY`，失败回执使用当前 `X/Y`。

- **补齐任务**：
  1. ~~在 `HumanPlayer` 补齐/暴露 `GetActionX/GetActionY`（或等价的“本次动作坐标”）~~ ?（目前使用 `AliveObject.ActionX/ActionY`）
  2. ~~`SendActionResult` 调用对齐 C++：成功回执应使用动作坐标，而不是当前坐标~~ ?

---

### 2.12 Ping：`0x3d3` → `0x3d4`

- **C++ 行为**：`SendMsg(pMsg->dwFlag, 0x3d4, 0,0,0)`
- **C# 现状**：
  - ~~`HandlePing`/`HandlePingResponse` 都发送 `SM_PINGRESPONSE`，存在潜在“互相回”风险~~ ? **已修复**：客户端 `0x3d3` 时仅回 `0x3d4`；`HandlePingResponse` 不再回包（仅 keepalive）。
- **补齐任务**：
  1. ~~统一方向：客户端 `0x3d3` 时才回 `0x3d4`（或确认你 `ServerCommands` 常量映射）~~ ?
  2. ~~`PingResponse` 类消息不要再回包，只更新时间戳/延迟~~ ?

---

## 4. 断线/退出链路对照（`OnDisconnect`）

- **C++ 行为（`gsClientObj.cpp` 中 `OnDisconnect`）**：
  1. 执行登出脚本：`CSystemScript::Execute(LogoutScript)`
  2. 行会：`Guild->MemberLogoff(m_pPlayer)`
  3. 交易：若 `GetExchangeObject()!=NULL` → `End(ET_CANCEL)`
  4. 组队：若 `GetGroupObject()` → `DelMember(m_pPlayer)`
  5. 特定地图标志 `MF_NORECONNECT`：随机传送或回城
  6. 排行：`CTopManager::UpdateTopInfo`
  7. 清理宠物、好友离线通知
  8. `SaveVars/UpdateToDB/UpdateTaskToDB/UpdateItemsToDB`
  9. `RemoveMapObject` + `DeletePlayer`
  10. **如果不是完全退出且已验证**：向 ServerCenter 发送 `MAS_RESTARTGAME`（用于回到选角）

- **C# 现状**：
  - ~~`GameClient.OnDisconnect`：从地图移除；`SavePlayerDataToDB()`/`CleanupPlayerResources()` 仅日志占位；未触发脚本/行会/交易/组队清理；未按“非完全退出”触发跨服重登。~~ ? **已部分补齐**：
    - 已增加：Logout 脚本 best-effort、交易取消、组队移除、宠物清理、`UpdateToDB()` 保存、从世界/地图移除。
    - 仍缺：`MF_NORECONNECT` 规则、排行榜/好友离线通知、以及“非完全退出回选角（MAS_RESTARTGAME）”。

- **补齐任务**：
  1. ~~对齐登出脚本执行~~ ?（best-effort）
  2. ~~对齐行会/交易/组队收尾~~ ?（行会仅做安全清理，尚缺 `MemberLogoff` 真实实现）
  3. 对齐 `MF_NORECONNECT` 地图规则处理
  4. 对齐数据保存：vars/task/items（目前走 `UpdateToDB()` 入口，细节待补齐）
  5. 对齐“非完全退出自动回选角”：需要复用 `MAS_RESTARTGAME` 跨服发送

---

### 12.2 地面物品拾取：`MirWorld_Server\\GameServer\\DownItemMgr.cpp/.h`

- **C# 对照结论（需要修/补齐）**
  1. ~~“金币用 duras 拼金额”的约定需要对齐，否则客户端/掉落系统会出现金币拾取异常~~ ? 已对齐：金币金额使用 `CurDura | (MaxDura<<16)`
  2. `SF_GODBLESS` 的拾取触发路径依赖 downItem 的 `btStdMode==255` 与 `btShape`，C# 若没有该分支会缺失玩法（未实现）
  3. 拾取后脚本页 `szPickupPage` 的执行与 UR_DELETED/UPDATED 的二次包（`SendTakeBagItem/SendUpdateItem`）是客户端一致性的关键（未实现）

---

## 3. 登录/验证/DB 消息链路对照（`OnVerifyString`/`OnCodedMsg`/`OnDBMsg`）

### 3.1 验证字符串解析（`OnVerifyString`）

- **C++ 行为**：
  - 必须以 `***` 开头，否则直接返回
  - 解析格式：`***loginid/角色名/selectcharid/20041118/0`
  - 从 `EnterInfoList` 校验角色名，查重登（`FindbyName`）
  - 通过 DB 连接发送 `SendQueryDbInfo(getId(), clientKey, account, serverNickname, charName)`
  - 状态置为 `GSUM_WAITINGDBINFO`

- **C# 现状**：
  - `DecodeVerifyString` 支持 `#...!` 编码，并 `ProcessDecodedString`
  - `ProcessDecodedString` 中也检查 `***` 与参数数量
  - 但账户/EnterInfo 获取逻辑与 C++ 有差异，并有“找不到 ServerCenter 信息时构造假账号”的兜底

- **待核对/差异风险点**：
  1. C++ 直接要求 `***`；C# 允许 `#...!` 解码后再判断（兼容性可能更好，但需确认客户端实际行为）
  2. C++ `SendQueryDbInfo` 参数含 server nickname；C# 用 `GetCharDBInfoBytesAsync2(account, serverName, charName, clientKey, 0)`（需要确认 DB 协议字段对齐）

### 3.2 状态机入口（`OnCodedMsg`）

- **C++ 行为**：
  - 非 `GSUM_VERIFIED`：
    - `GSUM_NOTVERIFIED`：`OnVerifyString((char*)pMsg)`
    - `GSUM_WAITINGCONFIRM`：仅处理 `CM_CONFIRMFIRSTDIALOG`：
      - 置 `GSUM_VERIFIED`
      - `AddMapObject(m_pPlayer)`
      - DB 依次查询：Magic、Equipment、Upgrade、Bag(CountLimit)、Bank、PetBank、TaskInfo
      - 若首次登录：添加进程 `EP_FIRSTLOGINPROCESS`
  - 已验证且玩家死亡：
    - 限制大部分命令（只允许 `0x3f1/CM_SAY/0x45/0x6a`），否则提示“你已经死亡”
  - 最后 `ProcClientMsg(pMsg, datasize)`

- **C# 现状**：
  - `ProcessMessageWithErrorHandling` 里也实现了 3 段状态：NOTVERIFIED/WAITINGCONFIRM/WAITINGDBINFO
  - `HandleConfirmFirstDialog` 里也发送 DB 查询（magic/equipment/upgrade/bag/bank/petbank/task），并设置 `_state=GSUM_VERIFIED`，逻辑基本对齐。
  - **缺失：玩家死亡时的“命令白名单限制”**（C# `HandleGameMessage` 未见同等限制）。

- **补齐任务**：
  1. 在 C# `GSUM_VERIFIED` 路径增加死亡限制（允许的命令白名单对齐 C++）
  2. bag CountLimit：C# 目前固定 `40`，C++ 用 `m_pPlayer->GetBag().GetCountLimit()`（需要补齐真实背包限制）

### 3.3 DB 消息处理（`OnDBMsg`）

- **C++ 关键点**：
  - `DM_QUERYMAGIC`：`wParam[2]` 表示数量（或 `0x8000|err`），并 `SetMagic((MAGICDB*)data, wParam[2])` 后 `SendMagicList`
  - `DM_QUERYITEMS`：`data` 前 4字节是 `clientKey`，其后是 `DBITEM[]`；调用 `OnDBItem(items,count,flag)`
  - `DM_GETCHARDBINFO`：严格检查 `pMsg->wParam[0] != SE_OK` 失败逻辑；成功时 NewPlayer/Init/LoadVars/`SendFirstDlg`，状态置 `GSUM_WAITINGCONFIRM`
  - `DM_CREATEITEM`：调用 `OnCreateItem(item,pos,flag)`：`IDF_BAG` → `AddBagItem`；否则 `DropItem`
  - `DM_QUERYCOMMUNITY`：直接 `m_pPlayer->OnCommunityInfo(pMsg->data)`

- **C# 现状差异（已观察）**：
  1. ~~`DM_QUERYCOMMUNITY`：C# 仅日志/未调用玩家处理~~ ? 已补齐：`GameClient.OnDBMsg` 已将 `pMsg.data` 转发到 `_player.OnCommunityInfo(...)`，玩家侧缓存原始数据以备后续解析。
  2. ~~`DM_CREATEITEM`：C# 解析后 `OnCreateItem` 目前只是日志占位，不会真正 `AddBagItem/DropItem`~~ ? 已补齐：`GameClient.OnCreateItem` 已根据 `btFlag` 将物品加入背包（失败则尝试掉落）或直接掉落到地图（调用 `DownItemMgr.DropItem`）。
  3. `DM_GETCHARDBINFO`：C# 对错误码检查使用了 `dwFlag` 而不是 `wParam[0]`（代码中有注释说明修复点，但现状仍需要再核对）

- **补齐任务**：
  1. ~~对齐 `DM_QUERYCOMMUNITY` → `_player.OnCommunityInfo(...)`~~ ?
  2. ~~对齐 `DM_CREATEITEM` → 背包/掉落真实处理~~ ?
  3. ~~完整实现 `OnDBItem`：把 DBITEM 真正装载到背包/装备/仓库容器（当前多处是注释占位；已先对齐 DBITEM 按 `wPos` 排序以匹配 C++ `qsort` 行为）~~ ?（已补齐：`OnDBItem` 现在会清空并装载 **背包/装备**；`IDF_BANK` 先落到 `_bankCache`（等待后续实现真实仓库容器 + `SendBank(0x2c0)` UI 下发）；`IDF_PETBANK` 维持调用 `_player.OnPetBank(...)`。）
---

## 4. 断线/退出链路对照（`OnDisconnect`）

- **C++ 行为（`gsClientObj.cpp` 中 `OnDisconnect`）**：
  1. 执行登出脚本：`CSystemScript::Execute(LogoutScript)`
  2. 行会：`Guild->MemberLogoff(m_pPlayer)`
  3. 交易：若 `GetExchangeObject()!=NULL` → `End(ET_CANCEL)`
  4. 组队：若 `GetGroupObject()` → `DelMember(m_pPlayer)`
  5. 特定地图标志 `MF_NORECONNECT`：随机传送或回城
  6. 排行：`CTopManager::UpdateTopInfo`
  7. 清理宠物、好友离线通知
  8. `SaveVars/UpdateToDB/UpdateTaskToDB/UpdateItemsToDB`
  9. `RemoveMapObject` + `DeletePlayer`
  10. **如果不是完全退出且已验证**：向 ServerCenter 发送 `MAS_RESTARTGAME`（用于回到选角）

- **C# 现状**：
  - ~~`GameClient.OnDisconnect`：从地图移除；`SavePlayerDataToDB()`/`CleanupPlayerResources()` 仅日志占位；未触发脚本/行会/交易/组队清理；未按“非完全退出”触发跨服重登。~~ ? **已部分补齐**：
    - 已增加：Logout 脚本 best-effort、交易取消、组队移除、宠物清理、`UpdateToDB()` 保存、从世界/地图移除。
    - 仍缺：`MF_NORECONNECT` 规则、排行榜/好友离线通知、以及“非完全退出回选角（MAS_RESTARTGAME）”。

- **补齐任务**：
  1. ~~对齐登出脚本执行~~ ?（best-effort）
  2. ~~对齐行会/交易/组队收尾~~ ?（行会仅做安全清理，尚缺 `MemberLogoff` 真实实现）
  3. 对齐 `MF_NORECONNECT` 地图规则处理
  4. 对齐数据保存：vars/task/items（目前走 `UpdateToDB()` 入口，细节待补齐）
  5. 对齐“非完全退出自动回选角”：需要复用 `MAS_RESTARTGAME` 跨服发送

---

## 5. 下一步深化分析计划（第二轮）

本轮已补齐并固化到文档的 C++ 对照段落包括：
- `CM_SAY` 完整分支（禁言/过滤/命令/私聊/组队/行会/喊话/滚动字幕/公告模式）
- `CM_TURN/CM_WALK` 的回执风格（`GetActionX/Y` vs `getX/Y`）
- `OnCodedMsg` 的死亡白名单限制
- 登录/DB 链路关键点
- 断线/退出完整收尾链路

下一步建议继续对照以下 C++ 文件，以把“被调用系统”的协议与行为也对齐（避免仅在 ClientObj 层补齐却缺少返回包/状态机）：
1. `MirWorld_Server\\GameServer\\MarketManager.cpp/.h`：细到 wParam/data 语义
2. `MirWorld_Server\\GameServer\\groupobject.*`：组队协议回包与广播
3. `MirWorld_Server\\GameServer\\guildex.* / guildmanager.*`：行会协议与权限规则
4. `MirWorld_Server\\GameServer\\humanplayer*`：`ChannelSay`、物品/交易/脚本入口等

---

## 6. 优先级建议（可执行顺序）

- **P0**：修复 `CM_SAY` 发包通道（目前 C# 广播注定无效）
- **P0**：补齐死亡白名单限制（避免死亡状态下执行非法命令引发数据异常）
- **P0**：补齐 `OnDisconnect` 收尾（交易/组队/行会/脚本/DB保存），防止脏状态
- **P1**：补齐市场/私店/NPC脚本入口
- **P1**：补齐 `DM_CREATEITEM / DM_QUERYCOMMUNITY` 与 `OnDBItem` 的真实容器装载
- **P2**：补齐 `0x8d00` 历史地址编码回包、查看装备结构回包等兼容项

---

## 7. 第二轮新增优先级建议（基于子系统协议风险）

- **P0**：市场 `0x1000` 的字符串协议与元宝购买校验（客户端强依赖格式）
- **P0**：普通聊天改为 `ChannelSay` 体系并对齐 `g_ChatChannelMsg/Attrib`，避免“客户端不显示/乱码/范围异常”
- **P1**：组队消息 `SM_GROUPMEMBERLIST/SM_UPDATEMEMBERINFO` 的 payload 格式对齐（字符串与 WORD[]）
- **P1**：行会页面/成员列表/经验 `0x2f1/0x2f4/0x2cd` 的字符串协议对齐

---

## 9. 第三轮深化对照：交易 / 物品流转 / 仓库 / 私人商店

本节补充对照 `gsClientObj.cpp` 中与客户端交互高度相关、且 C# 侧目前多为占位的业务命令：交易、穿脱、丢弃拾取、仓库，以及 DB 下发装载契约。

### 9.1 交易协议：`CM_QUERYTRADE*`（`gsClientObj.cpp`）

- **C++ 行为**
  - `CM_QUERYTRADE`：`m_pPlayer->Trade();`
  - `CM_PUTTRADEITEM`：
    - `PutTradeItem(dwFlag)` 后 **无论成功失败都回** `SM_PUTTRADEITEMOK`（原版如此；可能是历史兼容/bug，但客户端可能依赖）
  - `CM_PUTTRADEGOLD`：
    - 成利：
      - `PutTradeMoney( (money_type)wParam[0], dwFlag )`
      - 回 `SM_PUTTRADEGOLDOK`，其中：
        - `dwFlag = putAmount`
        - `w1 = curMoney & 0xffff`
        - `w2 = curMoney >> 16`
        - `w3 = moneyType (wParam[0])`
    - 失败：回 `SM_PUTTRADEGOLDFAIL`
  - `CM_QUERYTRADEEND`：`ConfirmTrade()`
  - `CM_CANCELTRADE`：`CancelTrade()`

- **C# 现状**
  - `GameClientEx.HandleQueryTrade/HandlePutTradeItem/HandlePutTradeGold/HandleQueryTradeEnd/HandleCancelTrade` 全为日志占位

- **补齐任务**
  1. 复刻 C++ 的回包语义（尤其 `SM_PUTTRADEGOLDOK` 的 w1/w2/w3）
  2. 明确 `SM_PUTTRADEITEMOK` 是否需要始终回 OK（建议先对齐 C++，再按测试决定是否增加 FAIL）
  3. 在 `HumanPlayer`/`TradeSystem` 层补齐：`Trade/PutTradeItem/PutTradeMoney/ConfirmTrade/CancelTrade` 状态机与校验

---

### 9.2 穿戴/脱下：`CM_TAKEONITEM` / `CM_TAKEOFFITEM` 的“后续刷新与 DB 同步”

- **C++ 行为**（`CClientObj::SendEquipItemResult/SendUnEquipItemResult`）
  - 成功：
    - 回 `SM_TAKEON_OK`/`SM_TAKEOFF_OK`，`dwFlag=feather`
    - 且会触发：
      - `SendFeatureChanged()`
      - `UpdateProp()`
      - `UpdateSubProp()`
      - `SendStatusChanged()`
    - 装备位置同步：`CItemManager::UpdateItemPos(makeIndex, IDF_EQUIPMENT/BAG, pos)`
  - 失败：
    - 回 `SM_TAKEON_FAIL/SM_TAKEOFF_FAIL`，`dwFlag=0xffffffff`，并直接 return
  - 脱下额外包：成功后再发 `SendMsg(0, 0x26c, 0,0,0)`（客户端可能用于刷新某 UI 状态）

- **C# 现状**
  - `HandleTakeOnItem/HandleTakeOffItem` 只发“永远成功”的结果包
  - `SendEquipItemResult/SendUnEquipItemResult` 未对齐 feather/失败 flag，也未触发属性/状态刷新，更没有 `0x26c` 与 `UpdateItemPos`

- **补齐任务**
  1. 按 C++ 逻辑实现 `SendEquipItemResult/SendUnEquipItemResult` 的成功/失败 feather 语义
  2. 成功后补齐 Feature/Prop/SubProp/Status 四连刷
  3. 脱下成功后补齐 `0x26c`
  4. 建立物品位置同步等价方法（C++ `CItemManager::UpdateItemPos`）：
     - 若 C# 侧是 DBServer 驱动，则需要补相应 DB 命令（或在内存落地后统一在保存时写回）

---

### 9.3 丢弃/拾取：`CM_DROPITEM` / `CM_PICKUPITEM`

- **C++ 行为**
  - `CM_DROPITEM`：
    - 成功：回 `0x258`（dwFlag=makeIndex），并 `SendWeightChanged()`
    - 失败：回 `0x259`（dwFlag=makeIndex）
  - `CM_PICKUPITEM`：成功时仅 `SendWeightChanged()`（无显式 OK 回包）

- **C# 现状**
  - `HandleDropItem/HandlePickupItem` 使用自定义 `SendDropItemResult/SendPickupItemResult`，并且目前默认成功

- **补齐任务**
  1. 对齐丢弃回包 cmd：`0x258/0x259`（或确认 `ServerCommands` 是否已有对应别名）
  2. 对齐拾取：成功只发重量变化（必要时补齐地面物品消失/新增逻辑由地图系统处理）

---

### 9.4 仓库：客户端命令与回包（C++ `0x407/0x408`）

- **C++ 行为（`gsClientObj.cpp`）**
  - `0x407`（存入仓库）：
    - `CScriptNpc* pNpc = GetScriptNpcById(dwFlag)`
    - `dwItemId = *(DWORD*)&wParam[0]`
    - 成功回 `0x2bd`，失败回 `0x2be`（dwFlag=dwItemId）
  - `0x408`（取出仓库）：
    - 同样要求 `pNpc != NULL`
    - 成功回 `0x2c1`，失败回 `0x2c2`

- **C# 现状**
  - `HandleTakeBankItem/HandlePutBankItem`：
    - 目前不校验 NPC 存在
    - 回包使用 `SM_BANKTAKEOK/FAIL`、`SM_BANKPUTOK/FAIL`（需核对是否等同 `0x2c1/0x2c2/0x2bd/0x2be`）

- **补齐任务**
  1. 对齐 NPC 校验：只有 `dwFlag` 对应的 ScriptNpc 存在时才允许
  2. 对齐回包命令号：优先按 C++ 的 `0x2bd/0x2be/0x2c1/0x2c2`（若你常量映射一致则只需校验）
  3. 物品 id 解析：C++ 是 `*(DWORD*)&wParam[0]`（即 wParam0+1 拼 DWORD），C# 现有拼法需再核对 endian

---

### 9.5 DB 下发装载契约：`CClientObj::OnDBItem`（关键）

- **C++ 行为**
  1. `DBITEM[]` 会按 `pos` 排序（`qsort`）
  2. `btFlag` 分支：
     - `IDF_BAG`：
       - `SetSystemFlag(SF_BAGLOADED, TRUE)`
       - 循环 `AddBagItem(item, TRUE)`（注意保留 DBITEM 原 item 以便后面发包用原始结构）
       - 再调用 `SendBagItems(DBITEM[], nCount)`：会发 `0xc9`（ITEMCLIENT数组）+ `SendWeightChanged()` + `0x46`（BAGITEMPOS数组）并 `SetBagItemPos`
     - `IDF_EQUIPMENT`：
       - `SetSystemFlag(SF_EQUIPMENTLOADED, TRUE)`
       - 循环 `EquipItem(pos, item, TRUE)`
       - `SendEquipments()`（并刷新 feature/props/status）
     - `IDF_BANK`：循环 `AddBankItem(item,FALSE)`（本分支不在这里直接发包，通常由打开仓库/查看仓库触发下发）
     - `IDF_PETBANK`：`OnPetBank(DBITEM[], nCount)`

- **C# 现状**
  - `GameClient.OnDBItem`/`SendBagItems`/`SendEquipments` 存在，但装载/排序/flag 标记与“后续 UI/位置包”不完全一致；并且银行/宠物仓库多处是占位。

- **补齐任务**
  1. DBITEM 按 `pos` 排序后再装载（尤其背包 `0x46` 位置包依赖 `pos`）
  2. 将 `SF_BAGLOADED/SF_EQUIPMENTLOADED` 与 C# 的 `_bagLoaded/_equipmentLoaded` 统一（避免重复发 `EnterGameOk`）
  3. `IDF_BANK`/`IDF_PETBANK` 的下发策略：需要确定客户端 UI 打开时如何触发发送（C++ 可能在 NPC 脚本里触发银行列表发送）

---

### 9.6 私人商店：`CM_QUERYSTARTPRIVATESHOP`（补充细节）

- **C++ 行为（`gsClientObj.cpp`）**
  - payload 强转为 `PRIVATESHOPQUERY*`，`wParam[0]` 表示条目数
  - 若当前动作已是 `AT_PRIVATESHOP`，则 `bStarted = TRUE`
  - `SetPrivateShop(count, pQuery)` 成功：
    - 若已开始：`UpdatePrivateShopToAround()`
    - 否则：`SendStartPrivateShop()`
  - 失败或 count==0：`StopPrivateShop()` + `SendStopPrivateShop()`

- **C# 现状**
  - `HandleQueryStartPrivateShop` 仍为空实现

- **补齐任务**
  1. 明确 `PRIVATESHOPQUERY` 结构并做 payload 解析
  2. 对齐“已开始 vs 新开始”的广播差异（更新周围可见对象）
  3. 对齐失败/关闭路径：必须通知客户端停止（否则客户端 UI 与服务器状态漂移）

---

## 10. 第四轮深化对照：`CHumanPlayer` 业务函数（支撑客户端命令）

本节把“ClientObj 分发层”继续下钻到 `MirWorld_Server\\GameServer\\HumanPlayer.cpp` 的具体业务实现，提取 **校验点、状态副作用、与客户端交互的隐含约定**，用于指导 C# 侧补齐时不漏关键边界。

### 10.1 交易：`CHumanPlayer::Trade/PutTradeItem/PutTradeMoney/ConfirmTrade/CancelTrade`

- **C++ `Trade()` 关键校验（决定能否开始交易）**
  1. 交易对象必须是“前方一格”的玩家：
     - 自己朝向下一格必须有 `OBJ_PLAYER`
  2. 对方也必须“面对着你”（双向面对校验）：
     - 取对方朝向下一格，必须回到你当前位置
  3. 满足后双方共用同一个 `CExchangeObj`：
     - `m_pExchangeObj = new CExchangeObj;`
     - `other->m_pExchangeObj = m_pExchangeObj;`
     - `m_pExchangeObj->Begin(this, other)`

- **C++ `PutTradeItem(dwMakeIndex)`**
  - 必须有 `m_pExchangeObj`
  - 物品必须来自背包 `m_ItemBox.FindItem`
  - 受限物品 `IL_NOEXCHANGE` 直接拒绝（并提示“物品不能交换”）
  - 成功后通知对方：`Send_Exchg_OtherAddItem(sender, item)`（即对方收到 `SM_OTHERPUTTRADEITEM`）

- **C++ `PutTradeMoney(type, dwCount)`**
  - 逻辑是“把交易栏金额调整到 dwCount（可增可减）”——不是单纯追加
  - 若当前交易栏金额 `< dwCount`：
    - 先 `CostMoney(type, delta, FALSE)`（失败提示“没这么多钱”）
  - 若当前交易栏金额 `> dwCount`：
    - 先 `AddMoney(type, delta, FALSE)`（失败提示“身上钱太多，无法拿回”）
  - 再调用 `m_pExchangeObj->PutMoney(this, type, dwCount)`；失败会把钱加回去兜底

- **C++ `CancelTrade/ConfirmTrade`**
  - 都是 `m_pExchangeObj->End(this, ET_CANCEL/ET_CONFIRM)`

- **C# 现状与缺失**
  - `GameClientEx` 交易 handler 仍是日志占位，没有“面对校验/建立交换对象/背包移除/金额调节”
  - `TradeSystem.cs` 目前实现更像“直接放入 amount 并立即从玩家扣钱”，但 C++ 的语义是“把交易栏金额调到指定值（可回退）”，且扣/退的 timing 与失败兜底不同

- **补齐建议（高优先级）**
  1. C# `HumanPlayer.Trade()` 应实现“双向面对”校验（否则可隔空/背对交易）
  2. `PutTradeMoney` 需改为“设置交易栏金额为 amount”语义，并支持减少时返还（且，需要处理返还失败提示）
  3. `PutTradeItem` 需检查 `IL_NOEXCHANGE` 等限制，并在成功后才从背包移除

- **涉及文件**：`GameClientEx.cs`、`TradeSystem.cs`、`HumanPlayer.cs`（背包/货币 API）

---

### 10.2 仓库：`CHumanPlayer::PutBankItem/TakeBankItem/SendBank/AddBankItem`

- **C++ `PutBankItem(dwMakeIndex)`**
  - item 必须在背包 `m_ItemBox`
  - 受限检查：`IL_NOSTORAGE`（提示“不能存仓库”）
  - 存入成功：
    - `AddBankItem(*pItem)`
    - 从背包移除该物品
    - `SendWeightChanged()`（注意：这里会发重量变化）

- **C++ `TakeBankItem(dwMakeIndex)`**
  - item 必须在仓库 `m_ItemBank`
  - 取出成功：`AddBagItem(*pItem, FALSE, TRUE)` 后从仓库移除
  - 注意：这里本函数本身不 `SendWeightChanged()`，对应 ClientObj 层只回 `0x2c1/0x2c2`

- **C++ `SendBank(dwNpcId)`**
  - 发送 cmd `0x2c0`，`dwFlag = npcId`，`w3 = count`，payload 为 `ITEMCLIENT[count]`

- **C# 现状与缺失**
  - C# handler 已存在，但：
    - 未对齐 `IL_NOSTORAGE` 限制
    - `SendBank` 同等协议（`0x2c0` + ITEMCLIENT数组）是否存在需核对
    - “PutBankItem 成功后发送重量变化”与“TakeBankItem 不一定发重量变化”的差异目前未必对齐

- **涉及文件**：`GameClientEx.cs`、`HumanPlayer.cs`（Bank 容器/限制检查）、`ItemManager`/DB 同步

---

### 10.3 私人商店：`CHumanPlayer::SetPrivateShop/SendPrivateShopPage/UpdatePrivateShopToAround/BuyPrivateShopItem`

- **C++ `SetPrivateShop(count, PRIVATESHOPQUERY*)` 核心行为**
  - 每条上架项：
    - `pItem = m_ItemBox.FindItem(makeIndex)` 必须存在
    - 受限检查：`IL_NOPRIVATESHOP`（提示“摊位里有不能出售的物品”）
    - 记录价格 `dwPrice` 与货币类型 `pricetype = (wPriceType & 1)`
  - 必须至少成功缓存 1 条，否则失败
  - 会强制把朝向调整为“斜向”（odd direction），并 `SetAction(AT_PRIVATESHOP, dir, x, y, 0xffffffff)`

- **C++ `SendPrivateShopPage(queryer, wFlag)`**
  - 只有当 `this->m_ActionType == AT_PRIVATESHOP` 才可展示
  - 回包 cmd `0xfca0`：
    - `dwFlag = shopOwnerId`
    - `w1=x, w2=y, w3=dir`
    - payload = `PRIVATESHOPHEADER + ITEMCLIENT[]`
    - `header.w2 = wFlag`（1=首次打开，会 `SetCurPrivateShopView(this)`；2=刷新）

- **C++ `UpdatePrivateShopToAround()`**
  - 先发 `0x80d7`（看起来是摆摊开始/状态变化）
  - 只对“正在查看该摊位的人”推送 `0xfca0(wFlag=2)` 刷新包（通过 visible list + `GetCurPrivateShopView()==this` 筛选）

- **C++ `BuyPrivateShopItem(buyer, itemId, name)` 核心校验**
  - 买家背包空间必须足够（否则提示）
  - 买家货币必须足够
  - 卖家必须能再收下这笔钱（`TestAddMoney`，否则交易失败并双向提示）
  - 成功后：
    - 买家 `AddBagItem(item)`
    - 买家扣钱 `CostMoney`，卖家加钱 `AddMoney`
    - 从 cache 中移除该项并从背包移除原物品
    - 卖家 `SendTakeBagItem(&item)` + `SendWeightChanged()`

- **C# 现状与缺失**
  - 目前 C# 私店相关 handler/结构解析为占位
  - 未实现：`0xfca0` 的结构包、`0x80d7` 状态包、以及“只推给正在查看的人”的刷新机制

- **涉及文件**：`GameClientEx.cs`、`HumanPlayer.cs`（私店状态/缓存/CurView）、`DownItemMgr`/物品系统

---

### 10.4 物品拾取/丢弃/穿脱：`PickupItem/DropBagItem/EquipItem/UnEquipItem`

- **C++ `PickupItem()`**
  - 只能拾取脚下 `OBJ_DOWNITEM`
  - 物品有效性 `UpdateValid()`
  - 非金币：
    - 背包空位必须 >0
    - 负重必须允许（`CanBearItem`）
  - 归属校验：
    - `ownerId==0` 或 `ownerId==this->GetDBId()` 或（组队且 owner 为队员 DBId）
  - 真正拾取由 `CDownItemMgr::PickupItem(map, downItem, this)` 执行

- **C++ `DropBagItem(makeIndex)`**
  - 必须在背包
  - `IL_NODROP` 限制
  - 成功后：落地 `DropItem(*pItem)` + 从背包移除

- **C++ `EquipItem(pos, makeIndex)`**
  - 必须在背包
  - 装备成功后：
    - 立即 `UpdateItemPos(makeIndex, IDF_EQUIPMENT, pos)`
    - 从背包移除
    - 如果有换下来的 `itemout`：自动放回背包并 `UpdateItemPos(itemout, IDF_BAG, 0)`

- **C++ `UnEquipItem(pos, makeIndex)`**
  - makeIndex 必须匹配该装备位
  - 耐久为 0 的装备会被“直接删除并发 TakeBagItem”（不进背包）
  - `IL_NOTAKEOFF` 限制
  - 背包无空位直接失败

- **C# 现状与缺失（关键差异）**
  - C# 目前拾取/丢弃/穿脱不完整，尤其：
    - 拾取归属规则（ownerId + 组队共享）
    - 装备成功后的“自动回填背包 itemout”
    - 脱下时耐久为 0 的删除路径
    - `UpdateItemPos` 的时机与同步

- **涉及文件**：`HumanPlayer.cs`、`DownItemMgr.cs`、`ItemManager`、`GameClient.cs`（回包）

---

## 11. 第四轮：C# 对应落地点（建议修改入口）

- `GameServer\\GameClientEx.cs`
  - 交易：`HandleQueryTrade/HandlePutTradeItem/HandlePutTradeGold/HandleQueryTradeEnd/HandleCancelTrade`
  - 仓库：`HandlePutBankItem/HandleTakeBankItem`（并对齐 NPC gating 与 cmd 号）
  - 私店：`HandleQueryStartPrivateShop` + 查看/购买相关命令（需要再定位对应 cmd）

- `GameServer\\TradeSystem.cs`
  - 将 PutMoney 语义调整为“设置交易栏金额=amount（可增可减）”
  - 增加与客户端回包一致的通知/回执

- `GameServer\\HumanPlayer.cs`
  - 补齐或对齐：私店缓存/CurPrivateShopView/ownerId 共享拾取/装备换下回填/耐久为0删除等规则

---

## 12. 第五轮深化对照：`CExchangeObj` / `CDownItemMgr` / 私人商店结构体与线上协议

本轮聚焦三个“业务中枢”：交易对象 `CExchangeObj`（完整状态机与最终结算）、地面物品管理 `CDownItemMgr`（拾取的真正落地逻辑），以及私店相关结构体（`PRIVATESHOPHEADER/SHOW`）在 on-wire 的字段来源。

### 12.1 交易对象：`MirWorld_Server\\GameServer\\ExchangeObj.h/.cpp`

- **结构与状态**
  - 两侧数据：`exchange_side { player; ITEM m_Items[10]; DWORD dwGold; DWORD dwYuanbao; BOOL bReady; }`
  - 状态：`EE_PUTITEMS`（放置）→ `EE_WAITFOROTHER`（等待对方确认）
  - 结束类型：`ET_CANCEL` / `ET_CONFIRM`

- **Begin(p1,p2)**
  - 向双方发 `SM_TRADESTART`，payload 为对方名字字符串
  - 设置双方 `SetExchangeObject(this)`
  - 并 `SaySystemAttrib(CC_EXCHANGE,"xxx和你开始交易")`

- **PutItem(player, item)**
  - 若状态不是 `EE_PUTITEMS`：
    - 设置 errorMsg “无法放入物品，对方已按交易按钮”
    - 向发起方发送 `0x2a4`（交易错误提示包）
    - 返回 false
  - 交易栏容量 10 格，满则返回 false 且同样发 `0x2a4`
  - 成功后通知对方：`Send_Exchg_OtherAddItem(sender, item)`（即对方收到 `SM_OTHERPUTTRADEITEM`）

- **PutMoney(player, type, dwCount)**
  - 直接把 side 的 `dwGold/dwYuanbao` 设置为 `dwCount`
  - 通知对方：`Send_Exchg_OtherAddMoney(...)`（对方收到 `SM_OTHERPUTTRADEGOLD`，其 `dwFlag=dwCount, w3=type`）

- **End(player, ET_CONFIRM)**（确认逻辑非常重要）
  - 若自己已 Ready：
    - 自己提示“让对方按交易按钮”
    - 对方提示“对方再次要求确认，按[交易]确认”
  - 否则置 `Ready=TRUE`
    - 若对方也 Ready：执行 `DoExchange`，失败则 `DoCancel`，并结束
    - 若对方未 Ready：切到 `EE_WAITFOROTHER`，发送同样的双方提示

- **End(player, ET_CANCEL)**
  - 对方会收到“对方取消交易”提示
  - 执行 `DoCancel` 并结束

- **DoExchange() 结算检查**
  1. **背包空间**：双方各自要能装下对方给的 itemcount
  2. **货币上限**：用 `TestAddGold` 与 `TestAddMoney(MT_YUANBAO)` 检查“对方拿不下”
  3. **交换落地顺序**：
     - 双向 `AddBagItem(item, FALSE, TRUE, FALSE)`
     - 双方 `SendWeightChanged()`
     - 双向加金币/元宝（注意：这里是 Add，不涉及扣，因为扣发生在 PutTradeMoney 时）
     - 双方发 `SM_TRADEEND` 并 `SaySystemAttrib(CC_EXCHANGE,"交易成功")`

- **DoCancel() 回滚**
  - 返还物品：`AddBagItem(item, TRUE, FALSE, FALSE)`
  - 返还金币/元宝：`AddGold(dwGold, FALSE)` / `AddMoney(dwYuanbao, FALSE)`
  - 双方发 `SM_TRADECANCELED` 并提示“交易取消”

- **结束清理**
  - 交易结束时必须：
    - 双方 `SetExchangeObject(NULL)`
    - `CExchangeObjectMgr::EndExchange(this)`（池/生命周期回收）

- **C# 对照结论（需要修）**
  - `TradeSystem.cs` 目前是“范围<=5格”的 Begin；但 C++ 是“前方一格 + 双向面对”（第四轮已指出）
  - `PutMoney` 目前实现为“直接扣 amount”，但 C++ 语义是“设置交易栏金额=amount”，并支持减少时返还（第四轮已指出）
  - C++ 的“交易错误包”是 `0x2a4`（PutItem 阶段的硬失败提示），C# 目前用 `SM_TRADEERROR(0x291?)`，需要核对 cmd 常量映射

---

### 12.2 地面物品拾取：`MirWorld_Server\\GameServer\\DownItemMgr.cpp/.h`

- **拾取真正落地在 `CDownItemMgr::PickupItem(map, downItem, player)`**
  - 前置：必须先 `map->RemoveObject(downItem)` 成功
  - 取出 `ITEM item = downItem->GetItem()`，以及 `ITEMCLASS* pClass`

- **金币/祝福油类特殊分支**
  - 条件：`(item.dwMakeIndex & 0x80000000) && item.baseitem.btStdMode == 255`
  - `btShape == 0`：金币
    - 金额 = `item.wCurDura | (item.wMaxDura<<16)`
    - 用 `AddGold(dwGold)` 尝试加钱，失败提示 `SD_STDERROR_CANNOTPICKUPMOREGOLD`
  - `btShape in [1..4]`：设置 `SF_GODBLESS`
    - `dwParam = (minDef<<16)|shape`
    - 超时 = `specialpower * 60000`
  - 若成功（或 map 无法重新 AddObject）：
    - `DeleteItem(item.dwMakeIndex)` + `DeleteDownItemObject`
    - 否则会尝试把 downItem 加回 map

- **普通物品分支**
  - `AddBagItem(item, FALSE, TRUE)` 成功：删除地面对象
  - 失败：尝试把对象 `AddObject` 加回 map；加回失败则删除 item + 对象

- **拾取成功后的脚本钩子与可能的二次删除/更新**
  - 先 `OnPickupItem(*p, x, y)`
  - 若 `pClass->szPickupPage` 非空：
    - `SetUsingItem(p)`
    - `CSystemScript::Execute(scriptTarget, pickupPage, 0)`
    - 若脚本把 `p->dwParam[3]==UR_DELETED`：
      - 删除背包 item + DeleteItem(makeIndex) + `SendTakeBagItem(&item)`
    - 若 `item.dwParam[3]==UR_UPDATED`：`SendUpdateItem(*p)`

- **C# 对照结论（需要修/补齐）**
  1. “金币用 duras 拼金额”的约定需要对齐，否则客户端/掉落系统会出现金币拾取异常
  2. `SF_GODBLESS` 的拾取触发路径依赖 downItem 的 `btStdMode==255` 与 `btShape`，C# 若没有该分支会缺失玩法
  3. 拾取后脚本页 `szPickupPage` 的执行与 UR_DELETED/UPDATED 的二次包（`SendTakeBagItem/SendUpdateItem`）是客户端一致性的关键

---

### 12.3 私人商店结构体与 on-wire 字段：`human_item.cpp` + `HumanPlayer.cpp`

> 私店相关结构体定义不在 `HumanPlayer.h`，但它的字段来源在 `CHumanPlayer::GetPrivateShopView` 与 `SendStart/Stop/Show` 中可完全还原。

- **PrivateShopItemCache（缓存结构）**
  - `struct PrivateShopItemCache { ITEM* pItem; money_type pricetype; DWORD dwPrice; }`（来自 `localdefine.h`）

- **`PRIVATESHOPHEADER` 的字段来源（来自 `CHumanPlayer::GetPrivateShopView`）**
  - `szName`：来自 `m_szPrivateShopName`（写入长度约 50/51）
  - `dw1`：来自 `m_wPrivateShopSign`（注意：字段名是 dw1，但值是 sign）
  - `w1`：来自 `m_wPrivateShopStyle`
  - `w2`：通常作为“状态/标志位”使用（GetView 里赋 0，Start/Stop/Show 会覆盖）
  - `btFlag`：来自 `m_wPrivateShopFlags`

- **开始/停止摆摊的两种广播包**
  - `0x80d7`：payload 是 **两个 DWORD**（从 `&psheader.w1` 起取 `DWORD[2]`）
  - `0xfca0`：向周围广播 `PRIVATESHOPHEADER`（用于视野内显示摊位信息）
  - 开始：`psheader.w2 = 0`；停止：`psheader.w2 = 0xff`

- **视野消息注入（非常关键）**
  - 若玩家当前 action 为 `AT_PRIVATESHOP`
    - 在 `GetViewmsg` 里会额外 Encode 一个 `0xfca0` 包，让新进入视野的人立刻看到摊位
    - 这里的 payload 只带 header（没有 item 列表）

- **店铺页面展示 `SendPrivateShopPage`/`UpdatePrivateShopToAround`（第四轮已覆盖，补充结构层）**
  - `0xfca0` 在“页面展示/刷新”时 payload 是 `PRIVATESHOPHEADER + ITEMCLIENT[]`
  - 每个 ITEMCLIENT 会把价格写进 `baseitem.nPrice`，货币类型写进 `baseitem.btPriceType`
  - `header.wCount` 是条目数；`header.w2` 在 show/refresh 语义下被用作 1/2 标志

- **C# 对照结论（结构层）**
  1. 需要在 C# 定义 `[StructLayout(LayoutKind.Sequential, Pack=1)]` 的 `PrivateShopHeader/PrivateShopQuery/PrivateShopShow`（其中 header 字段顺序必须与 C++ 内存布局一致）
  2. 对齐 `0x80d7` 的 payload 是从 `&header.w1` 取 `DWORD[2]` 这一点：意味着并不是完整 header，而是压缩字段组合
  3. `GetViewmsg` 注入 `0xfca0(header-only)` 是“视野同步”关键；否则玩家走近不会看到摊位，除非额外触发广播

---
