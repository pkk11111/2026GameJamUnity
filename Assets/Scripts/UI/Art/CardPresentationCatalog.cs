// 职责：稳定选项ID到卡图的显式映射及已审核的精简文案；不创建奖励或推断玩法。
// 依赖：Core、Unity；维护Dada；交接docs/handoffs/Dada.handoff；规范AGENTS.md。
using System;
using System.Text.RegularExpressions;
using Regrowth.Core;
using UnityEngine;

namespace Regrowth.UI.Art
{
    [CreateAssetMenu(menuName = "pawgatory/UI/Card Presentation Catalog")]
    public sealed class CardPresentationCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Artwork
        {
            public string keyword;
            public Sprite sprite;
            public string previewTitle;
            [TextArea] public string previewDescription;
        }

        [Serializable]
        public sealed class Binding
        {
            public string optionId;
            public string artworkKeyword;
            [Tooltip("只替换这段已审核源文案；源文案变更后保留原文，避免隐藏新数值/效果。")]
            [TextArea] public string sourceDescription;
            public string shortTitle;
            public string regrowthTitle;
            [TextArea] public string shortDescription;
        }

        public Artwork[] artworks = Array.Empty<Artwork>();
        public Binding[] bindings = Array.Empty<Binding>();

        public Artwork FindArtwork(string keyword)
        {
            return Array.Find(artworks, entry => string.Equals(entry.keyword, keyword, StringComparison.Ordinal));
        }

        /// <summary>只认明确列出的稳定ID，不按标题子串或文件名模糊猜图。原ChoiceOption不变。</summary>
        public void Resolve(ChoiceOption option, out Sprite sprite, out string title, out string description)
        {
            title = option.Title;
            description = option.Description;
            sprite = null;
            // Dynamic copies preserve numbers supplied by the real transaction. No values are calculated here.
            if (ResolveDynamic(option, out string key, out string heading, out string body))
            {
                sprite = FindArtwork(key)?.sprite;
                title = heading;
                description = body;
                return;
            }
            var binding = Array.Find(bindings, entry => string.Equals(entry.optionId, option.Id, StringComparison.Ordinal));
            if (binding == null)
            {
                return;
            }

            sprite = FindArtwork(binding.artworkKeyword)?.sprite;
            if (!string.Equals(option.Description, binding.sourceDescription, StringComparison.Ordinal))
            {
                return;
            }

            title = !string.IsNullOrEmpty(binding.regrowthTitle)
                && option.Title.StartsWith("Regrow ", StringComparison.Ordinal)
                ? binding.regrowthTitle : binding.shortTitle;
            description = binding.shortDescription;
        }

        private static bool ResolveDynamic(ChoiceOption option, out string key, out string title, out string body)
        {
            key = ""; title = option.Title; body = option.Description;
            string source = option.Description;
            string reason = "";
            int split = source.IndexOf("\nUnavailable: ", StringComparison.Ordinal);
            if (split >= 0) { reason = source.Substring(split + 14); source = source.Substring(0,split); }
            Match m;
            switch (option.Id)
            {
                case "heal":
                    m = Regex.Match(source,@"^Restore (\d+) HP\.( Already at full health\.)?$");
                    if (!m.Success) { return false; }
                    key="RestoreHP"; title="Restore HP"; body="Restore "+m.Groups[1]+" HP."+(m.Groups[2].Success ? "\nAlready at full HP." : ""); break;
                case "max-health":
                    m=Regex.Match(source,@"^Increase maximum and current HP by (\d+)\.$");
                    if (!m.Success) { return false; }
                    key="MaxHPUp"; title="Max HP Up"; body="Max and current HP\n+"+m.Groups[1]+"."; break;
                case "attack":
                    m=Regex.Match(source,@"^All attacks: \+(\d+) damage per hit \(including each fire tick\)\.$");
                    if (!m.Success) { return false; }
                    key="AttackUp"; title="Attack Up"; body="All attacks +"+m.Groups[1]+".\nIncludes fire ticks."; break;
                case "COST_CURRENT_HP":
                    m=Regex.Match(source,@"^Pay (\d+) HP \((\d+)% current\)\. HP: (\d+) -> (-?\d+)$");
                    if (!m.Success) { return false; }
                    key="PayHP"; title="Pay HP"; body="Pay "+m.Groups[1]+" HP ("+m.Groups[2]+"%).\nHP: "+m.Groups[3]+" → "+m.Groups[4]; break;
                case "COST_MAX_HP":
                    m=Regex.Match(source,@"^Maximum HP: (\d+) -> (-?\d+); current: (\d+) -> (-?\d+)$");
                    if (!m.Success) { return false; }
                    key="MaxHPDown"; title="Max HP Down"; body="Max HP: "+m.Groups[1]+" → "+m.Groups[2]+"\nHP: "+m.Groups[3]+" → "+m.Groups[4]; break;
                case "COST_ATTACK":
                    m=Regex.Match(source,@"^Damage -(\d+): bite (\d+) -> (-?\d+), sword (\d+) -> (-?\d+), fire/tick (\d+) -> (-?\d+)\.$");
                    if (!m.Success) { return false; }
                    key="AttackDown"; title="Attack Down"; body="Bite: "+m.Groups[2]+" → "+m.Groups[3]+"\nSword: "+m.Groups[4]+" → "+m.Groups[5]+"\nFire/tick: "+m.Groups[6]+" → "+m.Groups[7]; break;
                case "COST_ENEMY_HP":
                    m=Regex.Match(source,@"^All living and future enemies: \+(\d+) maximum HP\. Keep current HP ratio\.$");
                    if (!m.Success) { return false; }
                    key="EnemyHPUp"; title="Enemy HP Up"; body="All enemies: +"+m.Groups[1]+" HP.\nKeep current HP %.\nIncludes future enemies."; break;
                case "COST_ENEMY_ATTACK":
                    m=Regex.Match(source,@"^All living and future enemies: \+(\d+) contact damage\.$");
                    if (!m.Success) { return false; }
                    key="EnemyAttackUp"; title="Enemy Attack Up"; body="Contact damage +"+m.Groups[1]+".\nAll current and\nfuture enemies."; break;
                case "enter":
                    if(source=="Give up arms and sword for this attempt. Basic bite remains available.")
                    { key="LoseArms"; title="Enter Challenge"; body="Lose arms and sword.\nKeep bite.\nEnter this attempt."; }
                    else if(source=="Give up legs and double jump for this attempt. Basic movement and jump remain available.")
                    { key="LoseLegs"; title="Enter Challenge"; body="Lose legs / double jump.\nKeep basic jump.\nEnter this attempt."; }
                    else if(source=="Start this attempt without giving up anything else.")
                    { title="Enter Challenge"; body="Enter this attempt.\nNo further cost."; }
                    else { return false; }
                    break;
                default: return false;
            }
            if (reason.Length>0)
            {
                string shortReason;
                if(reason=="Would cause death.") { shortReason="Unavailable: fatal cost."; }
                else if(reason=="Would reduce maximum health to zero.") { shortReason="Unavailable: max HP 0."; }
                else if(Regex.IsMatch(reason,@"^Attack cannot fall below \d+ damage points\.$"))
                { shortReason="Minimum damage: "+Regex.Match(reason,@"\d+").Value+"."; }
                else { return false; }
                body+="\n"+shortReason;
            }
            return true;
        }
    }
}
