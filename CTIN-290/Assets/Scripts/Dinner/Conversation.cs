using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dinner
{
    /// Plays the friends' table talk on its own clock and keeps score of what you were there for.
    public class Conversation : MonoBehaviour
    {
        public Func<bool> IsAttending = () => true;   // is the player looking at / sitting with the friends?
        public FriendCharacter[] Friends = new FriendCharacter[0];
        public Vector3 PlayerFocus;                   // where friends look when they address you (stove, later your seat)

        public event Action<Line> OnLineStarted;
        public event Action<Beat, bool> OnBeatEnded; // beat, caught
        public event Action OnLaughter;

        public Line CurrentLine { get; private set; }
        public Beat CurrentBeat { get; private set; }
        public float LineProgress { get; private set; }
        public bool SomeoneTalking => CurrentLine != null;
        public bool IsLaughing => Time.time < _laughUntil;
        public List<Beat> Caught { get; } = new List<Beat>();
        public List<Beat> Missed { get; } = new List<Beat>();
        public bool CookingTalkFinished { get; private set; }

        /// A missed beat just ended: you can ask what it was about (and get brushed off).
        public Beat AskableBeat => (_askable != null && Time.time < _askUntil && _interjection == null) ? _askable : null;

        Beat _askable;
        float _askUntil, _laughUntil;
        Line _interjection;          // a line cut in between scripted lines (smoke reactions, your question)
        Line _interjectionReply;
        Coroutine _running;
        AudioSource _tableAudio, _murmur;
        AudioClip _laughClip;
        int _lastFiller;

        void Awake()
        {
            _tableAudio = gameObject.AddComponent<AudioSource>();
            _tableAudio.spatialBlend = 0.7f;
            _tableAudio.minDistance = 1f; _tableAudio.maxDistance = 12f;
            _murmur = gameObject.AddComponent<AudioSource>();
            _murmur.clip = ProceduralAudio.Murmur();
            _murmur.loop = true; _murmur.spatialBlend = 0.7f; _murmur.volume = 0f;
            _murmur.minDistance = 1f; _murmur.maxDistance = 12f;
            _murmur.Play();
            _laughClip = ProceduralAudio.Laughter();
        }

        // ------------------------------------------------------------------ cooking phase
        public void StartCookingTalk(List<Beat> beats)
        {
            Stop();
            CookingTalkFinished = false;
            _running = StartCoroutine(RunBeats(beats));
        }

        public void Stop()
        {
            if (_running != null) StopCoroutine(_running);
            _running = null;
            CurrentLine = null; CurrentBeat = null;
        }

        /// Something at the stove made them react (smoke, alarm). Plays between lines.
        public void Interject(int speaker, string text)
        {
            if (_interjection != null) return;
            _interjection = new Line { Speaker = speaker, Text = text };
        }

        /// "Wait, what was that about?" — only after you missed something, and it never works.
        public bool AskWhatWasThat()
        {
            var beat = AskableBeat;
            if (beat == null) return false;
            _askable = null;
            _interjection = new Line { Speaker = ConversationScript.You, Text = "Wait— what was that about?" };
            _interjectionReply = new Line { Speaker = beat.BrushOffSpeaker, Text = beat.BrushOff };
            return true;
        }

        IEnumerator RunBeats(List<Beat> beats)
        {
            yield return new WaitForSeconds(1.5f);
            foreach (var beat in beats)
            {
                CurrentBeat = beat;
                float attended = 0f, total = 0f, payoffAttended = 0f, payoffTotal = 0f;
                foreach (var line in beat.Lines)
                {
                    yield return PlayInterjections();
                    float dur = line.Seconds;
                    float t = 0f;
                    BeginLine(line);
                    while (t < dur)
                    {
                        float dt = Time.deltaTime;
                        t += dt; total += dt;
                        bool here = IsAttending();
                        if (here) attended += dt;
                        if (line.Payoff) { payoffTotal += dt; if (here) payoffAttended += dt; }
                        LineProgress = t / dur;
                        yield return null;
                    }
                    EndLine(line);
                    if (line.Laughter) yield return new WaitForSeconds(1.1f);
                    else yield return new WaitForSeconds(0.35f);
                }
                CurrentBeat = null;
                bool caught = (payoffTotal > 0f && payoffAttended / payoffTotal >= 0.5f) || (total > 0f && attended / total >= 0.6f);
                if (caught) Caught.Add(beat); else { Missed.Add(beat); _askable = beat; _askUntil = Time.time + 9f; }
                OnBeatEnded?.Invoke(beat, caught);
                yield return new WaitForSeconds(beat.PauseAfter);
            }
            CookingTalkFinished = true;

            // the scripted part is over; they keep chatting until you sit down
            while (true)
            {
                var filler = ConversationScript.Filler[_lastFiller++ % ConversationScript.Filler.Length];
                int speaker = UnityEngine.Random.Range(0, 3);
                foreach (var text in filler)
                {
                    yield return PlayInterjections();
                    var line = new Line { Speaker = speaker, Text = text };
                    speaker = (speaker + 1 + UnityEngine.Random.Range(0, 2)) % 3;
                    yield return Say(line);
                }
                yield return new WaitForSeconds(UnityEngine.Random.Range(4f, 8f));
            }
        }

        IEnumerator PlayInterjections()
        {
            while (_interjection != null)
            {
                var line = _interjection; _interjection = null;
                yield return Say(line);
                if (_interjectionReply != null) { var reply = _interjectionReply; _interjectionReply = null; yield return Say(reply); }
            }
        }

        // ------------------------------------------------------------------ shared primitives (dinner phase drives these directly)
        public IEnumerator Say(Line line)
        {
            BeginLine(line);
            yield return new WaitForSeconds(line.Seconds);
            EndLine(line);
            yield return new WaitForSeconds(line.Laughter ? 1.1f : 0.35f);
        }

        public IEnumerator SayAll(IList<Line> lines)
        {
            foreach (var l in lines) yield return Say(l);
        }

        void BeginLine(Line line)
        {
            CurrentLine = line; LineProgress = 0f;
            float dur = line.Seconds;
            if (line.Speaker >= 0 && line.Speaker < Friends.Length)
            {
                var speaker = Friends[line.Speaker];
                speaker.Talk(dur);
                bool addressingPlayer = line.Text.Contains("Chef") || line.Text.Contains("over there") || line.Text.Contains("You're coming") || line.Text.StartsWith("Steak");
                speaker.LookAt(addressingPlayer ? PlayerFocus : OtherFriendHead(line.Speaker));
                for (int i = 0; i < Friends.Length; i++) if (i != line.Speaker) Friends[i].LookAt(speaker.Head.position);
            }
            else if (line.Speaker == ConversationScript.You)
            {
                foreach (var f in Friends) f.LookAt(PlayerFocus);
            }
            OnLineStarted?.Invoke(line);
        }

        void EndLine(Line line)
        {
            CurrentLine = null; LineProgress = 1f;
            if (line.Laughter)
            {
                _laughUntil = Time.time + 1.5f;
                foreach (var f in Friends) f.Laugh();
                _tableAudio.PlayOneShot(_laughClip, 0.8f);
                OnLaughter?.Invoke();
            }
        }

        Vector3 OtherFriendHead(int speaker)
        {
            // glance at whichever friend sits across; fall back to straight ahead
            if (Friends.Length < 2) return PlayerFocus;
            var other = Friends[(speaker + 1 + UnityEngine.Random.Range(0, Friends.Length - 1)) % Friends.Length];
            return other == Friends[speaker] ? PlayerFocus : other.Head.position;
        }

        void Update()
        {
            // voices carry as murmur whenever someone's talking; words only reach you through the HUD when you attend
            float target = SomeoneTalking ? (IsAttending() ? 0.25f : 0.5f) : 0.05f;
            _murmur.volume = Mathf.MoveTowards(_murmur.volume, target, Time.deltaTime * 1.5f);
        }

        /// Words you only half-hear: keeps the rhythm of the sentence, loses the content.
        public static string Muffle(string text)
        {
            var chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (char.IsLetterOrDigit(chars[i])) chars[i] = '·';
            return new string(chars);
        }
    }
}
