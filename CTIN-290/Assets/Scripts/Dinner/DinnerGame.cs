using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Dinner
{
    /// Bootstraps the whole prototype at runtime around the imported kitchen and runs the evening:
    /// Intro -> a few rounds of Cooking (stove vs. table), each ending in a rating -> Ending.
    public class DinnerGame : MonoBehaviour
    {
        public enum Phase { Intro, Cooking, Rating, Ending }

        [Header("Scene hooks")]
        public Transform KitchenRoot;                 // found by name if left empty
        public float WorldScale = 1.42f;              // the kitchen is modelled ~40% larger than life
        public float FloorY = 0.26f;

        [Header("Anchors — refined from the kitchen's materials at startup, override if needed")]
        public Vector3 PanCenter = new Vector3(-1.11f, 1.645f, -1.33f);
        public Vector3 TableCenter = new Vector3(-0.95f, 1.45f, 0.74f);
        public Vector3 TableSize = new Vector3(2.05f, 0.37f, 2.18f);
        public float SeatHeight = 0.95f;
        public string[] HideMaterials = { "Material.013", "Material.014", "Material.016", "Material.002", "Material.001" }; // the model's own pan
        public string ChairMaterial = "Tauari wood.001";
        public string RoomMaterial = "Material.026";                                 // the cutaway room box
        [HideInInspector] public List<Vector3> ChairSeats = new List<Vector3>();   // filled by KitchenLayout, sorted by x
        [HideInInspector] public Bounds RoomBounds;
        [HideInInspector] public Color WallColor = new Color(0.8f, 0.68f, 0.58f);

        [Header("Clock")]
        public int StartHour = 19;
        public float CookingMinutesPerSecond = 0.6f;

        [Header("Rounds")]
        public int SteaksToCook = 3;
        public float RatingSeconds = 5f;

        [Serializable]
        public class PropSet
        {
            [Header("Ultimate Food Pack")]
            public GameObject Steak, Pan, Plate, Fork, Knife, Soda, Fries, Broccoli, Bread, Bottle1, Bottle2, Ketchup, Jar, SquarePlate, Lettuce, Tomato, TomatoSlice, Pepper, Mushroom, Egg;
            [Header("LowPoly Kitchen Starter Pack")]
            public GameObject Spatula, CuttingBoard, KitchenKnife, Milk;
        }

        [Header("Props — filled from Assets/Models in the Editor when left empty")]
        public PropSet Pack = new PropSet();
        public Vector3 CounterSpot = new Vector3(0.75f, 1.60f, -0.80f);    // cutting-board cluster on the counter
        public Vector3 CounterSpot2 = new Vector3(0.18f, 1.60f, -1.70f);   // milk and eggs back by the wall

        public Phase Current { get; private set; }
        public int Round { get; private set; }
        public List<SteakReport> Reports { get; } = new List<SteakReport>();

        AttentionCamera _cam;
        DinnerHUD _hud;
        SteakCooking _steak;
        Conversation _talk;
        FriendCharacter[] _friends;
        Light _sun, _tableLamp, _hoodLamp;
        AudioSource _uiAudio;
        AudioClip _chime;
        float _clockMinutes;
        bool _forcedServe;
        Transform[] _plates = new Transform[0];   // one per friend, filled by DressTable

        void Start()
        {
            Setup();
            StartCoroutine(Run());
        }

        // ------------------------------------------------------------------ build the world
        void Setup()
        {
            if (KitchenRoot == null)
            {
                var k = GameObject.Find("Isometric Kitchen Scene");
                if (k != null) KitchenRoot = k.transform;
            }
            if (KitchenRoot != null) KitchenLayout.Apply(this);
#if UNITY_EDITOR
            AutoFillPack();
#endif
            BuildBackdrop();

            // camera
            var cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" }.GetComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            _cam = cam.GetComponent<AttentionCamera>();
            if (_cam == null) _cam = cam.gameObject.AddComponent<AttentionCamera>();
            ComputePoses();
            _cam.Set(Attention.Intro, true);

            // light: warm early evening, dims as the clock runs
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) _sun = l;
            if (_sun == null) { _sun = new GameObject("Evening Sun").AddComponent<Light>(); _sun.type = LightType.Directional; _sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f); }
            _sun.shadows = LightShadows.Soft;
            _tableLamp = MakeLamp("TableLamp", TableCenter + Vector3.up * 1.6f * WorldScale * 0.8f, new Color(1f, 0.82f, 0.6f), 1.2f, 6f);
            _hoodLamp = MakeLamp("HoodLamp", PanCenter + new Vector3(0f, 1.0f, 0.1f), new Color(1f, 0.95f, 0.85f), 1.4f, 3f);

            // the pan, on the stove (steaks arrive when each round starts)
            _steak = new GameObject("Steaks").AddComponent<SteakCooking>();
            _steak.Build(PanCenter, new Vector3(0.35f, 0f, 1f), WorldScale * 0.95f, Pack.Pan, Pack.Steak, Pack.Spatula);
            _steak.OnPop += OnPop;
            _steak.OnSmokeStarted += OnSmoke;

            // the friends: two on the kitchen's own chairs along the far side, one at the end of the table
            _talk = new GameObject("Conversation").AddComponent<Conversation>();
            _talk.transform.position = TableCenter;
            _friends = new FriendCharacter[3];
            float farZ = TableCenter.z + TableSize.z * 0.5f - 0.06f;
            var seats = new List<(Vector3 pos, Vector3 facing)>();
            if (ChairSeats.Count >= 2)
            {
                seats.Add((new Vector3(ChairSeats[0].x, FloorY, farZ), Vector3.back));
                seats.Add((new Vector3(ChairSeats[1].x, FloorY, farZ), Vector3.back));
                seats.Add((new Vector3(TableCenter.x - TableSize.x * 0.5f - 0.02f, FloorY, TableCenter.z + 0.15f), Vector3.right));
            }
            else
            {
                float[] xs = { -0.6f, 0f, 0.6f };
                foreach (var x in xs) seats.Add((new Vector3(TableCenter.x + x * (TableSize.x / 2.05f), FloorY, farZ), Vector3.back));
            }
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Friend_" + ConversationScript.Names[i]);
                go.transform.position = seats[i].pos;
                go.transform.localScale = Vector3.one * WorldScale;
                var f = go.AddComponent<FriendCharacter>();
                f.Build(ConversationScript.Names[i], i, ConversationScript.ShirtColors[i], (SeatHeight - FloorY) / WorldScale, seats[i].facing);
                _friends[i] = f;
                go.SetActive(false);   // they arrive during the intro
            }
            if (ChairSeats.Count >= 2) BuildChair(_friends[2]);   // the model only has two chairs
            DressTable();
            DressCounter();
            _talk.Friends = _friends;
            _talk.PlayerFocus = _cam.PanPose.Position;
            _talk.IsAttending = () => _cam.AttendingFriends;
            _talk.OnBeatEnded += OnBeatEnded;
            _talk.OnLaughter += OnLaughter;

            _hud = new GameObject("HUD").AddComponent<DinnerHUD>();
            _hud.Build();

            _uiAudio = gameObject.AddComponent<AudioSource>();
            _uiAudio.spatialBlend = 0f;
            _chime = ProceduralAudio.Chime();
        }

        /// The kitchen is an isometric cutaway, open on two sides. From inside you'd see sky, so close it
        /// with single-sided quads: invisible from the establishing shot outside, solid from the stove.
        void BuildBackdrop()
        {
            if (RoomBounds.size.x < 0.5f) return;
            var parent = new GameObject("Backdrop").transform;
            var wall = Visuals.Opaque(WallColor, 0.15f);
            var b = RoomBounds;
            var back = Visuals.Primitive(PrimitiveType.Quad, "WallBack", parent, new Vector3(b.center.x, b.center.y, b.max.z - 0.01f), new Vector3(b.size.x, b.size.y, 1f), wall);
            back.transform.rotation = Quaternion.identity;
            var side = Visuals.Primitive(PrimitiveType.Quad, "WallSide", parent, new Vector3(b.min.x + 0.01f, b.center.y, b.center.z), new Vector3(b.size.z, b.size.y, 1f), wall);
            side.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            var ceiling = Visuals.Primitive(PrimitiveType.Quad, "Ceiling", parent, new Vector3(b.center.x, b.max.y - 0.01f, b.center.z), new Vector3(b.size.x, b.size.z, 1f), Visuals.Opaque(Color.Lerp(WallColor, Color.white, 0.5f), 0.1f));
            ceiling.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        /// A plain white chair in the style of the kitchen's own, under a friend who didn't get one.
        void BuildChair(FriendCharacter f)
        {
            var white = Visuals.Opaque(new Color(0.93f, 0.92f, 0.9f), 0.3f);
            var root = new GameObject("Chair_" + f.Name).transform;
            root.SetPositionAndRotation(f.transform.position, f.transform.rotation);
            float s = WorldScale, h = SeatHeight - FloorY;
            Visuals.Primitive(PrimitiveType.Cube, "Seat", root, new Vector3(0f, h - 0.03f * s, 0f), new Vector3(0.5f * s, 0.06f * s, 0.5f * s), white);
            Visuals.Primitive(PrimitiveType.Cube, "Back", root, new Vector3(0f, h + 0.32f * s, -0.23f * s), new Vector3(0.5f * s, 0.7f * s, 0.05f * s), white);
            for (int i = 0; i < 4; i++)
                Visuals.Primitive(PrimitiveType.Cube, "Leg", root, new Vector3((i % 2 == 0 ? -0.21f : 0.21f) * s, h * 0.5f, (i < 2 ? -0.21f : 0.21f) * s), new Vector3(0.05f * s, h, 0.05f * s), white);
        }

#if UNITY_EDITOR
        /// In the Editor, any prop left unassigned is picked up from the packs by path.
        void AutoFillPack()
        {
            const string food = "Assets/Models/Ultimate_Food_Pack/Prefabs/";
            const string low = "Assets/Models/LowPolyKitchenStarterPack/Prefabs/";
            GameObject L(string path) => UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (Pack.Steak == null) Pack.Steak = L(food + "Steak.prefab");
            if (Pack.Pan == null) Pack.Pan = L(food + "FryingPan.prefab");
            if (Pack.Plate == null) Pack.Plate = L(food + "Plate2.prefab");
            if (Pack.Fork == null) Pack.Fork = L(food + "Fork.prefab");
            if (Pack.Knife == null) Pack.Knife = L(food + "Knife.prefab");
            if (Pack.Soda == null) Pack.Soda = L(food + "Soda.prefab");
            if (Pack.Fries == null) Pack.Fries = L(food + "Fries.prefab");
            if (Pack.Broccoli == null) Pack.Broccoli = L(food + "Broccoli.prefab");
            if (Pack.Bread == null) Pack.Bread = L(food + "Bread.prefab");
            if (Pack.Bottle1 == null) Pack.Bottle1 = L(food + "Bottle1.prefab");
            if (Pack.Bottle2 == null) Pack.Bottle2 = L(food + "Bottle2.prefab");
            if (Pack.Ketchup == null) Pack.Ketchup = L(food + "KetchupMustard.prefab");
            if (Pack.Jar == null) Pack.Jar = L(food + "Jar_Large.prefab");
            if (Pack.SquarePlate == null) Pack.SquarePlate = L(food + "Plate_Square.prefab");
            if (Pack.Lettuce == null) Pack.Lettuce = L(food + "Lettuce.prefab");
            if (Pack.Tomato == null) Pack.Tomato = L(food + "Tomato.prefab");
            if (Pack.TomatoSlice == null) Pack.TomatoSlice = L(food + "Tomato_Slice.prefab");
            if (Pack.Pepper == null) Pack.Pepper = L(food + "Pepper_Red.prefab");
            if (Pack.Mushroom == null) Pack.Mushroom = L(food + "Mushroom.prefab");
            if (Pack.Egg == null) Pack.Egg = L(food + "Egg_Whole.prefab");
            if (Pack.Spatula == null) Pack.Spatula = L(low + "Spatula.prefab");
            if (Pack.CuttingBoard == null) Pack.CuttingBoard = L(low + "CuttingBoard.prefab");
            if (Pack.KitchenKnife == null) Pack.KitchenKnife = L(low + "KitchenKnife.prefab");
            if (Pack.Milk == null) Pack.Milk = L(low + "MilkCarton.prefab");
        }
#endif

        GameObject Place(GameObject prefab, Transform parent, Vector3 pos, float yaw, float size, Props.Fit fit = Props.Fit.Largest)
            => Props.Spawn(prefab, parent, pos, yaw, size, fit);

        /// A place for everyone (yours stays empty all night) and the middle of the table.
        void DressTable()
        {
            var parent = new GameObject("TableSetting").transform;
            float top = TableCenter.y + 0.004f;
            _plates = new Transform[_friends.Length];
            for (int i = 0; i < _friends.Length; i++)
            {
                var f = _friends[i].transform;
                bool endSeat = i == 2 && ChairSeats.Count >= 2;
                var spot = f.position + f.forward * (endSeat ? 0.55f : 0.62f) + (endSeat ? f.right * 0.14f : Vector3.zero);
                spot.y = top;
                float yaw = f.eulerAngles.y;
                var side = Quaternion.Euler(0f, yaw, 0f);
                var plate = Place(Pack.Plate, parent, spot, yaw, 0.30f);
                _plates[i] = plate != null ? plate.transform : null;
                Place(Pack.Fork, parent, spot + side * new Vector3(-0.21f, 0f, 0f), yaw, 0.22f);
                Place(Pack.Knife, parent, spot + side * new Vector3(0.21f, 0f, 0f), yaw, 0.22f);
                Place(Pack.Soda, parent, spot + side * new Vector3(0.27f, 0f, -0.15f), 0f, 0.085f);
            }
            var mine = new Vector3(TableCenter.x + 0.1f, top, TableCenter.z - TableSize.z * 0.5f + 0.40f);
            Place(Pack.Plate, parent, mine, 0f, 0.30f);
            Place(Pack.Fork, parent, mine + new Vector3(-0.21f, 0f, 0f), 0f, 0.22f);
            Place(Pack.Knife, parent, mine + new Vector3(0.21f, 0f, 0f), 0f, 0.22f);

            Place(Pack.Bread, parent, new Vector3(TableCenter.x - 0.12f, top, TableCenter.z + 0.02f), 20f, 0.26f);
            Place(Pack.Bottle1, parent, new Vector3(TableCenter.x - 0.5f, top, TableCenter.z + 0.36f), 0f, 0.10f);
            Place(Pack.Bottle2, parent, new Vector3(TableCenter.x - 0.63f, top, TableCenter.z + 0.17f), 0f, 0.10f);
            Place(Pack.Ketchup, parent, new Vector3(TableCenter.x + 0.36f, top, TableCenter.z + 0.4f), -30f, 0.20f);
            Place(Pack.Jar, parent, new Vector3(TableCenter.x + 0.42f, top, TableCenter.z - 0.08f), 0f, 0.12f);
            var salad = Place(Pack.SquarePlate, parent, new Vector3(TableCenter.x - 0.15f, top, TableCenter.z + 0.56f), 15f, 0.26f);
            if (salad != null)
            {
                float saladTop = Props.WorldBounds(salad).max.y;
                var c = new Vector3(TableCenter.x - 0.15f, saladTop, TableCenter.z + 0.56f);
                Place(Pack.Lettuce, parent, c + new Vector3(-0.03f, 0f, 0.0f), 0f, 0.16f);
                Place(Pack.TomatoSlice, parent, c + new Vector3(0.07f, 0.004f, 0.05f), 30f, 0.09f);
                Place(Pack.TomatoSlice, parent, c + new Vector3(0.06f, 0.004f, -0.05f), -40f, 0.09f);
            }
        }

        /// Prep clutter on the counter: what the chef was working with before the guests arrived.
        void DressCounter()
        {
            var parent = new GameObject("CounterProps").transform;
            var board = Place(Pack.CuttingBoard, parent, CounterSpot, 15f, 0.42f);
            float boardTop = board != null ? Props.WorldBounds(board).max.y : CounterSpot.y;
            var onBoard = new Vector3(0f, boardTop - CounterSpot.y + 0.002f, 0f);
            Place(Pack.Tomato, parent, CounterSpot + onBoard + new Vector3(-0.08f, 0f, 0.03f), 0f, 0.09f);
            Place(Pack.TomatoSlice, parent, CounterSpot + onBoard + new Vector3(0.06f, 0f, -0.02f), 40f, 0.09f);
            Place(Pack.TomatoSlice, parent, CounterSpot + onBoard + new Vector3(0.11f, 0f, 0.06f), -20f, 0.09f);
            Place(Pack.KitchenKnife, parent, CounterSpot + onBoard + new Vector3(0.0f, 0f, 0.15f), 100f, 0.25f);
            Place(Pack.Pepper, parent, CounterSpot + new Vector3(-0.33f, 0f, -0.05f), 0f, 0.10f);
            Place(Pack.Mushroom, parent, CounterSpot + new Vector3(-0.27f, 0f, 0.14f), 0f, 0.06f);
            Place(Pack.Mushroom, parent, CounterSpot + new Vector3(-0.2f, 0f, 0.19f), 60f, 0.055f);
            Place(Pack.Broccoli, parent, CounterSpot + new Vector3(0.34f, 0f, 0.1f), 0f, 0.11f);
            Place(Pack.Milk, parent, CounterSpot2, 10f, 0.10f);
            for (int i = 0; i < 3; i++) Place(Pack.Egg, parent, CounterSpot2 + new Vector3(0.15f + i * 0.07f, 0f, (i % 2) * 0.03f), 0f, 0.06f);
        }

        /// The round's steaks go onto one friend's plate, with sides. Yours stays empty.
        void PlateRound(int round)
        {
            var steaks = _steak.TakeSteaks();
            int who = (round - 1) % Mathf.Max(1, _plates.Length);
            var plate = _plates.Length > 0 ? _plates[who] : null;
            if (plate == null) { foreach (var s in steaks) Destroy(s.gameObject); return; }
            var b = Props.WorldBounds(plate.gameObject);
            var center = new Vector3(b.center.x, b.max.y + 0.002f, b.center.z);
            float yaw = plate.eulerAngles.y;
            var side = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < steaks.Count; i++)
            {
                var s = steaks[i];
                float half = Props.WorldBounds(s.gameObject).size.y * 0.5f;
                s.rotation = Quaternion.Euler(0f, yaw + 70f + i * 14f, s.eulerAngles.z);   // keep whichever face was up
                s.position = center + side * new Vector3(-0.03f + i * 0.02f, half, 0.03f - i * 0.06f);
                s.SetParent(plate, true);
            }
            Place(Pack.Fries, plate, center + side * new Vector3(0.09f, 0f, -0.04f), yaw + 20f, 0.13f);
            Place(Pack.Broccoli, plate, center + side * new Vector3(-0.09f, 0f, -0.07f), yaw, 0.08f);
        }

        Light MakeLamp(string name, Vector3 pos, Color color, float intensity, float range)
        {
            var l = new GameObject(name).AddComponent<Light>();
            l.transform.position = pos;
            l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        void ComputePoses()
        {
            float eye = FloorY + 1.62f * WorldScale;
            var standing = new Vector3(PanCenter.x + 0.1f, eye, PanCenter.z + 0.78f);
            var friendsFocus = new Vector3(TableCenter.x, FloorY + 1.25f * WorldScale, TableCenter.z + 0.45f);   // chest height across the table
            _cam.PanPose = Pose(standing, PanCenter + new Vector3(0f, 0f, 0.02f), 50f);
            _cam.FriendsPose = Pose(standing, friendsFocus, 60f);
            var intro = new Vector3(TableCenter.x - 3.7f, FloorY + 3.4f, TableCenter.z + 3.2f);
            _cam.IntroPose = Pose(intro, (TableCenter + PanCenter) * 0.5f + Vector3.up * 0.2f, 34f);
        }

        static CamPose Pose(Vector3 from, Vector3 lookAt, float fov)
            => new CamPose(from, Quaternion.LookRotation(lookAt - from, Vector3.up).eulerAngles, fov);

        // ------------------------------------------------------------------ the evening
        IEnumerator Run()
        {
            Current = Phase.Intro;
            _hud.SetPrompts("");
            _hud.ShowCard("You Had to Be There", "Friday. You've been practising this steak.", 4f);
            yield return new WaitForSeconds(4.2f);

            foreach (var f in _friends) f.gameObject.SetActive(true);
            _hud.ShowCard("They're here.", "The pan is already hot.", 3f);
            yield return new WaitForSeconds(3.2f);

            _cam.Set(Attention.Pan, instant: true);   // a cut: from outside the house to behind the stove
            _talk.StartCookingTalk(ConversationScript.CookingBeats());
            _hud.ShowCard("", "<b>H</b> heat  ·  <b>F</b> flip  ·  <b>S</b> salt  ·  <b>B</b> butter  ·  <b>E</b> plate        <b>Space</b> — look back at them", 6f);

            for (Round = 1; Round <= SteaksToCook; Round++)
            {
                Current = Phase.Cooking;
                _forcedServe = false;
                _steak.NewSteaks();
                _steak.SetHeat(true);
                if (Round > 1) _hud.ShowCard("Steak " + Round + " of " + SteaksToCook, "", 2.5f);

                while (!_steak.Served)
                {
                    if (_steak.Doneness > 1.5f && !_forcedServe)
                    {
                        _forcedServe = true;
                        _talk.Interject(ConversationScript.Maya, "Chef— that one's done. Plate it.");
                        StartCoroutine(ForceServeSoon(6f));
                    }
                    yield return null;
                }

                // --- the rating; the steaks go to the table
                Current = Phase.Rating;
                var report = _steak.Report();
                Reports.Add(report);
                PlateRound(Round);
                _uiAudio.PlayOneShot(_chime, 0.5f);
                _hud.ShowRating(Round, SteaksToCook, report, RatingSeconds);
                var verdict = ConversationScript.SteakVerdict(report);
                _talk.Interject(UnityEngine.Random.Range(0, 3), verdict[UnityEngine.Random.Range(0, verdict.Length)]);
                yield return new WaitForSeconds(RatingSeconds + 0.5f);
            }

            // --- goodnight
            Current = Phase.Ending;
            _talk.Stop();
            string clock = ClockString();
            _hud.ClearSubtitle();
            _hud.ShowCard("", "Coats. Hugs at the door.", 3.5f);
            yield return new WaitForSeconds(2f);
            foreach (var f in _friends) f.gameObject.SetActive(false);
            _cam.Set(Attention.Intro, instant: true);   // and back out: the house from the street, lights low
            yield return new WaitForSeconds(2f);
            _hud.ShowEnd(Reports, _talk.Caught, _talk.Missed, clock);

            while (true)
            {
                var kb = Keyboard.current;
                if (kb != null && kb.rKey.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                yield return null;
            }
        }

        IEnumerator ForceServeSoon(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!_steak.Served) { _steak.Serve(); _hud.ShowCard("", "You pull the pan off. It's... fine. It's fine.", 3f); }
        }

        // ------------------------------------------------------------------ per-frame
        void Update()
        {
            if (_hud == null) return;
            float rate = Current == Phase.Cooking || Current == Phase.Rating ? CookingMinutesPerSecond : 0f;
            _clockMinutes += rate * Time.deltaTime;
            _hud.SetClock(ClockString(), "Friday");
            EveningLight();
            HandleInput();
            UpdateHud();
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (Current != Phase.Cooking && Current != Phase.Rating) return;

            bool atPan = _cam.Target == Attention.Pan;
            if (kb.spaceKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
                _cam.Set(atPan ? Attention.Friends : Attention.Pan);

            if (Current == Phase.Cooking)
            {
                bool action = kb.hKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame || kb.bKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame;
                if (atPan && !_cam.Turning)
                {
                    if (kb.hKey.wasPressedThisFrame) _steak.CycleHeat();
                    if (kb.fKey.wasPressedThisFrame) _steak.Flip();
                    if (kb.sKey.wasPressedThisFrame) _steak.Season();
                    if (kb.bKey.wasPressedThisFrame) _steak.AddButter();
                    if (kb.eKey.wasPressedThisFrame) _steak.Serve();
                }
                else if (action)
                {
                    _cam.Set(Attention.Pan);   // you have to turn back first; that costs you the sentence
                }
            }

            if (!atPan && kb.qKey.wasPressedThisFrame && _talk.AskWhatWasThat()) _hud.Flash(0.15f);
        }

        void UpdateHud()
        {
            bool atStove = Current == Phase.Cooking || Current == Phase.Rating;
            bool facingPan = atStove && !_cam.AttendingFriends;
            _hud.SetPan(atStove && (facingPan || _steak.Served), _steak);

            var line = _talk.CurrentLine;
            if (line != null)
            {
                bool friendLine = line.Speaker >= 0;
                bool muffled = atStove && !_cam.AttendingFriends && friendLine;
                string who = friendLine ? ConversationScript.Names[line.Speaker] : line.Speaker == ConversationScript.You ? "You" : "";
                Color c = friendLine ? Color.Lerp(ConversationScript.ShirtColors[line.Speaker], Color.white, 0.35f) : new Color(0.9f, 0.9f, 0.8f);
                string text = muffled ? Conversation.Muffle(line.Text) : line.Speaker == ConversationScript.Stage ? "<i>" + line.Text + "</i>" : line.Text;
                _hud.SetSubtitle(who, c, text, muffled);
            }
            else if (_talk.IsLaughing) _hud.SetSubtitle("", Color.white, "<i>(laughter)</i>", false);
            else _hud.ClearSubtitle();

            string prompts = "";
            if (Current == Phase.Cooking)
            {
                if (_cam.Target == Attention.Pan)
                    prompts = "<b>H</b>  heat: " + _steak.HeatWord.ToLower() + "\n<b>F</b>  flip\n<b>S</b>  salt this side\n<b>B</b>  " + (_steak.ButterIn ? "baste" : "butter") + "\n<b>E</b>  plate & serve\n\n<b>Space</b>  look back at them";
                else
                    prompts = (_talk.AskableBeat != null ? "<b>Q</b>  \"wait, what was that about?\"\n" : "") + "<b>Space</b>  back to the pan";
            }
            else if (Current == Phase.Rating) prompts = "<alpha=#70>steak " + Round + " of " + SteaksToCook;
            _hud.SetPrompts(prompts);
        }

        void OnPop()
        {
            if (Current != Phase.Cooking || !_cam.AttendingFriends) return;
            _hud.PanCue("<b>*pop*</b>  — from the pan, behind you", 2f);
            _hud.Flash(0.22f);
        }

        void OnSmoke()
        {
            if (Current != Phase.Cooking) return;
            var lines = ConversationScript.SmokeReactions;
            _talk.Interject(UnityEngine.Random.Range(0, 3), lines[UnityEngine.Random.Range(0, lines.Length)]);
        }

        void OnLaughter()
        {
            if (Current == Phase.Cooking && !_cam.AttendingFriends) _hud.PanCue("behind you: everyone laughs", 2.2f);
        }

        void OnBeatEnded(Beat beat, bool caught)
        {
            if (Current == Phase.Cooking && !caught && !_cam.AttendingFriends) _hud.PanCue("...you missed something", 2.2f);
        }

        void EveningLight()
        {
            float t = Mathf.Clamp01(_clockMinutes / 270f);   // 7:00 PM -> 11:30 PM
            _sun.intensity = Mathf.Lerp(1.05f, 0.12f, t);
            _sun.color = Color.Lerp(new Color(1f, 0.87f, 0.72f), new Color(0.45f, 0.5f, 0.78f), t);
            _tableLamp.intensity = Mathf.Lerp(1.2f, 2.6f, t);
            _hoodLamp.intensity = Current == Phase.Ending ? Mathf.Lerp(1.4f, 0.4f, t) : 1.4f;
            RenderSettings.ambientIntensity = Mathf.Lerp(1f, 0.4f, t);
        }

        string ClockString()
        {
            int total = StartHour * 60 + Mathf.FloorToInt(_clockMinutes);
            int h = (total / 60) % 24, m = total % 60;
            int h12 = h % 12 == 0 ? 12 : h % 12;
            return h12 + ":" + m.ToString("00") + (h >= 12 ? " PM" : " AM");
        }
    }

    /// Reads the imported kitchen for the few things the game needs, hides the model's own pan,
    /// and switches off the camera and area lights that came along with the FBX.
    public static class KitchenLayout
    {
        /// The model's own tray, pastries, cutlery and candle on the table — replaced by the pack's setting.
        static readonly string[] TableClutter = { "Material.007", "Material.008", "Material.030", "Material.029", "Material.022", "Material.019", "Material.018", "plate.001" };

        public static void Apply(DinnerGame g)
        {
            foreach (var c in g.KitchenRoot.GetComponentsInChildren<Camera>(true)) c.enabled = false;        // would draw over Main Camera
            foreach (var l in g.KitchenRoot.GetComponentsInChildren<Light>(true)) l.enabled = false;         // Blender area lights: baked-only in URP
            foreach (var a in g.KitchenRoot.GetComponentsInChildren<AudioListener>(true)) a.enabled = false;

            // Textured materials in this FBX are named by texture, which can change between imports;
            // the plain "Material.NNN" ones are stable. Names are matched loosely and chairs have a geometric fallback.
            Bounds stove = default, cloth = default;
            bool hasStove = false, hasCloth = false;
            var chairParts = new List<Bounds>();
            var renderers = g.KitchenRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string n = m.name;
                    if (n.StartsWith("gas stove")) { stove = hasStove ? Encapsulate(stove, r.bounds) : r.bounds; hasStove = true; }
                    else if (n.IndexOf("Tartan", StringComparison.OrdinalIgnoreCase) >= 0) { cloth = hasCloth ? Encapsulate(cloth, r.bounds) : r.bounds; hasCloth = true; }
                    else if (n == g.ChairMaterial) chairParts.Add(r.bounds);
                    else if (n == g.RoomMaterial)
                    {
                        g.RoomBounds = r.bounds;
                        if (m.HasProperty("_BaseColor")) g.WallColor = m.GetColor("_BaseColor");
                    }
                    if (Array.IndexOf(g.HideMaterials, n) >= 0 || Array.IndexOf(TableClutter, n) >= 0) r.enabled = false;
                }
            }
            if (hasStove) g.PanCenter = new Vector3(stove.center.x + stove.size.x * 0.3f, stove.max.y + 0.055f, stove.center.z + stove.size.z * 0.35f);
            if (hasCloth) { g.TableCenter = new Vector3(cloth.center.x, cloth.max.y, cloth.center.z); g.TableSize = cloth.size; }

            // --- geometry takes over for whatever the names didn't settle
            if (g.RoomBounds.size.x < 0.5f)
            {
                // the room is the smallest box-sized renderer that is still bigger than any furniture
                float best = float.MaxValue;
                foreach (var r in renderers)
                {
                    var b = r.bounds;
                    if (b.size.x < 3.5f || b.size.x > 7.5f || b.size.z < 3.5f || b.size.z > 7.5f || b.size.y < 2.5f || b.size.y > 6f) continue;
                    if (b.size.x * b.size.z < best) { best = b.size.x * b.size.z; g.RoomBounds = b; var m = r.sharedMaterial; if (m != null && m.HasProperty("_BaseColor")) g.WallColor = m.GetColor("_BaseColor"); }
                }
            }
            foreach (var r in renderers)
            {
                if (!r.enabled) continue;
                var b = r.bounds;
                // the model's own pan (and whatever is in it) on our burner — small things just above the grate
                float dx = b.center.x - g.PanCenter.x, dz = b.center.z - g.PanCenter.z;
                bool onBurner = dx * dx + dz * dz < 0.33f * 0.33f && b.center.y > g.PanCenter.y - 0.012f && b.center.y < g.PanCenter.y + 0.15f && b.size.magnitude < 0.8f;
                // the model's own table setting — small things sitting on the table top, within the table's footprint
                bool onTable = Mathf.Abs(b.center.x - g.TableCenter.x) < 0.8f && Mathf.Abs(b.center.z - g.TableCenter.z) < 0.8f
                               && b.center.y > g.TableCenter.y - 0.06f && b.center.y < g.TableCenter.y + 0.4f && b.size.x < 0.7f && b.size.z < 0.7f && b.size.y < 0.5f;
                if (onBurner || onTable) r.enabled = false;
            }
            if (chairParts.Count == 0)
            {
                // the pieces standing in the strip behind the table are the chairs
                foreach (var r in renderers)
                {
                    var b = r.bounds;
                    bool inStrip = Mathf.Abs(b.center.x - g.TableCenter.x) < 1.4f && b.center.z > g.TableCenter.z + 0.5f && b.center.z < g.TableCenter.z + 1.5f;
                    bool chairSized = b.size.x < 1.3f && b.size.z < 1.3f && b.center.y < g.FloorY + 1.6f;
                    if (inStrip && chairSized && r.enabled) chairParts.Add(b);
                }
            }

            // chairs come in as a dozen loose parts; cluster them along x to find each seat
            g.ChairSeats.Clear();
            chairParts.Sort((a, b) => a.center.x.CompareTo(b.center.x));
            Bounds cluster = default; bool open = false;
            foreach (var b in chairParts)
            {
                if (open && b.center.x - cluster.max.x < 0.15f) { cluster.Encapsulate(b); continue; }
                if (open) g.ChairSeats.Add(new Vector3(cluster.center.x, g.FloorY, cluster.center.z));
                cluster = b; open = true;
            }
            if (open) g.ChairSeats.Add(new Vector3(cluster.center.x, g.FloorY, cluster.center.z));
        }

        static Bounds Encapsulate(Bounds a, Bounds b) { a.Encapsulate(b); return a; }
    }
}
