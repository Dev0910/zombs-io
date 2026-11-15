using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zombs
{
    internal sealed class IndustrialActors
    {
        private sealed class Figure
        {
            public Transform Root, Upper, LeftLeg, RightLeg, LeftArm, RightArm;
            public Transform LeftKnee, RightKnee, LeftElbow, RightElbow, Grip;
            public Transform Hat, Pickaxe, Spear, Bow, Bomb;
            public MeshRenderer Shell;
        }

        private readonly Transform root;
        private readonly Mesh bevel, rounded, shell;
        private readonly Material ivory, steel, suit, visor, amber, infected, fabric, eyes, hotShell, shellEdge, bossPlate, timber, foliage;
        private readonly Figure operatorFigure;
        private readonly Transform carl, woody;
        private readonly List<Figure> enemies = new();
        private bool operatorDrawn, carlDrawn, woodyDrawn;
        private int used;
        private Vector2 travelDirection;
        private float walkPhase, walkWeight, attackTime, attackDuration;
        private ItemKind attackWeapon;

        public IndustrialActors(Transform parent)
        {
            root = Joint(parent, "Industrial actors");
            bevel = CreateBeveledBox();
            rounded = CreateRoundedMesh("Rounded armor", .78f);
            shell = CreateRoundedMesh("Infected carapace", .91f);
            var shader = Shader.Find("Standard");
            ivory = Finish(shader, "Weathered ivory ceramic", new Color(.67f, .70f, .67f), .07f, .31f);
            steel = Finish(shader, "Equipment steel", new Color(.27f, .31f, .33f), .48f, .34f);
            suit = Finish(shader, "Carbon suit joints", new Color(.105f, .13f, .145f), .01f, .19f);
            visor = Finish(shader, "Polarized visor", new Color(.065f, .16f, .19f), .27f, .60f,
                new Color(.013f, .055f, .082f));
            amber = Finish(shader, "Ochre enamel", new Color(.83f, .47f, .13f), .12f, .33f,
                new Color(.19f, .07f, .008f));
            infected = Finish(shader, "Oxidized infected shell", new Color(.38f, .34f, .33f), .27f, .24f);
            shellEdge = Finish(shader, "Worn infected edges", new Color(.46f, .42f, .39f), .27f, .30f);
            fabric = Finish(shader, "Infected undersuit", new Color(.20f, .16f, .15f), .06f, .21f);
            eyes = Finish(shader, "Infected eye glow", new Color(.87f, .24f, .12f), .06f, .28f,
                new Color(1.04f, .24f, .10f));
            hotShell = Finish(shader, "Hit flash", new Color(.60f, .36f, .30f), .18f, .26f,
                new Color(.25f, .065f, .018f));
            bossPlate = Finish(shader, "Infected boss plating", new Color(.37f, .29f, .43f), .32f, .33f);
            timber = Finish(shader, "Woody bark", new Color(.37f, .29f, .17f), 0, .16f);
            foliage = Finish(shader, "Woody leaves", new Color(.38f, .46f, .25f), 0, .19f);
            operatorFigure = CreateOperator();
            carl = CreateCompanion(true);
            woody = CreateCompanion(false);
        }

        public void BeginFrame()
        {
            used = 0;
            operatorDrawn = carlDrawn = woodyDrawn = false;
        }

        public void EndFrame()
        {
            if (!operatorDrawn && operatorFigure.Root.gameObject.activeSelf)
            {
                operatorFigure.Root.gameObject.SetActive(false);
                ResetOperatorAnimation();
            }
            if (!carlDrawn) carl.gameObject.SetActive(false);
            if (!woodyDrawn) woody.gameObject.SetActive(false);
            for (int i = used; i < enemies.Count && enemies[i].Root.gameObject.activeSelf; i++)
                enemies[i].Root.gameObject.SetActive(false);
        }

        public void ResetOperatorAnimation()
        {
            walkPhase = walkWeight = attackTime = 0;
            travelDirection = Vector2.zero;
        }

        public void StartOperatorAttack(ItemKind weapon, float interval)
        {
            attackWeapon = weapon;
            float duration = weapon switch
            {
                ItemKind.Spear => .23f,
                ItemKind.Bow => .34f,
                ItemKind.Bomb => .38f,
                _ => .28f
            };
            attackTime = attackDuration = Mathf.Min(duration, Mathf.Max(interval, RunState.TickSeconds));
        }

        public float DrawOperator(RunState run, float clock, float dt, Vector2 travel)
        {
            operatorDrawn = true;
            Figure figure = operatorFigure;
            figure.Root.gameObject.SetActive(true);
            figure.Root.localPosition = new Vector3(run.Player.x, run.Player.y, 0);
            figure.Root.localRotation = Quaternion.Euler(0, 0, RunState.Angle(run.Aim));

            float distance = travel.magnitude;
            // Simulation ticks at 30 Hz, so ease out over several rendered frames between footsteps.
            bool stepping = distance > .001f && distance < 1.8f;
            walkWeight = Mathf.MoveTowards(walkWeight, stepping ? 1 : 0, dt * (stepping ? 20 : 7));
            if (stepping && dt > 0)
            {
                const float strideLength = 2.9f;
                float cycle = 2 * Mathf.PI;
                walkPhase = Mathf.Repeat(walkPhase + distance * cycle / strideLength, cycle);
                travelDirection = Vector2.Lerp(travelDirection, travel / distance, 1 - Mathf.Exp(-24 * dt));
            }
            Vector2 movement = new(Vector2.Dot(travelDirection, new Vector2(run.Aim.y, -run.Aim.x)),
                Vector2.Dot(travelDirection, run.Aim));
            PoseLeg(figure.LeftLeg, figure.LeftKnee, movement, walkPhase, -1);
            PoseLeg(figure.RightLeg, figure.RightKnee, movement, walkPhase + Mathf.PI, 1);

            if (attackWeapon != run.Weapon) attackTime = 0;
            float action = attackTime > 0 ? 1 - attackTime / attackDuration : 1;
            float strike = 1 - Mathf.SmoothStep(0, 1, action);
            float followThrough = Mathf.Sin(action * Mathf.PI) * strike;
            attackTime = Mathf.Max(0, attackTime - dt);
            float gait = Mathf.Cos(walkPhase) * walkWeight;
            PoseUpperBody(figure, run.Weapon, movement, gait, clock, strike, followThrough);

            figure.Pickaxe.gameObject.SetActive(run.Weapon == ItemKind.Pickaxe);
            figure.Spear.gameObject.SetActive(run.Weapon == ItemKind.Spear);
            figure.Bow.gameObject.SetActive(run.Weapon == ItemKind.Bow);
            figure.Bomb.gameObject.SetActive(run.Weapon == ItemKind.Bomb && action > .64f);
            figure.Hat.gameObject.SetActive(run.WearingHat);
            return strike;
        }

        private void PoseLeg(Transform hip, Transform knee, Vector2 movement, float phase, int side)
        {
            float stride = Mathf.Cos(phase) * walkWeight;
            float lift = Mathf.Max(0, Mathf.Sin(phase)) * walkWeight;
            Vector2 reach = movement * (.14f * stride);
            var position = new Vector3(side * (.17f + .055f * walkWeight) - reach.x, -.02f - reach.y, -.49f);
            hip.SetLocalPositionAndRotation(position, Quaternion.Euler(52 * stride * movement.y - 25 * lift,
                -50 * stride * movement.x, 0));
            knee.localRotation = Quaternion.Euler(59 * lift, 0, 0);
        }

        private void PoseUpperBody(Figure figure, ItemKind weapon, Vector2 movement, float gait, float clock,
            float strike, float followThrough)
        {
            float torsoPitch = 16 * movement.y * walkWeight;
            float torsoTurn = gait * 9;
            Vector3 leftShoulder = new(-10 - gait * 34, 0, -12 + gait * 15);
            Vector3 rightShoulder = new(-17 + gait * 27, 0, 13 - gait * 16);
            float leftElbow = -20, rightElbow = -29;
            float reach = 0, toolPitch = -10, toolTurn = 9;
            switch (weapon)
            {
                case ItemKind.Pickaxe:
                    torsoTurn -= 22 * strike;
                    leftShoulder.x -= 18 * strike;
                    rightShoulder += new Vector3(-65 * strike + 30 * followThrough, 0, -48 * strike);
                    rightElbow += 21 * strike;
                    toolPitch += 30 * strike - 78 * followThrough;
                    toolTurn -= 45 * strike;
                    break;
                case ItemKind.Spear:
                    torsoPitch += 13 * strike;
                    torsoTurn -= 16 * strike;
                    rightShoulder.x -= 58 * strike;
                    rightElbow += 25 * strike;
                    reach = .15f * strike;
                    toolTurn = -torsoTurn;
                    toolPitch = -5;
                    break;
                case ItemKind.Bow:
                    torsoTurn -= 14 * strike;
                    leftShoulder = new Vector3(-64 + 55 * strike, 0, -66 + 60 * strike);
                    rightShoulder = new Vector3(-58 + 26 * strike, 0, 24);
                    leftElbow = -75 + 44 * strike;
                    rightElbow = -10;
                    reach = .11f * (1 - strike);
                    toolPitch += 24 * strike - 22 * followThrough;
                    toolTurn = -torsoTurn;
                    break;
                case ItemKind.Bomb:
                    torsoPitch += 19 * strike;
                    torsoTurn += 27 * strike - 22 * followThrough;
                    rightShoulder += new Vector3(-70 * strike + 29 * followThrough, 0, 31 * strike);
                    rightElbow += 24 * strike;
                    reach = .13f * strike;
                    break;
            }

            float bob = .104f * Mathf.Abs(Mathf.Sin(walkPhase)) * walkWeight;
            float breathing = .028f * Mathf.Sin(clock * 2.9f) * (1 - walkWeight);
            Vector3 waist = Vector3.back * .51f;
            Quaternion torso = Quaternion.Euler(torsoPitch, -15 * movement.x * walkWeight + gait * 5, torsoTurn);
            figure.Upper.SetLocalPositionAndRotation(waist - torso * waist + new Vector3(gait * .065f, 0, -bob - breathing), torso);
            figure.LeftArm.localRotation = Quaternion.Euler(leftShoulder);
            figure.RightArm.SetLocalPositionAndRotation(new Vector3(.33f, .04f + reach, -.72f), Quaternion.Euler(rightShoulder));
            figure.LeftElbow.localRotation = Quaternion.Euler(leftElbow, 0, 0);
            figure.RightElbow.localRotation = Quaternion.Euler(rightElbow, 0, 0);
            figure.Grip.localRotation = Quaternion.Inverse(figure.RightArm.localRotation * figure.RightElbow.localRotation)
                * Quaternion.Euler(toolPitch, 0, toolTurn);
        }

        public void DrawEnemy(Enemy enemy, float clock)
        {
            if (used == enemies.Count) enemies.Add(CreateEnemy());
            Figure figure = enemies[used++];
            figure.Root.gameObject.SetActive(true);
            float scale = enemy.Radius / .44f;
            float phase = clock * (enemy.Boss ? 5 : 8) + enemy.Position.x * 2 + enemy.Position.y * 3;
            float step = Mathf.Sin(phase);
            float flinch = Mathf.Clamp01(enemy.Flash / .14f);
            figure.Root.localPosition = new Vector3(enemy.Position.x, enemy.Position.y, 0);
            figure.Root.localRotation = Quaternion.Euler(0, 0, enemy.Angle);
            figure.Root.localScale = Vector3.one * scale;
            figure.Upper.localPosition = new Vector3(.025f * step, -.10f * flinch, -.024f * Mathf.Abs(step));
            figure.Upper.localRotation = Quaternion.Euler(10 - 17 * flinch, -step * 3, step * 5);
            figure.LeftLeg.localRotation = Quaternion.Euler(step * 26, 0, -11);
            figure.RightLeg.localRotation = Quaternion.Euler(-step * 23, 0, 9);
            figure.LeftArm.localRotation = Quaternion.Euler(12 - step * 24, 0, -14);
            figure.RightArm.localRotation = Quaternion.Euler(27 + step * 22, 0, 13);
            figure.Shell.sharedMaterial = enemy.Flash > 0 ? hotShell : infected;
            figure.Hat.gameObject.SetActive(enemy.Boss);
        }

        public void DrawCompanion(ItemKind kind, Vector2 point, Vector2 aim, float clock)
        {
            bool drone = kind == ItemKind.Carl;
            Transform companion = drone ? carl : woody;
            if (drone) carlDrawn = true;
            else woodyDrawn = true;
            companion.gameObject.SetActive(true);
            companion.localPosition = new Vector3(point.x, point.y, -.055f * Mathf.Sin(clock * 4 + point.x));
            companion.localRotation = Quaternion.Euler(0, 0, RunState.Angle(aim));
        }

        internal Transform CreateIconModel(ItemKind kind)
        {
            Transform source = kind switch
            {
                ItemKind.Pickaxe => operatorFigure.Pickaxe,
                ItemKind.Spear => operatorFigure.Spear,
                ItemKind.Bow => operatorFigure.Bow,
                ItemKind.Bomb => operatorFigure.Bomb,
                ItemKind.Hat => operatorFigure.Hat,
                ItemKind.Carl => carl,
                ItemKind.Woody => woody,
                _ => null
            };
            if (source != null)
            {
                var model = Object.Instantiate(source.gameObject).transform;
                model.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.localScale = source.localScale;
                if (kind is ItemKind.Pickaxe or ItemKind.Spear or ItemKind.Bow)
                    model.localRotation = Quaternion.Euler(0, 0, -33);
                if (kind is ItemKind.Carl or ItemKind.Woody)
                    model.localRotation = Quaternion.Euler(0, 0, 152);
                model.gameObject.SetActive(true);
                return model;
            }

            if (kind is not (ItemKind.Shield or ItemKind.HealthPotion or ItemKind.PetPotion)) return null;
            var icon = new GameObject(kind.ToString()).transform;
            if (kind == ItemKind.Shield)
            {
                Part(icon, "Edge frame", steel, 0, 0, 0, .76f, .97f, .14f);
                Part(icon, "Ceramic shell", ivory, 0, .015f, -.075f, .65f, .86f, .12f);
                Part(icon, "Reinforced face", suit, 0, .015f, -.139f, .46f, .63f, .08f);
                Beam(icon, "Left chevron", amber, new Vector3(-.16f, .10f, -.193f), new Vector3(0, -.09f, -.193f), .07f);
                Beam(icon, "Right chevron", amber, new Vector3(0, -.09f, -.193f), new Vector3(.16f, .10f, -.193f), .07f);
            }
            else
            {
                bool health = kind == ItemKind.HealthPotion;
                Material accent = health ? eyes : bossPlate;
                Part(icon, "Canister", steel, 0, -.045f, 0, .47f, .72f, .31f, shape: rounded);
                Part(icon, "Canister cap", ivory, 0, .345f, 0, .29f, .17f, .28f, shape: rounded);
                Part(icon, "Label", accent, 0, -.045f, -.153f, .38f, .42f, .044f);
                if (health)
                {
                    Part(icon, "Cross upright", ivory, 0, -.045f, -.182f, .065f, .26f, .036f);
                    Part(icon, "Cross bar", ivory, 0, -.045f, -.182f, .25f, .065f, .036f);
                }
                else
                {
                    var symbol = Part(icon, "Companion marker", ivory, 0, -.045f, -.182f, .14f, .14f, .036f);
                    symbol.transform.localRotation = Quaternion.Euler(0, 0, 45);
                }
            }
            icon.localRotation = Quaternion.Euler(0, 0, -19);
            return icon;
        }

        public void Dispose()
        {
            if (root != null) Release(root.gameObject);
            Release(bevel);
            Release(rounded);
            Release(shell);
            foreach (var material in new[] { ivory, steel, suit, visor, amber, infected, fabric, eyes, hotShell, shellEdge, bossPlate, timber, foliage })
                Release(material);
        }

        private static void Release(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private Transform CreateCompanion(bool drone)
        {
            Transform companion = Joint(root, drone ? "Carl drone" : "Woody scout");
            if (drone)
            {
                Part(companion, "Core hull", steel, 0, 0, -.47f, .75f, .59f, .32f, shape: rounded);
                Part(companion, "Backplate", ivory, 0, -.095f, -.598f, .46f, .25f, .073f, shape: bevel);
                Part(companion, "Optic housing", suit, 0, .235f, -.46f, .34f, .175f, .21f, shape: bevel);
                Part(companion, "Optic", visor, 0, .313f, -.48f, .226f, .102f, .131f, shape: rounded);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(companion, "Thruster", suit, side * .317f, -.04f, -.39f, .198f, .398f, .22f, shape: bevel);
                    Part(companion, "Thruster marker", amber, side * .324f, -.194f, -.463f, .096f, .10f, .069f);
                }
            }
            else
            {
                Part(companion, "Trunk", timber, 0, -.04f, -.42f, .43f, .43f, .53f, shape: shell);
                Part(companion, "Crown", foliage, -.115f, -.02f, -.703f, .44f, .43f, .30f, -11, shell);
                Part(companion, "Crown", foliage, .147f, -.077f, -.647f, .36f, .39f, .25f, 16, shell);
                for (int side = -1; side <= 1; side += 2)
                {
                    Part(companion, "Root", timber, side * .20f, .07f, -.165f, .223f, .371f, .19f, 17, rounded);
                    Part(companion, "Glow bud", amber, side * .11f, .13f, -.469f, .096f, .12f, .09f, shape: rounded);
                }
            }
            companion.gameObject.SetActive(false);
            return companion;
        }

        private Figure CreateOperator()
        {
            Figure figure = CreateFigure("Suited operator");
            Part(figure.Upper, "Suit torso", ivory, 0, .01f, -.77f, .65f, .43f, .57f, shape: rounded);
            Part(figure.Upper, "Pressure bellows", suit, 0, .015f, -.54f, .44f, .35f, .20f, shape: rounded);
            Part(figure.Upper, "Chest harness", steel, 0, .206f, -.76f, .46f, .10f, .25f);
            Part(figure.Upper, "Neck seal", suit, 0, .063f, -.995f, .38f, .37f, .16f, shape: rounded);
            Part(figure.Upper, "Oxygen pack", suit, 0, -.245f, -.79f, .53f, .32f, .52f);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(figure.Upper, "Reserve cylinder", steel, side * .14f, -.34f, -.83f, .20f, .20f, .52f, shape: rounded);
                Part(figure.Upper, "Reserve cap", amber, side * .14f, -.34f, -1.075f, .16f, .17f, .105f, shape: rounded);
            }
            Part(figure.Upper, "Pressure helmet", ivory, 0, .077f, -1.155f, .52f, .49f, .48f, shape: rounded);
            Part(figure.Upper, "Faceplate gasket", suit, 0, .247f, -1.174f, .423f, .212f, .283f, -9, rounded);
            Part(figure.Upper, "Polarized faceplate", visor, 0, .279f, -1.186f, .366f, .19f, .229f, -9, rounded);
            Part(figure.Upper, "Faceplate rim", steel, 0, .278f, -1.291f, .325f, .11f, .045f, -9, rounded);
            Part(figure.Upper, "Comms cover", steel, -.226f, .069f, -1.169f, .104f, .183f, .195f, shape: rounded);
            figure.Hat = Part(figure.Upper, "Helmet crest", amber, 0, .058f, -1.387f, .26f, .28f, .095f, shape: rounded).transform;
            figure.LeftElbow = OperatorArm(figure.LeftArm);
            figure.RightElbow = OperatorArm(figure.RightArm);
            figure.LeftKnee = OperatorLeg(figure.LeftLeg);
            figure.RightKnee = OperatorLeg(figure.RightLeg);
            Transform grip = figure.Grip = Joint(figure.RightElbow, "Tool grip", new Vector3(.025f, .087f, .141f));
            figure.Pickaxe = Joint(grip, "Pickaxe");
            Beam(figure.Pickaxe, "Handle", steel, new Vector3(0, -.16f, .06f), new Vector3(0, .49f, -.20f), .105f);
            Beam(figure.Pickaxe, "Pick head", ivory, new Vector3(-.23f, .47f, -.23f), new Vector3(.20f, .51f, -.16f), .115f);
            figure.Spear = Joint(grip, "Spear");
            Beam(figure.Spear, "Shaft", steel, new Vector3(0, -.31f, .14f), new Vector3(0, .63f, -.12f), .08f);
            Part(figure.Spear, "Blade", ivory, 0, .60f, -.12f, .14f, .29f, .10f, 45);
            figure.Bow = Joint(grip, "Bow");
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 gripEnd = new(side * .09f, .45f, 0);
                Vector3 shoulder = new(side * .22f, .40f, 0);
                Vector3 elbow = new(side * .34f, .29f, 0);
                Vector3 tip = new(side * .42f, .32f, 0);
                Beam(figure.Bow, "Riser", steel, new Vector3(0, .46f, 0), gripEnd, .089f);
                Beam(figure.Bow, "Limb shoulder", steel, gripEnd, shoulder, .072f);
                Beam(figure.Bow, "Flex limb", ivory, shoulder, elbow, .056f);
                Beam(figure.Bow, "Recurve tip", ivory, elbow, tip, .048f);
            }
            Beam(figure.Bow, "String", ivory, new Vector3(-.42f, .32f, 0), new Vector3(.42f, .32f, 0), .022f);
            figure.Bomb = Joint(grip, "Bomb");
            Part(figure.Bomb, "Canister", steel, 0, .20f, -.04f, .29f, .30f, .28f, shape: rounded);
            Part(figure.Bomb, "Arming cap", amber, 0, .22f, -.16f, .16f, .15f, .12f, shape: rounded);
            return figure;
        }

        private Transform OperatorArm(Transform arm)
        {
            Part(arm, "Shoulder pauldron", ivory, 0, .006f, -.06f, .30f, .29f, .31f, shape: rounded);
            Transform elbow = Joint(arm, "Elbow pivot", new Vector3(0, .043f, .079f));
            Part(elbow, "Elbow seal", suit, 0, 0, 0, .20f, .20f, .20f, shape: rounded);
            Part(elbow, "Forearm plating", ivory, 0, .049f, .081f, .206f, .223f, .235f, -12, rounded);
            Part(elbow, "Arm marker", amber, 0, .142f, .068f, .135f, .049f, .103f, -12, rounded);
            Part(elbow, "Glove", suit, 0, .094f, .176f, .154f, .169f, .147f, shape: rounded);
            return elbow;
        }

        private Transform OperatorLeg(Transform leg)
        {
            Part(leg, "Thigh plate", ivory, 0, -.012f, .105f, .244f, .268f, .30f, shape: rounded);
            Transform knee = Joint(leg, "Knee pivot", new Vector3(0, .02f, .235f));
            Part(knee, "Knee seal", suit, 0, 0, 0, .203f, .23f, .167f, shape: rounded);
            Part(knee, "Shin plate", ivory, 0, .032f, .091f, .205f, .241f, .206f, shape: rounded);
            Part(knee, "Boot sole", suit, 0, .065f, .194f, .235f, .33f, .113f);
            Part(knee, "Armored boot cap", steel, 0, .133f, .153f, .212f, .18f, .104f, shape: rounded);
            return knee;
        }

        private Figure CreateEnemy()
        {
            Figure figure = CreateFigure("Infected");
            Part(figure.Upper, "Hunched body", fabric, 0, -.02f, -.51f, .74f, .68f, .56f, -17, rounded);
            figure.Shell = Part(figure.Upper, "Armored back", infected, 0, -.105f, -.67f, .82f, .75f, .53f, -17, shell);
            Part(figure.Upper, "Fractured spine plate", shellEdge, -.085f, -.095f, -.87f, .29f, .58f, .144f, -15, bevel);
            Part(figure.Upper, "Shell seam", fabric, .096f, -.16f, -.881f, .071f, .41f, .086f, -20, bevel);
            Part(figure.Upper, "Dropped head", infected, -.055f, .315f, -.576f, .475f, .41f, .39f, 18, rounded);
            Part(figure.Upper, "Brow guard", shellEdge, -.056f, .39f, -.728f, .40f, .255f, .12f, 14, rounded);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(figure.Upper, "Eye socket", fabric, -.055f + side * .151f, .471f, -.626f, .19f, .137f, .167f, 14, rounded);
                Part(figure.Upper, "Eye glow", eyes, -.055f + side * .16f, .502f, -.654f, .111f, .102f, .101f, 14, rounded);
                Part(figure.Upper, "Exposed shoulder growth", eyes, side * .377f, -.146f, -.747f, .135f, .139f, .105f, shape: rounded);
            }
            InfectedLimb(figure.LeftArm, true, -.036f);
            InfectedLimb(figure.RightArm, true, .044f);
            InfectedLimb(figure.LeftLeg, false, -.025f);
            InfectedLimb(figure.RightLeg, false, .04f);
            figure.Hat = Joint(figure.Upper, "Boss plating");
            Part(figure.Hat, "Boss mantle", bossPlate, 0, -.225f, -.794f, .98f, .67f, .30f, -15, shell);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(figure.Hat, "Crown vane", shellEdge, side * .245f, .116f, -.92f, .138f, .365f, .15f, 26, bevel);
                Part(figure.Hat, "Crown light", eyes, side * .246f, .248f, -.993f, .116f, .141f, .071f, 14, rounded);
            }
            return figure;
        }

        private void InfectedLimb(Transform joint, bool arm, float offset)
        {
            float reach = arm ? .14f : -.04f;
            Part(joint, arm ? "Arm shell" : "Leg shell", infected, offset, reach * .46f, .102f,
                .221f, .271f, arm ? .306f : .37f, 12, rounded);
            Part(joint, arm ? "Foreclaw" : "Foot", fabric, offset, reach + .043f, arm ? .26f : .35f,
                .18f, .291f, .153f, 14, rounded);
        }

        private Figure CreateFigure(string name)
        {
            Transform body = Joint(root, name);
            Transform upper = Joint(body, "Upper body");
            return new Figure
            {
                Root = body,
                Upper = upper,
                LeftLeg = Joint(body, "Left leg pivot", new Vector3(-.17f, -.02f, -.49f)),
                RightLeg = Joint(body, "Right leg pivot", new Vector3(.17f, -.02f, -.49f)),
                LeftArm = Joint(upper, "Left shoulder pivot", new Vector3(-.33f, .015f, -.70f)),
                RightArm = Joint(upper, "Right shoulder pivot", new Vector3(.33f, .04f, -.72f))
            };
        }

        private static Transform Joint(Transform parent, string name, Vector3 position = default)
        {
            Transform joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            return joint;
        }

        private MeshRenderer Part(Transform parent, string name, Material material,
            float x, float y, float z, float width, float depth, float height, float pitch = 0, Mesh shape = null)
        {
            Transform part = Joint(parent, name, new Vector3(x, y, z));
            part.localScale = new Vector3(width, depth, height);
            part.localRotation = Quaternion.Euler(pitch, 0, 0);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = shape != null ? shape : bevel;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (width < .18f || depth < .18f || height < .14f)
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private void Beam(Transform parent, string name, Material material, Vector3 start, Vector3 end, float width)
        {
            Vector3 middle = (start + end) * .5f;
            var part = Part(parent, name, material, middle.x, middle.y, middle.z, width, (end - start).magnitude, width);
            part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
        }

        private static Material Finish(Shader shader, string name, Color color, float metallic, float gloss,
            Color emission = default)
        {
            var material = new Material(shader) { name = name, color = color, enableInstancing = true };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", gloss);
            if (emission.maxColorComponent > 0)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            return material;
        }

        private static Mesh CreateRoundedMesh(string name, float roundness)
        {
            const int divisions = 4;
            const int stride = divisions + 1;
            var vertices = new List<Vector3>(6 * stride * stride);
            var normals = new List<Vector3>(vertices.Capacity);
            var triangles = new List<int>(6 * divisions * divisions * 6);
            Vector3[] faces = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 face in faces)
            {
                Vector3 tangent = Mathf.Abs(face.z) > .5f ? Vector3.right : Vector3.forward;
                Vector3 bitangent = Vector3.Cross(face, tangent);
                int offset = vertices.Count;
                for (int y = 0; y <= divisions; y++)
                for (int x = 0; x <= divisions; x++)
                {
                    Vector3 cube = face + tangent * (x * (2f / divisions) - 1) + bitangent * (y * (2f / divisions) - 1);
                    Vector3 sphere = cube.normalized;
                    vertices.Add(Vector3.Lerp(cube, sphere, roundness) * .5f);
                    normals.Add(sphere);
                    if (x == divisions || y == divisions) continue;
                    int first = offset + y * stride + x;
                    triangles.Add(first);
                    triangles.Add(first + 1);
                    triangles.Add(first + stride + 1);
                    triangles.Add(first);
                    triangles.Add(first + stride + 1);
                    triangles.Add(first + stride);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateBeveledBox()
        {
            var vertices = new List<Vector3>(96);
            var normals = new List<Vector3>(96);
            var triangles = new List<int>(324);
            var bands = new[] { -.5f, -.32f, .32f, .5f };
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 normal in directions)
            {
                Vector3 tangent = Mathf.Abs(normal.z) > .5f ? Vector3.right : Vector3.forward;
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                int offset = vertices.Count;
                foreach (float y in bands)
                foreach (float x in bands)
                {
                    Vector3 cube = normal * .5f + tangent * x + bitangent * y;
                    Vector3 inside = new(Mathf.Clamp(cube.x, -.32f, .32f), Mathf.Clamp(cube.y, -.32f, .32f),
                        Mathf.Clamp(cube.z, -.32f, .32f));
                    Vector3 bevelNormal = (cube - inside).normalized;
                    vertices.Add(inside + bevelNormal * .18f);
                    normals.Add(bevelNormal);
                }
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    int corner = offset + y * 4 + x;
                    triangles.Add(corner);
                    triangles.Add(corner + 1);
                    triangles.Add(corner + 5);
                    triangles.Add(corner);
                    triangles.Add(corner + 5);
                    triangles.Add(corner + 4);
                }
            }
            var mesh = new Mesh { name = "Rounded armor box" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
