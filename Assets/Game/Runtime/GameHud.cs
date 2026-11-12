using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombs
{
    public sealed partial class GameSession
    {
        private static readonly Color Panel = new(.085f, .12f, .064f, .72f);
        private static readonly Color DeepPanel = new(.06f, .09f, .047f, .86f);
        private static readonly Color Slot = new(.11f, .15f, .076f, .60f);
        private static readonly Color TextColor = new(.96f, .97f, .92f);
        private static readonly Color MutedText = new(.76f, .81f, .69f);
        private static readonly Color BrightGold = new(1, .76f, .35f);
        private static readonly Color Green = new(.33f, .55f, .025f);
        private static readonly Color Red = new(.66f, .19f, .18f);
        private static readonly Color Purple = new(.50f, .43f, .83f);
        private static readonly string[] TabNames = { "Weapons", "Armor", "Hats", "Pets", "Utility" };
        private static readonly ItemKind[][] ShopItems =
        {
            new[] { ItemKind.Pickaxe, ItemKind.Spear, ItemKind.Bow, ItemKind.Bomb },
            new[] { ItemKind.Shield }, new[] { ItemKind.Hat },
            new[] { ItemKind.Carl, ItemKind.Woody }, new[] { ItemKind.HealthPotion, ItemKind.PetPotion }
        };
        private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
        private readonly Dictionary<(int, TextAnchor, bool), GUIStyle> labelStyles = new();
        private GUIStyle inputStyle;
        private Font font;

        private float Scale => Mathf.Min(Mathf.Clamp(Screen.height / 880f, .5f, 1.4f), Screen.width / 720f);
        private float Width => Screen.width / Scale;
        private float Height => Screen.height / Scale;
        private Vector2 Mouse => new(Input.mousePosition.x / Scale, (Screen.height - Input.mousePosition.y) / Scale);
        private Rect ResourcesRect => new(Width - 240, Height - 180, 220, 104);
        private Rect MapRect => new(20, Height - 159, 140, 139);
        private Rect ClockRect => new(20, Height - 205, 140, 25);
        private float BuildSlotSize => Mathf.Min(56, (Width - 420 - 80) / 11);
        private float BuildSlotStart => (Width - BuildSlotStride * 11 + 8) * .5f - 40;
        private float BuildSlotStride => BuildSlotSize + 8;
        private Rect BuildRect(int index) => new(BuildSlotStart + index * BuildSlotStride, Height - 20 - BuildSlotSize,
            BuildSlotSize, BuildSlotSize);
        private Rect WeaponRect(int index) => new(Width * .5f - 194 + index * 55, Height - 145, 48, 48);
        private Rect ShopRect => new((Width - Mathf.Min(730, Width - 96)) * .5f, Mathf.Max(58, (Height - 469) * .32f),
            Mathf.Min(730, Width - 96), 469);
        private Rect SettingsRect => new((Width - 400) * .5f, (Height - 292) * .5f, 400, 292);
        private Rect SideRect(bool right, int row) => new(right ? Width - 56 : 0, Height * .5f - 56 + 56 * row, 56, 56);

        private bool BlocksWorld(Vector2 screen)
        {
            Vector2 point = screen / Scale;
            if (settings || shop || intro || run.Dead || run.Defeated) return true;
            if (ResourcesRect.Contains(point) || MapRect.Contains(point) || ClockRect.Contains(point) ||
                new Rect(Width - 240, Height - 58, 220, 38).Contains(point) ||
                SideRect(false, 0).Contains(point) || SideRect(false, 1).Contains(point) ||
                SideRect(true, 0).Contains(point) || SideRect(true, 1).Contains(point)) return true;
            for (int i = 0; i < 11; i++)
                if ((i != 10 || run.Generator == null) && BuildRect(i).Contains(point)) return true;
            for (int i = 0; i < 7; i++)
                if (WeaponRect(i).Contains(point)) return true;
            return selected != null && StructureRect().Contains(point);
        }

        private void OnGUI()
        {
            if (run == null) return;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
            bool previousEnabled = GUI.enabled;
            if (intro)
            {
                GUI.enabled = previousEnabled && !settings;
                DrawIntro();
            }
            else
            {
                GUI.enabled = previousEnabled && !shop && !settings && !run.Dead && !run.Defeated;
                DrawWorldLabels();
                DrawStatus();
                DrawToolbar();
                DrawSideButtons();
                DrawMap();
                DrawNotice();
                if (selected != null && !shop && !settings) DrawStructure();
                if (shop)
                {
                    GUI.enabled = previousEnabled && !settings;
                    DrawShop();
                }
                if (run.Dead || run.Defeated)
                {
                    GUI.enabled = previousEnabled && !settings;
                    DrawDeath();
                }
            }
            GUI.enabled = previousEnabled;
            if (settings) DrawSettings();
            GUI.matrix = matrix;
        }

        private void DrawIntro()
        {
            Fill(new Rect(0, 0, Width, Height), new Color(.035f, .052f, .023f, .42f));
            float center = Width * .5f;
            float titleY = Mathf.Max(41, Height * .11f);
            var title = new Rect(center - 250, titleY, 500, 91);
            Text(new Rect(title.x + 3, title.y + 4, title.width, title.height), "ZOMBS.io", 70, new Color(.10f, .12f, .07f),
                TextAnchor.MiddleCenter, true);
            Text(title, "ZOMBS.io", 70, TextColor, TextAnchor.MiddleCenter, true);
            Text(new Rect(center - 250, titleY + 86, 500, 37), "Build. Defend. Survive.", 28, TextColor, TextAnchor.MiddleCenter, true);
            float startY = titleY + 148;
            var box = new Rect(center - 195, startY, 390, 133);
            Fill(box, DeepPanel);
            inputStyle ??= new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft, font = font, fontSize = 17, padding = new RectOffset(11, 10, 0, 0)
            };
            GUI.SetNextControlName("Nickname");
            profile.nickname = GUI.TextField(new Rect(box.x + 16, box.y + 16, 358, 41), profile.nickname ?? "Player", 20, inputStyle);
            if (Button(new Rect(box.x + 16, box.y + 69, 358, 48), "Play", Green, 20) ||
                (!settings && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                Begin();
                return;
            }
            float cardsY = startY + 152;
            var cards = new Rect(center - 333, cardsY, 666, 185);
            Fill(cards, Panel);
            Text(new Rect(cards.x + 22, cards.y + 13, 330, 31), "How to Play", 21, TextColor, bold: true);
            Text(new Rect(cards.x + 22, cards.y + 51, 311, 22), "Gather wood and stone with your pickaxe.", 15, MutedText);
            Text(new Rect(cards.x + 22, cards.y + 80, 311, 22), "Place a Oxygen Generator to establish your base.", 15, MutedText);
            Text(new Rect(cards.x + 22, cards.y + 109, 311, 22), "Build towers and survive the night.", 15, MutedText);
            Text(new Rect(cards.x + 22, cards.y + 138, 311, 22), $"Best wave  {profile.bestWave}    •    Best score  {profile.bestScore:N0}", 15, BrightGold);
            Fill(new Rect(center, cards.y + 18, 1, cards.height - 36), new Color(1, 1, 1, .15f));
            Text(new Rect(center + 22, cards.y + 13, 292, 31), "Controls", 21, TextColor, bold: true);
            Text(new Rect(center + 22, cards.y + 51, 292, 22), "WASD   Move     •     Mouse   Aim", 15, MutedText);
            Text(new Rect(center + 22, cards.y + 80, 292, 22), "Left click   Gather, attack, or build", 15, MutedText);
            Text(new Rect(center + 22, cards.y + 109, 292, 22), "B   Shop     •     Q   Cycle weapons", 15, MutedText);
            Text(new Rect(center + 22, cards.y + 138, 292, 22), "Space   Auto-attack     •     Esc   Cancel", 15, MutedText);
            if (Button(new Rect(Width - 106, 20, 86, 34), "Settings", Slot, 14)) settings = true;
        }

        private void DrawWorldLabels()
        {
            if (!run.Dead && !run.Defeated)
                WorldLabel(run.Player + Vector2.up * 1.48f, profile.nickname, TextColor);
            world.DrawLabels(WorldLabel);
        }

        private Vector2 ToGui(Vector2 point)
        {
            Vector3 screen = sceneCamera.WorldToScreenPoint(point);
            return new Vector2(screen.x / Scale, (Screen.height - screen.y) / Scale);
        }

        private void WorldLabel(Vector2 point, string label, Color color)
        {
            Vector2 screen = ToGui(point);
            var rect = new Rect(screen.x - 120, screen.y - 12, 240, 24);
            var shadow = new Color(.10f, .08f, .13f, color.a);
            for (int i = 0; i < 4; i++)
            {
                Vector2 offset = new(i == 0 ? 2 : i == 1 ? -2 : 0, i == 2 ? 2 : i == 3 ? -2 : 0);
                Text(new Rect(rect.position + offset, rect.size), label, 16, shadow, TextAnchor.MiddleCenter, true);
            }
            Text(rect, label, 16, Color.Lerp(color, color * new Color(.83f, .83f, 1, 1), run.LightBlend),
                TextAnchor.MiddleCenter, true);
        }

        private void DrawStatus()
        {
            var score = new Rect(Width - 305, 20, 285, 48);
            Text(new Rect(score.x, score.y, 133, 18), "Name", 13, MutedText);
            Text(new Rect(score.x + 133, score.y, 104, 18), "Score", 13, MutedText, TextAnchor.MiddleRight);
            Text(new Rect(score.x + 237, score.y, 48, 18), "Wave", 13, MutedText, TextAnchor.MiddleRight);
            Text(new Rect(score.x, score.y + 23, 133, 20), profile.nickname, 14, BrightGold, bold: true);
            Text(new Rect(score.x + 133, score.y + 23, 104, 20), run.Score.ToString("N0"), 14, TextColor, TextAnchor.MiddleRight);
            Text(new Rect(score.x + 237, score.y + 23, 48, 20), WaveText, 14, TextColor, TextAnchor.MiddleRight);

            Rect rect = ResourcesRect;
            Fill(rect, Panel);
            var price = run.Resources;
            Resource(new Rect(rect.x, rect.y, 110, 34), "WOOD", Number(price.wood));
            Resource(new Rect(rect.x + 110, rect.y, 110, 34), "STONE", Number(price.stone));
            Resource(new Rect(rect.x, rect.y + 34, 110, 34), "GOLD", Number(price.gold));
            Resource(new Rect(rect.x + 110, rect.y + 34, 110, 34), "TOKENS", price.tokens.ToString());
            Resource(new Rect(rect.x, rect.y + 68, 220, 34), "WAVE", WaveText);
            Fill(new Rect(rect.x + 110, rect.y, 1, 68), new Color(1, 1, 1, .10f));
            Fill(new Rect(rect.x, rect.y + 34, 220, 1), new Color(1, 1, 1, .10f));
            Fill(new Rect(rect.x, rect.y + 68, 220, 1), new Color(1, 1, 1, .10f));
            HudBar(new Rect(Width - 240, Height - 56, 220, 36), run.Health / catalog.playerHealth, new Color(.42f, .65f, .03f), "HEALTH");
            if (run.MaxShield > 0) HudBar(new Rect(rect.x, Height - 74, 220, 14), run.Shield / run.MaxShield, new Color(.32f, .56f, .77f), null);
        }

        private string WaveText => run.Wave == 0 ? "—" : run.Wave.ToString();

        private void Resource(Rect rect, string label, string value)
        {
            Text(new Rect(rect.x + 9, rect.y, rect.width - 16, rect.height), label, 13, MutedText, bold: true);
            Text(new Rect(rect.x + 8, rect.y, rect.width - 17, rect.height), value, 15, TextColor, TextAnchor.MiddleRight, true);
        }

        private void DrawMap()
        {
            Rect map = MapRect;
            Fill(map, Panel);
            Fill(new Rect(map.center.x, map.y, 1, map.height), new Color(0, 0, 0, .18f));
            Fill(new Rect(map.x, map.center.y, map.width, 1), new Color(0, 0, 0, .18f));
            if (run.Generator != null) Dot(map, run.Generator.Position, 6, BrightGold);
            Dot(map, run.Player, 4, new Color(.68f, .62f, .98f));

            var clock = ClockRect;
            Fill(clock, Panel);
            float width = clock.width - 10;
            float cycle = catalog.daySeconds + catalog.nightSeconds;
            float dayFraction = catalog.daySeconds / cycle;
            float phaseAtLeft = (run.PhaseTime + (run.Night ? catalog.daySeconds : 0)) / cycle - .5f;
            Fill(new Rect(clock.x + 5, clock.y + 4, width, clock.height - 8), Purple);
            for (int repeat = -1; repeat <= 1; repeat++)
            {
                float start = Mathf.Clamp01(repeat - phaseAtLeft);
                float end = Mathf.Clamp01(repeat + dayFraction - phaseAtLeft);
                if (end > start)
                    Fill(new Rect(clock.x + 5 + width * start, clock.y + 4, width * (end - start), clock.height - 8), BrightGold);
            }
            Fill(new Rect(clock.center.x - .75f, clock.y + 1, 1.5f, clock.height - 2), TextColor);
            if (clock.Contains(Mouse))
            {
                string phase = run.Night ? "Night" : "Day";
                Tooltip(new Rect(clock.x, clock.y - 41, 160, 34), $"{phase}  •  {Mathf.CeilToInt(run.PhaseLength - run.PhaseTime)}s");
            }
        }

        private void Dot(Rect map, Vector2 position, float size, Color color)
        {
            Vector2 relative = new(position.x / catalog.worldSize, -position.y / catalog.worldSize);
            Vector2 pixel = map.center + Vector2.Scale(relative, map.size - Vector2.one * 8);
            Fill(new Rect(pixel - Vector2.one * size / 2, Vector2.one * size), color);
        }

        private void DrawToolbar()
        {
            for (int index = 0; index < 7; index++)
            {
                Rect rect = WeaponRect(index);
                bool owned = index < 4 && run.ItemTier((ItemKind)index) > 0;
                bool active = owned && (int)run.Weapon == index;
                Fill(rect, active ? new Color(.80f, .89f, .66f, .22f) : new Color(.58f, .67f, .42f, .13f));
                if (owned)
                {
                    Icon(rect, SpriteArt.Item((ItemKind)index, run.ItemTier((ItemKind)index)), .75f, Color.white);
                    if (Hit(rect))
                    {
                        ResetSelection();
                        run.Equip((ItemKind)index);
                    }
                }
                if (index == 4) DrawSmallItem(rect, ItemKind.HealthPotion, "F", () => run.HealPlayer(), run.Resources.gold >= 100);
                if (index == 5) DrawSmallItem(rect, ItemKind.PetPotion, "", () => run.HealPet(), run.ActivePet != null);
                if (index == 6)
                {
                    Icon(rect, SpriteArt.Whistle, .63f, Color.white);
                    if (Hit(rect)) run.Whistle();
                }
            }
            BuildingData hovered = null;
            Rect hoverRect = default;
            for (int index = 0; index < 11; index++)
            {
                var kind = (BuildingKind)index;
                if (kind == BuildingKind.Generator && run.Generator != null) continue;
                Rect rect = BuildRect(index);
                bool available = kind == BuildingKind.Generator || run.Generator != null;
                var data = catalog.Building(kind);
                Color tint = available ? Color.white : new Color(1, 1, 1, .32f);
                Fill(rect, placing == kind ? new Color(.81f, .95f, .54f, .4f) : available ? Slot : new Color(.11f, .15f, .076f, .32f));
                Icon(rect, SpriteArt.Base(kind, 1), data.size == 1 ? .48f : .75f, tint);
                if (SpriteArt.IsTower(kind)) Icon(rect, SpriteArt.Turret(kind, 1), .80f, tint);
                string key = index == 10 ? "G" : ((index + 1) % 10).ToString();
                Text(new Rect(rect.xMax - 20, rect.yMax - 18, 16, 16), key, 12, tint, TextAnchor.MiddleRight, true);
                if (!shop && !settings && !run.Dead && !run.Defeated && Hit(rect)) SelectBuild(kind);
                if (rect.Contains(Mouse))
                {
                    hovered = data;
                    hoverRect = rect;
                }
            }
            if (hovered != null && !shop && !settings && !run.Dead && !run.Defeated)
            {
                Rect rect = new(Mathf.Clamp(hoverRect.center.x - 140, 12, Width - 292), hoverRect.y - 126, 280, 115);
                Fill(rect, DeepPanel);
                Text(Inset(rect, 12, 8, 26), hovered.title, 20, TextColor, bold: true);
                Text(Inset(rect, 12, 35, 21), $"Tier I Building     {run.Count(hovered.kind)} / {hovered.limit}", 14, MutedText, bold: true);
                Text(Inset(rect, 12, 59, 19), hovered.description, 12, MutedText);
                Text(Inset(rect, 12, 82, 22), Format(hovered.prices[0]), 14, BrightGold);
            }
        }

        private void DrawSmallItem(Rect rect, ItemKind item, string key, Func<bool> use, bool available)
        {
            Icon(rect, SpriteArt.Item(item), .64f, available ? Color.white : new Color(1, 1, 1, .24f));
            Text(new Rect(rect.xMax - 17, rect.yMax - 16, 14, 14), key, 11, MutedText, TextAnchor.MiddleRight);
            if (Hit(rect) && !use()) Notify("Potion unavailable.");
        }

        private void DrawSideButtons()
        {
            Rect heal = SideRect(false, 0), timeout = SideRect(false, 1), store = SideRect(true, 0), gear = SideRect(true, 1);
            Fill(heal, Slot);
            Fill(timeout, Slot);
            Fill(store, Slot);
            Fill(gear, Slot);
            Icon(heal, SpriteArt.HealIcon, .46f, new Color(.75f, .25f, .22f));
            Icon(timeout, SpriteArt.ClockIcon, .41f, BrightGold);
            Icon(store, SpriteArt.ShopIcon, .49f, MutedText);
            Icon(gear, SpriteArt.GearIcon, .49f, MutedText);
            if (Hit(heal))
            {
                ResetSelection();
                if (!run.Resources.Covers(catalog.healPrice)) Notify($"Heal Towers costs {Format(catalog.healPrice)}.");
                else healing = true;
            }
            if (Hit(timeout) && !run.Cast(SpellKind.Timeout, run.Player)) Notify($"Timeout costs {Format(catalog.timeoutPrice)}.");
            if (Hit(store)) ToggleShop();
            if (Hit(gear)) settings = true;
            if (heal.Contains(Mouse)) Tooltip(new Rect(63, heal.y, 225, 37), $"Heal Towers  •  {Format(catalog.healPrice)}");
            if (timeout.Contains(Mouse)) Tooltip(new Rect(63, timeout.y, 218, 37), $"Timeout  •  {Format(catalog.timeoutPrice)}");
            if (store.Contains(Mouse)) Tooltip(new Rect(store.x - 100, store.y + 9, 94, 36), "Shop (B)");
            if (gear.Contains(Mouse)) Tooltip(new Rect(gear.x - 100, gear.y + 9, 94, 36), "Settings");
        }

        private Rect StructureRect()
        {
            Vector2 screen = ToGui(selected.Position);
            return new Rect(Mathf.Clamp(screen.x - 175, 12, Width - 362), Mathf.Clamp(screen.y - 271, 12, Height - 293), 350, 223);
        }

        private void DrawStructure()
        {
            var building = selected;
            Rect rect = StructureRect();
            Fill(rect, DeepPanel);
            Text(Inset(rect, 12, 8, 26), building.Data.title, 23, TextColor, bold: true);
            Text(Inset(rect, 12, 35, 22), $"Tier {Roman[building.Tier - 1]} Building", 15, MutedText, bold: true);
            HudBar(new Rect(rect.xMax - 93, rect.y + 12, 81, 22), building.Health / building.MaximumHealth, Green, "HP");
            int tier = building.Tier;
            bool canAdvance = tier < building.Data.prices.Length;
            string health = canAdvance ? $"Health: {building.MaximumHealth:N0}    ›    {building.Data.health[tier]:N0}"
                : $"Health: {building.MaximumHealth:N0}";
            Text(Inset(rect, 12, 62, 23), health, 15, TextColor);
            string stats = building.Kind switch
            {
                BuildingKind.Smelter => $"Gold/sec: {building.Data.Value(building.Data.income, tier):0.##}",
                BuildingKind.Miner => $"Stored: {building.Stored:N0}     Fuel: {building.Fuel:N0}",
                _ => building.Data.Value(building.Data.damage, tier) > 0 ? $"Damage: {building.Data.Value(building.Data.damage, tier):N0}" : building.Data.description
            };
            Text(Inset(rect, 12, 83, 22), stats, 13, MutedText);
            string upgrade = canAdvance ? $"Upgrade  ({Format(building.Data.prices[tier])})   E" : "Maximum tier";
            if (Button(Inset(rect, 12, 110, 32), upgrade, run.CanUpgrade(building) ? Green : Slot, 13)) Upgrade(building);
            bool stash = building == run.Generator;
            var left = new Rect(rect.x + 12, rect.y + 148, 160, 29);
            var right = new Rect(rect.x + 178, rect.y + 148, 160, 29);
            if (!stash && Button(left, $"Sell ({Format(run.Refund(building))})   T", Red, 11)) Sell(building);
            if (Button(stash ? Inset(rect, 12, 148, 29) : right,
                $"Repair ({Format(run.RepairPrice(building))})", Slot, 12)) run.Repair(building);
            if (building.Kind == BuildingKind.Miner)
            {
                left.y += 34;
                right.y += 34;
                if (Button(left, "Deposit gold", Slot, 13)) run.Deposit(building);
                if (Button(right, "Collect", Green, 13)) run.Collect(building);
            }
            else Text(Inset(rect, 12, 185, 24), stash ? "Upgrade the stash to unlock higher building tiers." : "Shift or Alt + E upgrades matching buildings.",
                13, MutedText);
        }

        private void DrawShop()
        {
            var rect = ShopRect;
            Fill(rect, DeepPanel);
            Text(Inset(rect, 20, 12, 36), "Shop", 23, TextColor, bold: true);
            Text(new Rect(rect.xMax - 183, rect.y + 15, 120, 30), $"{Number(run.Resources.gold)} gold", 16, BrightGold, TextAnchor.MiddleRight);
            if (Button(new Rect(rect.xMax - 50, rect.y + 14, 30, 30), "×", Slot, 21)) { shop = false; return; }
            for (int i = 0; i < TabNames.Length; i++)
            {
                var tab = new Rect(rect.x + 20 + i * 87, rect.y + 62, 86, 40);
                if (Button(tab, TabNames[i], shopTab == i ? Slot : DeepPanel, 15)) shopTab = i;
            }
            for (int i = 0; i < ShopItems[shopTab].Length; i++)
            {
                ItemKind kind = ShopItems[shopTab][i];
                var item = catalog.Item(kind);
                int tier = run.ItemTier(kind);
                bool reusable = kind is ItemKind.HealthPotion or ItemKind.PetPotion or ItemKind.Hat or ItemKind.Carl or ItemKind.Woody;
                bool maxed = !reusable && tier == item.prices.Length;
                var row = new Rect(rect.x + 20, rect.y + 117 + 72 * i, rect.width - 40, 64);
                bool pet = kind is ItemKind.Carl or ItemKind.Woody;
                string price = maxed ? "Max tier" : pet ? (run.ActivePet == kind && run.PetHealth > 0 ? "Active" : "Adopt") :
                    kind == ItemKind.Hat && tier > 0 ? (run.WearingHat ? "Unequip" : "Equip") : Format(item.prices[Math.Min(tier, item.prices.Length - 1)]);
                string detail = maxed ? $"Tier {tier}" : pet && run.ActivePet == kind ? $"Level {run.PetLevel}   •   {run.PetExperience % 50}/50 XP" :
                    $"Tier {Mathf.Min(item.prices.Length, tier + 1)}";
                Fill(row, row.Contains(Mouse) ? new Color(.74f, .79f, .65f, .22f) : new Color(.74f, .79f, .65f, .11f));
                Icon(new Rect(row.x + 8, row.y + 4, 55, 55), SpriteArt.Item(kind, Math.Max(1, tier)), .81f, Color.white);
                Text(new Rect(row.x + 67, row.y + 8, 158, 27), item.title, 17, TextColor, bold: true);
                Text(new Rect(row.x + 67, row.y + 34, 150, 22), detail, 13, MutedText);
                Text(new Rect(row.x + 218, row.y + 7, row.width - 340, 24), item.description, 13, MutedText);
                string value = ShopStats(item, tier);
                Text(new Rect(row.x + 218, row.y + 33, row.width - 340, 22), value, 12, TextColor);
                Text(new Rect(row.xMax - 118, row.y, 103, row.height), price, 15, BrightGold, TextAnchor.MiddleRight, true);
                if (!settings && !maxed && Hit(row) && !run.Buy(kind)) Notify("This item is unavailable.");
            }
            if (shopTab == 3 && run.ActivePet.HasValue)
            {
                if (Button(new Rect(rect.x + 20, rect.y + 279, 221, 36), $"Evolve pet  •  {100 * (1 + run.PetEvolution / 2)} tokens", Slot, 14)
                    && !run.EvolvePet()) Notify("Your pet needs more experience or tokens.");
                if (Button(new Rect(rect.x + 250, rect.y + 279, 120, 36), "Call pet (H)", Slot, 14)) run.Whistle();
            }
            DrawNotice(false);
        }

        private string ShopStats(ItemData item, int tier)
        {
            int next = Math.Min(item.prices.Length - 1, tier);
            if (item.kind == ItemKind.Pickaxe)
                return $"Damage {item.power[next]:0}    •    Harvest {catalog.harvestYield[next]:0.##}    •    Speed {1 / item.interval:0.##}/s";
            if (item.kind <= ItemKind.Bomb) return $"Damage {item.power[next]:0}    •    Speed {1 / item.interval:0.##}/s";
            if (item.kind == ItemKind.Shield) return $"Capacity {item.power[next]:0}    •    Recharge after {item.recovery[next]:0}s";
            return "";
        }

        private void DrawNotice(bool includeHint = true)
        {
            string message = notice != null && Time.unscaledTime < noticeUntil ? notice : null;
            if (message == null && includeHint && profile.hints && !shop && !settings && !run.Dead && !run.Defeated)
            {
                if (run.Resources.wood >= 5 && hintStep == 0) hintStep = 1;
                if (run.Resources.stone >= 5 && hintStep == 1) hintStep = 2;
                if (run.Generator != null && hintStep < 3) hintStep = 3;
                if (run.Count(BuildingKind.Arrow) > 0 && hintStep == 3) hintStep = 4;
                if (run.Count(BuildingKind.Smelter) > 0 && hintStep == 4) hintStep = 5;
                message = hintStep switch
                {
                    0 => "Aim at a tree and hold left click to gather wood.",
                    1 => "Gather stone from a rock with your pickaxe.",
                    2 => "Choose the Oxygen Generator in the toolbar to start your base.",
                    3 => "Build an Arrow Tower (4) to defend your stash.",
                    4 => "Build a Gold Mine (9) to earn gold while you defend.",
                    _ => null
                };
                if (!run.Night && run.Generator != null && run.PhaseLength - run.PhaseTime < 6)
                    message = "Night is fast approaching. Get to safety!";
            }
            if (message == null) return;
            var rect = new Rect(Width * .5f - 207, 20, 414, 43);
            Fill(rect, Panel);
            Text(rect, message, 14, TextColor, TextAnchor.MiddleCenter);
        }

        private void DrawDeath()
        {
            Fill(new Rect(0, 0, Width, Height), new Color(.015f, .022f, .011f, .58f));
            var box = new Rect(Width * .5f - 305, Height * .5f - 113, 610, 226);
            Fill(box, DeepPanel);
            Text(Inset(box, 20, 23, 46), "Oh dear...", 34, TextColor, TextAnchor.MiddleCenter, true);
            string message = run.Defeated
                ? $"Your Oxygen Generator was destroyed at wave {run.Wave}. Final score: {run.Score:N0}."
                : "You were killed. Your fortress survives!";
            Text(Inset(box, 20, 82, 30), message, 17, MutedText, TextAnchor.MiddleCenter);
            if (Button(new Rect(box.center.x - 133, box.y + 136, 126, 45), "Respawn", Green, 17))
            {
                if (run.Defeated) RestartAfterLoss();
                else run.Respawn();
            }
            if (Button(new Rect(box.center.x + 7, box.y + 136, 126, 45), "New run", Slot, 17)) Begin();
        }

        private void DrawSettings()
        {
            Fill(new Rect(0, 0, Width, Height), new Color(.02f, .03f, .015f, .54f));
            var box = SettingsRect;
            Fill(box, DeepPanel);
            Text(Inset(box, 18, 10, 40), intro ? "Settings" : "Paused", 26, TextColor, bold: true);
            if (Button(new Rect(box.xMax - 47, box.y + 14, 30, 30), "×", Slot, 20)) settings = false;
            if (Button(Inset(box, 18, 62, 36), profile.muted ? "Sound   Off" : "Sound   On", Slot, 15))
            {
                profile.muted = !profile.muted;
                profile.Save();
            }
            if (Button(Inset(box, 18, 107, 36), profile.hints ? "Tutorial hints   On" : "Tutorial hints   Off", Slot, 15))
            {
                profile.hints = !profile.hints;
                profile.Save();
            }
            if (Button(Inset(box, 18, 152, 37), Screen.fullScreen ? "Display   Full screen" : "Display   Windowed", Slot, 15))
            {
                profile.fullscreen = !Screen.fullScreen;
                Screen.fullScreenMode = profile.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                profile.Save();
            }
            if (Button(Inset(box, 18, 200, 38), intro ? "Back" : "Resume", Green, 16)) settings = false;
            if (!intro && Button(Inset(box, 18, 246, 30), "Main menu", Slot, 14))
            {
                intro = true;
                settings = false;
                Attach(new RunState(catalog, 5238));
            }
        }

        private void Tooltip(Rect rect, string label)
        {
            Fill(rect, DeepPanel);
            Text(rect, label, 14, TextColor, TextAnchor.MiddleCenter);
        }

        private void HudBar(Rect rect, float fraction, Color color, string label)
        {
            Fill(rect, DeepPanel);
            Fill(new Rect(rect.x + 4, rect.y + 4, Mathf.Clamp01(fraction) * (rect.width - 8), rect.height - 8), color);
            if (label != null) Text(new Rect(rect.x + 8, rect.y, rect.width - 8, rect.height), label, 12, TextColor, bold: true);
        }

        private static Rect Inset(Rect rect, float x, float y, float height) =>
            new(rect.x + x, rect.y + y, rect.width - 2 * x, height);

        private static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 3);
        }

        private static void Icon(Rect rect, Sprite sprite, float scale, Color tint)
        {
            if (Event.current.type != EventType.Repaint) return;
            float size = Mathf.Min(rect.width, rect.height) * scale;
            GUI.DrawTexture(new Rect(rect.center - Vector2.one * size / 2, Vector2.one * size), sprite.texture,
                ScaleMode.ScaleToFit, true, 0, tint, 0, 0);
        }

        private void Text(Rect rect, string value, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(value)) return;
            var key = (size, alignment, bold);
            if (!labelStyles.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    font = font, fontSize = size, alignment = alignment, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    clipping = TextClipping.Clip, padding = new RectOffset(), richText = false
                };
                labelStyles.Add(key, style);
            }
            style.normal.textColor = color;
            GUI.Label(rect, value, style);
        }

        private bool Button(Rect rect, string label, Color background, int size)
        {
            Fill(rect, rect.Contains(Mouse) ? Color.Lerp(background, new Color(.68f, .77f, .47f, background.a), .22f) : background);
            Text(rect, label, size, TextColor, TextAnchor.MiddleCenter, true);
            return Hit(rect);
        }

        private static bool Hit(Rect rect) => GUI.Button(rect, GUIContent.none, GUIStyle.none);

        private static string Number(float amount) => amount >= 1_000_000 ? $"{amount / 1_000_000:0.#}M" :
            amount >= 1_000 ? $"{amount / 1_000:0.#}K" : Mathf.FloorToInt(amount + .001f).ToString();

        private static string Format(Price cost)
        {
            var parts = new List<string>(4);
            if (cost.wood > 0) parts.Add($"{cost.wood:N0} wood");
            if (cost.stone > 0) parts.Add($"{cost.stone:N0} stone");
            if (cost.gold > 0) parts.Add($"{cost.gold:N0} gold");
            if (cost.tokens > 0) parts.Add(cost.tokens == 1 ? "1 token" : $"{cost.tokens} tokens");
            return parts.Count == 0 ? "Free" : string.Join(", ", parts);
        }
    }
}
