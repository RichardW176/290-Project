using UnityEngine;

namespace Dinner
{
    public enum Attention { Intro, Pan, Friends }

    [System.Serializable]
    public struct CamPose
    {
        public Vector3 Position;
        public Vector3 Euler;
        public float Fov;
        public CamPose(Vector3 position, Vector3 euler, float fov) { Position = position; Euler = euler; Fov = fov; }
        public Quaternion Rotation => Quaternion.Euler(Euler);
    }

    /// Where you're looking is the whole game. The camera eases between a few fixed poses;
    /// `FriendsBlend` tells the conversation how much of your attention the table actually has.
    public class AttentionCamera : MonoBehaviour
    {
        public CamPose IntroPose, PanPose, FriendsPose;
        public float TurnSeconds = 0.75f;

        public Attention Target { get; private set; } = Attention.Intro;
        public float FriendsBlend { get; private set; }
        public bool AttendingFriends => Target == Attention.Friends && FriendsBlend > 0.5f;
        public bool Turning { get; private set; }

        Camera _cam;

        void Awake() { _cam = GetComponent<Camera>(); if (_cam == null) _cam = gameObject.AddComponent<Camera>(); }

        public void Set(Attention target, bool instant = false)
        {
            Target = target;
            if (!instant) return;
            var p = Current;
            transform.SetPositionAndRotation(p.Position, p.Rotation);
            _cam.fieldOfView = p.Fov;
        }

        public CamPose Current
        {
            get
            {
                switch (Target)
                {
                    case Attention.Pan: return PanPose;
                    case Attention.Friends: return FriendsPose;
                    default: return IntroPose;
                }
            }
        }

        void LateUpdate()
        {
            var p = Current;
            float t = Time.time;
            // breathing: a little drift so the fixed poses don't feel like a security camera
            var sway = new Vector3(Mathf.Sin(t * 0.6f) * 0.006f, Mathf.Sin(t * 1.3f) * 0.005f, 0f);
            var swayRot = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 0.35f, Mathf.Sin(t * 0.55f) * 0.5f, 0f);

            float k = 1f - Mathf.Exp(-Time.deltaTime * (4f / Mathf.Max(0.05f, TurnSeconds)));
            transform.position = Vector3.Lerp(transform.position, p.Position + sway, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, p.Rotation * swayRot, k);
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, p.Fov, k);

            float full = Quaternion.Angle(PanPose.Rotation, FriendsPose.Rotation);
            float toFriends = Quaternion.Angle(transform.rotation, FriendsPose.Rotation);
            FriendsBlend = full > 1f ? Mathf.Clamp01(1f - toFriends / full) : (Target == Attention.Friends ? 1f : 0f);
            Turning = Quaternion.Angle(transform.rotation, p.Rotation) > 4f;
        }
    }
}
