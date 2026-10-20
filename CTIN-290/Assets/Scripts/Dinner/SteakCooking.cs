using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dinner
{
    public enum Heat { Low, Medium, High }

    /// The pan and, round by round, a pair of steaks. Cooks in real time whether or not anyone is watching.
    /// A steak has two faces: the one on the pan cooks through and builds a crust; the other one waits.
    public class SteakCooking : MonoBehaviour
    {
        // Tuning at medium heat: a face needs ~45 s on the pan to cook through and ~25 s to build a crust.
        // High heat is 1.5x / 1.6x (fast crust, easy to burn); low is 0.6x / 0.35x (safe, slow — the evening drifts).
        public float PanSideRate = 1f / 45f;
        public float UpSideRate = 1f / 320f;
        public float SearRate = 1f / 25f;
        public float PopIntervalMin = 7f, PopIntervalMax = 15f;
        public const float Target = 0.72f;

        [Header("Prop sizes (world units; the kitchen is ~1.4x life)")]
        public float PanWidth = 0.50f;
        public float SteakLength = 0.20f;
        public float SpatulaLength = 0.34f;
        public float SpatulaYaw = 180f;                                      // the pack's spatula points its blade along +z
        public Vector3 SpatulaRestOffset = new Vector3(-0.42f, 0f, 0.02f);   // the burner next door

        public event Action OnPop;
        public event Action OnSmokeStarted;
        public event Action OnServed;

        public Heat HeatLevel { get; private set; } = Heat.Medium;
        public bool HeatOn { get; private set; }
        public bool Served { get; private set; }
        public bool ButterIn { get; private set; }
        public float Baste { get; private set; }          // 0..1, spooning the butter over once it's in
        public string LastEvent { get; private set; } = "";
        public float LastEventTime { get; private set; } = -99f;
        public Vector3 PanCenter => transform.position;

        class Face { public float Done, Sear, Butter; public bool Salted; }   // Butter: 0..1, how much of it has reached this face (display only)

        class Steak
        {
            public Transform Root;
            public Material MatA, MatB, MatSide, MatFat;   // bottom face, top face, sides, fat cap
            public Face A = new Face(), B = new Face();
            public bool AIsDown = true;    // which face touches the pan
            public float FlipT = 1f;       // flip animation progress
            public float FlipFrom, FlipTo; // z-rotation in degrees
            public float RestY;            // local height when lying in the pan
            public Face Down => AIsDown ? A : B;
            public Face Up => AIsDown ? B : A;
            public float Avg => (A.Done + B.Done) * 0.5f;
        }

        Steak[] _steaks = new Steak[0];
        GameObject _steakPrefab, _spatula;
        Transform _butterPat, _rosemary;      // in the pan once the butter goes in
        float _butterInAt;
        public Transform Spatula => _spatula != null ? _spatula.transform : null;
        Vector3 _spatulaRest;
        Quaternion _spatulaRestRot;
        float _steakBaseY = 0.035f;        // local y of the pan floor
        ParticleSystem _steam, _smoke, _grease;
        AudioSource _sizzle, _oneShot;
        AudioClip _popClip, _alarmClip;
        float _nextPop, _residual, _butterOnHigh;
        bool _smoking;
        static readonly Color Raw = new Color(0.86f, 0.24f, 0.27f);
        static readonly Color Seared = new Color(0.47f, 0.25f, 0.12f);
        static readonly Color Char = new Color(0.07f, 0.05f, 0.04f);
        static readonly Color FatRaw = new Color(0.96f, 0.9f, 0.78f);
        static readonly Color FatRendered = new Color(0.8f, 0.58f, 0.3f);

        public float HeatMultiplier => HeatLevel == Heat.High ? 1.5f : HeatLevel == Heat.Low ? 0.6f : 1f;
        public float SearMultiplier => HeatLevel == Heat.High ? 1.6f : HeatLevel == Heat.Low ? 0.35f : 1f;
        public string HeatWord => HeatLevel == Heat.High ? "HIGH" : HeatLevel == Heat.Low ? "LOW" : "MEDIUM";

        public float Doneness
        {
            get { if (_steaks.Length == 0) return 0f; float s = 0f; foreach (var k in _steaks) s += k.Avg; return s / _steaks.Length; }
        }

        public float Evenness
        {
            get { if (_steaks.Length == 0) return 1f; float s = 0f; foreach (var k in _steaks) s += 1f - Mathf.Clamp01(Mathf.Abs(k.A.Done - k.B.Done) / 0.6f); return s / _steaks.Length; }
        }

        /// Average crust quality: builds to 1, then a face gone dark (past 1.15) loses points again.
        public float Crust
        {
            get { if (_steaks.Length == 0) return 0f; float s = 0f; foreach (var k in _steaks) s += CrustQuality(k.A.Sear) + CrustQuality(k.B.Sear); return s / (_steaks.Length * 2f); }
        }

        static float CrustQuality(float sear) => Mathf.Clamp01(Mathf.Clamp01(sear) - Mathf.Max(0f, sear - 1.15f) * 2f);

        public float Salt
        {
            get { if (_steaks.Length == 0) return 0f; float s = 0f; foreach (var k in _steaks) s += (k.A.Salted ? 1f : 0f) + (k.B.Salted ? 1f : 0f); return s / (_steaks.Length * 2f); }
        }

        // the faces you can see / can't see right now (both steaks move together, so the first one stands for both)
        public bool UpSalted => _steaks.Length > 0 && _steaks[0].Up.Salted;
        public bool DownSalted => _steaks.Length > 0 && _steaks[0].Down.Salted;
        public float UpSear => _steaks.Length > 0 ? _steaks[0].Up.Sear : 0f;
        public float DownSear => _steaks.Length > 0 ? _steaks[0].Down.Sear : 0f;
        public bool Smoking => _smoking;
        public bool Burnt => Doneness > 1.25f;
        public bool HasSteaks => _steaks.Length > 0;

        /// One face of the steak, as the HUD shows it. Side 0 is A (in the pan first), side 1 is B.
        public struct SideView { public float Sear, Butter; public bool Salted, OnPan; }
        public SideView Side(int i)
        {
            if (_steaks.Length == 0) return default;
            var s = _steaks[0];
            var f = i == 0 ? s.A : s.B;
            return new SideView { Sear = f.Sear, Butter = f.Butter, Salted = f.Salted, OnPan = (i == 0) == s.AIsDown };
        }

        /// Build the pan at a world position with the handle pointing along `handleDir`.
        /// Pass the pack prefabs to use them; without them the pan, steaks and spatula are primitives.
        public void Build(Vector3 panCenter, Vector3 handleDir, float scale = 1f, GameObject panPrefab = null, GameObject steakPrefab = null, GameObject spatulaPrefab = null)
        {
            transform.position = panCenter;
            transform.rotation = Quaternion.LookRotation(handleDir.sqrMagnitude > 0.001f ? handleDir : Vector3.back, Vector3.up);
            transform.localScale = Vector3.one * scale;
            _steakPrefab = steakPrefab;
            float yaw = transform.eulerAngles.y;

            if (panPrefab != null)
            {
                // the pack's pan: body width fitted to the burner, handle (+z in the model) along handleDir
                var pan = Props.Spawn(panPrefab, transform, panCenter - Vector3.up * 0.004f, yaw, PanWidth, Props.Fit.X);
                // Spawn centres the whole model, handle included; put the body (its widest mesh) on the burner instead
                var body = Props.WidestPart(pan);
                if (body != null)
                {
                    var off = panCenter - body.bounds.center;
                    pan.transform.position += new Vector3(off.x, 0f, off.z);
                }
                // where a steak actually rests: the pan's surface at its centre (low-poly pans are often solid)
                _steakBaseY = (Props.TopSurfaceAtCenter(pan) + 0.003f - transform.position.y) / scale;
            }
            else
            {
                var iron = Visuals.Opaque(new Color(0.09f, 0.09f, 0.1f), 0.55f, 0.6f);
                Visuals.Primitive(PrimitiveType.Cylinder, "Pan", transform, Vector3.zero, new Vector3(0.34f, 0.012f, 0.34f), iron);
                Visuals.Primitive(PrimitiveType.Cylinder, "PanRim", transform, new Vector3(0, 0.02f, 0), new Vector3(0.36f, 0.025f, 0.36f), iron);
                Visuals.Primitive(PrimitiveType.Cylinder, "PanFloor", transform, new Vector3(0, 0.032f, 0), new Vector3(0.325f, 0.006f, 0.325f), iron);
                Visuals.Primitive(PrimitiveType.Cube, "Handle", transform, new Vector3(0, 0.04f, 0.27f), new Vector3(0.035f, 0.02f, 0.2f), iron);
                _steakBaseY = 0.035f;
            }

            if (spatulaPrefab != null)
            {
                // resting flat on the burner next door, long axis toward the cook; slides under the steaks on a flip
                var rest = panCenter + SpatulaRestOffset;
                _spatula = Props.Spawn(spatulaPrefab, transform, rest, yaw + SpatulaYaw, SpatulaLength, Props.Fit.Largest);
                _spatulaRest = _spatula.transform.localPosition;
                _spatulaRestRot = _spatula.transform.localRotation;
            }

            var particleY = _steakBaseY + 0.02f;
            _steam = Visuals.Particles("Steam", transform, new Vector3(0, particleY, 0), new Color(1f, 1f, 1f, 0.35f), 0.05f, 0.35f, 0.9f, 0.1f, 0f);
            _smoke = Visuals.Particles("Smoke", transform, new Vector3(0, particleY + 0.01f, 0), new Color(0.35f, 0.35f, 0.35f, 0.5f), 0.12f, 0.45f, 2.6f, 0.08f, 0f);
            _grease = Visuals.Particles("Grease", transform, new Vector3(0, particleY, 0), new Color(1f, 0.92f, 0.6f, 0.9f), 0.015f, 1.4f, 0.5f, 0.08f, 0f);
            var gshape = _grease.shape; gshape.angle = 40f;
            var gmain = _grease.main; gmain.gravityModifier = 1.2f;

            _sizzle = gameObject.AddComponent<AudioSource>();
            _sizzle.clip = ProceduralAudio.Sizzle();
            _sizzle.loop = true;
            _sizzle.spatialBlend = 0.85f;
            _sizzle.minDistance = 0.4f; _sizzle.maxDistance = 6f;
            _sizzle.rolloffMode = AudioRolloffMode.Linear;
            _sizzle.volume = 0f;
            _sizzle.Play();
            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.spatialBlend = 0.7f;
            _popClip = ProceduralAudio.Pop();
            _alarmClip = ProceduralAudio.Alarm();
        }

        /// A fresh pair of steaks in the pan; butter and basting start over, the burner stays where you left it.
        public void NewSteaks()
        {
            foreach (var s in _steaks) if (s.Root != null) Destroy(s.Root.gameObject);
            int seed = UnityEngine.Random.Range(0, 100000);
            // side by side across the pan, long axes along the handle, fat caps facing opposite ways
            _steaks = new[] { MakeSteak(new Vector3(-0.075f, 0f, 0.0f), 96f, seed), MakeSteak(new Vector3(0.075f, 0f, -0.01f), 264f, seed + 1) };
            if (_butterPat != null) Destroy(_butterPat.gameObject);
            if (_rosemary != null) Destroy(_rosemary.gameObject);
            Served = false; ButterIn = false; Baste = 0f;
            _residual = 0f; _butterOnHigh = 0f; _smoking = false;
            LastEvent = ""; LastEventTime = -99f;
            _nextPop = Time.time + UnityEngine.Random.Range(PopIntervalMin, PopIntervalMax);
            _grease.Emit(12);
            _oneShot.PlayOneShot(_popClip, 0.6f);
        }

        /// Hand the steaks over for plating; the pan stays empty until NewSteaks().
        public List<Transform> TakeSteaks()
        {
            var list = new List<Transform>();
            foreach (var s in _steaks) if (s.Root != null) { s.Root.SetParent(null, true); list.Add(s.Root); }
            _steaks = new Steak[0];
            return list;
        }

        Steak MakeSteak(Vector3 localOffset, float yaw, int seed)
        {
            var s = new Steak();
            var root = Visuals.Empty("Steak", transform, Vector3.zero).transform;
            var marbling = Visuals.Marbling();
            s.MatA = Visuals.OpaqueTextured(Raw, marbling, 0.6f);
            s.MatB = Visuals.OpaqueTextured(Raw, marbling, 0.6f);
            s.MatSide = Visuals.Opaque(Raw, 0.5f);
            s.MatFat = Visuals.Opaque(FatRaw, 0.45f);
            float half;

            if (_steakPrefab != null)
            {
                // the pack's steak, with its meat mesh split so each face can brown on its own
                var model = Props.Spawn(_steakPrefab, root, root.position, 0f, SteakLength * (0.94f + 0.12f * UnityEngine.Random.value));
                var b = Props.WorldBounds(model);
                half = b.size.y * 0.5f / transform.lossyScale.x;
                model.transform.localPosition -= Vector3.up * half;   // centre the slab on the root so a flip rolls in place
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    bool fat = r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("White");
                    if (fat) { r.sharedMaterial = s.MatFat; continue; }
                    mf.sharedMesh = Props.SplitByFacing(mf.sharedMesh);
                    r.sharedMaterials = new[] { s.MatA, s.MatB, s.MatSide };
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
            else
            {
                var meat = new GameObject("Meat");
                meat.transform.SetParent(root, false);
                meat.AddComponent<MeshFilter>().sharedMesh = SteakMesh.Build(seed);
                var mr = meat.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { s.MatA, s.MatB, s.MatSide, s.MatFat };
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                half = 0.015f;
            }

            s.RestY = _steakBaseY + half;
            root.localPosition = new Vector3(localOffset.x, s.RestY, localOffset.z);
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            s.Root = root;
            return s;
        }

        public void SetHeat(bool on) { HeatOn = on && !Served; }

        public void CycleHeat()
        {
            if (Served) return;
            HeatLevel = (Heat)(((int)HeatLevel + 1) % 3);
            Event("Heat " + HeatWord.ToLower());
        }

        public void Flip()
        {
            if (Served || _steaks.Length == 0) return;
            bool early = _steaks[0].Down.Sear < 0.4f;
            foreach (var s in _steaks)
            {
                s.AIsDown = !s.AIsDown;
                s.FlipFrom = s.Root.localEulerAngles.z;
                s.FlipTo = s.FlipFrom + 180f;
                s.FlipT = 0f;
            }
            Event(early ? "Flipped — no crust on that side yet" : "Flipped");
            _grease.Emit(10);
            _oneShot.PlayOneShot(_popClip, 0.35f);
        }

        /// Salt lands on whatever face is up. Both faces need it.
        public void Season()
        {
            if (Served || _steaks.Length == 0) return;
            bool fresh = false;
            foreach (var s in _steaks) { if (!s.Up.Salted) fresh = true; s.Up.Salted = true; }
            Event(fresh ? (DownSalted ? "Salted — both sides now" : "Salted this side") : "This side's already salted");
            _steam.Emit(6);
        }

        /// First press puts the butter in; after that each press spoons it over the top face.
        public void AddButter()
        {
            if (Served || _steaks.Length == 0) return;
            if (!ButterIn)
            {
                ButterIn = true;
                Event("Butter in");
                AddButterToPan();
                _grease.Emit(16);
                _oneShot.PlayOneShot(_popClip, 0.5f);
                return;
            }
            Baste = Mathf.Min(1f, Baste + 0.34f);
            foreach (var s in _steaks) { s.Up.Sear = Mathf.Min(1.3f, s.Up.Sear + 0.06f); s.Up.Butter = Mathf.Min(1f, s.Up.Butter + 0.34f); }
            Event(Baste >= 0.99f ? "Basted" : "Basting...");
            _grease.Emit(12);
        }

        /// A pat of butter at the handle side of the pan (it melts into a puddle) and a sprig of rosemary beside the steaks.
        void AddButterToPan()
        {
            var butter = Visuals.Opaque(new Color(1f, 0.88f, 0.5f), 0.75f);
            _butterPat = Visuals.Primitive(PrimitiveType.Cube, "Butter", transform, new Vector3(0.02f, _steakBaseY + 0.012f, 0.14f), new Vector3(0.05f, 0.024f, 0.04f), butter).transform;
            _butterPat.localRotation = Quaternion.Euler(0f, 25f, 0f);
            _butterInAt = Time.time;
            _rosemary = Garnish.Rosemary(transform, transform.TransformPoint(new Vector3(0f, _steakBaseY + 0.003f, -0.13f)), transform.eulerAngles.y + 75f, 0.15f * transform.lossyScale.x);
        }

        public void Serve()
        {
            if (Served || _steaks.Length == 0) return;
            Served = true;
            HeatOn = false;
            Event("Plated");
            OnServed?.Invoke();
        }

        void Event(string what) { LastEvent = what; LastEventTime = Time.time; }

        void Update()
        {
            float dt = Time.deltaTime;
            bool cooking = HeatOn && !Served && _steaks.Length > 0;
            float heat = cooking ? HeatMultiplier * (ButterIn ? 1.1f : 1f) : 0f;
            float sear = cooking ? SearRate * SearMultiplier * (ButterIn ? 1.2f : 1f) : 0f;
            bool anyCharring = false;
            float flipPhase = 0f;

            foreach (var s in _steaks)
            {
                if (cooking)
                {
                    s.Down.Done += PanSideRate * heat * dt;
                    s.Down.Sear = Mathf.Min(1.3f, s.Down.Sear + sear * (s.Down.Sear < 1f ? 1f : 0.35f) * dt);   // a crust darkens slower than it forms
                    s.Up.Done += UpSideRate * heat * dt;
                    if (ButterIn) s.Down.Butter = Mathf.Min(1f, s.Down.Butter + dt / 12f);   // the face in the pan sits in it
                    if (s.Down.Done > 1.1f) anyCharring = true;
                }
                else if (Served && _residual < 0.05f)
                {
                    float r = 0.005f * dt; _residual += r; s.A.Done += r; s.B.Done += r;   // carry-over heat while it rests
                }
                if (s.FlipT < 1f)
                {
                    s.FlipT = Mathf.Min(1f, s.FlipT + dt / 0.38f);
                    float e = Mathf.SmoothStep(0f, 1f, s.FlipT);
                    var ea = s.Root.localEulerAngles;
                    s.Root.localEulerAngles = new Vector3(ea.x, ea.y, Mathf.Lerp(s.FlipFrom, s.FlipTo, e));
                    var p = s.Root.localPosition;
                    s.Root.localPosition = new Vector3(p.x, s.RestY + Mathf.Sin(s.FlipT * Mathf.PI) * 0.08f, p.z);
                    flipPhase = Mathf.Max(flipPhase, Mathf.Sin(s.FlipT * Mathf.PI));
                }
                Color ca = FaceColor(s.A), cb = FaceColor(s.B);
                s.MatA.SetColor("_BaseColor", ca); s.MatA.SetFloat("_Smoothness", Mathf.Lerp(0.6f, 0.28f, Mathf.Clamp01(s.A.Sear)));
                s.MatB.SetColor("_BaseColor", cb); s.MatB.SetFloat("_Smoothness", Mathf.Lerp(0.6f, 0.28f, Mathf.Clamp01(s.B.Sear)));
                s.MatSide.SetColor("_BaseColor", Color.Lerp(ca, cb, 0.5f));
                float render = Mathf.Clamp01((s.A.Sear + s.B.Sear) * 0.5f);
                s.MatFat.SetColor("_BaseColor", Color.Lerp(FatRaw, FatRendered, render));
                s.MatFat.SetFloat("_Smoothness", Mathf.Lerp(0.45f, 0.7f, render));
            }

            // the spatula darts under the steaks while they're in the air, then goes back to its burner
            if (_spatula != null)
            {
                var under = new Vector3(0f, _steakBaseY + 0.01f, 0.06f);
                _spatula.transform.localPosition = Vector3.Lerp(_spatulaRest, under, flipPhase);
                _spatula.transform.localRotation = _spatulaRestRot * Quaternion.Euler(-28f * flipPhase, 0f, 0f);
            }

            if (_butterPat != null)
            {
                // the pat slumps into a foaming puddle over a few seconds
                float m = Mathf.Clamp01((Time.time - _butterInAt) / 6f) * (HeatOn ? 1f : 0.3f);
                _butterPat.localScale = new Vector3(Mathf.Lerp(0.05f, 0.11f, m), Mathf.Lerp(0.024f, 0.003f, m), Mathf.Lerp(0.04f, 0.09f, m));
                _butterPat.localPosition = new Vector3(0.02f, _steakBaseY + _butterPat.localScale.y * 0.5f, 0.14f);
            }

            // butter can only take so much of a hot pan
            _butterOnHigh = cooking && ButterIn && HeatLevel == Heat.High ? _butterOnHigh + dt : Mathf.Max(0f, _butterOnHigh - dt);

            // sound + steam follow the heat; smoke follows neglect
            float d = Doneness;
            _sizzle.volume = Mathf.MoveTowards(_sizzle.volume, cooking ? 0.3f + 0.25f * HeatMultiplier + 0.2f * (ButterIn ? 1f : 0f) : 0f, dt * 0.8f);
            _sizzle.pitch = 0.85f + 0.2f * HeatMultiplier;
            var em = _steam.emission; em.rateOverTime = cooking ? 6f + 14f * HeatMultiplier : 0f;
            bool smokingNow = cooking && (anyCharring || _butterOnHigh > 20f);
            var sm = _smoke.emission; sm.rateOverTime = smokingNow ? 10f + 40f * Mathf.Clamp01((d - 1.1f) * 3f + (_butterOnHigh > 20f ? 0.4f : 0f)) : 0f;
            if (smokingNow && !_smoking) { _smoking = true; _oneShot.PlayOneShot(_alarmClip, 0.6f); OnSmokeStarted?.Invoke(); }
            if (!smokingNow) _smoking = false;

            if (cooking && Time.time >= _nextPop)
            {
                _nextPop = Time.time + UnityEngine.Random.Range(PopIntervalMin, PopIntervalMax) / HeatMultiplier;
                _grease.Emit(14);
                _oneShot.PlayOneShot(_popClip, 1f);
                OnPop?.Invoke();
            }
        }

        /// What a face looks like: crust browns it, overcooking blackens it.
        static Color FaceColor(Face f)
        {
            var c = Color.Lerp(Raw, Seared, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(f.Sear)));
            float burn = Mathf.Clamp01((f.Done - 1f) / 0.5f) * 0.8f + Mathf.Clamp01((f.Sear - 1f) / 0.3f) * 0.4f;
            return Color.Lerp(c, Char, Mathf.Clamp01(burn));
        }

        public static string DonenessWord(float d)
        {
            if (d < 0.15f) return "Raw";
            if (d < 0.35f) return "Blue";
            if (d < 0.5f) return "Rare";
            if (d < 0.62f) return "Medium-rare";
            if (d < 0.8f) return "Medium";
            if (d < 0.95f) return "Medium-well";
            if (d < 1.15f) return "Well-done";
            if (d < 1.4f) return "Overdone";
            return "Charcoal";
        }

        public static string CrustWord(float sear)
            => sear < 0.15f ? "no crust" : sear < 0.6f ? "light crust" : sear <= 1.15f ? "good crust" : "dark crust";

        public SteakReport Report()
        {
            float d = Doneness, dd = d - Target;
            float doneScore = Mathf.Exp(-(dd * dd) / (2f * 0.13f * 0.13f));
            float crust = Crust, salt = Salt;
            float score = 100f * (0.45f * doneScore + 0.2f * crust + 0.15f * salt + 0.1f * Baste + 0.1f * Evenness);
            var r = new SteakReport
            {
                Doneness = d, Evenness = Evenness, Crust = crust, Salt = salt, Baste = Baste,
                Burnt = Burnt, Score = Mathf.RoundToInt(Mathf.Clamp(score, 0f, 100f)),
            };
            string s = DonenessWord(d) + ", " + CrustWord(crust);
            s += salt >= 0.99f ? ", salted both sides" : salt > 0f ? ", salted on one side only" : ", unsalted";
            if (Baste >= 0.99f) s += ", butter-basted";
            else if (ButterIn) s += ", butter in but never basted";
            if (Evenness < 0.6f) s += ", one side darker than the other";
            r.Summary = s + ".";
            return r;
        }
    }
}
