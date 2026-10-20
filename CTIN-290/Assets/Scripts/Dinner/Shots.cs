using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dinner
{
    /// Your hands: a sleeve, a palm and four fingers, built in life-size units and scaled with the kitchen.
    /// The root's +z runs from the elbow to the palm.
    public class Hand : MonoBehaviour
    {
        public const float Length = 0.34f;   // elbow to palm centre, life size
        readonly List<Transform> _fingers = new List<Transform>();
        float _grip, _gripTarget;

        public static Hand Build(string name, float scale)
        {
            var root = new GameObject(name).transform;
            root.localScale = Vector3.one * scale;
            var sleeve = Visuals.Opaque(new Color(0.22f, 0.27f, 0.36f), 0.2f);
            var cuff = Visuals.Opaque(new Color(0.9f, 0.89f, 0.86f), 0.25f);
            var skin = Visuals.Opaque(new Color(0.87f, 0.68f, 0.55f), 0.3f);
            const float L = Length;
            Rot(Visuals.Primitive(PrimitiveType.Cylinder, "Sleeve", root, new Vector3(0f, 0f, L * 0.36f), new Vector3(0.085f, L * 0.38f, 0.085f), sleeve));
            Rot(Visuals.Primitive(PrimitiveType.Cylinder, "Cuff", root, new Vector3(0f, 0f, L * 0.76f), new Vector3(0.089f, 0.02f, 0.089f), cuff));
            Rot(Visuals.Primitive(PrimitiveType.Cylinder, "Wrist", root, new Vector3(0f, 0f, L * 0.86f), new Vector3(0.062f, 0.03f, 0.062f), skin));
            Visuals.Primitive(PrimitiveType.Sphere, "Palm", root, new Vector3(0f, 0f, L), new Vector3(0.085f, 0.04f, 0.1f), skin);
            var h = root.gameObject.AddComponent<Hand>();
            for (int i = 0; i < 4; i++)
            {
                var knuckle = Visuals.Empty("Knuckle" + i, root, new Vector3(-0.03f + i * 0.02f, 0f, L + 0.045f)).transform;
                var f = Visuals.Primitive(PrimitiveType.Capsule, "Finger" + i, knuckle, new Vector3(0f, 0f, 0.035f), new Vector3(0.019f, 0.036f - Mathf.Abs(i - 1.5f) * 0.004f, 0.019f), skin);
                Rot(f);
                h._fingers.Add(knuckle);
            }
            var thumb = Visuals.Empty("ThumbBase", root, new Vector3(0.045f, -0.005f, L - 0.01f)).transform;
            thumb.localRotation = Quaternion.Euler(0f, 40f, 0f);
            Rot(Visuals.Primitive(PrimitiveType.Capsule, "Thumb", thumb, new Vector3(0f, 0f, 0.03f), new Vector3(0.022f, 0.032f, 0.022f), skin));
            h._fingers.Add(thumb);
            return h;
        }

        static void Rot(GameObject g) => g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // primitives stand on y; lay them along z

        /// World position of the palm.
        public Vector3 Palm => transform.TransformPoint(new Vector3(0f, 0f, Length));

        /// Place the arm so the palm is at `palm`, the forearm coming in along `direction` (elbow -> palm).
        public void Reach(Vector3 palm, Vector3 direction, float roll = 0f)
        {
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            transform.position = palm - transform.rotation * new Vector3(0f, 0f, Length * transform.lossyScale.z);
        }

        public void Grip(float amount) { _gripTarget = Mathf.Clamp01(amount); }
        public void GripNow(float amount) { _grip = _gripTarget = Mathf.Clamp01(amount); Apply(); }

        void Update()
        {
            _grip = Mathf.MoveTowards(_grip, _gripTarget, Time.deltaTime * 5f);
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < _fingers.Count; i++)
            {
                bool thumb = i == _fingers.Count - 1;
                _fingers[i].localRotation = thumb ? Quaternion.Euler(0f, 40f - 30f * _grip, 0f) : Quaternion.Euler(70f * _grip + 8f, 0f, 0f);
            }
        }
    }

    /// Small things built from primitives: rosemary, a salt shaker, a mug, the butter dish, a couple of tubs.
    public static class Garnish
    {
        /// A rosemary sprig lying flat, `length` world units long, centred on `pos`.
        public static Transform Rosemary(Transform parent, Vector3 pos, float yaw, float length)
        {
            var root = new GameObject("Rosemary").transform;
            root.SetParent(parent, true);
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var stem = Visuals.Opaque(new Color(0.42f, 0.36f, 0.22f), 0.2f);
            var leaf = Visuals.Opaque(new Color(0.2f, 0.36f, 0.22f), 0.35f);
            float r = length * 0.035f;
            var s = Visuals.Primitive(PrimitiveType.Cylinder, "Stem", root, new Vector3(0f, r, 0f), new Vector3(r, length * 0.5f, r), stem);
            s.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            int n = 16;
            for (int i = 0; i < n; i++)
            {
                float z = Mathf.Lerp(-0.45f, 0.48f, i / (n - 1f)) * length;
                float side = i % 2 == 0 ? 1f : -1f;
                var needle = Visuals.Primitive(PrimitiveType.Capsule, "Needle", root, new Vector3(side * length * 0.06f, r * 1.6f, z), new Vector3(r * 1.5f, length * 0.07f, r * 1.1f), leaf);
                needle.transform.localRotation = Quaternion.Euler(90f, side * 55f, 0f);
                needle.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root;
        }

        public static Transform SaltShaker(Transform parent, Vector3 basePos, float height)
        {
            var root = Root("SaltShaker", parent, basePos);
            float w = height * 0.5f;
            Visuals.Primitive(PrimitiveType.Cylinder, "Glass", root, new Vector3(0f, height * 0.38f, 0f), new Vector3(w, height * 0.38f, w), Visuals.Opaque(new Color(0.86f, 0.9f, 0.92f), 0.9f));
            Visuals.Primitive(PrimitiveType.Cylinder, "Salt", root, new Vector3(0f, height * 0.25f, 0f), new Vector3(w * 1.01f, height * 0.25f, w * 1.01f), Visuals.Opaque(new Color(0.97f, 0.97f, 0.96f), 0.2f));
            Visuals.Primitive(PrimitiveType.Sphere, "Cap", root, new Vector3(0f, height * 0.8f, 0f), new Vector3(w * 1.02f, height * 0.36f, w * 1.02f), Visuals.Opaque(new Color(0.75f, 0.76f, 0.78f), 0.7f, 0.9f));
            return root;
        }

        public static Transform Mug(Transform parent, Vector3 basePos, float height, Color color, float yaw)
        {
            var root = Root("Mug", parent, basePos);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var m = Visuals.Opaque(color, 0.55f);
            float w = height * 0.85f;
            Visuals.Primitive(PrimitiveType.Cylinder, "Body", root, new Vector3(0f, height * 0.5f, 0f), new Vector3(w, height * 0.5f, w), m);
            Visuals.Primitive(PrimitiveType.Cylinder, "Coffee", root, new Vector3(0f, height * 0.98f, 0f), new Vector3(w * 0.86f, 0.002f, w * 0.86f), Visuals.Opaque(new Color(0.22f, 0.13f, 0.08f), 0.8f));
            var handle = Visuals.Primitive(PrimitiveType.Cylinder, "Handle", root, new Vector3(w * 0.58f, height * 0.52f, 0f), new Vector3(height * 0.42f, height * 0.06f, height * 0.42f), m);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return root;
        }

        public static Transform ButterDish(Transform parent, Vector3 basePos, float length, float yaw)
        {
            var root = Root("ButterDish", parent, basePos);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            Visuals.Primitive(PrimitiveType.Cube, "Dish", root, new Vector3(0f, length * 0.04f, 0f), new Vector3(length * 0.62f, length * 0.08f, length), Visuals.Opaque(new Color(0.95f, 0.95f, 0.93f), 0.7f));
            var butter = Visuals.Opaque(new Color(1f, 0.9f, 0.55f), 0.45f);
            Visuals.Primitive(PrimitiveType.Cube, "Butter", root, new Vector3(0f, length * 0.08f + length * 0.14f, -length * 0.1f), new Vector3(length * 0.38f, length * 0.28f, length * 0.62f), butter);
            Visuals.Primitive(PrimitiveType.Cube, "Pat", root, new Vector3(length * 0.05f, length * 0.08f + length * 0.04f, length * 0.33f), new Vector3(length * 0.3f, length * 0.08f, length * 0.12f), butter).transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            return root;
        }

        /// A square tub; `fill` decides what's in it (rosemary sprigs stick out of an herb tub).
        public static Transform Tub(Transform parent, Vector3 basePos, float size, Color color, bool herbs)
        {
            var root = Root(herbs ? "HerbTub" : "SaltTub", parent, basePos);
            Visuals.Primitive(PrimitiveType.Cube, "Tub", root, new Vector3(0f, size * 0.35f, 0f), new Vector3(size, size * 0.7f, size), Visuals.Opaque(color, 0.4f));
            Visuals.Primitive(PrimitiveType.Cube, "Inside", root, new Vector3(0f, size * 0.66f, 0f), new Vector3(size * 0.86f, 0.004f, size * 0.86f),
                Visuals.Opaque(herbs ? new Color(0.25f, 0.2f, 0.14f) : new Color(0.97f, 0.96f, 0.94f), 0.15f));
            if (herbs)
                for (int i = 0; i < 4; i++)
                {
                    var sprig = Rosemary(root, root.position + new Vector3((i - 1.5f) * size * 0.16f, size * 0.62f, 0f), i * 40f, size * 1.25f);
                    sprig.localRotation = Quaternion.Euler(-78f + i * 5f, i * 47f, 0f);   // standing up out of the tub
                }
            return root;
        }

        static Transform Root(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, true);
            t.position = pos;
            return t;
        }
    }

    /// The kitchen's fridge, made openable. The model's fridge is a solid box, so for the shot its body is
    /// swapped for an open shell with shelves and food, and the lower door swings out on its hinge.
    public class Fridge : MonoBehaviour
    {
        public CamPose Pose;

        Renderer _body, _door;
        Transform _doorParent, _rig, _interior, _tray;
        Vector3 _doorLocalPos, _doorLocalScale, _trayHomePos;
        Quaternion _doorLocalRot, _rigClosed, _trayHomeRot;
        Vector3 _inward, _right, _front;
        float _w, _h, _d;
        Hand _hand;
        Light _light;

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        public static Fridge Build(DinnerGame g, Renderer body, Renderer door)
        {
            var f = new GameObject("Fridge").AddComponent<Fridge>();
            f.Setup(g, body, door);
            return f;
        }

        void Setup(DinnerGame g, Renderer body, Renderer door)
        {
            _body = body; _door = door;
            var b = body.bounds; var db = door.bounds;
            var into = Flat(b.center - db.center);
            _inward = Mathf.Abs(into.x) > Mathf.Abs(into.z) ? new Vector3(Mathf.Sign(into.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(into.z));
            _right = Vector3.Cross(Vector3.up, _inward);   // your right, standing at the open fridge
            _d = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(_inward.x), 0f, Mathf.Abs(_inward.z))));
            _w = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(_right.x), 0f, Mathf.Abs(_right.z))));
            _h = db.size.y;
            _front = Flat(b.center) - _inward * (_d * 0.5f) + Vector3.up * db.min.y;
            float doorT = Mathf.Abs(Vector3.Dot(db.size, new Vector3(Mathf.Abs(_inward.x), 0f, Mathf.Abs(_inward.z))));

            // --- the shell (local: x = right, y = up, z = into the fridge)
            _interior = new GameObject("FridgeInterior").transform;
            _interior.SetParent(transform, false);
            _interior.SetPositionAndRotation(_front, Quaternion.LookRotation(_inward));
            var white = Visuals.Opaque(new Color(0.9f, 0.93f, 0.95f), 0.55f);
            var shelfMat = Visuals.Opaque(new Color(0.78f, 0.88f, 0.92f), 0.92f);
            float t = 0.035f, w = _w, h = _h, d = _d;
            Box("Back", new Vector3(0f, h * 0.5f, d - t * 0.5f), new Vector3(w, h, t), white);
            Box("Left", new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, d * 0.5f), new Vector3(t, h, d), white);
            Box("Right", new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, d * 0.5f), new Vector3(t, h, d), white);
            Box("Floor", new Vector3(0f, t * 0.5f, d * 0.5f), new Vector3(w, t, d), white);
            Box("Ceiling", new Vector3(0f, h - t * 0.5f, d * 0.5f), new Vector3(w, t, d), white);
            float above = b.max.y - db.min.y - h;   // the freezer, behind the upper door, stays solid
            if (above > 0.02f) Box("Freezer", new Vector3(0f, h + above * 0.5f, d * 0.5f), new Vector3(w, above, d), white);

            float[] shelves = { 0.3f, 0.56f, 0.8f };
            foreach (var s in shelves) Box("Shelf", new Vector3(0f, h * s, d * 0.5f + 0.02f), new Vector3(w - 2f * t, 0.014f, d - 0.06f), shelfMat);
            // crisper drawer: a low front panel with fruit and veg behind it
            Box("Crisper", new Vector3(0f, t + h * 0.07f, 0.06f), new Vector3(w - 2f * t - 0.02f, h * 0.14f, 0.02f), Visuals.Opaque(new Color(0.84f, 0.9f, 0.93f), 0.8f));
            Box("CrisperHandle", new Vector3(0f, t + h * 0.12f, 0.045f), new Vector3(w * 0.3f, 0.02f, 0.02f), Visuals.Opaque(new Color(0.7f, 0.75f, 0.78f), 0.6f));

            var p = g.Pack;
            float top0 = t, top1 = h * 0.3f + 0.007f, top2 = h * 0.56f + 0.007f, top3 = h * 0.8f + 0.007f;
            Put(p.Tomato, new Vector3(-w * 0.28f, top0, d * 0.4f), 0f, 0.13f);
            Put(p.Tomato, new Vector3(-w * 0.14f, top0, d * 0.55f), 50f, 0.12f);
            Put(p.Pepper, new Vector3(w * 0.05f, top0, d * 0.45f), 20f, 0.15f);
            Put(p.Lettuce, new Vector3(w * 0.27f, top0, d * 0.5f), 0f, 0.22f);
            // shelf 1: eggs, a lidded box, milk
            var carton = Box("EggCarton", new Vector3(-w * 0.22f, top1 + 0.03f, d * 0.45f), new Vector3(0.34f, 0.06f, 0.2f), Visuals.Opaque(new Color(0.72f, 0.62f, 0.48f), 0.1f));
            for (int i = 0; i < 6; i++)
                Put(p.Egg, new Vector3(-w * 0.22f + (i % 3 - 1) * 0.1f, top1 + 0.05f, d * 0.45f + (i < 3 ? -0.05f : 0.05f)), 0f, 0.07f);
            Box("Leftovers", new Vector3(w * 0.15f, top1 + 0.05f, d * 0.5f), new Vector3(0.26f, 0.1f, 0.2f), Visuals.Opaque(new Color(0.85f, 0.92f, 0.95f), 0.85f));
            Box("Lid", new Vector3(w * 0.15f, top1 + 0.105f, d * 0.5f), new Vector3(0.27f, 0.015f, 0.21f), Visuals.Opaque(new Color(0.35f, 0.6f, 0.75f), 0.4f));
            Put(p.Milk, new Vector3(w * 0.36f, top1, d * 0.55f), -20f, 0.12f);
            // shelf 2: tonight's steaks, on a tray at the front where a hand can reach them
            _tray = new GameObject("SteakTray").transform;
            _tray.SetParent(_interior, false);
            _tray.localPosition = new Vector3(-w * 0.08f, top2, d * 0.32f);
            _tray.localRotation = Quaternion.Euler(0f, 8f, 0f);
            var tray = Props.Spawn(p.SquarePlate, _tray, _tray.position, _tray.eulerAngles.y, 0.32f);
            float trayTop = tray != null ? Props.WorldBounds(tray).max.y : _tray.position.y + 0.02f;
            var raw = Visuals.OpaqueTextured(new Color(0.86f, 0.24f, 0.27f), Visuals.Marbling(), 0.6f);
            var fat = Visuals.Opaque(new Color(0.96f, 0.9f, 0.78f), 0.45f);
            for (int i = 0; i < 2; i++)
            {
                var s = Props.Spawn(p.Steak, _tray, _tray.TransformPoint(new Vector3(-0.05f + i * 0.1f, 0f, 0.01f - i * 0.02f)) + Vector3.up * (trayTop - _tray.position.y + 0.002f), _tray.eulerAngles.y + 90f + i * 180f, 0.17f);
                if (s == null) continue;
                foreach (var r in s.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("White") ? fat : raw;
            }
            _trayHomePos = _tray.localPosition; _trayHomeRot = _tray.localRotation;
            Put(p.Jar, new Vector3(w * 0.3f, top2, d * 0.55f), 0f, 0.13f);
            // shelf 3: bottles at the back
            Put(p.Bottle1, new Vector3(-w * 0.3f, top3, d * 0.6f), 0f, 0.1f);
            Put(p.Bottle2, new Vector3(-w * 0.18f, top3, d * 0.65f), 0f, 0.1f);
            Put(p.Ketchup, new Vector3(w * 0.12f, top3, d * 0.55f), 20f, 0.2f);
            Put(p.Jar, new Vector3(w * 0.32f, top3, d * 0.6f), 0f, 0.11f);

            // --- the door, on a hinge at your right; its shelves ride along
            var hinge = _front + _right * (w * 0.5f) - _inward * doorT;
            _rig = new GameObject("FridgeDoorHinge").transform;
            _rig.SetParent(transform, false);
            _rig.SetPositionAndRotation(hinge, Quaternion.LookRotation(_inward));
            _rigClosed = _rig.rotation;
            _doorParent = door.transform.parent;
            _doorLocalPos = door.transform.localPosition; _doorLocalRot = door.transform.localRotation; _doorLocalScale = door.transform.localScale;
            var bin = Visuals.Opaque(new Color(0.85f, 0.92f, 0.95f), 0.8f);
            float[] bins = { 0.2f, 0.48f, 0.76f };
            foreach (var y in bins)
            {
                RigBox("DoorBin", new Vector3(-w * 0.5f, h * y, doorT + 0.11f), new Vector3(w - 0.12f, 0.08f, 0.015f), bin);
                RigBox("DoorShelf", new Vector3(-w * 0.5f, h * y - 0.035f, doorT + 0.06f), new Vector3(w - 0.12f, 0.012f, 0.11f), bin);
            }
            RigPut(p.Milk, new Vector3(-w * 0.75f, h * 0.2f - 0.03f, doorT + 0.06f), 0f, 0.1f);
            RigPut(p.Bottle1, new Vector3(-w * 0.45f, h * 0.2f - 0.03f, doorT + 0.06f), 0f, 0.085f);
            RigPut(p.Soda, new Vector3(-w * 0.25f, h * 0.2f - 0.03f, doorT + 0.06f), 0f, 0.07f);
            RigPut(p.Ketchup, new Vector3(-w * 0.6f, h * 0.48f - 0.03f, doorT + 0.06f), 0f, 0.15f);
            RigPut(p.Jar, new Vector3(-w * 0.3f, h * 0.48f - 0.03f, doorT + 0.06f), 0f, 0.09f);
            RigPut(p.Bottle2, new Vector3(-w * 0.7f, h * 0.76f - 0.03f, doorT + 0.06f), 0f, 0.085f);
            RigPut(p.Jar, new Vector3(-w * 0.35f, h * 0.76f - 0.03f, doorT + 0.06f), 0f, 0.08f);

            _light = new GameObject("FridgeLight").AddComponent<Light>();
            _light.transform.SetParent(_interior, false);
            _light.transform.localPosition = new Vector3(0f, h * 0.92f, d * 0.35f);
            _light.type = LightType.Point; _light.color = new Color(0.9f, 0.96f, 1f); _light.intensity = 1.6f; _light.range = 1.8f;
            _light.shadows = LightShadows.None;

            _hand = Hand.Build("FridgeHand", g.WorldScale);
            _hand.transform.SetParent(transform, true);

            // standing at the open fridge, bent a little, looking in and down at the middle shelf
            var eye = _front - _inward * 0.88f + Vector3.up * (h * 0.86f) - _right * 0.06f;
            var look = _front + _inward * (d * 0.5f) + Vector3.up * (h * 0.42f);
            Pose = new CamPose(eye, Quaternion.LookRotation(look - eye, Vector3.up).eulerAngles, 60f);

            Show(false);
        }

        GameObject Box(string name, Vector3 local, Vector3 size, Material m) => Visuals.Primitive(PrimitiveType.Cube, name, _interior, local, size, m);
        GameObject RigBox(string name, Vector3 local, Vector3 size, Material m) => Visuals.Primitive(PrimitiveType.Cube, name, _rig, local, size, m);
        void Put(GameObject prefab, Vector3 local, float yaw, float size) => Props.Spawn(prefab, _interior, _interior.TransformPoint(local), _interior.eulerAngles.y + yaw, size);
        void RigPut(GameObject prefab, Vector3 local, float yaw, float size) => Props.Spawn(prefab, _rig, _rig.TransformPoint(local), _rig.eulerAngles.y + yaw, size);

        void Show(bool open)
        {
            _interior.gameObject.SetActive(open);
            _hand.gameObject.SetActive(open);
            for (int i = 0; i < _rig.childCount; i++) if (_rig.GetChild(i) != _door.transform) _rig.GetChild(i).gameObject.SetActive(open);
            _body.enabled = !open;
            if (open)
            {
                _door.transform.SetParent(_rig, true);
            }
            else
            {
                _rig.rotation = _rigClosed;
                _door.transform.SetParent(_doorParent, false);
                _door.transform.localPosition = _doorLocalPos; _door.transform.localRotation = _doorLocalRot; _door.transform.localScale = _doorLocalScale;
            }
        }

        /// Open up, reach in for the steaks, take them out. Ends with the fridge closed again.
        public IEnumerator Play(AttentionCamera cam, float seconds)
        {
            Show(true);
            _tray.SetParent(_interior, false);
            _tray.localPosition = _trayHomePos; _tray.localRotation = _trayHomeRot;
            cam.Cut(Pose);
            var camRot = Pose.Rotation;
            var down = camRot * Vector3.down; var camRight = camRot * Vector3.right; var camFwd = camRot * Vector3.forward;
            var start = Pose.Position + down * 0.45f + camRight * 0.36f + camFwd * 0.5f;   // just under the frame, bottom right
            var grab = _tray.position + Vector3.up * 0.06f - _inward * 0.05f;
            _hand.GripNow(0f);

            float open = seconds * 0.2f, reach = seconds * 0.55f, grip = seconds * 0.67f;
            bool holding = false;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float a = Mathf.SmoothStep(0f, 1f, t / open);
                _rig.rotation = _rigClosed * Quaternion.Euler(0f, -100f * a, 0f);   // negative: the free edge swings out, toward you

                Vector3 palm;
                if (t < reach) palm = Vector3.Lerp(start, grab, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(open * 0.6f, reach, t)));
                else if (t < grip) { palm = grab; _hand.Grip(1f); }
                else
                {
                    if (!holding) { holding = true; _tray.SetParent(_hand.transform, true); }
                    palm = Vector3.Lerp(grab, start + down * 0.15f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(grip, seconds, t)));
                }
                var dir = (palm - (start - camFwd * 0.3f)).normalized;
                _hand.Reach(palm, dir, -15f);
                yield return null;
            }
            Show(false);
        }
    }
}
