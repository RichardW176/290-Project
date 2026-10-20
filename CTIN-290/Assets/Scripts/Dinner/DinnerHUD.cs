using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using static Dinner.UIKit;

namespace Dinner
{
    /// Screen-space overlay built in code, laid out after the HUD mockup (1920x1080 reference):
    /// Serve (top left), the Raw-to-Burnt bar (top centre), clock and day (top right), the turn-around key
    /// (right), a card per side of the steak (bottom left), the pan keys (bottom right). Plus the title,
    /// centre cards, subtitles and the end screen.
    public class DinnerHUD : MonoBehaviour
    {
        const float DoneMax = 1.5f;                     // the bar runs from raw (0) to burnt (1.5 doneness)
        const float WindowLo = 0.62f, WindowHi = 0.8f;  // the green: medium rare to medium
        static readonly Vector2 WideBox = new Vector2(1382f, 216f);    // the dialogue box, facing the table or between shots
        static readonly Vector2 CompactBox = new Vector2(800f, 124f);  // at the pan: narrow enough to clear the side cards and keys
        static readonly Color LineInk = new Color(0.78f, 0.57f, 0.27f);

        public event System.Action OnStart, OnQuit, OnServe;

        TextMeshProUGUI _clock, _day, _round, _doneWord, _cue, _cardTitle, _cardSub, _speaker, _subtitle, _endText, _butterLabel;
        CanvasGroup _chrome, _stove, _space, _askRow, _card, _subtitleGroup, _endGroup, _title;
        RectTransform _roundRect, _cardRect, _doneFill, _friendsIcon, _panIcon;
        RectTransform _subtitleBox, _speakerTag;
        Image _doneFillImage, _vignette;
        Image[] _heatPips = new Image[3];
        SideCard[] _sides = new SideCard[2];
        Button _startButton;
        float _cardUntil, _cueUntil, _cardAlphaTarget, _flash, _doneShown;
        bool _chromeOn, _endShowing, _cooking, _facingPan, _askable;
        SteakCooking _steak;

        class SideCard
        {
            public Image Back;
            public RectTransform[] Fill = new RectTransform[3];
            public Image[] FillImage = new Image[3];
            public float[] Shown = new float[3];
            public TextMeshProUGUI Where;
        }

        public void Build(TMP_FontAsset display = null, TMP_FontAsset body = null)
        {
            UIKit.Display = display; UIKit.Body = body;   // qualified: UnityEngine has a Display type
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
            EnsureEventSystem();

            _vignette = Panel(Node("Flash", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), new Color(0f, 0f, 0f, 0f), 0f);

            BuildChrome(root);
            BuildStove(root);
            BuildCentreCard(root);
            BuildSubtitles(root);
            BuildEnd(root);
            BuildTitle(root);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();
        }

        // ------------------------------------------------------------------ always-on chrome: clock, day, round
        void BuildChrome(Transform root)
        {
            var g = Node("Chrome", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _chrome = g.gameObject.AddComponent<CanvasGroup>();
            var clock = At("Clock", g, new Vector2(1f, 1f), new Vector2(-64f, -78f), new Vector2(196f, 80f));
            Panel(clock, Gold, 40f);
            _clock = Label(Fill(clock), "7:00 PM", 38f, Cream, TextAlignmentOptions.Center, true);
            _day = Outlined(Label(At("Day", g, new Vector2(1f, 1f), new Vector2(-64f, -168f), new Vector2(196f, 44f)), "Friday", 32f, Gold, TextAlignmentOptions.Center));
            _roundRect = At("Round", g, new Vector2(0f, 1f), new Vector2(84f, -96f), new Vector2(520f, 44f));
            _round = Outlined(Label(_roundRect, "", 28f, Gold, TextAlignmentOptions.Left, true));
            _chrome.alpha = 0f;
        }

        // ------------------------------------------------------------------ the stove HUD (from the mockup)
        void BuildStove(Transform root)
        {
            var g = Node("Stove", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _stove = g.gameObject.AddComponent<CanvasGroup>();

            // Serve: a cream pill in a gold ring, E as its shortcut
            var serve = At("Serve", g, new Vector2(0f, 1f), new Vector2(80f, -80f), new Vector2(206f, 92f));
            Panel(serve, Gold, 46f);
            var inner = Node("Inner", serve, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-18f, -18f));
            var innerImg = Panel(inner, Cream, 37f);
            innerImg.raycastTarget = true;
            Label(Fill(inner), "Serve", 50f, Gold, TextAlignmentOptions.Center, true);
            var button = inner.gameObject.AddComponent<Button>();
            button.targetGraphic = innerImg;
            var cb = button.colors; cb.highlightedColor = new Color(1f, 0.93f, 0.8f); cb.pressedColor = GoldLight; cb.selectedColor = Color.white; button.colors = cb;
            button.onClick.AddListener(() => OnServe?.Invoke());
            Key(serve, "E", new Vector2(1f, 0f), new Vector2(10f, -8f), new Vector2(38f, 38f));

            // doneness: Raw [red fill | cream | green window | cream] Burnt
            var bar = At("Doneness", g, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(864f, 28f));
            bar.pivot = new Vector2(0.5f, 0.5f);
            Panel(bar, Cream, 14f);
            var window = Node("Window", bar, new Vector2(WindowLo / DoneMax, 0f), new Vector2(WindowHi / DoneMax, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Panel(window, Green, 0f);
            _doneFill = Node("Fill", bar, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(28f, 0f));
            _doneFillImage = Panel(_doneFill, Red, 14f);
            foreach (var edge in new[] { WindowLo, WindowHi })   // the window's edges stay visible over the fill
                Panel(Node("Edge", bar, new Vector2(edge / DoneMax, 0.5f), new Vector2(edge / DoneMax, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 36f)), new Color(0.27f, 0.21f, 0.17f, 0.7f), 2f);
            Outlined(Label(At("Raw", g, new Vector2(0.5f, 1f), new Vector2(-446f, -100f), new Vector2(160f, 60f)), "Raw", 46f, Gold, TextAlignmentOptions.Right, true)).rectTransform.pivot = new Vector2(1f, 0.5f);
            Outlined(Label(At("Burnt", g, new Vector2(0.5f, 1f), new Vector2(446f, -100f), new Vector2(180f, 60f)), "Burnt", 46f, Gold, TextAlignmentOptions.Left, true)).rectTransform.pivot = new Vector2(0f, 0.5f);
            _doneWord = Outlined(Label(Node("Word", _doneFill, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(-14f, -6f), new Vector2(220f, 34f)), "", 24f, Cream, TextAlignmentOptions.Top));

            // the two faces of the steak
            for (int i = 0; i < 2; i++) _sides[i] = BuildSide(g, i == 0 ? "Side A" : "Side B", new Vector2(77f + i * 229f, 69f));

            // pan keys, bottom right: H F S B (heat shows its level as pips)
            string[] keys = { "H", "F", "S", "B" }, words = { "Heat", "Flip", "Salt", "Butter" };
            for (int i = 0; i < 4; i++)
            {
                var row = At("Row " + keys[i], g, new Vector2(1f, 0f), new Vector2(-228f, 277f - i * 62f), new Vector2(200f, 44f));
                row.pivot = new Vector2(0f, 0.5f);
                Key(row, keys[i], new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(44f, 44f)).pivot = new Vector2(0f, 0.5f);
                var label = Outlined(Label(Node("Label", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(160f, 44f)), words[i], 32f, Cream, TextAlignmentOptions.Left));
                if (i == 3) _butterLabel = label;
                if (i == 0)
                    for (int p = 0; p < 3; p++)
                        _heatPips[p] = Panel(Node("Pip", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(146f + p * 16f, -2f + p * 3f), new Vector2(11f, 16f + p * 6f)), Gold, 4f);
            }

            // the table's question, shown instead of the pan keys while you're facing them
            var ask = At("Ask", root, new Vector2(1f, 0.5f), new Vector2(-330f, -84f), new Vector2(300f, 44f));   // under the Space key, clear of the dialogue box
            ask.pivot = new Vector2(0f, 0.5f);
            _askRow = ask.gameObject.AddComponent<CanvasGroup>();
            Key(ask, "Q", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(44f, 44f)).pivot = new Vector2(0f, 0.5f);
            Outlined(Label(Node("Label", ask, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(240f, 44f)), "What was that?", 32f, Cream, TextAlignmentOptions.Left));
            _askRow.alpha = 0f;

            // turn around: friends (or, facing them, the pan) over a Space key
            var space = At("Space", root, new Vector2(1f, 0.5f), new Vector2(-142f, 55f), new Vector2(140f, 170f));
            space.pivot = new Vector2(0.5f, 0.5f);
            _space = space.gameObject.AddComponent<CanvasGroup>();
            _friendsIcon = FriendsIcon(space, new Vector2(0.5f, 1f), Vector2.zero, 88f);
            _panIcon = PanIcon(space, new Vector2(0.5f, 1f), Vector2.zero, 88f);
            var spaceKey = At("Key", space, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(126f, 46f));
            Panel(spaceKey, Gold, 9f);
            Label(Fill(spaceKey), "Space", 32f, Cream, TextAlignmentOptions.Center, true);

            _cue = Outlined(Label(At("Cue", root, new Vector2(0f, 0f), new Vector2(80f, 372f), new Vector2(760f, 44f)), "", 28f, GoldLight, TextAlignmentOptions.Left, false, FontStyles.Italic));
            _cue.rectTransform.pivot = new Vector2(0f, 0.5f);

            _stove.alpha = 0f; _space.alpha = 0f;
            _stove.interactable = false; _stove.blocksRaycasts = false;
        }

        SideCard BuildSide(Transform parent, string title, Vector2 pos)
        {
            var c = new SideCard();
            var card = At(title, parent, Vector2.zero, pos, new Vector2(212f, 273f));
            card.pivot = Vector2.zero;
            c.Back = Panel(card, Gold, 16f);
            Label(Node("Title", card, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 66f)), title, 38f, Cream, TextAlignmentOptions.Center, true);
            var body = Node("Body", card, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 207f));
            Panel(body, Cream, 16f);
            Panel(Node("Square", body, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 20f)), Cream, 0f);   // square where it meets the header
            for (int r = 0; r < 3; r++)
            {
                var row = Node("Row", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -15f - r * 60f), new Vector2(182f, 50f));
                Panel(row, Beige, 10f);
                var iconAt = new Vector2(5f, 0f);
                if (r == 0) SaltIcon(row, new Vector2(0f, 0.5f), iconAt, 42f);
                else if (r == 1) ButterIcon(row, new Vector2(0f, 0.5f), iconAt, 42f);
                else SteakIcon(row, new Vector2(0f, 0.5f), iconAt, 42f);
                var track = Node("Track", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(114f, 22f));
                Panel(track, Cream, 11f);
                c.Fill[r] = Node("Fill", track, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 0f));
                c.FillImage[r] = Panel(c.Fill[r], Red, 11f);
            }
            c.Where = Outlined(Label(Node("Where", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(212f, 34f)), "", 22f, Gold, TextAlignmentOptions.Top));
            return c;
        }

        // ------------------------------------------------------------------ centre card, subtitles, end, title
        void BuildCentreCard(Transform root)
        {
            _cardRect = At("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1300f, 210f));
            Panel(_cardRect, new Color(Cream.r, Cream.g, Cream.b, 0.94f), 26f);
            _card = _cardRect.gameObject.AddComponent<CanvasGroup>();
            _cardTitle = Label(Node("Title", _cardRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(-60f, -110f)), "", 54f, Gold, TextAlignmentOptions.Center, true);
            _cardSub = Label(Node("Sub", _cardRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -42f), new Vector2(-60f, -120f)), "", 30f, Ink, TextAlignmentOptions.Center, false, FontStyles.Italic);
            _cardSub.textWrappingMode = TextWrappingModes.Normal;
            _card.alpha = 0f;
        }

        void BuildSubtitles(Transform root)
        {
            // a wide beige box along the bottom, the speaker's name on a gold tag over its top-left corner
            _subtitleBox = At("Dialogue", root, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(WideBox.x, WideBox.y));
            _subtitleBox.pivot = new Vector2(0.5f, 0f);
            Panel(_subtitleBox, new Color(0.925f, 0.85f, 0.73f, 0.97f), 26f);
            _subtitleGroup = _subtitleBox.gameObject.AddComponent<CanvasGroup>();
            _speakerTag = Node("Tag", _subtitleBox, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(-44f, 0f), new Vector2(236f, 84f));
            Panel(_speakerTag, Gold, 42f);
            _speaker = Label(Fill(_speakerTag), "", 54f, Cream, TextAlignmentOptions.Center, true);
            _subtitle = Label(Node("Line", _subtitleBox, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(-140f, -70f)), "", 44f, LineInk, TextAlignmentOptions.Center, true);
            _subtitle.enableAutoSizing = true; _subtitle.fontSizeMin = 28f; _subtitle.fontSizeMax = 44f;
            _subtitle.textWrappingMode = TextWrappingModes.Normal;
            _subtitleGroup.alpha = 0f;
        }

        void BuildEnd(Transform root)
        {
            var end = Node("End", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Panel(end, new Color(0.13f, 0.1f, 0.08f, 0.94f), 0f);
            _endGroup = end.gameObject.AddComponent<CanvasGroup>();
            _endText = Label(Node("Text", end, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-300f, -160f)), "", 30f, Cream, TextAlignmentOptions.Center);
            _endText.textWrappingMode = TextWrappingModes.Normal;
            _endGroup.alpha = 0f;
            _endGroup.blocksRaycasts = false;
        }

        void BuildTitle(Transform root)
        {
            var bg = Node("TitleScreen", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Panel(bg, new Color(0.13f, 0.12f, 0.12f, 0.72f), 0f).raycastTarget = true;   // the kitchen, dimmed well down under the gold
            _title = bg.gameObject.AddComponent<CanvasGroup>();
            Outlined(Label(At("Name", bg, new Vector2(0.5f, 0.5f), new Vector2(0f, 165f), new Vector2(1700f, 200f)), "Everything Is At Steak", 136f, Gold, TextAlignmentOptions.Center, true), 0.12f);
            _startButton = TitleButton(bg, "Start Game", new Vector2(0f, -130f), Gold, Cream, () => OnStart?.Invoke());
            var quit = TitleButton(bg, "Quit Game", new Vector2(0f, -282f), new Color(0.925f, 0.85f, 0.73f), GoldDark, () => OnQuit?.Invoke());
            _startButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = quit, selectOnUp = quit };
            quit.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = _startButton, selectOnUp = _startButton };
            ShowTitle(false);
        }

        /// A pill button; the selected one (mouse over, or arrow keys) grows a little and brightens.
        Button TitleButton(Transform parent, string label, Vector2 pos, Color fill, Color ink, UnityEngine.Events.UnityAction onClick)
        {
            var rt = At(label.Replace(" ", "") + "Button", parent, new Vector2(0.5f, 0.5f), pos, new Vector2(336f, 104f));
            var img = Panel(rt, fill, 30f);
            img.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;   // the look is driven in Update, so hover and keyboard selection match
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            Label(Fill(rt), label, 42f, ink, TextAlignmentOptions.Center, true);
            _titleButtons.Add((b, img, fill));
            return b;
        }

        readonly List<(Button button, Image image, Color fill)> _titleButtons = new List<(Button, Image, Color)>();

        void UpdateTitleButtons(float dt)
        {
            if (_title.alpha < 0.01f || EventSystem.current == null) return;
            // moving the mouse over a button selects it, so there's only ever one highlighted (arrow keys still work)
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool moved = mouse != null && mouse.delta.ReadValue().sqrMagnitude > 0.01f;
            foreach (var t in _titleButtons)
            {
                bool hovered = moved && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)t.button.transform, mouse.position.ReadValue());
                if (hovered && EventSystem.current.currentSelectedGameObject != t.button.gameObject) EventSystem.current.SetSelectedGameObject(t.button.gameObject);
            }
            foreach (var t in _titleButtons)
            {
                bool on = EventSystem.current.currentSelectedGameObject == t.button.gameObject;
                t.button.transform.localScale = Vector3.Lerp(t.button.transform.localScale, Vector3.one * (on ? 1.07f : 1f), 1f - Mathf.Exp(-dt * 14f));
                t.image.color = on ? Color.Lerp(t.fill, Color.white, 0.18f) : t.fill;
            }
        }

        // ------------------------------------------------------------------ API
        public void ShowTitle(bool on)
        {
            _title.alpha = on ? 1f : 0f;
            _title.interactable = on;
            _title.blocksRaycasts = on;
            if (on && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_startButton.gameObject);
        }

        public void SetChrome(bool on) { _chromeOn = on; }
        public void SetRound(string text) { _round.text = text; }
        public void SetClock(string time, string day) { _clock.text = time; _day.text = day; }

        /// The stove HUD, every frame: shown while cooking; the pan half only while you face the pan.
        public void SetStove(bool cooking, bool facingPan, bool askable, SteakCooking s)
        {
            _cooking = cooking; _facingPan = facingPan; _askable = askable; _steak = s;
        }

        /// `y`: the card's height on screen; raise it (300) when the middle of the shot matters.
        public void ShowCard(string title, string sub, float seconds, float y = 120f)
        {
            _cardRect.anchoredPosition = new Vector2(0f, y);
            _cardTitle.text = title; _cardSub.text = sub;
            _cardUntil = Time.time + seconds; _cardAlphaTarget = 1f;
        }

        public void HideCard() { _cardUntil = 0f; }

        public void SetSubtitle(string speaker, Color speakerColor, string text, bool muffled)
        {
            // muffled (you're at the pan): no name, just faint dots
            _speaker.text = muffled ? "" : speaker;
            _speakerTag.gameObject.SetActive(!muffled && !string.IsNullOrEmpty(speaker));
            _subtitle.text = text;
            _subtitle.color = muffled ? new Color(LineInk.r, LineInk.g, LineInk.b, 0.6f) : LineInk;
            _subtitleGroup.alpha = 1f;
        }

        public void ClearSubtitle() { _subtitleGroup.alpha = 0f; }

        /// A cue from the pan while you're facing the other way.
        public void PanCue(string cue, float seconds = 1.6f) { _cue.text = cue; _cueUntil = Time.time + seconds; }

        public void Flash(float strength = 0.35f) { _flash = strength; }

        /// The card after each plated steak, above the plate in the overhead shot.
        public void ShowRating(int round, int total, SteakReport r, float seconds)
        {
            string grade = r.Score >= 85 ? "<color=#6E9E6A>" : r.Score >= 60 ? "<color=#C2913F>" : "<color=#A8424A>";
            ShowCard("Steak " + round + " of " + total + "     " + grade + r.Score + " / 100</color>", r.Summary, seconds);
            _cardRect.anchoredPosition = new Vector2(0f, 300f);
        }

        public void ShowEnd(List<SteakReport> reports, List<Beat> caught, List<Beat> missed, string clock)
        {
            int total = caught.Count + missed.Count;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<size=56><b><color=#E0B05E>The evening was over before you noticed.</color></b></size>");
            sb.AppendLine("<size=26><i>" + clock + ". Everyone's gone. The pan is in the sink.</i></size>\n");
            int sum = 0;
            sb.AppendLine("<b><color=#E0B05E>THE STEAKS</color></b>");
            sb.Append("<size=26>");
            for (int i = 0; i < reports.Count; i++)
            {
                sum += reports[i].Score;
                sb.AppendLine((i + 1) + "   <b>" + reports[i].Score + "</b>   <alpha=#B0>" + reports[i].Summary + "<alpha=#FF>");
            }
            if (reports.Count > 0) sb.AppendLine("<alpha=#80>average " + Mathf.RoundToInt(sum / (float)reports.Count) + " / 100<alpha=#FF>");
            sb.AppendLine("</size>");
            sb.AppendLine("<b><color=#E0B05E>THE EVENING</color></b>   " + caught.Count + " / " + total + " moments you were there for");
            sb.Append("<size=26>");
            foreach (var b in caught) sb.AppendLine("<color=#8FBF8A>there</color>   " + b.Title);
            foreach (var b in missed) sb.AppendLine("<color=#D07A80>missed</color>   " + b.Title + "  <alpha=#70>— \"" + b.BrushOff + "\"<alpha=#FF>");
            sb.AppendLine("</size>");
            sb.Append("\n<size=24><alpha=#80>[R]  do it again, differently<alpha=#FF></size>");
            _endText.text = sb.ToString();
            _endGroup.alpha = 0f;
            _endShowing = true;
            ClearSubtitle();
        }

        // ------------------------------------------------------------------ per frame
        void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time > _cardUntil) _cardAlphaTarget = 0f;
            _card.alpha = Mathf.MoveTowards(_card.alpha, _cardAlphaTarget, dt * (_cardAlphaTarget > 0f ? 2.5f : 2f));
            if (_endShowing) _endGroup.alpha = Mathf.MoveTowards(_endGroup.alpha, 1f, dt * 0.6f);
            _chrome.alpha = Mathf.MoveTowards(_chrome.alpha, _chromeOn && !_endShowing ? 1f : 0f, dt * 2f);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 1.5f);
            _vignette.color = new Color(0f, 0f, 0f, _flash);
            UpdateTitleButtons(dt);

            bool atPan = _cooking && _facingPan && !_endShowing;
            _stove.alpha = Mathf.MoveTowards(_stove.alpha, atPan ? 1f : 0f, dt * 5f);
            _stove.interactable = _stove.blocksRaycasts = atPan;
            _space.alpha = Mathf.MoveTowards(_space.alpha, _cooking && !_endShowing ? 1f : 0f, dt * 5f);
            _askRow.alpha = Mathf.MoveTowards(_askRow.alpha, _cooking && !_facingPan && _askable ? 1f : 0f, dt * 5f);
            var box = atPan ? CompactBox : WideBox;
            _subtitleBox.sizeDelta = Vector2.Lerp(_subtitleBox.sizeDelta, box, 1f - Mathf.Exp(-dt * 10f));
            _friendsIcon.gameObject.SetActive(_facingPan);
            _panIcon.gameObject.SetActive(!_facingPan);
            // the round sits where Serve is until there's a Serve button to make room for
            _roundRect.anchoredPosition = Vector2.Lerp(_roundRect.anchoredPosition, new Vector2(84f, _stove.alpha > 0.5f ? -196f : -96f), 1f - Mathf.Exp(-dt * 10f));

            if (Time.time > _cueUntil) _cue.text = "";
            if (_steak == null) return;
            if (_cue.text.Length == 0 || Time.time > _cueUntil)
                _cue.text = !atPan ? "" : _steak.Smoking ? "<color=#E58A5C>it's smoking</color>" : Time.time - _steak.LastEventTime < 2.5f ? _steak.LastEvent : "";
            UpdateDoneness(dt);
            UpdateSides(dt);
            int heat = _steak.HeatLevel == Dinner.Heat.Low ? 1 : _steak.HeatLevel == Dinner.Heat.Medium ? 2 : 3;
            for (int p = 0; p < 3; p++) _heatPips[p].color = p < heat ? (heat == 3 ? UIKit.Heat : Gold) : new Color(Cream.r, Cream.g, Cream.b, 0.35f);
            _butterLabel.text = _steak.ButterIn ? "Baste" : "Butter";
        }

        void UpdateDoneness(float dt)
        {
            float d = _steak.HasSteaks ? _steak.Doneness : 0f;
            _doneShown = Mathf.Lerp(_doneShown, d, 1f - Mathf.Exp(-dt * 6f));
            float width = ((RectTransform)_doneFill.parent).rect.width;
            _doneFill.sizeDelta = new Vector2(Mathf.Max(28f, width * Mathf.Clamp01(_doneShown / DoneMax)), 0f);
            _doneFillImage.color = _doneShown < WindowLo ? Red : _doneShown <= WindowHi ? Green : _doneShown < 1.15f ? Gold : UIKit.Burnt;
            _doneWord.text = _steak.HasSteaks ? SteakCooking.DonenessWord(d).ToLower() : "";
        }

        void UpdateSides(float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                var v = _steak.Side(i);
                var c = _sides[i];
                float[] want = { v.Salted ? 1f : 0f, v.Butter, Mathf.Clamp01(v.Sear / 1.1f) };
                Color[] col =
                {
                    v.Salted ? Green : Red,
                    v.Butter < 0.34f ? Red : v.Butter < 0.67f ? Gold : Green,
                    v.Sear > 1.1f ? UIKit.Burnt : v.Sear < 0.3f ? Red : v.Sear < 0.75f ? Gold : Green,
                };
                for (int r = 0; r < 3; r++)
                {
                    c.Shown[r] = Mathf.MoveTowards(c.Shown[r], want[r], dt * 2.5f);
                    float w = ((RectTransform)c.Fill[r].parent).rect.width;
                    c.Fill[r].sizeDelta = new Vector2(c.Shown[r] > 0.005f ? Mathf.Max(22f, w * c.Shown[r]) : 0f, 0f);
                    c.FillImage[r].color = col[r];
                }
                // the face on the pan glows a little hotter
                c.Back.color = Color.Lerp(c.Back.color, v.OnPan ? Color.Lerp(Gold, UIKit.Heat, 0.45f) : Gold, 1f - Mathf.Exp(-dt * 8f));
                c.Where.text = v.OnPan ? "in the pan" : "facing up";
                c.Where.color = v.OnPan ? GoldLight : new Color(Cream.r, Cream.g, Cream.b, 0.6f);
            }
        }
    }
}
