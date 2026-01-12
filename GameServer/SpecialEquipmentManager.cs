using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MirCommon.Utils;

namespace GameServer
{
    
    
    
    
    
    public class SpecialEquipmentManager
    {
        private static SpecialEquipmentManager? _instance;
        
        
        
        
        
        private readonly Dictionary<string, string> _specialEquipmentFunctions;
        
        
        
        
        
        private readonly Dictionary<string, SpecialEquipmentEffect> _specialEquipmentEffects;

        
        
        
        public static SpecialEquipmentManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SpecialEquipmentManager();
                }
                return _instance;
            }
        }

        
        
        
        private SpecialEquipmentManager()
        {
            _specialEquipmentFunctions = new Dictionary<string, string>();
            _specialEquipmentEffects = new Dictionary<string, SpecialEquipmentEffect>();
        }

        
        
        
        
        
        
        public bool LoadSpecialEquipmentFunction(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    LogManager.Default.Warning($"特殊装备配置文件不存在: {filePath}");
                    return false;
                }

                LogManager.Default.Info($"加载特殊装备配置: {filePath}");
                
                var lines = SmartReader.ReadAllLines(filePath);
                int loadedCount = 0;
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    
                    var parts = line.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string equipmentName = parts[0].Trim();
                        string functionConfig = parts[1].Trim();
                        
                        
                        _specialEquipmentFunctions[equipmentName] = functionConfig;
                        
                        
                        var effect = ParseSpecialEquipmentEffect(equipmentName, functionConfig);
                        if (effect != null)
                        {
                            _specialEquipmentEffects[equipmentName] = effect;
                        }
                        
                        loadedCount++;
                        LogManager.Default.Debug($"特殊装备: {equipmentName} -> {functionConfig}");
                    }
                }

                LogManager.Default.Info($"成功加载 {loadedCount} 个特殊装备配置");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"加载特殊装备配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        
        
        
        private SpecialEquipmentEffect? ParseSpecialEquipmentEffect(string equipmentName, string functionConfig)
        {
            try
            {
                var effect = new SpecialEquipmentEffect
                {
                    EquipmentName = equipmentName,
                    FunctionConfig = functionConfig
                };

                
                
                
                var configParts = functionConfig.Split('/');
                
                foreach (var part in configParts)
                {
                    var effectPart = part.Trim();
                    if (effectPart.Contains("+"))
                    {
                        var effectParts = effectPart.Split('+');
                        if (effectParts.Length == 2)
                        {
                            string attribute = effectParts[0].Trim();
                            if (int.TryParse(effectParts[1].Trim(), out int value))
                            {
                                effect.AttributeBonuses[attribute] = value;
                            }
                        }
                    }
                    else if (effectPart.Contains("-"))
                    {
                        var effectParts = effectPart.Split('-');
                        if (effectParts.Length == 2)
                        {
                            string attribute = effectParts[0].Trim();
                            if (int.TryParse(effectParts[1].Trim(), out int value))
                            {
                                effect.AttributeBonuses[attribute] = -value;
                            }
                        }
                    }
                }

                return effect;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"解析特殊装备效果失败: {equipmentName}", exception: ex);
                return null;
            }
        }

        
        
        
        public string GetSpecialEquipmentFunction(string equipmentName)
        {
            return _specialEquipmentFunctions.TryGetValue(equipmentName, out var function) 
                ? function 
                : string.Empty;
        }

        
        
        
        public SpecialEquipmentEffect? GetSpecialEquipmentEffect(string equipmentName)
        {
            return _specialEquipmentEffects.TryGetValue(equipmentName, out var effect) 
                ? effect 
                : null;
        }

        
        
        
        public bool IsSpecialEquipment(string equipmentName)
        {
            return _specialEquipmentFunctions.ContainsKey(equipmentName);
        }

        
        
        
        public bool ApplySpecialEquipmentEffect(HumanPlayer player, string equipmentName)
        {
            if (player == null || string.IsNullOrEmpty(equipmentName))
                return false;

            var effect = GetSpecialEquipmentEffect(equipmentName);
            if (effect == null)
                return false;

            try
            {
                
                foreach (var bonus in effect.AttributeBonuses)
                {
                    ApplyAttributeBonus(player, bonus.Key, bonus.Value);
                    LogManager.Default.Debug($"玩家 {player.Name} 应用特殊装备效果: {equipmentName} -> {bonus.Key}: {bonus.Value}");
                }

                
                if (effect.HasSpecialEffect)
                {
                    ApplySpecialEffect(player, effect);
                }

                LogManager.Default.Info($"玩家 {player.Name} 应用特殊装备效果: {equipmentName}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"应用特殊装备效果失败: {equipmentName} 玩家: {player.Name}", exception: ex);
                return false;
            }
        }

        
        
        
        public bool RemoveSpecialEquipmentEffect(HumanPlayer player, string equipmentName)
        {
            if (player == null || string.IsNullOrEmpty(equipmentName))
                return false;

            var effect = GetSpecialEquipmentEffect(equipmentName);
            if (effect == null)
                return false;

            try
            {
                
                foreach (var bonus in effect.AttributeBonuses)
                {
                    RemoveAttributeBonus(player, bonus.Key, bonus.Value);
                    LogManager.Default.Debug($"玩家 {player.Name} 移除特殊装备效果: {equipmentName} -> {bonus.Key}: {bonus.Value}");
                }

                
                if (effect.HasSpecialEffect)
                {
                    RemoveSpecialEffect(player, effect);
                }

                LogManager.Default.Info($"玩家 {player.Name} 移除特殊装备效果: {equipmentName}");
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"移除特殊装备效果失败: {equipmentName} 玩家: {player.Name}", exception: ex);
                return false;
            }
        }

        
        
        
        public List<string> GetAllSpecialEquipmentNames()
        {
            return new List<string>(_specialEquipmentFunctions.Keys);
        }

        
        
        
        public int GetSpecialEquipmentCount()
        {
            return _specialEquipmentFunctions.Count;
        }

        
        
        
        public bool Reload(string filePath)
        {
            try
            {
                _specialEquipmentFunctions.Clear();
                _specialEquipmentEffects.Clear();
                return LoadSpecialEquipmentFunction(filePath);
            }
            catch (Exception ex)
            {
                LogManager.Default.Error($"重新加载特殊装备配置失败: {filePath}", exception: ex);
                return false;
            }
        }

        
        
        
        private void ApplyAttributeBonus(HumanPlayer player, string attributeName, int bonusValue)
        {
            if (player == null || bonusValue == 0)
                return;

            
            switch (attributeName.ToLower())
            {
                case "攻击力":
                case "dc":
                    player.Stats.MinDC += bonusValue;
                    player.Stats.MaxDC += bonusValue;
                    break;
                    
                case "魔法力":
                case "mc":
                    player.Stats.MinMC += bonusValue;
                    player.Stats.MaxMC += bonusValue;
                    break;
                    
                case "道术力":
                case "sc":
                    player.Stats.MinSC += bonusValue;
                    player.Stats.MaxSC += bonusValue;
                    break;
                    
                case "防御力":
                case "ac":
                    player.Stats.MinAC += bonusValue;
                    player.Stats.MaxAC += bonusValue;
                    break;
                    
                case "魔防力":
                case "mac":
                    player.Stats.MinMAC += bonusValue;
                    player.Stats.MaxMAC += bonusValue;
                    break;
                    
                case "准确":
                case "accuracy":
                    player.Accuracy += bonusValue;
                    break;
                    
                case "敏捷":
                case "agility":
                    player.Agility += bonusValue;
                    break;
                    
                case "幸运":
                case "lucky":
                    player.Lucky += bonusValue;
                    break;
                    
                case "最大生命值":
                case "maxhp":
                    player.MaxHP += bonusValue;
                    if (player.CurrentHP > player.MaxHP)
                        player.CurrentHP = player.MaxHP;
                    break;
                    
                case "最大魔法值":
                case "maxmp":
                    player.MaxMP += bonusValue;
                    if (player.CurrentMP > player.MaxMP)
                        player.CurrentMP = player.MaxMP;
                    break;
                    
                case "当前生命值":
                case "hp":
                    player.CurrentHP = Math.Min(player.CurrentHP + bonusValue, player.MaxHP);
                    break;
                    
                case "当前魔法值":
                case "mp":
                    player.CurrentMP = Math.Min(player.CurrentMP + bonusValue, player.MaxMP);
                    break;
                    
                case "基础攻击力":
                case "basedc":
                    player.BaseDC += bonusValue;
                    break;
                    
                case "基础魔法力":
                case "basemc":
                    player.BaseMC += bonusValue;
                    break;
                    
                case "基础道术力":
                case "basesc":
                    player.BaseSC += bonusValue;
                    break;
                    
                case "基础防御力":
                case "baseac":
                    player.BaseAC += bonusValue;
                    break;
                    
                case "基础魔防力":
                case "basemac":
                    player.BaseMAC += bonusValue;
                    break;
                    
                default:
                    LogManager.Default.Warning($"未知的属性名称: {attributeName}");
                    break;
            }
        }

        
        
        
        private void RemoveAttributeBonus(HumanPlayer player, string attributeName, int bonusValue)
        {
            if (player == null || bonusValue == 0)
                return;

            
            ApplyAttributeBonus(player, attributeName, -bonusValue);
        }

        
        
        
        private void ApplySpecialEffect(HumanPlayer player, SpecialEquipmentEffect effect)
        {
            if (player == null || effect == null || string.IsNullOrEmpty(effect.SpecialEffectType))
                return;

            
            switch (effect.SpecialEffectType.ToLower())
            {
                case "麻痹":
                    
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得麻痹效果");
                    break;
                    
                case "复活":
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得复活效果");
                    break;
                    
                case "隐身":
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得隐身效果");
                    break;
                    
                case "传送":
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得传送效果");
                    break;
                    
                case "吸血":
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得吸血效果");
                    break;
                    
                case "吸魔":
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得吸魔效果");
                    break;
                    
                case "破防":
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得破防效果");
                    break;
                    
                case "破魔":
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得破魔效果");
                    break;
                    
                case "暴击":
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得暴击效果");
                    break;
                    
                case "闪避":
                    
                    LogManager.Default.Info($"玩家 {player.Name} 获得闪避效果");
                    break;
                    
                default:
                    LogManager.Default.Warning($"未知的特殊效果类型: {effect.SpecialEffectType}");
                    break;
            }
        }

        
        
        
        private void RemoveSpecialEffect(HumanPlayer player, SpecialEquipmentEffect effect)
        {
            if (player == null || effect == null || string.IsNullOrEmpty(effect.SpecialEffectType))
                return;

            
            switch (effect.SpecialEffectType.ToLower())
            {
                case "麻痹":
                    LogManager.Default.Info($"玩家 {player.Name} 移除麻痹效果");
                    break;
                    
                case "复活":
                    LogManager.Default.Info($"玩家 {player.Name} 移除复活效果");
                    break;
                    
                case "隐身":
                    LogManager.Default.Info($"玩家 {player.Name} 移除隐身效果");
                    break;
                    
                case "传送":
                    LogManager.Default.Info($"玩家 {player.Name} 移除传送效果");
                    break;
                    
                case "吸血":
                    LogManager.Default.Info($"玩家 {player.Name} 移除吸血效果");
                    break;
                    
                case "吸魔":
                    LogManager.Default.Info($"玩家 {player.Name} 移除吸魔效果");
                    break;
                    
                case "破防":
                    LogManager.Default.Info($"玩家 {player.Name} 移除破防效果");
                    break;
                    
                case "破魔":
                    LogManager.Default.Info($"玩家 {player.Name} 移除破魔效果");
                    break;
                    
                case "暴击":
                    LogManager.Default.Info($"玩家 {player.Name} 移除暴击效果");
                    break;
                    
                case "闪避":
                    LogManager.Default.Info($"玩家 {player.Name} 移除闪避效果");
                    break;
                    
                default:
                    LogManager.Default.Warning($"未知的特殊效果类型: {effect.SpecialEffectType}");
                    break;
            }
        }

        
        
        
        public void Update()
        {
            
            UpdateTemporaryEffects();
        }

        
        
        
        private void UpdateTemporaryEffects()
        {
            
            
            
        }
    }

    
    
    
    public class SpecialEquipmentEffect
    {
        
        
        
        public string EquipmentName { get; set; } = string.Empty;

        
        
        
        public string FunctionConfig { get; set; } = string.Empty;

        
        
        
        
        public Dictionary<string, int> AttributeBonuses { get; set; } = new Dictionary<string, int>();

        
        
        
        public string SpecialEffectType { get; set; } = string.Empty;

        
        
        
        public int Duration { get; set; }

        
        
        
        public int TriggerProbability { get; set; }

        
        
        
        public int Cooldown { get; set; }

        
        
        
        public int GetAttributeBonus(string attributeName)
        {
            return AttributeBonuses.TryGetValue(attributeName, out int value) ? value : 0;
        }

        
        
        
        public bool HasSpecialEffect => !string.IsNullOrEmpty(SpecialEffectType);

        
        
        
        public bool IsTemporaryEffect => Duration > 0;
    }
}
