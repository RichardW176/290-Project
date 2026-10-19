using System.Collections.Generic;
using UnityEngine;

namespace Dinner
{
    public enum BeatKind { Chat, Joke, Story, News }

    /// One spoken line. Speaker 0..2 = a friend, -1 = you, -2 = stage direction.
    public class Line
    {
        public int Speaker;
        public string Text;
        public bool Payoff;    // the moment that lands; being present for it is what "being there" means
        public bool Laughter;  // the table laughs after this line (audible even from the stove)
        public float Duration; // seconds; 0 = derived from the text length

        public float Seconds => Duration > 0f ? Duration : ConversationScript.EstimateDuration(Text);
    }

    /// A short stretch of table talk while you cook: a joke, a story, some news.
    public class Beat
    {
        public string Id;
        public BeatKind Kind;
        public string Title;         // how the end screen names it: "Sam's landlord joke"
        public List<Line> Lines = new List<Line>();
        public int BrushOffSpeaker;  // who answers "what was that about?"
        public string BrushOff;      // ...and what they say
        public float PauseAfter = 8f;

        public float TotalSeconds
        {
            get { float t = 0f; foreach (var l in Lines) t += l.Seconds; return t; }
        }
    }

    /// Kept deliberately sparse: the talk is texture behind the cooking, not the main event.
    public static class ConversationScript
    {
        public const int Maya = 0, Sam = 1, Theo = 2, You = -1, Stage = -2;
        public static readonly string[] Names = { "Maya", "Sam", "Theo" };
        public static readonly Color[] ShirtColors =
        {
            new Color(0.85f, 0.45f, 0.30f), // Maya - terracotta
            new Color(0.30f, 0.50f, 0.75f), // Sam  - denim
            new Color(0.40f, 0.60f, 0.40f), // Theo - sage
        };

        public static float EstimateDuration(string text)
        {
            int words = string.IsNullOrEmpty(text) ? 1 : text.Split(' ').Length;
            return Mathf.Clamp(1.1f + words * 0.34f, 1.6f, 5.5f);
        }

        static Line L(int speaker, string text, bool payoff = false, bool laughter = false, float duration = 0f)
            => new Line { Speaker = speaker, Text = text, Payoff = payoff, Laughter = laughter, Duration = duration };

        public static List<Beat> CookingBeats()
        {
            return new List<Beat>
            {
                new Beat
                {
                    Id = "landlord", Kind = BeatKind.Joke, Title = "Sam's landlord joke",
                    Lines =
                    {
                        L(Sam, "So my landlord finally looks at the sink."),
                        L(Sam, "Stares at it. A full minute."),
                        L(Sam, "\"Have you tried not using it?\"", payoff: true, laughter: true),
                        L(Maya, "No. No, he did not."),
                    },
                    BrushOffSpeaker = Sam, BrushOff = "Eh. You had to be there.",
                },
                new Beat
                {
                    Id = "wedding", Kind = BeatKind.Story, Title = "Maya's wedding story",
                    Lines =
                    {
                        L(Maya, "My cousin's wedding. The dog had the rings."),
                        L(Maya, "Saw a squirrel. Gone. Rings and all.", payoff: true, laughter: true),
                        L(Sam, "The squirrel did not sign the registry.", laughter: true),
                    },
                    BrushOffSpeaker = Maya, BrushOff = "Long story. Later.",
                },
                new Beat
                {
                    Id = "news", Kind = BeatKind.News, Title = "Theo's news", PauseAfter = 6f,
                    Lines =
                    {
                        L(Theo, "I have news. Kind of."),
                        L(Theo, "I got the Portland job.", payoff: true),
                        L(Maya, "Theo! When?"),
                        L(Theo, "March.", duration: 2f),
                    },
                    BrushOffSpeaker = Theo, BrushOff = "Don't worry about it. Steak first.",
                },
            };
        }

        /// Lines the friends toss out once the scripted beats run dry and you're still cooking.
        public static readonly string[][] Filler =
        {
            new[] { "Should we help?", "With what?" },
            new[] { "We could just order something.", "Don't let them hear you." },
        };

        public static readonly string[] SmokeReactions =
        {
            "Is it supposed to smoke like that?",
            "Chef. The pan.",
        };

        /// One line from the table when a steak is plated.
        public static string[] SteakVerdict(SteakReport r)
        {
            if (r.Burnt) return new[] { "Steak's... bold.", "Very smoky. Intentional?" };
            if (r.Score >= 85) return new[] { "Okay, that one's unreal.", "Teach me. Tonight." };
            if (r.Score >= 60) return new[] { "That's a good steak.", "Oh, that's nice." };
            return new[] { "It's... good! It's good.", "It's a choice." };
        }
    }

    /// How a steak turned out — filled in by the cooking sim, shown on the rating card and the end screen.
    public struct SteakReport
    {
        public float Doneness;    // 0 raw .. 1 well .. 1.5+ charcoal
        public float Evenness;    // 1 = both faces matched
        public float Crust;       // 0..1 average sear
        public float Salt;        // 0..1 fraction of faces salted
        public float Baste;       // 0..1
        public bool Burnt;
        public int Score;         // 0..100
        public string Summary;    // "Medium, good crust, salted both sides, butter-basted."
    }
}
