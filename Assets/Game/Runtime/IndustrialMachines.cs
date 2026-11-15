using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Zombs
{
    internal sealed class IndustrialMachines
    {
        private sealed class Model
        {
            public Transform Root, Head, Rotor;
            public MeshRenderer Lens;
            public MeshRenderer[] Indicators;
        }

        private sealed class Preview
        {
            public Model Model;
            public MeshRenderer[] Renderers;
            public Color Tint;
        }

        private readonly Transform parent;
        private readonly Dictionary<BuildingKind, List<Model>> pools = new();
        private readonly Dictionary<BuildingKind, Preview> previews = new();
        private readonly Dictionary<Material, Material> previewMaterials = new();
        private readonly int[] used = new int[System.Enum.GetValues(typeof(BuildingKind)).Length];
        private readonly Material graphite, steel, armor, amber, cyan, darkLens, red, violet, furnace;
        private readonly Mesh hull, rim, disk, boxMesh, crystal;
        private readonly Texture2D casingTexture;
        private readonly MaterialPropertyBlock lensProperties = new();
        private readonly MaterialPropertyBlock previewProperties = new();
        private Preview activePreview;
        private bool previewDrawn;

        public IndustrialMachines(Transform parent)
        {
            this.parent = parent;
            Shader lit = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            graphite = Metal(lit, "Graphite housing", new Color(.15f, .19f, .22f), .26f);
            steel = Metal(lit, "Worn steel", new Color(.29f, .34f, .38f), .36f);
            armor = Metal(lit, "Pale edge trim", new Color(.52f, .57f, .59f), .34f);
            amber = Metal(lit, "Hazard amber", new Color(.91f, .52f, .15f), .24f);
            red = Metal(lit, "Oxide red", new Color(.55f, .22f, .15f), .26f);
            darkLens = Metal(lit, "Cold reactor glass", new Color(.09f, .15f, .17f), .34f);
            cyan = Metal(lit, "Energized reactor glass", new Color(.08f, .46f, .52f), .40f,
                new Color(.035f, .60f, .76f));
            furnace = Metal(lit, "Furnace grille", new Color(.78f, .39f, .07f), .30f,
                new Color(.63f, .23f, .025f));
            violet = Metal(lit, "Charged emitter crystal", new Color(.42f, .23f, .61f), .32f,
                new Color(.27f, .09f, .52f));
            casingTexture = CasingTexture();
            graphite.mainTexture = steel.mainTexture = armor.mainTexture = casingTexture;

            hull = Profile("Beveled octagonal housing", 8,
                new Vector2(.82f, 0), new Vector2(1, .14f), new Vector2(1, .70f),
                new Vector2(.86f, 1), new Vector2(0, 1));
            rim = Profile("Beveled reactor collar", 12,
                new Vector2(1, 0), new Vector2(1, .52f), new Vector2(.91f, 1),
                new Vector2(.75f, 1), new Vector2(.67f, .45f), new Vector2(.67f, 0));
            disk = Profile("Faceted lens and bearing", 16,
                new Vector2(.92f, 0), new Vector2(1, .29f), new Vector2(.83f, 1), new Vector2(0, 1));
            boxMesh = BoxMesh();
            crystal = Profile("Faceted emitter crystal", 4,
                new Vector2(.01f, 0), new Vector2(1, .43f), new Vector2(.75f, .69f), new Vector2(0, 1));
        }

        public void BeginFrame()
        {
            System.Array.Clear(used, 0, used.Length);
            previewDrawn = false;
        }

        public bool Draw(Structure building, bool powered, float clock)
        {
            BuildingKind kind = building.Kind;
            if (!pools.TryGetValue(kind, out var pool))
            {
                pool = new List<Model>();
                pools.Add(kind, pool);
            }
            int index = used[(int)kind]++;
            if (index == pool.Count) pool.Add(Create(kind));
            Model model = pool[index];
            if (!model.Root.gameObject.activeSelf) model.Root.gameObject.SetActive(true);
            Place(model, building.Position, building.Data.size);

            if (kind == BuildingKind.Generator)
            {
                DrawGenerator(model, powered, clock);
            }
            else if (kind is BuildingKind.Arrow or BuildingKind.Cannon or BuildingKind.Melee or BuildingKind.Bomb or BuildingKind.Mage)
            {
                bool hammer = kind == BuildingKind.Melee;
                model.Head.localRotation = Quaternion.Euler(0, 0, building.Angle);
                float kick = Mathf.Clamp01(1 - (building.Data.interval - building.Cooldown) / .16f);
                model.Rotor.localPosition = new Vector3(0, -.14f * kick, 0);
                if (hammer) model.Rotor.localRotation = Quaternion.Euler(-22 * kick, 0, 0);
            }
            else if (kind is BuildingKind.Smelter or BuildingKind.Assembler or BuildingKind.Miner)
            {
                bool active = kind == BuildingKind.Miner
                    ? building.Fuel > 0 && building.DepositNode != null && !building.DepositNode.Depleted &&
                        building.Stored < building.Data.Value(building.Data.capacity, building.Tier)
                    : building.Queued > 0;
                Material signal = active ? kind == BuildingKind.Smelter ? furnace : cyan : building.Stored >= 1 ? cyan : amber;
                foreach (var indicator in model.Indicators) indicator.sharedMaterial = signal;
                if (kind == BuildingKind.Assembler)
                    model.Rotor.localRotation = Quaternion.Euler(0, 0, active ? Mathf.Sin(clock * 4.1f) * 13 : 0);
                else if (kind == BuildingKind.Miner && active)
                    model.Rotor.localRotation = Quaternion.Euler(0, clock * 450, 0);
            }
            return true;
        }

        public void DrawPreview(BuildingKind kind, Vector2 point, float size, float angle, Color tint)
        {
            if (!previews.TryGetValue(kind, out Preview preview))
            {
                Model model = Create(kind);
                model.Root.name = $"{kind} placement preview";
                var renderers = model.Root.GetComponentsInChildren<MeshRenderer>();
                foreach (var renderer in renderers)
                {
                    renderer.sharedMaterial = PreviewMaterial(renderer.sharedMaterial);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                preview = new Preview { Model = model, Renderers = renderers };
                previews.Add(kind, preview);
            }
            if (activePreview != preview)
            {
                activePreview?.Model.Root.gameObject.SetActive(false);
                activePreview = preview;
                preview.Model.Root.gameObject.SetActive(true);
            }
            previewDrawn = true;

            Model ghost = preview.Model;
            Place(ghost, point, size);
            if (ghost.Head != null) ghost.Head.localRotation = Quaternion.Euler(0, 0, angle);
            if (preview.Tint == tint) return;
            preview.Tint = tint;
            foreach (var renderer in preview.Renderers)
            {
                Color color = Color.Lerp(renderer.sharedMaterial.color, tint, .48f);
                color.a = tint.a;
                previewProperties.SetColor("_Color", color);
                renderer.SetPropertyBlock(previewProperties);
            }
        }

        private Material PreviewMaterial(Material source)
        {
            if (previewMaterials.TryGetValue(source, out Material material)) return material;
            // Use the Standard shader's Fade mode so the preview shares the model's lighting and texture.
            material = new Material(source)
            {
                name = source.name + " (preview)",
                renderQueue = (int)RenderQueue.Transparent
            };
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Mode", 2);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            previewMaterials.Add(source, material);
            return material;
        }

        private void DrawGenerator(Model model, bool powered, float clock)
        {
            model.Lens.sharedMaterial = powered ? cyan : darkLens;
            foreach (var indicator in model.Indicators) indicator.sharedMaterial = powered ? cyan : darkLens;
            if (powered)
            {
                model.Rotor.localRotation = Quaternion.Euler(0, 0, clock * 39);
                lensProperties.SetColor("_EmissionColor",
                    new Color(.08f, .72f, .89f) * (.87f + .13f * Mathf.Sin(clock * 3.3f)));
                model.Lens.SetPropertyBlock(lensProperties);
            }
            else model.Lens.SetPropertyBlock(null);
        }

        public void EndFrame()
        {
            foreach (var pair in pools)
                HideUnused(pair.Value, used[(int)pair.Key]);
            if (!previewDrawn && activePreview != null)
            {
                activePreview.Model.Root.gameObject.SetActive(false);
                activePreview = null;
            }
        }

#if UNITY_EDITOR
        internal Transform CreateIconModel(BuildingKind kind)
        {
            Model model = Create(kind);
            if (kind == BuildingKind.Generator) DrawGenerator(model, true, .38f);
            if (model.Head != null) model.Head.localRotation = Quaternion.Euler(0, 0, -145);
            return model.Root;
        }
#endif

        public void Dispose(bool immediate = false)
        {
            foreach (var material in previewMaterials.Values) Release(material);
            Release(graphite);
            Release(steel);
            Release(armor);
            Release(amber);
            Release(cyan);
            Release(darkLens);
            Release(red);
            Release(violet);
            Release(furnace);
            Release(hull);
            Release(rim);
            Release(disk);
            Release(boxMesh);
            Release(crystal);
            Release(casingTexture);

            void Release(Object asset)
            {
                if (immediate) Object.DestroyImmediate(asset);
                else Object.Destroy(asset);
            }
        }

        private Model Create(BuildingKind kind) => kind switch
        {
            BuildingKind.Generator => Generator(),
            BuildingKind.Wall or BuildingKind.Door or BuildingKind.SlowTrap => Barrier(kind),
            BuildingKind.Arrow or BuildingKind.Cannon or BuildingKind.Melee or BuildingKind.Bomb or BuildingKind.Mage => Turret(kind),
            BuildingKind.Smelter => Smelter(),
            BuildingKind.Assembler => Assembler(),
            BuildingKind.Miner => Miner(),
            _ => throw new System.ArgumentOutOfRangeException(nameof(kind))
        };

        private static void Place(Model model, Vector2 point, float size)
        {
            model.Root.localPosition = new Vector3(point.x, point.y, -.02f);
            model.Root.localScale = Vector3.one * (size * .5f);
        }

        private static void HideUnused(List<Model> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
                if (pool[i].Root.gameObject.activeSelf) pool[i].Root.gameObject.SetActive(false);
        }

        private Model Generator()
        {
            Transform root = Child("Oxygen generator model", parent);
            Lathe(root, "Foundation", hull, graphite, 0, .95f, .25f, 22.5f);
            Lathe(root, "Pressure vessel", hull, graphite, .19f, .87f, .46f, 22.5f);
            Lathe(root, "Service deck", hull, graphite, .62f, .77f, .13f, 22.5f);
            Lathe(root, "Lens collar", rim, steel, .72f, .59f, .19f);
            MeshRenderer lens = Lathe(root, "Glass core", disk, cyan, .72f, .37f, .16f);
            Transform rotor = Child("Core rotation", root);
            var strips = new List<MeshRenderer>();
            for (int i = 0; i < 8; i++)
            {
                Transform mount = Child("Casing segment", root);
                mount.localRotation = Quaternion.Euler(0, 0, i * 45);
                Box(mount, "Side rib", steel, 0, .79f, .34f, .23f, .15f, .42f);
                Box(mount, "Cap trim", armor, 0, .69f, .72f, .26f, .19f, .08f);
                Box(mount, "Inset panel", graphite, 0, .793f, .52f, .30f, .14f, .16f);
                strips.Add(Box(mount, "Power conduit", cyan, 0, .86f, .56f, .17f, .05f, .046f).GetComponent<MeshRenderer>());
                Transform tab = Child("Rotor segment", rotor);
                tab.localRotation = Quaternion.Euler(0, 0, i * 45);
                Box(tab, "Turbine fin", steel, 0, .38f, .875f, .095f, .14f, .06f);
            }
            Box(root, "Front step", graphite, 0, .89f, .17f, .48f, .21f, .22f);
            return new Model { Root = root, Rotor = rotor, Lens = lens, Indicators = strips.ToArray() };
        }

        private Model Barrier(BuildingKind kind)
        {
            Transform root = Child($"{kind} model", parent);
            if (kind == BuildingKind.SlowTrap)
            {
                Lathe(root, "Recessed mount", hull, graphite, .015f, .82f, .21f, 22.5f);
                Lathe(root, "Coil", rim, steel, .19f, .58f, .19f);
                Lathe(root, "Slow field emitter", disk, violet, .19f, .39f, .13f);
                for (int i = 0; i < 4; i++)
                {
                    Transform tab = Child("Ground lug", root);
                    tab.localRotation = Quaternion.Euler(0, 0, i * 90);
                    Box(tab, "Strut", armor, 0, .70f, .19f, .31f, .20f, .20f);
                }
            }
            else
            {
                bool door = kind == BuildingKind.Door;
                Box(root, "Foundation", graphite, 0, 0, .14f, 1.94f, 1.46f, .28f);
                Box(root, "Armored shell", steel, 0, 0, .46f, 1.82f, 1.29f, .52f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Box(root, "Pale cap", armor, side * .43f, 0, .75f, .70f, 1.06f, .14f);
                    Box(root, "Front recess", graphite, side * .43f, .64f, .49f, .57f, .085f, .23f);
                    if (door)
                        Box(root, "Door signal", cyan, side * .25f, .19f, .83f, .08f, .34f, .04f);
                    else
                    {
                        Transform brace = Box(root, "Diagonal brace", graphite, side * .43f, .04f, .827f, .12f, .57f, .055f);
                        brace.localRotation = Quaternion.Euler(0, 0, -31);
                    }
                }
            }
            return new Model { Root = root };
        }

        private Model Turret(BuildingKind kind)
        {
            Transform root = Child($"{kind} tower model", parent);
            Lathe(root, "Foundation", hull, graphite, 0, .84f, .25f, 22.5f);
            Lathe(root, "Armored pedestal", hull, steel, .15f, .70f, .31f, 22.5f);
            Lathe(root, "Rotating bearing", rim, armor, .39f, .57f, .14f);
            Box(root, "Base plate", graphite, 0, .64f, .23f, .51f, .19f, .22f);
            Box(root, "Type stripe", kind == BuildingKind.Arrow ? cyan : kind == BuildingKind.Mage ? violet : amber,
                0, .68f, .37f, .39f, .10f, .045f);
            Transform head = Child("Aim pivot", root);
            Transform carriage = Child("Recoil carriage", head);
            switch (kind)
            {
                case BuildingKind.Arrow:
                    ArrowHead(carriage);
                    break;
                case BuildingKind.Cannon:
                    CannonHead(carriage);
                    break;
                case BuildingKind.Melee:
                    MeleeHead(carriage);
                    break;
                case BuildingKind.Bomb:
                    BombHead(carriage);
                    break;
                case BuildingKind.Mage:
                    MageHead(carriage);
                    break;
            }
            return new Model { Root = root, Head = head, Rotor = carriage };
        }

        private void ArrowHead(Transform carriage)
        {
            Box(carriage, "Rail housing", graphite, 0, -.08f, .63f, .49f, .87f, .25f);
            Box(carriage, "Launch rail", steel, 0, .28f, .73f, .26f, 1.14f, .19f);
            Box(carriage, "Guide", armor, 0, .28f, .85f, .11f, .99f, .045f);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform limb = Box(carriage, "Launcher limb", armor, side * .34f, .40f, .76f, .65f, .13f, .14f);
                limb.localRotation = Quaternion.Euler(0, 0, -side * 22);
                Box(carriage, "Limb tip", steel, side * .62f, .30f, .76f, .15f, .23f, .18f);
                Transform cord = Box(carriage, "Tension cord", cyan, side * .32f, .16f, .86f, .66f, .033f, .035f);
                cord.localRotation = Quaternion.Euler(0, 0, side * 24);
            }
        }

        private void CannonHead(Transform carriage)
        {
            Box(carriage, "Breech", steel, 0, -.20f, .65f, .71f, .69f, .36f);
            Box(carriage, "Breech plate", graphite, 0, -.28f, .83f, .45f, .34f, .10f);
            Pipe(carriage, "Single barrel", hull, steel, new Vector3(0, -.13f, -.73f),
                new Vector3(0, .72f, -.91f), .19f);
            Pipe(carriage, "Muzzle brake", rim, armor, new Vector3(0, .62f, -.89f),
                new Vector3(0, .88f, -.95f), .235f);
            Pipe(carriage, "Muzzle bore", disk, graphite, new Vector3(0, .76f, -.92f),
                new Vector3(0, .79f, -.93f), .13f);
        }

        private void MeleeHead(Transform carriage)
        {
            Box(carriage, "Swing motor", steel, 0, -.16f, .67f, .62f, .60f, .38f);
            Box(carriage, "Locking bridge", amber, 0, .03f, .81f, .78f, .15f, .12f);
            Pipe(carriage, "Wedge blade", crystal, armor, new Vector3(0, -.05f, -.70f),
                new Vector3(0, .75f, -1.17f), .30f);
            Box(carriage, "Blade spine", steel, 0, .20f, .80f, .13f, .61f, .29f);
        }

        private void BombHead(Transform carriage)
        {
            Box(carriage, "Launcher cowl", steel, 0, -.05f, .68f, 1.27f, .86f, .34f);
            Box(carriage, "Red armor strap", red, 0, -.18f, .89f, 1.15f, .25f, .075f);
            for (int barrel = -1; barrel <= 1; barrel++)
            {
                float x = barrel * .34f;
                Pipe(carriage, "Mortar port", rim, armor, new Vector3(x, .21f, -.70f),
                    new Vector3(x, .57f, -.93f), .17f);
                Pipe(carriage, "Dark mortar bore", disk, graphite, new Vector3(x, .42f, -.83f),
                    new Vector3(x, .45f, -.86f), .104f);
            }
        }

        private void MageHead(Transform carriage)
        {
            Lathe(carriage, "Crystal core", crystal, violet, .52f, .35f, .47f, 45);
            for (int side = -1; side <= 1; side += 2)
            {
                Beam(carriage, "Emitter frame", graphite, new Vector3(side * .53f, -.16f, -.57f),
                    new Vector3(side * .25f, .51f, -.95f), .24f);
                Beam(carriage, "Emitter trim", armor, new Vector3(side * .53f, -.15f, -.71f),
                    new Vector3(side * .25f, .51f, -1.06f), .11f);
            }
        }

        private Model Smelter()
        {
            Transform root = Child("Smelter model", parent);
            root.localRotation = Quaternion.Euler(0, 0, 180);
            Box(root, "Low foundation", graphite, 0, 0, .12f, 1.72f, 1.63f, .24f);
            Box(root, "Furnace shell", steel, 0, -.04f, .49f, 1.58f, 1.34f, .68f);
            Box(root, "Raised top plate", armor, 0, -.10f, .82f, 1.35f, 1.11f, .13f);
            Box(root, "Furnace opening", graphite, 0, .64f, .52f, 1.11f, .16f, .45f);
            var grille = new MeshRenderer[4];
            for (int i = 0; i < grille.Length; i++)
                grille[i] = Box(root, "Firebrick gap", furnace, (i - 1.5f) * .19f, .729f, .51f, .12f, .036f, .30f)
                    .GetComponent<MeshRenderer>();
            for (int side = -1; side <= 1; side += 2)
            {
                Transform chimney = Child("Chimney", root);
                chimney.localPosition = new Vector3(side * .37f, -.31f, 0);
                Lathe(chimney, "Flue shroud", hull, graphite, .80f, .22f, .36f, 22.5f);
                Lathe(chimney, "Exhaust rim", rim, armor, 1.11f, .20f, .10f);
                Lathe(chimney, "Exhaust bore", disk, graphite, 1.14f, .126f, .03f);
                Box(root, "Armored corner", armor, side * .71f, .44f, .36f, .19f, .51f, .50f);
            }
            return new Model { Root = root, Indicators = grille };
        }

        private Model Assembler()
        {
            Transform root = Child("Assembler model", parent);
            root.localRotation = Quaternion.Euler(0, 0, 180);
            Box(root, "Low foundation", graphite, 0, 0, .13f, 1.78f, 1.61f, .26f);
            Box(root, "Roller bed", steel, 0, .22f, .37f, 1.58f, 1.0f, .42f);
            Box(root, "Infeed recess", graphite, 0, .35f, .59f, 1.11f, .68f, .11f);
            Box(root, "Status recess", graphite, 0, .74f, .36f, 1.16f, .12f, .25f);
            var indicators = new MeshRenderer[4];
            for (int i = 0; i < indicators.Length; i++)
            {
                float x = (i - 1.5f) * .24f;
                Box(root, "Roller", armor, x, .31f, .64f, .10f, .61f, .08f);
                indicators[i] = Box(root, "Bay lamp", cyan, x, .80f, .37f, .105f, .044f, .15f)
                    .GetComponent<MeshRenderer>();
            }
            Transform pedestal = Child("Robot base", root);
            pedestal.localPosition = new Vector3(-.52f, -.46f, 0);
            Lathe(pedestal, "Servo pedestal", hull, steel, .31f, .32f, .33f, 22.5f);
            Transform arm = Child("Robot pivot", pedestal);
            arm.localPosition = new Vector3(0, 0, -.57f);
            Vector3 shoulder = new(0, 0, 0), elbow = new(.51f, .12f, -.54f), wrist = new(1.00f, .56f, -.26f);
            Beam(arm, "Lower actuator", graphite, shoulder, elbow, .21f);
            Beam(arm, "Upper actuator", armor, elbow, wrist, .18f);
            Box(arm, "Joint accent", amber, elbow.x, elbow.y, -elbow.z, .25f, .22f, .21f);
            Box(arm, "Tool head", steel, wrist.x, wrist.y, -wrist.z + .09f, .26f, .22f, .29f);
            return new Model { Root = root, Rotor = arm, Indicators = indicators };
        }

        private Model Miner()
        {
            Transform root = Child("Miner model", parent);
            root.localRotation = Quaternion.Euler(0, 0, 180);
            Box(root, "Crawler body", graphite, 0, -.04f, .39f, 1.30f, 1.36f, .44f);
            Box(root, "Engine housing", steel, 0, -.39f, .60f, 1.23f, .75f, .43f);
            Box(root, "Engine cover", armor, 0, -.37f, .835f, .72f, .42f, .08f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * .65f;
                Box(root, "Track core", graphite, x, -.04f, .31f, .42f, 1.64f, .62f);
                for (int tread = -2; tread <= 2; tread++)
                    Box(root, "Tread shoe", steel, x, tread * .255f - .035f, .61f, .39f, .16f, .115f);
                Box(root, "Axle guard", armor, x, .68f, .31f, .30f, .19f, .31f);
            }
            Box(root, "Gearbox", graphite, 0, .29f, .58f, .61f, .49f, .43f);
            Transform rotor = Child("Drill spindle", root);
            rotor.localPosition = new Vector3(0, .40f, -.49f);
            Pipe(rotor, "Conical drill", crystal, amber, new Vector3(0, -.02f, 0), new Vector3(0, .57f, 0), .29f);
            for (int i = 0; i < 3; i++)
            {
                float radius = .27f - i * .055f;
                Pipe(rotor, "Drill edge", rim, graphite, new Vector3(0, .19f + i * .11f, 0),
                    new Vector3(0, .23f + i * .11f, 0), radius);
            }
            var signal = Box(root, "Control lamp", cyan, 0, -.11f, .855f, .23f, .09f, .05f).GetComponent<MeshRenderer>();
            return new Model { Root = root, Rotor = rotor, Indicators = new[] { signal } };
        }

        private void Beam(Transform parent, string name, Material material, Vector3 start, Vector3 end, float width)
        {
            Vector3 center = (start + end) * .5f;
            Transform beam = Box(parent, name, material, center.x, center.y, -center.z, width,
                Vector3.Distance(start, end), width);
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
        }

        private static void Pipe(Transform parent, string name, Mesh mesh, Material material, Vector3 start, Vector3 end, float radius)
        {
            Transform pipe = Lathe(parent, name, mesh, material, 0, radius, Vector3.Distance(start, end)).transform;
            pipe.localPosition = start;
            pipe.localRotation = Quaternion.FromToRotation(Vector3.back, end - start);
        }

        private static Transform Child(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private Transform Box(Transform parent, string name, Material material,
            float x, float y, float height, float width, float length, float depth)
        {
            Transform box = Child(name, parent);
            box.localPosition = new Vector3(x, y, -height);
            box.localScale = new Vector3(width, length, depth);
            box.gameObject.AddComponent<MeshFilter>().sharedMesh = boxMesh;
            box.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }

        private static Mesh BoxMesh()
        {
            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var uv = new List<Vector2>(96);
            var triangles = new List<int>(324);
            const float bevel = .08f;
            const float inner = .5f - bevel;
            float[] stops = { -.5f, -inner, inner, .5f };
            foreach (Vector3 normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 horizontal = Vector3.Cross(normal, Mathf.Abs(normal.y) > .5f ? Vector3.forward : Vector3.up);
                Vector3 vertical = Vector3.Cross(normal, horizontal);
                Vector3 center = normal * .5f;
                int first = vertices.Count;
                foreach (float y in stops)
                foreach (float x in stops)
                {
                    Vector3 cube = center + horizontal * x + vertical * y;
                    Vector3 clipped = new(Mathf.Clamp(cube.x, -inner, inner),
                        Mathf.Clamp(cube.y, -inner, inner), Mathf.Clamp(cube.z, -inner, inner));
                    Vector3 outward = (cube - clipped).normalized;
                    vertices.Add(clipped + outward * bevel);
                    normals.Add(outward);
                    uv.Add(new Vector2(x + .5f, y + .5f));
                }
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int start = first + y * 4 + x;
                    triangles.AddRange(new[] { start, start + 1, start + 5, start, start + 5, start + 4 });
                }
            }
            var mesh = new Mesh { name = "Machine casing panels" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        private static Texture2D CasingTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Brushed casing",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float grain = Mathf.PerlinNoise(x * .057f, y * .62f);
                float wear = Mathf.PerlinNoise(x * .15f + 16, y * .13f + 16);
                byte tone = (byte)(207 + 23 * grain + 24 * wear);
                pixels[y * size + x] = new Color32(tone, tone, tone, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        private static MeshRenderer Lathe(Transform parent, string name, Mesh mesh, Material material,
            float height, float radius, float depth, float angle = 0)
        {
            Transform part = Child(name, parent);
            part.localPosition = new Vector3(0, 0, -height);
            part.localRotation = Quaternion.Euler(0, 0, angle);
            part.localScale = new Vector3(radius, radius, depth);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static Material Metal(Shader shader, string name, Color color, float smoothness, Color emission = default)
        {
            var material = new Material(shader) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .18f);
            if (emission.maxColorComponent > 0 && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            return material;
        }

        private static Mesh Profile(string name, int sides, params Vector2[] profile)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 1; ring < profile.Length; ring++)
            for (int side = 0; side < sides; side++)
            {
                float angle = side * 2 * Mathf.PI / sides;
                float next = (side + 1) * 2 * Mathf.PI / sides;
                int index = vertices.Count;
                vertices.Add(ProfilePoint(profile[ring - 1], angle));
                vertices.Add(ProfilePoint(profile[ring], angle));
                vertices.Add(ProfilePoint(profile[ring], next));
                vertices.Add(ProfilePoint(profile[ring - 1], next));
                for (int corner = index; corner < index + 4; corner++)
                {
                    Vector3 point = vertices[corner];
                    uv.Add(new Vector2(point.x * .43f + point.z * .31f + .5f, point.y * .43f + point.z * .27f + .5f));
                }
                if (profile[ring].x > 0)
                {
                    triangles.Add(index);
                    triangles.Add(index + 1);
                    triangles.Add(index + 2);
                }
                triangles.Add(index);
                triangles.Add(index + 2);
                triangles.Add(index + 3);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Vector3 ProfilePoint(Vector2 profile, float angle) =>
            new(profile.x * Mathf.Cos(angle), profile.x * Mathf.Sin(angle), -profile.y);
    }
}
