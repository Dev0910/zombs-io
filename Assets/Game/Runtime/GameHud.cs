using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zombs
{
    public sealed partial class GameSession
    {
        private static readonly Color Panel = new(.051f, .080f, .107f, .97f);
        private static readonly Color DeepPanel = new(.034f, .055f, .075f);
        private static readonly Color Slot = new(.093f, .139f, .173f);
        private static readonly Color Line = new(.19f, .28f, .32f);
        private static readonly Color TextColor = new(.94f, .97f, .96f);
        private static readonly Color MutedText = new(.72f, .81f, .84f);
        private static readonly Color Quiet = new(.51f, .63f, .68f);
        private static readonly Color Air = new(.44f, .92f, .84f);
        private static readonly Color BrightGold = new(1, .76f, .43f);
        private static readonly Color Red = new(.38f, .16f, .21f);
        private static readonly Color Coral = new(1, .54f, .54f);
        private static readonly Color Purple = new(.80f, .68f, 1);
        private static readonly Color AirBar = new(.17f, .64f, .59f);
        private static readonly Color Green = new(.14f, .37f, .39f);
        private static readonly string[] TabNames = { "Weapons", "Armor", "Hats", "Pets", "Utility" };
        private static readonly ItemKind[][] ShopItems =
        {
            new[] { ItemKind.Pickaxe, ItemKind.Spear, ItemKind.Bow, ItemKind.Bomb },
            new[] { ItemKind.Shield }, new[] { ItemKind.Hat },
            new[] { ItemKind.Carl, ItemKind.Woody }, new[] { ItemKind.HealthPotion, ItemKind.PetPotion }
        };
        private static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII" };
        private const int BuildSlots = (int)BuildingKind.Assembler + 1;
        private readonly Dictionary<(int, TextAnchor, bool, bool), GUIStyle> labelStyles = new();
        private GUIStyle inputStyle;
        private Font font, headingFont;
        private Texture2D titleArt, titleFade;

        private float Scale => Mathf.Min(1.55f, Screen.height / 780f, Screen.width / 920f);
        private float Width => Screen.width / Scale;
        private float Height => Screen.height / Scale;
        private Vector2 Mouse => new(Input.mousePosition.x / Scale, (Screen.height - Input.mousePosition.y) / Scale);
        private Rect AirRect => new(20, 20, 236, 136);
        private Rect ClockRect => new(AirRect.x, AirRect.yMax + 7, AirRect.width, 53);
        private Rect GuidanceRect => new(AirRect.x, ClockRect.yMax + 7, AirRect.width, 46);
        private Rect CommandRect => new(Width - 244, 20, 224, 86);
        private Rect MapRect => new(20, Height - 198, 172, 178);
        private Rect ResourcesRect => new(Width - 225, Height - 191, 205, 171);
        private float BuildSlotSize => Mathf.Min(48, (Width - 454) / BuildSlots - 4);
        private Rect BuildDock => new((Width - (BuildSlotSize + 4) * BuildSlots) * .5f - 13,
            Height - BuildSlotSize - 51, (BuildSlotSize + 4) * BuildSlots + 16, BuildSlotSize + 31);
        private Rect LoadoutDock => new(Width * .5f - 161, BuildDock.y - 60, 312, 52);
        private Rect ShopRect
        {
            get
            {
                int rows = ShopItems[shopTab].Length;
                float height = 178 + rows * 74 + (shopTab == 3 && run.ActivePet.HasValue ? 45 : 0);
                return new Rect((Width - 720) * .5f, (Height - 480) * .5f, 720, height);
            }
        }
        private Rect SettingsRect => new((Width - 454) * .5f, (Height - 371) * .5f, 454, 371);
        private Rect BuildRect(int index) => new(BuildDock.x + 10 + index * (BuildSlotSize + 4), BuildDock.y + 22, BuildSlotSize, BuildSlotSize);
        private Rect WeaponRect(int index) => new(LoadoutDock.x + 8 + index * 43, LoadoutDock.y + 7, 38, 38);

        private bool BlocksWorld(Vector2 screen)
        {
            Vector2 point = screen / Scale;
            if (settings || shop || intro || confirmNewRun || run.Dead || run.Defeated) return true;
            return AirRect.Contains(point) || ClockRect.Contains(point) || CommandRect.Contains(point) ||
                MapRect.Contains(point) || ResourcesRect.Contains(point) || BuildDock.Contains(point) ||
                LoadoutDock.Contains(point) || (selected != null && StructureRect().Contains(point));
        }

        private void OnGUI()
        {
            if (run == null) return;
            if (font == null)
            {
                font = Resources.Load<Font>("Presentation/Barlow-Medium") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                headingFont = Resources.Load<Font>("Presentation/BarlowCondensed-Bold") ?? font;
                titleArt = Resources.Load<Texture2D>("Presentation/Outpost");
                titleFade = new Texture2D(128, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[128];
                for (int index = 0; index < pixels.Length; index++)
                    pixels[index] = new Color32(4, 10, 15, (byte)(197 * Mathf.Pow(1 - index / 127f, 1.6f)));
                titleFade.SetPixels32(pixels);
                titleFade.Apply(false, true);
            }

            Matrix4x4 matrix = GUI.matrix;
            bool enabled = GUI.enabled;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1));
            if (intro)
            {
                GUI.enabled = enabled && !settings && !confirmNewRun;
                DrawIntro();
            }
            else if (run.Dead || run.Defeated)
            {
                GUI.enabled = enabled && !settings && !confirmNewRun;
                DrawDeath();
            }
            else
            {
                GUI.enabled = enabled && !settings && !shop;
                if (!settings && !shop) world.DrawLabels(WorldLabel);
                DrawStatus();
                DrawSideButtons();
                DrawMap();
                DrawToolbar();
                if (!settings && !shop)
                {
                    if (selected != null) DrawStructure();
                    DrawNotice();
                    DrawWorldTooltip();
                }
                if (shop)
                {
                    GUI.enabled = enabled && !settings;
                    DrawShop();
                }
            }
            GUI.enabled = enabled;
            if (settings) DrawSettings();
            if (confirmNewRun) DrawNewRunConfirmation();
            GUI.matrix = matrix;
        }

        private void DrawIntro()
        {
            Rect full = new(0, 0, Width, Height);
            Fill(full, DeepPanel);
            if (titleArt != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(full, titleArt, ScaleMode.ScaleAndCrop);
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(0, 0, Width * .69f, Height), titleFade, ScaleMode.StretchToFill, true);

            float left = Mathf.Clamp(Width * .061f, 42, 116);
            float top = Mathf.Max(76, (Height - 466) * .46f);
            const float width = 402;
            Fill(new Rect(left, top, 52, 3), Air);
            Text(new Rect(left, top + 12, width + 36, 96), "ZOMBS.IO", 82, TextColor, bold: true);
            Text(new Rect(left + 3, top + 105, width, 31), "OFFLINE SURVIVAL", 25, Air, bold: true);

            bool hasSave = savedRun != null;
            Rect card = new(left, top + 159, width, hasSave ? 198 : 166);
            Frame(card, Panel, Line);
            Text(Inset(card, 17, 12, 18), "CALL SIGN", 13, MutedText, bold: true);
            Text(new Rect(card.x + 247, card.y + 12, 138, 18), hasSave ? "MAP SEED (NEW RUN)" : "MAP SEED (OPTIONAL)",
                13, MutedText, bold: true);
            if (inputStyle == null)
            {
                inputStyle = new GUIStyle(GUI.skin.textField)
                {
                    font = font, fontSize = 18, alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(11, 9, 0, 0), richText = false
                };
                foreach (GUIStyleState state in new[] { inputStyle.normal, inputStyle.hover, inputStyle.active, inputStyle.focused })
                {
                    state.background = null;
                    state.textColor = TextColor;
                }
            }
            profile.nickname = Field(new Rect(card.x + 17, card.y + 37, 216, 39), "CallSign", profile.nickname ?? "", 20);
            seedInput = Field(new Rect(card.x + 247, card.y + 37, 138, 39), "MapSeed", seedInput, 11);
            bool enter = GUI.enabled && Event.current.type == EventType.KeyDown &&
                Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter;
            if (enter) Event.current.Use();
            if (hasSave)
            {
                if (Button(new Rect(card.x + 17, card.y + 89, 216, 48), "CONTINUE", Green, 23) || enter)
                {
                    ContinueRun();
                    return;
                }
                if (Button(new Rect(card.x + 247, card.y + 89, 138, 48), "NEW RUN", Slot, 20))
                    RequestNewRun();
                string stage = savedRun.Defeated ? $"Generator destroyed  /  Wave {savedRun.Wave}" :
                    savedRun.Dead ? $"Suit signal lost  /  Wave {savedRun.Wave}" :
                    savedRun.Generator == null ? "Before deployment" :
                    savedRun.Night ? $"Night  /  Wave {savedRun.Wave}" : $"Daylight  /  Wave {savedRun.Wave + 1} ahead";
                Text(Inset(card, 17, 141, 23), $"{stage}  /  Seed {savedRun.Seed}", 15, MutedText);
            }
            else if (Button(Inset(card, 17, 89, 48), "DEPLOY", Green, 23) || enter)
            {
                RequestNewRun();
                if (!intro) return;
            }
            if (!confirmNewRun) DrawCheckpointNotice(Inset(card, 17, hasSave ? 166 : 140, 23));

            Text(new Rect(left, card.yMax + 17, width, 24), "Place a generator. Gather supplies. Survive the night.", 17, TextColor);
            Text(new Rect(left, card.yMax + 48, width, 24), "WASD   Move     •     Mouse   Aim / interact     •     G   Generator", 14, MutedText);
            Text(new Rect(left, card.yMax + 75, width, 24), $"BEST WAVE  {profile.bestWave}     /     BEST SCORE  {profile.bestScore:N0}", 15, BrightGold, bold: true);
            if (Button(new Rect(Width - 133, 25, 108, 37), "SETTINGS", Slot, 16)) settings = true;
        }

        private void DrawNewRunConfirmation()
        {
            Dim();
            Rect rect = new((Width - 454) * .5f, (Height - 211) * .5f, 454, 211);
            Frame(rect, Panel, Line);
            Fill(new Rect(rect.x, rect.y, rect.width, 3), Coral);
            Text(Inset(rect, 22, 15, 42), "START A NEW RUN?", 33, TextColor, bold: true);
            Text(Inset(rect, 22, 62, 47), "This will replace your saved survival run.", 18, MutedText,
                TextAnchor.UpperLeft, wrap: true);
            if (Button(new Rect(rect.x + 22, rect.y + 117, 197, 44), "CANCEL", Green, 20))
            {
                confirmNewRun = false;
                notice = null;
            }
            if (Button(new Rect(rect.x + 235, rect.y + 117, 197, 44), "START NEW RUN", Red, 20)) Begin();
            DrawCheckpointNotice(Inset(rect, 22, 170, 29));
        }

        private void DrawCheckpointNotice(Rect rect)
        {
            if (notice != null && Time.unscaledTime < noticeUntil)
                Text(rect, notice, 14, Coral, wrap: true);
        }

        private string Field(Rect rect, string name, string value, int maxLength)
        {
            bool focused = GUI.GetNameOfFocusedControl() == name;
            Frame(rect, Slot, focused ? Air : Line);
            GUI.SetNextControlName(name);
            return GUI.TextField(rect, value, maxLength, inputStyle);
        }

        private void DrawStatus()
        {
            Rect rect = AirRect;
            Card(rect, "SUIT TELEMETRY", Air);
            Meter(Inset(rect, 12, 33, 27), "Health", $"{Mathf.CeilToInt(run.Health)} / {catalog.playerHealth:0}",
                run.Health / catalog.playerHealth, run.Health < catalog.playerHealth * .3f ? Coral : AirBar);
            if (run.MaxShield > 0)
                Fill(new Rect(rect.x + 12, rect.y + 62, (rect.width - 24) * Mathf.Clamp01(run.Shield / run.MaxShield), 2), Purple);
            string airState = run.CanBreathe ? "Breathable" : run.Breath > 0 ? $"{run.Breath:0.0}s reserve" : "Poison damage";
            Meter(Inset(rect, 12, 66, 26), "Oxygen", airState, run.Breath / Mathf.Max(.01f, catalog.breathSeconds),
                run.CanBreathe ? AirBar : run.Breath > 0 ? BrightGold : Coral);
            Structure generator = run.Generator;
            if (generator == null)
                Text(Inset(rect, 12, 96, 26), "[G]  Place Oxygen Generator", 15, MutedText);
            else
            {
                string fuel = run.GeneratorOnline ? TimeLeft(generator.Fuel) : "OFFLINE";
                if (run.GeneratorOnline && generator.Fuel <= 30) fuel += generator.Fuel <= 10 ? "  CRITICAL" : "  LOW";
                Meter(Inset(rect, 12, 99, 25), "Fuel", fuel, generator.Fuel / run.FuelCapacity(generator),
                    generator.Fuel <= 10 ? Coral : generator.Fuel <= 30 ? BrightGold : AirBar);
            }
            DrawPhase();
            DrawInventory();
        }

        private void DrawPhase()
        {
            Rect rect = ClockRect;
            Color accent = run.Night ? Purple : BrightGold;
            Frame(rect, Panel, Line);
            string phase = run.Generator == null ? "PREPARATION" : run.Night ? "NIGHT" : "DAYLIGHT";
            Text(Inset(rect, 11, 3, 22), phase, 19, accent, bold: true);
            string left = run.Generator == null ? "Place generator to begin" : TimeLeft(run.PhaseLength - run.PhaseTime);
            string wave = run.Wave > 0 ? $"Wave {run.Wave}" : "Wave 1 ahead";
            Text(Inset(rect, 11, 26, 18), run.Generator == null ? left : $"{left}   /   {wave}", 14, TextColor);
            float progress = run.Generator == null ? 0 : Mathf.Clamp01(run.PhaseTime / run.PhaseLength);
            Fill(new Rect(rect.x, rect.yMax - 3, rect.width, 3), Slot);
            Fill(new Rect(rect.x, rect.yMax - 3, rect.width * progress, 3), accent);
        }

        private void DrawInventory()
        {
            Rect rect = ResourcesRect;
            Card(rect, "MATERIALS", BrightGold);
            Price stock = run.Resources;
            Resource(rect, 0, 0, "Wood", stock.wood, ResourceKind.Wood);
            Resource(rect, 1, 0, "Stone", stock.stone, ResourceKind.Stone);
            Resource(rect, 0, 1, "Iron ore", stock.iron, ResourceKind.Iron);
            Resource(rect, 1, 1, "Coal", stock.coal, ResourceKind.Coal);
            Resource(rect, 0, 2, "Plates", stock.plate);
            Resource(rect, 1, 2, "Parts", stock.parts);
            Fill(Inset(rect, 10, 132, 1), Line);
            Text(Inset(rect, 12, 136, 24), "Boss tokens", 14, MutedText);
            Text(Inset(rect, 12, 136, 24), $"{stock.tokens:N0}", 17, Purple, TextAnchor.MiddleRight, bold: true);
        }

        private void Resource(Rect box, int column, int row, string label, float amount, ResourceKind? kind = null)
        {
            Rect rect = new(box.x + 10 + column * 94, box.y + 32 + row * 32, 91, 28);
            if (kind.HasValue) Icon(new Rect(rect.x, rect.y + 4, 18, 19), SpriteArt.MapDeposit(kind.Value), .65f, Color.white);
            Text(new Rect(rect.x + (kind.HasValue ? 17 : 3), rect.y, 53, 27), label, 13, MutedText);
            Text(new Rect(rect.x + 53, rect.y, 35, 27), Number(amount), 16, TextColor, TextAnchor.MiddleRight, bold: true);
        }

        private void DrawMap()
        {
            Rect rect = MapRect;
            Card(rect, "LOCAL MAP", Air);
            var view = new Rect(rect.x + 9, rect.y + 29, rect.width - 18, 140);
            Frame(view, DeepPanel, Line);
            if (Event.current.type != EventType.Repaint) return;
            GUI.BeginGroup(view);
            Rect area = new(Vector2.zero, view.size);
            Vector2 middle = area.center;
            float scale = area.width / 66;
            var grid = new Color(.19f, .33f, .38f, .52f);
            Fill(new Rect(middle.x, 0, 1, area.height), grid);
            Fill(new Rect(0, middle.y, area.width, 1), grid);
            if (run.OxygenRadius > 0)
            {
                Vector2 center = MapPoint(area, run.OxygenCenter, scale);
                float size = run.OxygenRadius * 2 * scale;
                Icon(new Rect(center - Vector2.one * size * .5f, Vector2.one * size), SpriteArt.MapBoundary(run.Generator == null), 1, Air);
            }
            foreach (var node in run.Nodes)
            {
                if (node.Depleted) continue;
                Vector2 point = MapPoint(area, node.Position, scale);
                if (area.Contains(point))
                    Icon(new Rect(point - Vector2.one * 3.5f, Vector2.one * 7), SpriteArt.MapDeposit(node.Kind), 1, Color.white);
            }
            foreach (var enemy in run.Enemies)
            {
                if (enemy.Health <= 0 || enemy.Neutral) continue;
                Vector2 point = MapPoint(area, enemy.Position, scale);
                if (area.Contains(point)) Fill(new Rect(point.x - 1.5f, point.y - 1.5f, 3, 3), Coral);
            }
            if (run.Generator != null)
            {
                Vector2 home = MapPoint(area, run.Generator.Position, scale);
                home.x = Mathf.Clamp(home.x, 5, area.width - 5);
                home.y = Mathf.Clamp(home.y, 5, area.height - 5);
                Frame(new Rect(home - Vector2.one * 4, Vector2.one * 8), DeepPanel, run.GeneratorOnline ? Air : Coral);
            }
            Fill(new Rect(middle - Vector2.one * 2, Vector2.one * 4), TextColor);
            GUI.EndGroup();
            if (!settings && !shop && view.Contains(Mouse))
                Tooltip(new Rect(rect.x, rect.y - 35, 272, 30), "Wood △   Stone ●   Iron ◆   Coal ■");
        }

        private Vector2 MapPoint(Rect area, Vector2 position, float scale)
        {
            Vector2 offset = position - run.Player;
            return area.center + new Vector2(offset.x, -offset.y) * scale;
        }

        private void DrawSideButtons()
        {
            Rect rect = CommandRect;
            Frame(rect, Panel, Line);
            Text(new Rect(rect.x + 11, rect.y + 5, 105, 24), profile.nickname, 16, TextColor);
            Text(new Rect(rect.x + 116, rect.y + 5, 96, 24), $"{run.Score:N0} SCORE", 15, MutedText, TextAnchor.MiddleRight, true);
            var store = new Rect(rect.x + 8, rect.y + 34, 51, 43);
            var heal = new Rect(rect.x + 61, rect.y + 34, 49, 43);
            var timeout = new Rect(rect.x + 113, rect.y + 34, 49, 43);
            var gear = new Rect(rect.x + 165, rect.y + 34, 51, 43);
            if (IconButton(store, SpriteArt.ShopIcon, Air, "B")) ToggleShop();
            if (IconButton(heal, SpriteArt.HealIcon, healing ? Air : Coral))
            {
                ResetSelection();
                if (run.Resources.Covers(catalog.healPrice)) healing = true;
                else Notify($"Heal Towers needs {Format(catalog.healPrice)}.");
            }
            if (IconButton(timeout, SpriteArt.ClockIcon, BrightGold) && !run.Cast(SpellKind.Timeout, run.Player))
                Notify($"Timeout needs {Format(catalog.timeoutPrice)} and a ready cooldown.");
            if (IconButton(gear, SpriteArt.GearIcon, MutedText)) settings = true;
            if (settings || shop || run.Dead || run.Defeated) return;
            string tip = store.Contains(Mouse) ? "Shop [B]" : heal.Contains(Mouse) ? $"Heal Towers  /  {Format(catalog.healPrice)}" :
                timeout.Contains(Mouse) ? $"Timeout  /  {Format(catalog.timeoutPrice)}" : gear.Contains(Mouse) ? "Settings [Esc]" : null;
            if (tip != null) Tooltip(new Rect(rect.xMax - 274, rect.yMax + 3, 274, 29), tip);
        }

        private void DrawToolbar()
        {
            Rect dock = LoadoutDock;
            Frame(dock, Panel, Line);
            string equipmentTip = null;
            Rect equipmentAnchor = default;
            for (int index = 0; index < 7; index++)
            {
                Rect rect = WeaponRect(index);
                if (index < 4)
                {
                    var kind = (ItemKind)index;
                    int tier = run.ItemTier(kind);
                    bool active = tier > 0 && run.Weapon == kind;
                    Tile(rect, active, tier > 0);
                    Icon(rect, SpriteArt.Item(kind, Mathf.Max(1, tier)), .78f, Tint(tier > 0));
                    if (tier > 0 && Hit(rect))
                    {
                        ResetSelection();
                        run.Equip(kind);
                    }
                    if (rect.Contains(Mouse))
                    {
                        equipmentTip = tier > 0 ? catalog.Item(kind).title : $"{catalog.Item(kind).title}  /  Available in shop";
                        equipmentAnchor = rect;
                    }
                }
                else
                {
                    bool usable = index switch { 4 => run.CanHealPlayer, 5 => run.CanHealPet, _ => run.PetHealth > 0 };
                    Sprite icon = index switch { 4 => SpriteArt.Item(ItemKind.HealthPotion), 5 => SpriteArt.Item(ItemKind.PetPotion), _ => SpriteArt.Whistle };
                    string key = index == 4 ? "F" : index == 6 ? "H" : "";
                    if (IconButton(rect, icon, Tint(usable), key, usable))
                    {
                        if (index == 6) run.Whistle();
                        else if (!(index == 4 ? run.HealPlayer() : run.HealPet())) Notify("Potion unavailable.");
                    }
                    if (rect.Contains(Mouse))
                    {
                        equipmentTip = index switch
                        {
                            4 => $"Health potion [F]  /  {PotionStatus(ItemKind.HealthPotion)}",
                            5 => $"Pet potion  /  {PotionStatus(ItemKind.PetPotion)}",
                            _ => "Call companion [H]"
                        };
                        equipmentAnchor = rect;
                    }
                }
            }

            dock = BuildDock;
            Frame(dock, Panel, Line);
            Text(Inset(dock, 10, 2, 18), placing.HasValue ? catalog.Building(placing.Value).title.ToUpperInvariant() : "BUILD", 15, Air, bold: true);
            if (placing.HasValue)
                Text(Inset(dock, 10, 2, 18), placing == BuildingKind.Melee ? "[R] Rotate    [Esc] Cancel" : "[Esc] Cancel",
                    13, MutedText, TextAnchor.MiddleRight);
            BuildingData hovered = null;
            Rect anchor = default;
            for (int i = 0; i < BuildSlots; i++)
            {
                BuildingKind kind = i switch { 0 => BuildingKind.Generator, 10 => BuildingKind.Assembler, 11 => BuildingKind.Miner, _ => (BuildingKind)(i - 1) };
                Rect rect = BuildRect(i);
                BuildingData data = catalog.Building(kind);
                bool online = kind == BuildingKind.Generator || run.Generator != null && run.Generator.Tier >= data.requiredGeneratorTier;
                bool active = placing == kind || kind == BuildingKind.Generator && selected != null && selected == run.Generator;
                Tile(rect, active, online);
                Rect glyph = new(rect.x + 2, rect.y, rect.width - 4, rect.height - 5);
                Icon(glyph, SpriteArt.Base(kind, 1), data.size == 1 ? .66f : .82f, Tint(online));
                if (SpriteArt.IsTower(kind)) Icon(glyph, SpriteArt.Turret(kind, 1), .84f, Tint(online));
                string key = kind switch
                {
                    BuildingKind.Generator => "G", BuildingKind.Assembler => "M", BuildingKind.Miner => "0",
                    _ => ((int)kind + 1).ToString()
                };
                Key(rect, key, online ? TextColor : Quiet);
                if (online && !run.Resources.Covers(data.prices[0]))
                    Fill(new Rect(rect.x + 3, rect.yMax - 4, 8, 2), BrightGold);
                if (Hit(rect))
                {
                    if (kind == BuildingKind.Generator && run.Generator != null)
                    {
                        ResetSelection();
                        if (run.InReach(run.Generator)) selected = run.Generator;
                        else Notify("Move closer to the Oxygen Generator.");
                    }
                    else SelectBuild(kind);
                }
                if (rect.Contains(Mouse))
                {
                    hovered = data;
                    anchor = rect;
                }
            }

            if (settings || shop || run.Dead || run.Defeated) return;
            if (hovered != null)
            {
                Rect rect = new(Mathf.Clamp(anchor.center.x - 142, 12, Width - 296), LoadoutDock.y - 107, 284, 100);
                Card(rect, hovered.title.ToUpperInvariant(), Air);
                Text(Inset(rect, 10, 28, 34), hovered.description, 14, TextColor, wrap: true);
                string cost = hovered.kind == BuildingKind.Generator && run.Generator != null ? "Open generator controls" :
                    hovered.kind != BuildingKind.Generator && run.Generator == null ? "Place the Oxygen Generator first" :
                    run.Generator != null && run.Generator.Tier < hovered.requiredGeneratorTier ? $"Requires generator tier {Roman[hovered.requiredGeneratorTier - 1]}" :
                    $"{Format(hovered.prices[0])}   /   {run.Count(hovered.kind)} of {hovered.limit} built";
                Text(Inset(rect, 10, 70, 20), cost, 14, BrightGold);
            }
            else if (equipmentTip != null)
                Tooltip(new Rect(Mathf.Clamp(equipmentAnchor.center.x - 140, 12, Width - 292), LoadoutDock.y - 39, 280, 31), equipmentTip);
        }

        private void DrawWorldTooltip()
        {
            if (BlocksWorld(Mouse * Scale) || Event.current.type != EventType.Repaint) return;
            if (placing.HasValue)
            {
                Placement result = run.CanPlace(placing.Value, cursor);
                if (result != Placement.Allowed && CursorTooltip(296, cursor) is Rect preview)
                    Tooltip(preview, PlacementMessage(result));
                return;
            }
            var node = run.ResourceAt(cursor);
            if (node == null) return;
            string kind = RunState.ResourceName(node.Kind);
            string amount = float.IsPositiveInfinity(node.Remaining) ? "Renewable" : $"{Number(node.Remaining)} remaining";
            if (CursorTooltip(198, cursor) is Rect hint)
                Tooltip(hint, $"{char.ToUpperInvariant(kind[0])}{kind.Substring(1)}   /   {amount}");
        }

        private Rect? CursorTooltip(float width, Vector2 point)
        {
            Vector3 pixel = sceneCamera.WorldToScreenPoint(point);
            Vector2 anchor = new(pixel.x / Scale, (Screen.height - pixel.y) / Scale);
            const float height = 36;
            float right = Mathf.Clamp(anchor.x + 17, 12, Width - width - 12);
            float left = Mathf.Clamp(anchor.x - width - 17, 12, Width - width - 12);
            float above = Mathf.Clamp(anchor.y - height - 10, 12, Height - height - 12);
            float below = Mathf.Clamp(anchor.y + 17, 12, Height - height - 12);
            var candidates = new Rect[]
            {
                new(right, below, width, height), new(left, below, width, height),
                new(right, above, width, height), new(left, above, width, height),
                new(right, LoadoutDock.yMin - height - 9, width, height)
            };
            foreach (Rect candidate in candidates)
            {
                if (candidate.Overlaps(AirRect) || candidate.Overlaps(ClockRect) || candidate.Overlaps(CommandRect) ||
                    candidate.Overlaps(GuidanceRect) || candidate.Overlaps(MapRect) || candidate.Overlaps(ResourcesRect) ||
                    candidate.Overlaps(BuildDock) || candidate.Overlaps(LoadoutDock) ||
                    selected != null && candidate.Overlaps(StructureRect())) continue;
                return candidate;
            }
            return null;
        }

        private Rect StructureRect() => new(Width - 353, 137, 333, 324);

        private void DrawStructure()
        {
            Structure building = selected;
            Rect rect = StructureRect();
            Frame(rect, Panel, Line);
            int tier = building.Tier;
            bool generator = building == run.Generator;
            bool miner = building.Kind == BuildingKind.Miner;
            var recipe = catalog.RecipeFor(building.Kind);
            bool upgradeable = tier < building.Data.prices.Length;
            var art = new Rect(rect.x + 12, rect.y + 13, 48, 48);
            Tile(art, false, true);
            Icon(art, SpriteArt.Base(building.Kind, tier), .86f, Color.white);
            if (SpriteArt.IsTower(building.Kind)) Icon(art, SpriteArt.Turret(building.Kind, tier), .86f, Color.white);
            Text(new Rect(rect.x + 71, rect.y + 9, 215, 29), building.Data.title.ToUpperInvariant(), 24, TextColor, bold: true);
            Text(new Rect(rect.x + 71, rect.y + 39, 210, 19), $"TIER {Roman[tier - 1]}   /   {MachineStatus(building)}", 14,
                generator && !run.GeneratorOnline ? Coral : Air, bold: true);
            if (Button(new Rect(rect.xMax - 35, rect.y + 9, 25, 26), "×", Slot, 17))
            {
                selected = null;
                return;
            }

            Meter(Inset(rect, 13, 69, 26), "Structure", $"{Mathf.CeilToInt(building.Health):N0} / {building.MaximumHealth:N0}",
                building.Health / building.MaximumHealth, AirBar);
            string stats = generator ? $"Air radius {building.Data.Range(tier):0}   /   Fuel {TimeLeft(building.Fuel)} of {TimeLeft(run.FuelCapacity(building))}" :
                miner ? $"{RunState.ResourceName(building.DepositKind.Value)}   /   {building.Stored:0.#} stored   /   {MachineFuelTime(building)} fuel" :
                recipe != null ? $"{Format(recipe.input)} → 1 {recipe.outputName}   /   {building.Queued} queued   /   {Number(building.Stored)} ready" :
                SpriteArt.IsTower(building.Kind) ? $"Damage {building.Data.Value(building.Data.damage, tier):0}   /   Range {building.Data.Range(tier):0.0}" :
                building.Data.description;
            Text(Inset(rect, 13, 100, 37), stats, 15, TextColor, wrap: true);

            Rect left = new(rect.x + 13, rect.y + 140, 150, 33), right = new(rect.x + 170, rect.y + 140, 150, 33);
            if (generator || miner)
            {
                DrawFuelAction(left, building, ResourceKind.Wood, run.Resources.wood);
                DrawFuelAction(right, building, ResourceKind.Coal, run.Resources.coal);
            }
            else if (recipe != null)
            {
                int count = run.ProductionBatchSize(building);
                bool full = building.Stored + building.Queued >= building.Data.Value(building.Data.capacity, tier);
                string batch = count > 0 ? $"Queue {count} {recipe.outputName}{(count == 1 ? "" : "s")}" :
                    full ? "Queue full" : "Need recipe inputs";
                if (Button(left, batch, Slot, 16, count > 0)) run.QueueProduction(building);
                if (Button(right, $"Collect {recipe.outputName}s", Green, 16, building.Stored > 0))
                    run.Collect(building);
            }
            else if (SpriteArt.IsTower(building.Kind))
                Text(Inset(rect, 13, 143, 30), "Selected range shown on terrain.", 14, MutedText);
            if (miner && Button(Inset(rect, 13, 178, 28), "Collect deposit", Green, 15, building.Stored > 0))
                run.Collect(building);
            else if (generator && upgradeable)
                Text(Inset(rect, 13, 178, 25), $"Next  /  Radius {building.Data.Range(tier + 1):0}   Tank {TimeLeft(building.Data.fuelCapacity[tier])}   Tier {Roman[tier]}",
                    14, MutedText);
            else if (recipe == null && !miner && !generator)
                Text(Inset(rect, 13, 178, 25), "[Shift+E] Upgrade matching structures", 14, MutedText);

            Fill(Inset(rect, 13, 210, 1), Line);
            string requirement = !upgradeable ? "Maximum tier" : !generator && tier >= run.Generator?.Tier
                ? $"Generator tier {Roman[tier]} required" : Format(building.Data.prices[tier]);
            Text(Inset(rect, 13, 214, 22), requirement, 14, upgradeable ? BrightGold : MutedText);
            if (Button(Inset(rect, 13, 238, 32), "UPGRADE  [E]", Green, 17, run.CanUpgrade(building)))
                Upgrade(building);
            Price repair = run.RepairPrice(building);
            Rect repairRect = new(rect.x + 13, rect.y + 279, generator ? 307 : 150, 31);
            bool damaged = building.Health < building.MaximumHealth;
            if (Button(repairRect, damaged ? $"Repair  /  {Format(repair)}" : "Fully repaired", Slot, 14, damaged && run.Resources.Covers(repair)))
                run.Repair(building);
            if (!generator)
            {
                Rect sell = new(rect.x + 170, rect.y + 279, 150, 31);
                if (Button(sell, "Sell  [T]", Red, 14))
                {
                    Sell(building);
                    return;
                }
                if (sell.Contains(Mouse))
                    Tooltip(new Rect(rect.x, rect.yMax + 5, rect.width, 31), $"Refund  /  {Format(run.Refund(building))}");
            }
        }

        private string MachineStatus(Structure building)
        {
            if (building.Kind == BuildingKind.Generator) return run.GeneratorOnline ? "ONLINE" : "OFFLINE";
            if (building.Kind == BuildingKind.Miner)
            {
                if (building.Fuel <= 0) return "NEEDS FUEL";
                if (building.Stored >= building.Data.Value(building.Data.capacity, building.Tier)) return "OUTPUT FULL";
                if (building.DepositNode == null || building.DepositNode.Depleted) return "DEPOSIT EMPTY";
                return "EXTRACTING";
            }
            if (catalog.RecipeFor(building.Kind) == null) return "DEFENSE";
            if (building.Queued > 0) return "PROCESSING";
            return building.Stored > 0 ? "OUTPUT READY" : "AWAITING INPUT";
        }

        private string MachineFuelTime(Structure building)
        {
            float rate = run.FuelRate(building);
            return rate > 0 ? TimeLeft(building.Fuel / rate) : "No burn";
        }

        private void DrawFuelAction(Rect rect, Structure building, ResourceKind kind, float stock)
        {
            int load = run.FuelLoad(building, kind);
            string material = RunState.ResourceName(kind);
            float rate = run.FuelRate(building);
            string duration = rate > 0 ? $"  +{load * run.FuelValue(kind) / rate:0}s" : "";
            string label = load > 0 ? $"Load {load} {material}{duration}" :
                stock < 1 ? $"No {material} available" : $"No room for {material}";
            if (Button(rect, label, Slot, 16, load > 0)) run.Refuel(building, kind);
        }

        private void DrawShop()
        {
            Dim();
            Rect rect = ShopRect;
            Frame(rect, Panel, Line);
            Text(Inset(rect, 21, 12, 43), "EQUIPMENT SHOP", 35, TextColor, bold: true);
            Price stock = run.Resources;
            string supplies = $"WOOD {Number(stock.wood)}   STONE {Number(stock.stone)}   COAL {Number(stock.coal)}   PLATES {Number(stock.plate)}   PARTS {Number(stock.parts)}";
            Text(new Rect(rect.xMax - 403, rect.y + 21, 338, 27), supplies, 16, BrightGold, TextAnchor.MiddleRight, true);
            if (Button(new Rect(rect.xMax - 52, rect.y + 17, 31, 31), "×", Slot, 20))
            {
                shop = false;
                return;
            }
            float tabWidth = (rect.width - 46) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
            {
                Rect tab = new(rect.x + 21 + i * tabWidth, rect.y + 67, tabWidth - 3, 36);
                if (Button(tab, TabNames[i].ToUpperInvariant(), shopTab == i ? Green : Slot, 17)) shopTab = i;
                if (shopTab == i) Fill(new Rect(tab.x, tab.yMax - 2, tab.width, 2), Air);
            }
            int index = 0;
            foreach (ItemKind kind in ShopItems[shopTab])
                DrawShopItem(new Rect(rect.x + 21, rect.y + 115 + index++ * 74, rect.width - 42, 66), kind);
            if (shopTab == 3 && run.ActivePet.HasValue)
            {
                string evolve = run.PetEvolution >= 3 ? "Maximum evolution" : run.PetHealth <= 0 ? "Companion unavailable" :
                    run.CanEvolvePet ? $"Evolve  /  {run.PetEvolutionPrice} tokens" :
                    $"Requires level {run.PetEvolutionLevel}  /  {run.PetEvolutionPrice} tokens";
                if (Button(new Rect(rect.x + 21, rect.y + 275, 238, 34), evolve, Slot, 16, run.CanEvolvePet)) run.EvolvePet();
                if (Button(new Rect(rect.x + 269, rect.y + 275, 133, 34), "Call companion", Slot, 17, run.PetHealth > 0)) run.Whistle();
            }
            Fill(Inset(rect, 21, rect.height - 55, 1), Line);
            string footer = notice != null && Time.unscaledTime < noticeUntil ? notice : null;
            if (footer == null && run.Generator != null)
                footer = $"{(run.Night ? "Night" : "Daylight")}  /  {TimeLeft(run.PhaseLength - run.PhaseTime)}   •   " +
                    (run.GeneratorOnline ? $"Fuel {TimeLeft(run.Generator.Fuel)}" : "Generator offline");
            Text(Inset(rect, 21, rect.height - 49, 30), footer, 15, run.GeneratorOnline ? MutedText : Coral);
        }

        private void DrawShopItem(Rect row, ItemKind kind)
        {
            ItemData item = catalog.Item(kind);
            int tier = run.ItemTier(kind);
            bool pet = kind is ItemKind.Carl or ItemKind.Woody;
            bool consumable = kind is ItemKind.HealthPotion or ItemKind.PetPotion;
            bool hat = kind == ItemKind.Hat;
            bool maxed = !pet && !consumable && !hat && tier >= item.prices.Length;
            bool active = pet && run.ActivePet == kind && run.PetHealth > 0;
            Price price = item.prices[Mathf.Min(tier, item.prices.Length - 1)];
            bool enough = pet || hat && tier > 0 || run.Resources.Covers(price);
            bool potionReady = !consumable || (kind == ItemKind.HealthPotion ? run.CanHealPlayer : run.CanHealPet);
            bool available = !maxed && !active && enough && potionReady;
            Frame(row, Slot, Line);
            Rect artwork = new(row.x + 8, row.y + 7, 50, 52);
            Fill(artwork, DeepPanel);
            Icon(artwork, SpriteArt.Item(kind, Mathf.Max(1, tier)), .83f, Color.white);
            Text(new Rect(row.x + 67, row.y + 7, 148, 25), item.title, 21, TextColor, bold: true);
            string tierLabel = active ? $"Level {run.PetLevel}   /   {run.PetExperience % 50}/50 XP" :
                pet || consumable || hat ? "" : maxed ? "Maximum tier" : $"Tier {tier + 1}";
            Text(new Rect(row.x + 67, row.y + 35, 143, 23), tierLabel, 14, MutedText);
            Text(new Rect(row.x + 218, row.y + 4, row.width - 365, 36), item.description, 14, MutedText, wrap: true);
            Text(new Rect(row.x + 218, row.y + 41, row.width - 365, 20), ShopStats(item, tier), 13, TextColor);
            string action = maxed ? "MAX TIER" : active ? "ACTIVE" : !enough ? "NEED MATERIALS" : !potionReady ? PotionStatus(kind).ToUpperInvariant() :
                pet ? "ADOPT" : consumable ? "USE" :
                hat && tier > 0 ? run.WearingHat ? "UNEQUIP" : "EQUIP" : tier > 0 ? "UPGRADE" : "BUY";
            if (Button(new Rect(row.xMax - 137, row.y + 8, 125, 29), action, Green, 16, available) && !run.Buy(kind))
                Notify("This item is unavailable right now.");
            if (!pet && !maxed && !(hat && tier > 0))
                Text(new Rect(row.xMax - 144, row.y + 39, 136, 22), Format(price), 13, BrightGold, TextAnchor.MiddleCenter);
        }

        private string PotionStatus(ItemKind kind)
        {
            Price price = catalog.Item(kind).prices[0];
            if (!run.Resources.Covers(price)) return Format(price);
            if (kind == ItemKind.HealthPotion)
                return run.Health >= catalog.playerHealth ? "Full health" : run.CanHealPlayer ? Format(price) : "Recharging";
            if (run.PetHealth <= 0) return "Needs companion";
            return run.PetHealth >= run.PetMaxHealth ? "Full health" : run.CanHealPet ? Format(price) : "Recharging";
        }

        private string ShopStats(ItemData item, int tier)
        {
            int next = Math.Min(item.prices.Length - 1, tier);
            if (item.kind == ItemKind.Pickaxe)
                return $"Damage {item.power[next]:0}   /   Harvest {catalog.harvestYield[next]:0.##}   /   {1 / item.interval:0.##}/s";
            if (item.kind <= ItemKind.Bomb) return $"Damage {item.power[next]:0}   /   {1 / item.interval:0.##}/s";
            if (item.kind == ItemKind.Shield) return $"Capacity {item.power[next]:0}   /   Recharge after {item.recovery[next]:0}s";
            return "";
        }

        private void DrawNotice()
        {
            string message = notice != null && Time.unscaledTime < noticeUntil ? notice : null;
            bool urgent = run.Generator != null && !run.GeneratorOnline || !run.CanBreathe && run.Breath <= 1.5f;
            if (urgent)
                message = run.Generator != null && !run.GeneratorOnline ? "Generator offline. Add wood or coal." : "Return to breathable air.";
            if (message == null && profile.hints)
            {
                if (run.Generator != null && hintStep < 1) hintStep = 1;
                if (run.Count(BuildingKind.Arrow) > 0 && hintStep == 1) hintStep = 2;
                if (run.Count(BuildingKind.Smelter) > 0 && hintStep == 2) hintStep = 3;
                if (run.Count(BuildingKind.Assembler) > 0 && hintStep == 3) hintStep = 4;
                if ((run.Generator?.Tier ?? 0) >= 2 && hintStep == 4) hintStep = 5;
                message = hintStep switch
                {
                    0 => placing == BuildingKind.Generator ? "Choose a site inside the dashed circle." : "Press G to place the Oxygen Generator.",
                    1 => "Gather wood and stone, then build an Arrow Tower [4].",
                    2 => "Refuel the generator [G]. Build a Smelter [9].",
                    3 => "Smelt iron ore. Use two plates to build an Assembler [M].",
                    4 => "Assemble parts to upgrade the generator or your gear.",
                    _ => null
                };
                if (!run.Night && run.Generator != null && run.PhaseLength - run.PhaseTime < 6)
                    message = "Night is approaching. Return to the generator.";
            }
            if (message == null) return;
            Rect rect = GuidanceRect;
            Frame(rect, urgent ? Red : Panel, urgent ? Coral : Line);
            Fill(new Rect(rect.x + 1, rect.y + 1, 2, rect.height - 2), urgent ? Coral : Air);
            Text(Inset(rect, 11, 1, rect.height - 2), message, 15, TextColor, wrap: true);
        }

        private void DrawDeath()
        {
            Dim();
            Rect rect = new(Width * .5f - 262, Height * .5f - 137, 524, 274);
            Frame(rect, Panel, Line);
            Fill(new Rect(rect.x, rect.y, rect.width, 3), Coral);
            Text(Inset(rect, 23, 16, 57), run.Defeated ? "GENERATOR DESTROYED" : "SUIT SIGNAL LOST", 39, TextColor, bold: true);
            Text(Inset(rect, 23, 77, 24), $"WAVE  {run.Wave}     /     SCORE  {run.Score:N0}", 19, Coral, bold: true);
            var recovery = new Rect(rect.x + 23, rect.y + 133, 233, 46);
            var restart = new Rect(rect.x + 267, rect.y + 133, 234, 46);
            Fill(Inset(rect, 23, 113, 1), Line);
            if (Button(recovery, run.Defeated ? "REBUILD" : "RESPAWN", Green, 21))
            {
                if (run.Defeated) RestartAfterLoss();
                else run.Respawn();
                return;
            }
            if (Button(restart, "NEW RUN", Slot, 21)) RequestNewRun();
            string recover = run.Defeated ? "Same map. Keep 75% of materials and all boss tokens." : "Return to the generator with your equipment and supplies.";
            Text(new Rect(recovery.x, recovery.yMax + 10, recovery.width, 61), recover, 16, MutedText, TextAnchor.UpperLeft, wrap: true);
            Text(new Rect(restart.x, restart.yMax + 10, restart.width, 61), "Start over with empty supplies.", 16, MutedText, TextAnchor.UpperLeft, wrap: true);
            if (!confirmNewRun) DrawCheckpointNotice(Inset(rect, 23, 237, 28));
        }

        private void DrawSettings()
        {
            Dim();
            Rect rect = SettingsRect;
            Frame(rect, Panel, Line);
            Text(Inset(rect, 22, 13, 55), intro ? "SETTINGS" : "PAUSED", 40, TextColor, bold: true);
            if (Button(new Rect(rect.xMax - 55, rect.y + 22, 32, 32), "×", Slot, 21)) settings = false;
            if (Setting(rect, 81, "Sound", profile.muted ? "OFF" : "ON"))
            {
                profile.muted = !profile.muted;
                profile.Save();
            }
            if (Setting(rect, 135, "Tutorial hints", profile.hints ? "ON" : "OFF"))
            {
                profile.hints = !profile.hints;
                profile.Save();
            }
            if (Setting(rect, 189, "Display", Screen.fullScreen ? "FULLSCREEN" : "WINDOWED"))
            {
                profile.fullscreen = !Screen.fullScreen;
                Screen.fullScreenMode = profile.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                profile.Save();
            }
            if (Button(Inset(rect, 22, 248, 44), intro ? "BACK" : "RESUME", Green, 22)) settings = false;
            if (!intro)
            {
                if (Button(new Rect(rect.x + 22, rect.y + 307, 164, 35), "SAVE & MAIN MENU", Slot, 16)) SaveAndReturnToTitle();
                Text(new Rect(rect.x + 185, rect.y + 309, 247, 31), $"Map seed  {run.Seed}", 16, MutedText, TextAnchor.MiddleRight);
                if (settings) DrawCheckpointNotice(Inset(rect, 22, 345, 23));
            }
        }

        private bool Setting(Rect panel, float top, string name, string value)
        {
            Rect row = Inset(panel, 22, top, 44);
            Frame(row, DeepPanel, Line);
            Text(Inset(row, 12, 0, row.height), name, 18, TextColor);
            return Button(new Rect(row.xMax - 139, row.y + 5, 133, 34), value, Slot, 16);
        }

        private void WorldLabel(Vector2 point, string label, Color color)
        {
            Vector3 screen = sceneCamera.WorldToScreenPoint(point);
            Vector2 anchor = new(screen.x / Scale, (Screen.height - screen.y) / Scale);
            if (anchor.y < 24 || anchor.y > LoadoutDock.y - 20 || AirRect.Contains(anchor) ||
                ClockRect.Contains(anchor) || CommandRect.Contains(anchor) || selected != null && StructureRect().Contains(anchor)) return;
            Rect rect = new(anchor.x - 105, anchor.y - 11, 210, 22);
            Text(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), label, 17, new Color(0, 0, 0, color.a),
                TextAnchor.MiddleCenter, true);
            Text(rect, label, 17, color, TextAnchor.MiddleCenter, true);
        }

        private void Card(Rect rect, string label, Color accent)
        {
            Frame(rect, Panel, Line);
            Fill(new Rect(rect.x + 1, rect.y + 1, 3, 25), accent);
            Text(Inset(rect, 12, 2, 25), label, 17, accent, bold: true);
            Fill(Inset(rect, 9, 27, 1), Line);
        }

        private void Dim() => Fill(new Rect(0, 0, Width, Height), new Color(.012f, .023f, .037f, .80f));

        private void Tooltip(Rect rect, string label)
        {
            Frame(rect, DeepPanel, Line);
            Text(Inset(rect, 6, 0, rect.height), label, 14, TextColor, TextAnchor.MiddleCenter, wrap: true);
        }

        private void Meter(Rect rect, string label, string value, float fraction, Color color)
        {
            Text(new Rect(rect.x, rect.y, rect.width * .44f, rect.height - 5), label, 15, MutedText);
            Text(new Rect(rect.x + rect.width * .39f, rect.y, rect.width * .61f, rect.height - 5), value, 16, TextColor,
                TextAnchor.MiddleRight, true);
            Fill(new Rect(rect.x, rect.yMax - 4, rect.width, 4), Slot);
            Fill(new Rect(rect.x, rect.yMax - 4, rect.width * Mathf.Clamp01(fraction), 4), color);
        }

        private bool IconButton(Rect rect, Sprite icon, Color tint, string key = "", bool enabled = true)
        {
            Tile(rect, false, enabled);
            Icon(rect, icon, .53f, tint);
            if (key.Length > 0) Key(rect, key, MutedText);
            return enabled && Hit(rect);
        }

        private void Tile(Rect rect, bool selectedTile, bool enabled) =>
            Frame(rect, selectedTile ? Green : enabled ? Slot : DeepPanel,
                selectedTile ? Air : GUI.enabled && rect.Contains(Mouse) ? Quiet : Line);

        private void Key(Rect rect, string key, Color color)
        {
            var cap = new Rect(rect.xMax - 17, rect.yMax - 16, 15, 14);
            Fill(cap, DeepPanel);
            Text(cap, key, 12, color, TextAnchor.MiddleCenter, true);
        }

        private static Color Tint(bool enabled) => enabled ? Color.white : new Color(.66f, .74f, .79f, .62f);
        private static Rect Inset(Rect rect, float x, float y, float height) => new(rect.x + x, rect.y + y, rect.width - 2 * x, height);

        private static void Frame(Rect rect, Color fill, Color border)
        {
            Fill(rect, border);
            Fill(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), fill);
        }

        private static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);
        }

        private static void Icon(Rect rect, Sprite sprite, float scale, Color tint)
        {
            if (Event.current.type != EventType.Repaint) return;
            float size = Mathf.Min(rect.width, rect.height) * scale;
            GUI.DrawTexture(new Rect(rect.center - Vector2.one * size * .5f, Vector2.one * size), sprite.texture,
                ScaleMode.ScaleToFit, true, 0, tint, 0, 0);
        }

        private void Text(Rect rect, string value, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft,
            bool bold = false, bool wrap = false)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(value)) return;
            var key = (size, alignment, bold, wrap);
            if (!labelStyles.TryGetValue(key, out var style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    font = bold ? headingFont : font, fontSize = size, alignment = alignment,
                    clipping = TextClipping.Clip, padding = new RectOffset(), richText = false, wordWrap = wrap
                };
                labelStyles.Add(key, style);
            }
            style.normal.textColor = color;
            GUI.Label(rect, value, style);
        }

        private bool Button(Rect rect, string label, Color background, int size, bool enabled = true)
        {
            bool hover = enabled && GUI.enabled && rect.Contains(Mouse);
            Frame(rect, enabled ? hover ? Color.Lerp(background, Air, .14f) : background : DeepPanel, hover ? Air : Line);
            Text(Inset(rect, 4, 0, rect.height), label, size, enabled ? TextColor : Quiet, TextAnchor.MiddleCenter, true);
            return enabled && Hit(rect);
        }

        private static bool Hit(Rect rect) => GUI.Button(rect, GUIContent.none, GUIStyle.none);

        private static string TimeLeft(float seconds)
        {
            int remaining = Mathf.CeilToInt(Mathf.Max(0, seconds));
            return $"{remaining / 60}:{remaining % 60:00}";
        }

        private static string Number(float amount) => amount >= 1_000_000 ? $"{amount / 1_000_000:0.#}M" :
            amount >= 1_000 ? $"{amount / 1_000:0.#}K" : Mathf.FloorToInt(amount + .001f).ToString();

        private static string Format(Price cost)
        {
            var parts = new List<string>(7);
            if (cost.wood > 0) parts.Add($"{cost.wood:N0} wood");
            if (cost.stone > 0) parts.Add($"{cost.stone:N0} stone");
            if (cost.iron > 0) parts.Add($"{cost.iron:N0} iron ore");
            if (cost.coal > 0) parts.Add($"{cost.coal:N0} coal");
            if (cost.plate > 0) parts.Add(cost.plate == 1 ? "1 plate" : $"{cost.plate:N0} plates");
            if (cost.parts > 0) parts.Add(cost.parts == 1 ? "1 part" : $"{cost.parts:N0} parts");
            if (cost.tokens > 0) parts.Add(cost.tokens == 1 ? "1 token" : $"{cost.tokens:N0} tokens");
            return parts.Count == 0 ? "Free" : string.Join(", ", parts);
        }
    }
}
