using System;
using UnityEngine;

namespace Zombs
{
    [RequireComponent(typeof(Camera), typeof(AudioListener))]
    public sealed partial class GameSession : MonoBehaviour
    {
        [SerializeField] private GameCatalog catalog;
        private Camera sceneCamera;
        private WorldView world;
        private RunState run;
        private AudioSource audioSource;
        private AudioClip hitSound, buildSound, blastSound;
        private float audioCooldown;
        private bool intro = true, shop, settings, healing, suppressAttack;
        private int rotation, shopTab, hintStep;
        private string seedInput = "";
        private LocalProfile profile;
        private BuildingKind? placing;
        private Structure selected;
        private string notice;
        private float noticeUntil, tickRemainder;
        private Vector2 cursor;
        private int lastWave;

        public void SetCatalog(GameCatalog value) => catalog = value;

        private void Awake()
        {
            if (catalog == null) catalog = GameCatalog.CreateDefaults();
            sceneCamera = GetComponent<Camera>();
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 11.2f;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color32(105, 138, 60, 255);
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            profile = LocalProfile.Load();
            if (profile.fullscreen) Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            hitSound = Sound("Strike", .065f, 135, .5f);
            buildSound = Sound("Build", .13f, 430, .12f);
            blastSound = Sound("Blast", .17f, 64, .70f);
            world = new WorldView(sceneCamera);
            Attach(new RunState(catalog, 5238));
        }

        private void OnDestroy()
        {
            if (run != null) run.EffectOccurred -= OnEffect;
            world?.Dispose();
        }

        private void Attach(RunState state)
        {
            if (run != null) run.EffectOccurred -= OnEffect;
            run = state;
            run.EffectOccurred += OnEffect;
            world.Attach(run);
            ResetSelection();
            tickRemainder = 0;
            lastWave = 0;
            hintStep = 0;
        }

        private void Begin()
        {
            int seed = Guid.NewGuid().GetHashCode();
            if (!string.IsNullOrWhiteSpace(seedInput) && !int.TryParse(seedInput, out seed))
            {
                Notify("Enter a whole number for the seed, or leave it blank.");
                return;
            }
            profile.nickname = string.IsNullOrWhiteSpace(profile.nickname) ? "Player" : profile.nickname.Trim();
            profile.Save();
            Attach(new RunState(catalog, seed));
            intro = false;
            settings = shop = false;
            notice = null;
        }

        private void RestartAfterLoss()
        {
            if (!run.RecoverAfterDefeat()) return;
            ResetSelection();
            notice = null;
            lastWave = 0;
            hintStep = 0;
        }

        private void Update()
        {
            audioCooldown -= Time.unscaledDeltaTime;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (UpdateVerification()) return;
#endif
            if (intro)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) settings = false;
                return;
            }
            Vector3 mouse = Input.mousePosition;
            Vector2 screen = new(mouse.x, Screen.height - mouse.y);
            cursor = ScreenToWorld(screen);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (settings) settings = false;
                else if (shop) shop = false;
                else if (placing.HasValue || healing || selected != null) ResetSelection();
                else if (!run.Dead && !run.Defeated) settings = true;
            }
            if (Input.GetMouseButtonDown(1)) ResetSelection();

            if (!settings && !run.Dead && !run.Defeated)
            {
                if (Input.GetKeyDown(KeyCode.B)) ToggleShop();
                if (!shop)
                {
                    HandleHotkeys();
                    bool overUi = BlocksWorld(screen);
                    if (Input.GetMouseButtonDown(0) && !overUi) WorldClick();
                    if (!Input.GetMouseButton(0)) suppressAttack = false;
                }
            }
            if (settings || run.Defeated) return;
            bool acceptInput = !shop && !run.Dead;
            Vector2 move = acceptInput ? new Vector2(
                Axis(KeyCode.A, KeyCode.D, KeyCode.LeftArrow, KeyCode.RightArrow),
                Axis(KeyCode.S, KeyCode.W, KeyCode.DownArrow, KeyCode.UpArrow)) : Vector2.zero;
            bool attack = acceptInput && !healing && !placing.HasValue && !suppressAttack &&
                !BlocksWorld(screen) && Input.GetMouseButton(0);
            StepGame(Mathf.Min(Time.unscaledDeltaTime, .15f), move, attack);
            if (selected != null && !run.InReach(selected)) selected = null;
            if (run.Dead || run.Defeated)
            {
                shop = false;
                ResetSelection();
            }
        }

        private void StepGame(float elapsed, Vector2 move, bool attack)
        {
            tickRemainder += elapsed;
            while (tickRemainder >= RunState.TickSeconds)
            {
                tickRemainder -= RunState.TickSeconds;
                run.Step(move, cursor, attack);
            }
            if (run.Wave != lastWave)
            {
                lastWave = run.Wave;
                if (run.TimeoutActive) Notify("Timeout. Your base is safe for this night.", 4);
                else if (catalog.IsBossWave(run.Wave)) Notify($"Boss wave {run.Wave}!", 4);
            }
        }

        private void LateUpdate()
        {
            Vector3 mouse = Input.mousePosition;
            bool pointerOnHud = BlocksWorld(new Vector2(mouse.x, Screen.height - mouse.y));
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (verification != null) pointerOnHud = false;
#endif
            world.Draw(settings ? 0 : Time.unscaledDeltaTime, intro, pointerOnHud ? null : placing,
                rotation, selected, healing && !pointerOnHud, cursor);
        }

        private void HandleHotkeys()
        {
            for (int index = 0; index < 10; index++)
                if (Input.GetKeyDown(index == 9 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + index)))
                    SelectBuild((BuildingKind)index);
            if (Input.GetKeyDown(KeyCode.G))
            {
                if (run.Generator == null) SelectBuild(BuildingKind.Generator);
                else if (run.InReach(run.Generator))
                {
                    ResetSelection();
                    selected = run.Generator;
                }
            }
            if (Input.GetKeyDown(KeyCode.M)) SelectBuild(BuildingKind.Assembler);
            if (Input.GetKeyDown(KeyCode.R) && placing == BuildingKind.Melee) rotation = (rotation + 3) % 4;
            if (Input.GetKeyDown(KeyCode.Space)) run.ToggleAutoAttack();
            if (Input.GetKeyDown(KeyCode.Q))
            {
                ResetSelection();
                run.CycleWeapon();
            }
            if (Input.GetKeyDown(KeyCode.E)) Upgrade(selected ?? run.At(cursor));
            if (Input.GetKeyDown(KeyCode.T)) Sell(selected ?? run.At(cursor));
            if (Input.GetKeyDown(KeyCode.F) && !run.HealPlayer()) Notify("Potion unavailable.");
            if (Input.GetKeyDown(KeyCode.H)) run.Whistle();
        }

        private void WorldClick()
        {
            if (healing)
            {
                if (run.Cast(SpellKind.HealTowers, cursor)) healing = false;
                else Notify("Heal Towers is unavailable at that location.");
                suppressAttack = true;
                return;
            }
            if (placing.HasValue)
            {
                if (run.Place(placing.Value, cursor, rotation, out var result))
                {
                    if (placing == BuildingKind.Generator) placing = null;
                    suppressAttack = true;
                }
                else Notify(PlacementMessage(result));
                return;
            }
            var target = run.At(cursor);
            if (target != null && run.InReach(target))
            {
                selected = target;
                suppressAttack = true;
            }
            else selected = null;
        }

        private void SelectBuild(BuildingKind kind)
        {
            if (kind == BuildingKind.Miner && run.Generator != null && run.Generator.Tier < catalog.Building(kind).requiredGeneratorTier)
            {
                Notify(PlacementMessage(Placement.TierLocked));
                return;
            }
            if (kind != BuildingKind.Generator && run.Generator == null)
            {
                Notify("Place an Oxygen Generator to start your base.");
                return;
            }
            selected = null;
            healing = false;
            rotation = 0;
            placing = placing == kind ? null : kind;
        }

        private void ResetSelection()
        {
            selected = null;
            placing = null;
            healing = false;
            rotation = 0;
        }

        private void ToggleShop()
        {
            shop = !shop;
            if (shop) ResetSelection();
        }

        private void Upgrade(Structure structure)
        {
            if (structure == null) return;
            bool bulk = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ||
                Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (run.Upgrade(structure, bulk) > 0) return;
            if (structure.Tier == structure.Data.prices.Length) Notify("Already at the maximum tier.");
            else if (structure != run.Generator && structure.Tier >= run.Generator?.Tier) Notify("Upgrade the Oxygen Generator first.");
            else Notify("The upgrade is unavailable.");
        }

        private void Sell(Structure structure)
        {
            if (structure == null || !run.Sell(structure)) return;
            if (selected == structure) selected = null;
            Notify($"Sold {structure.Data.title}.");
        }

        private void Notify(string message, float duration = 2.6f)
        {
            notice = message;
            noticeUntil = Time.unscaledTime + duration;
        }

        private void OnEffect(Effect effect)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (verification != null) return;
#endif
            if (effect.Kind == EffectKind.Defeated)
            {
                profile.bestWave = Mathf.Max(profile.bestWave, run.Wave);
                profile.bestScore = Mathf.Max(profile.bestScore, run.Score);
                profile.Save();
            }
            if (profile.muted || audioCooldown > 0) return;
            switch (effect.Kind)
            {
                case EffectKind.Gain:
                case EffectKind.Impact:
                    audioSource.PlayOneShot(hitSound, .24f);
                    audioCooldown = .09f;
                    break;
                case EffectKind.Placed:
                case EffectKind.Upgraded:
                    audioSource.PlayOneShot(buildSound, .28f);
                    audioCooldown = .06f;
                    break;
                case EffectKind.Explosion:
                    audioSource.PlayOneShot(blastSound, .26f);
                    audioCooldown = .12f;
                    break;
            }
        }

        private Vector2 ScreenToWorld(Vector2 screen)
        {
            float cellsPerPixel = sceneCamera.orthographicSize * 2 / Screen.height;
            return run.Player + new Vector2(screen.x - Screen.width * .5f, Screen.height * .5f - screen.y) * cellsPerPixel;
        }

        private static float Axis(KeyCode low, KeyCode high, KeyCode otherLow, KeyCode otherHigh) =>
            (Input.GetKey(high) || Input.GetKey(otherHigh) ? 1 : 0) -
            (Input.GetKey(low) || Input.GetKey(otherLow) ? 1 : 0);

        private static AudioClip Sound(string name, float duration, float pitch, float noise)
        {
            const int sampleRate = 22050;
            var samples = new float[Mathf.CeilToInt(duration * sampleRate)];
            var random = new System.Random(16);
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Min(1, time * 500) * Mathf.Pow(1 - time / duration, 2);
                samples[i] = envelope * Mathf.Lerp(Mathf.Sin(2 * Mathf.PI * pitch * time * (1 - .3f * time / duration)),
                    (float)random.NextDouble() * 2 - 1, noise);
            }
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static string PlacementMessage(Placement placement) => placement switch
        {
            Placement.NeedGenerator => "Place an Oxygen Generator to start your base.",
            Placement.TierLocked => "Upgrade the Oxygen Generator to tier II to unlock the Miner.",
            Placement.NeedsDeposit => "Place the Miner next to iron ore, coal, or stone.",
            Placement.StarterSite => "Place the Oxygen Generator in the marked starting area.",
            Placement.Limit => "You have reached the limit for this building.",
            Placement.TooFar => "This position is outside building range.",
            Placement.Occupied => "You can't place buildings in occupied cells.",
            Placement.Insufficient => "You don't have enough resources to place this building.",
            _ => "This building is unavailable."
        };
    }
}
