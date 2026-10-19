using UnityEngine;

namespace Dinner
{
    /// A seated friend built from primitives: torso, head, arms on the table. Talks, laughs, looks around.
    public class FriendCharacter : MonoBehaviour
    {
        public string Name { get; private set; }
        public int Index { get; private set; }
        public Transform Head { get; private set; }
        public Transform BubbleAnchor { get; private set; }   // where a speech bubble / subtitle tag hangs

        Transform _torso, _mouth, _headPivot;
        float _talkUntil, _laughUntil, _seed;
        Vector3 _lookTarget;
        bool _hasLook;
        Vector3 _restPos;

        /// `seatHeight` is where the seat of the chair is; the body is scaled to a ~1.7 m person.
        public void Build(string name, int index, Color shirt, float seatHeight, Vector3 facing)
        {
            Name = name; Index = index;
            _seed = index * 17.3f;
            transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.z).normalized, Vector3.up);
            _restPos = transform.position;

            var skin = Visuals.Opaque(new Color(0.87f, 0.68f, 0.55f) * (0.85f + 0.1f * index), 0.3f);
            var shirtMat = Visuals.Opaque(shirt, 0.25f);
            var hairMat = Visuals.Opaque(index == 0 ? new Color(0.18f, 0.1f, 0.06f) : index == 1 ? new Color(0.55f, 0.4f, 0.22f) : new Color(0.1f, 0.1f, 0.1f), 0.4f);
            var dark = Visuals.Opaque(new Color(0.12f, 0.06f, 0.05f), 0.2f);

            // lap: thighs forward under the table edge
            Visuals.Primitive(PrimitiveType.Cube, "Lap", transform, new Vector3(0, seatHeight + 0.07f, 0.15f), new Vector3(0.36f, 0.14f, 0.42f), Visuals.Opaque(new Color(0.2f, 0.22f, 0.3f), 0.2f));
            // torso: capsule from seat up to shoulders (~0.6 m)
            _torso = Visuals.Primitive(PrimitiveType.Capsule, "Torso", transform, new Vector3(0, seatHeight + 0.42f, 0), new Vector3(0.42f, 0.34f, 0.28f), shirtMat).transform;
            // arms resting forward on the table
            Visuals.Primitive(PrimitiveType.Capsule, "ArmL", _torso, new Vector3(-0.42f, 0.25f, 0.65f), new Vector3(0.28f, 0.75f, 0.42f), shirtMat).transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            Visuals.Primitive(PrimitiveType.Capsule, "ArmR", _torso, new Vector3(0.42f, 0.25f, 0.65f), new Vector3(0.28f, 0.75f, 0.42f), shirtMat).transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            Visuals.Primitive(PrimitiveType.Sphere, "HandL", _torso, new Vector3(-0.42f, 0.33f, 1.25f), new Vector3(0.26f, 0.3f, 0.4f), skin);
            Visuals.Primitive(PrimitiveType.Sphere, "HandR", _torso, new Vector3(0.42f, 0.33f, 1.25f), new Vector3(0.26f, 0.3f, 0.4f), skin);
            // neck + head
            Visuals.Primitive(PrimitiveType.Cylinder, "Neck", transform, new Vector3(0, seatHeight + 0.74f, 0), new Vector3(0.09f, 0.04f, 0.09f), skin);
            _headPivot = Visuals.Empty("HeadPivot", transform, new Vector3(0, seatHeight + 0.78f, 0)).transform;
            Head = Visuals.Primitive(PrimitiveType.Sphere, "Head", _headPivot, new Vector3(0, 0.12f, 0), new Vector3(0.22f, 0.25f, 0.23f), skin).transform;
            Visuals.Primitive(PrimitiveType.Sphere, "Hair", _headPivot, new Vector3(0, 0.17f, -0.02f), new Vector3(0.235f, 0.2f, 0.235f), hairMat);
            Visuals.Primitive(PrimitiveType.Cube, "EyeL", _headPivot, new Vector3(-0.045f, 0.13f, 0.105f), new Vector3(0.025f, 0.03f, 0.02f), dark);
            Visuals.Primitive(PrimitiveType.Cube, "EyeR", _headPivot, new Vector3(0.045f, 0.13f, 0.105f), new Vector3(0.025f, 0.03f, 0.02f), dark);
            _mouth = Visuals.Primitive(PrimitiveType.Cube, "Mouth", _headPivot, new Vector3(0, 0.06f, 0.11f), new Vector3(0.05f, 0.012f, 0.01f), dark).transform;
            BubbleAnchor = Visuals.Empty("BubbleAnchor", _headPivot, new Vector3(0, 0.34f, 0)).transform;
        }

        public void Talk(float seconds) { _talkUntil = Time.time + seconds; }
        public void Laugh(float seconds = 1.4f) { _laughUntil = Time.time + seconds; }
        public bool IsTalking => Time.time < _talkUntil;
        public bool IsLaughing => Time.time < _laughUntil;

        /// Turn the head toward a point (the speaker, the stove, the player's seat).
        public void LookAt(Vector3 worldPoint) { _lookTarget = worldPoint; _hasLook = true; }
        public void LookAhead() { _hasLook = false; }

        void Update()
        {
            float t = Time.time + _seed;

            // breathing / idle sway
            float sway = Mathf.Sin(t * 0.9f) * 1.2f + Mathf.Sin(t * 1.7f) * 0.6f;
            float laugh = IsLaughing ? Mathf.Abs(Mathf.Sin(t * 22f)) * (0.03f + 0.02f * Mathf.Sin(t * 3f)) : 0f;
            _torso.localScale = new Vector3(0.42f, 0.34f + 0.006f * Mathf.Sin(t * 1.4f) + laugh, 0.28f);
            transform.position = _restPos + Vector3.up * laugh * 0.6f;
            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, sway * (IsLaughing ? 2.5f : 0.6f));

            // head: look target or forward; nod while talking; tilt back while laughing
            Quaternion want = _hasLook
                ? Quaternion.LookRotation(Vector3.ProjectOnPlane(_lookTarget - _headPivot.position, Vector3.up).normalized + Vector3.up * Mathf.Clamp((_lookTarget.y - _headPivot.position.y) * 0.4f, -0.3f, 0.3f), Vector3.up)
                : transform.rotation;
            float nod = IsTalking ? Mathf.Sin(t * 6.3f) * 4f : Mathf.Sin(t * 0.7f) * 1.5f;
            float tilt = IsLaughing ? -14f : 0f;
            want *= Quaternion.Euler(nod + tilt, 0f, IsLaughing ? Mathf.Sin(t * 9f) * 5f : 0f);
            _headPivot.rotation = Quaternion.Slerp(_headPivot.rotation, want, Time.deltaTime * 6f);

            // mouth
            float open = IsTalking ? 0.012f + 0.035f * Mathf.Abs(Mathf.Sin(t * 19f) * Mathf.Sin(t * 7.3f)) : IsLaughing ? 0.04f : 0.012f;
            _mouth.localScale = new Vector3(IsLaughing ? 0.065f : 0.05f, open, 0.01f);
        }
    }
}
