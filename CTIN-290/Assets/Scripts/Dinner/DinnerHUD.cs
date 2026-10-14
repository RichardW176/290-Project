using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Dinner
{
    /// Screen-space overlay built in code: clock, subtitles, pan status, key prompts, cards, end screen.
    public class DinnerHUD : MonoBehaviour
    {
        static readonly Color Ink = new Color(0.96f, 0.94f, 0.9f);
        static readonly Color Dim = new Color(0.96f, 0.94f, 0.9f, 0.45f);
        static readonly Color Panel = new Color(0.05f, 0.04f, 0.04f, 0.62f);

        TextMeshProUGUI _clock, _day, _cardTitle, _cardSub, _speaker, _subtitle, _panTitle, _panDetail, _panCue, _prompts, _endText;
        CanvasGroup _card, _subtitleGroup, _panGroup, _endGroup;
        RectTransform _panBarFill;
        UnityEngine.UI.Image _panBarImage, _vignette;
        float _cardUntil, _cueUntil, _cardAlphaTarget, _flash;

        public void Build()
        {
            var canvasGo = new GameObject("DinnerHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // soft dark edges so text reads over the bright kitchen
            _vignette = Image("Vignette", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));

            // clock, top right
            _clock = Text("Clock", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -36f), new Vector2(420f, 64f), 46f, Ink, TextAlignmentOptions.TopRight, FontStyles.Bold);
            _day = Text("Day", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -92f), new Vector2(420f, 36f), 24f, Dim, TextAlignmentOptions.TopRight);

            // centre card (intro beats, phase changes)
            var card = Image("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1400f, 240f), new Color(0f, 0f, 0f, 0.5f));
            _card = card.gameObject.AddComponent<CanvasGroup>();
            _cardTitle = Text("Title", card.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), Vector2.zero, 54f, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            _cardSub = Text("Sub", card.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -44f), Vector2.zero, 30f, Ink, TextAlignmentOptions.Center, FontStyles.Italic);
            _card.alpha = 0f;

            // subtitle box, bottom centre
            var sub = Image("SubtitleBox", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1240f, 150f), Panel);
            _subtitleGroup = sub.gameObject.AddComponent<CanvasGroup>();
            _speaker = Text("Speaker", sub.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(-60f, 36f), 26f, Ink, TextAlignmentOptions.Top, FontStyles.Bold);
            _subtitle = Text("Line", sub.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -18f), new Vector2(-80f, -50f), 34f, Ink, TextAlignmentOptions.Center);
            _subtitleGroup.alpha = 0f;

            // pan status, bottom left
            var pan = Image("PanPanel", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(470f, 240f), Panel);
            _panGroup = pan.gameObject.AddComponent<CanvasGroup>();
            _panTitle = Text("PanTitle", pan.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(-40f, 44f), 34f, Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var bar = Image("Bar", pan.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(-40f, 14f), new Color(1f, 1f, 1f, 0.12f));
            _panBarImage = Image("Fill", bar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 0f), new Color(0.9f, 0.4f, 0.3f));
            _panBarFill = _panBarImage.rectTransform;
            // the ideal window, marked on the bar
            Image("Target", bar.rectTransform, new Vector2(0.62f / 1.5f, 0f), new Vector2(0.8f / 1.5f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 6f), new Color(1f, 1f, 1f, 0.35f));
            _panDetail = Text("PanDetail", pan.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(-40f, -100f), 22f, Ink, TextAlignmentOptions.TopLeft);
            _panCue = Text("PanCue", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 290f), new Vector2(470f, 50f), 30f, new Color(1f, 0.8f, 0.5f), TextAlignmentOptions.Left, FontStyles.Italic);

            // key prompts, bottom right
            _prompts = Text("Prompts", root, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(520f, 220f), 24f, Dim, TextAlignmentOptions.BottomRight);

            // end screen
            var end = Image("End", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0.03f, 0.02f, 0.02f, 0.92f));
            _endGroup = end.gameObject.AddComponent<CanvasGroup>();
            _endText = Text("EndText", end.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-300f, -160f), 30f, Ink, TextAlignmentOptions.Center);
            _endGroup.alpha = 0f;
            _endGroup.blocksRaycasts = false;
        }

        // ------------------------------------------------------------------ API
        public void SetClock(string time, string day) { _clock.text = time; _day.text = day; }

        public void ShowCard(string title, string sub, float seconds)
        {
            _cardTitle.text = title; _cardSub.text = sub;
            _cardUntil = Time.time + seconds; _cardAlphaTarget = 1f;
        }

        public void HideCard() { _cardUntil = 0f; }

        public void SetSubtitle(string speaker, Color speakerColor, string text, bool muffled)
        {
            _speaker.text = muffled ? "" : speaker;
            _speaker.color = speakerColor;
            _subtitle.text = text;
            _subtitle.color = muffled ? new Color(0.8f, 0.8f, 0.8f, 0.5f) : Ink;
            _subtitle.fontStyle = muffled ? FontStyles.Italic : FontStyles.Normal;
            _subtitleGroup.alpha = 1f;
        }

        public void ClearSubtitle() { _subtitleGroup.alpha = 0f; }

        /// The pan readout: overall doneness, the burner, and what each face of the steak is doing.
        public void SetPan(bool visible, SteakCooking s)
        {
            _panGroup.alpha = Mathf.MoveTowards(_panGroup.alpha, visible ? 1f : 0f, Time.deltaTime * 4f);
            float doneness = s.Doneness;
            string word = SteakCooking.DonenessWord(doneness);
            _panTitle.text = s.Served ? "Plated: " + word : word;
            float frac = Mathf.Clamp01(doneness / 1.5f);
            float width = _panBarFill.parent.GetComponent<RectTransform>().rect.width;
            _panBarFill.sizeDelta = new Vector2(width * frac, 0f);
            _panBarImage.color = doneness < 0.62f ? new Color(0.9f, 0.4f, 0.35f) : doneness < 0.8f ? new Color(0.55f, 0.8f, 0.4f) : doneness < 1.15f ? new Color(0.85f, 0.65f, 0.3f) : new Color(0.3f, 0.25f, 0.22f);

            string heat = s.HeatLevel == Heat.High ? "<color=#ffb070>HIGH</color>" : s.HeatLevel == Heat.Low ? "<color=#a0c8ff>LOW</color>" : "MEDIUM";
            string butter = s.Baste >= 0.99f ? "basted" : s.ButterIn ? "butter in" : "<alpha=#60>no butter<alpha=#FF>";
            string top = Face(s.UpSalted, s.UpSear), bottom = Face(s.DownSalted, s.DownSear);
            string status = s.Smoking ? "<color=#ff9966>smoking</color>" : Time.time - s.LastEventTime < 2.5f ? s.LastEvent : "";
            _panDetail.text = "heat " + heat + "   ·   " + butter + "\n<alpha=#A0>top<alpha=#FF>  " + top + "\n<alpha=#A0>pan side<alpha=#FF>  " + bottom + (status.Length > 0 ? "\n" + status : "");
        }

        static string Face(bool salted, float sear)
            => (salted ? "salted" : "<alpha=#60>unsalted<alpha=#FF>") + ", " + SteakCooking.CrustWord(sear);

        /// A cue from the pan while you're facing the other way.
        public void PanCue(string cue, float seconds = 1.6f) { _panCue.text = cue; _cueUntil = Time.time + seconds; }

        public void SetPrompts(string text) { _prompts.text = text; }
        public void Flash(float strength = 0.35f) { _flash = strength; }

        /// The card after each plated steak.
        public void ShowRating(int round, int total, SteakReport r, float seconds)
        {
            string grade = r.Score >= 85 ? "<color=#b8e0a0>" : r.Score >= 60 ? "<color=#f0e0a0>" : "<color=#e0a0a0>";
            ShowCard("Steak " + round + " of " + total + "     " + grade + r.Score + " / 100</color>", r.Summary, seconds);
        }

        public void ShowEnd(List<SteakReport> reports, List<Beat> caught, List<Beat> missed, string clock)
        {
            int total = caught.Count + missed.Count;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<size=56><b>The evening was over before you noticed.</b></size>");
            sb.AppendLine("<size=26><i>" + clock + ". Everyone's gone. The pan is in the sink.</i></size>\n");
            int sum = 0;
            sb.AppendLine("<b>THE STEAKS</b>");
            sb.Append("<size=26>");
            for (int i = 0; i < reports.Count; i++)
            {
                sum += reports[i].Score;
                sb.AppendLine((i + 1) + "   <b>" + reports[i].Score + "</b>   <alpha=#B0>" + reports[i].Summary + "<alpha=#FF>");
            }
            if (reports.Count > 0) sb.AppendLine("<alpha=#80>average " + Mathf.RoundToInt(sum / (float)reports.Count) + " / 100<alpha=#FF>");
            sb.AppendLine("</size>");
            sb.AppendLine("<b>THE EVENING</b>   " + caught.Count + " / " + total + " moments you were there for");
            sb.Append("<size=26>");
            foreach (var b in caught) sb.AppendLine("<color=#b8e0a0>there</color>   " + b.Title);
            foreach (var b in missed) sb.AppendLine("<color=#e0a0a0>missed</color>   " + b.Title + "  <alpha=#70>— \"" + b.BrushOff + "\"<alpha=#FF>");
            sb.AppendLine("</size>");
            sb.Append("\n<size=24><alpha=#80>[R]  do it again, differently<alpha=#FF></size>");
            _endText.text = sb.ToString();
            _endGroup.alpha = 0f;
            _endShowing = true;
            ClearSubtitle(); _panGroup.alpha = 0f;
        }

        bool _endShowing;

        void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time > _cardUntil) _cardAlphaTarget = 0f;
            _card.alpha = Mathf.MoveTowards(_card.alpha, _cardAlphaTarget, dt * (_cardAlphaTarget > 0f ? 2.5f : 1.2f));
            if (Time.time > _cueUntil) _panCue.text = "";
            if (_endShowing) _endGroup.alpha = Mathf.MoveTowards(_endGroup.alpha, 1f, dt * 0.6f);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 1.5f);
            _vignette.color = new Color(0f, 0f, 0f, 0.0f + _flash);
        }

        // ------------------------------------------------------------------ builders
        static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static UnityEngine.UI.Image Image(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, pivot, pos, size);
            var img = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI Text(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
                                    float fontSize, Color color, TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, pivot, pos, size);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize; t.color = color; t.alignment = align; t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }
    }
}
