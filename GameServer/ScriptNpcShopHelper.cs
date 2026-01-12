using System;
using System.Collections.Generic;
using System.Linq;

namespace GameServer
{
    internal static class ScriptNpcShopHelper
    {
        internal readonly struct GoodsEntry
        {
            public GoodsEntry(string templateName, int defaultCount, int refreshTime)
            {
                TemplateName = templateName;
                DefaultCount = defaultCount;
                RefreshTime = refreshTime;
            }

            public string TemplateName { get; }
            public int DefaultCount { get; }
            public int RefreshTime { get; }
        }

        internal static List<GoodsEntry> ExtractGoods(ScriptObject scriptObject)
        {
            var result = new List<GoodsEntry>();
            if (scriptObject?.Lines == null || scriptObject.Lines.Count == 0)
                return result;

            int start = -1;
            for (int i = 0; i < scriptObject.Lines.Count; i++)
            {
                string line = scriptObject.Lines[i].Trim();
                if (TryParseHeader(line, out string header) &&
                    string.Equals(header, "goods", StringComparison.OrdinalIgnoreCase))
                {
                    start = i + 1;
                    break;
                }
            }

            if (start < 0 || start >= scriptObject.Lines.Count)
                return result;

            for (int i = start; i < scriptObject.Lines.Count; i++)
            {
                string raw = scriptObject.Lines[i];
                string line = raw.Trim();

                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                if (TryParseHeader(line, out _))
                    break;

                
                var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3)
                    continue;

                if (!int.TryParse(tokens[^2], out int count))
                    continue;

                if (!int.TryParse(tokens[^1], out int refresh))
                    continue;

                string name = string.Join(" ", tokens.Take(tokens.Length - 2)).Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                result.Add(new GoodsEntry(name, count, refresh));
            }

            return result;
        }

        internal static uint CalcBuyPrice(ItemDefinition definition, float buyPercent)
        {
            if (definition == null)
                return 0;

            double v = definition.BuyPrice * buyPercent;
            if (double.IsNaN(v) || double.IsInfinity(v))
                v = definition.BuyPrice;

            if (v < 0)
                v = 0;

            uint price = (uint)Math.Round(v);
            if (price == 0 && definition.BuyPrice > 0)
                price = 1;

            return price;
        }

        internal static uint CalcSellPrice(ItemInstance item, float sellPercent)
        {
            if (item?.Definition == null)
                return 0;

            double v = item.Definition.BuyPrice * sellPercent;
            if (double.IsNaN(v) || double.IsInfinity(v))
                v = item.Definition.BuyPrice;

            if (v < 0)
                v = 0;

            uint unitPrice = (uint)Math.Round(v);
            int count = item.Count > 0 ? item.Count : 1;
            ulong total = (ulong)unitPrice * (ulong)count;
            return total > uint.MaxValue ? uint.MaxValue : (uint)total;
        }

        private static bool TryParseHeader(string line, out string header)
        {
            header = string.Empty;
            if (string.IsNullOrWhiteSpace(line))
                return false;

            string trimmed = line.Trim();
            if (trimmed.Length < 3 || trimmed[0] != '[' || trimmed[^1] != ']')
                return false;

            header = trimmed.Substring(1, trimmed.Length - 2).Trim();
            return header.Length > 0;
        }
    }
}

