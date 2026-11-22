using System;
using System.Collections;
using System.Collections.Generic;
using Ink;
using Ink.UnityIntegration;
using Hsinpa.Dialogue;
using Ink.Runtime;
using UnityEngine;

namespace Hsinpa.Director
{
    /// <summary>
    /// Plays dialogue events from a SceneDialogueAsset, line by line.
    /// <para>An event is named <c>file</c> (a whole ink file from its top) or <c>file.knot</c>
    /// (one knot of that file; <c>file.knot.stitch</c> works too). The file is the ink file's name.</para>
    /// <para>Two modes, picked by a tag at the top of the knot (or of the file for a whole file):</para>
    /// <list type="bullet">
    /// <item>No tag: a cutscene. Every CutsceneActor is told it started and ended (e.g. to turn player
    /// input off), and each line waits for the UI/Advance input (Space, Enter, click, gamepad south, touch).</item>
    /// <item><c># mode: bark</c>: background talk. Actors are not told, so input stays on, and each line
    /// moves on by itself after the bark line time (2 s by default).</item>
    /// </list>
    /// <para><c># hold: 3</c> on a line moves it on after 3 seconds, in either mode. Other tags that are not
    /// commands (e.g. <c># style: shout</c>) are passed on in DialogueLine.Tags for the visuals.
    /// Tag rules live in DialogueTags.</para>
    /// <para>One cutscene and one bark at most. Playing a cutscene stops everything that is playing; playing
    /// a bark replaces the current bark; a bark is ignored while a cutscene plays. Stop ends events early:
    /// the shown line is hidden, a cutscene ends as usual (actors told, Finished raised) and the actors the
    /// event's commands used drop their queued actions.</para>
    /// For each line, its <c># func ...</c> tags run first, in order. Commands that start something
    /// (walk_to, camera_follow) let the next one run at once; commands that hold the event (wait, wait_for,
    /// camera_pan) run to the end first. Then the line is shown. Ink attaches tag-only lines to the next line
    /// of text, so commands written above a line run before it. Commands are listed in DirectorCommands.
    /// <para>A broken command or a crash inside one is reported as a warning and skipped; an ink runtime
    /// error ends the event. Either way the event always ends cleanly, also when a listener or a command
    /// stops it halfway.</para>
    /// Lines are raised through LineShown and LineHidden for the dialogue box and bubbles, and printed to
    /// the Console while Log Lines To Console is on.
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        [SerializeField]
        SceneDialogueAsset dialogue;

        [SerializeField, Min(0.1f), Tooltip("Seconds a bark line stays up when it has no # hold tag.")]
        float barkLineSeconds = 2f;

        [SerializeField, Tooltip("Print each line to the Console. Turn off once a dialogue box listens to LineShown.")]
        bool logLinesToConsole = true;

        // One playing event. Everything about it lives here, so a cutscene and a bark never share state.
        sealed class Playback
        {
            public readonly string Name;
            public readonly Story Story;
            public readonly DialogueMode Mode;
            public readonly List<CutsceneActor> Notified = new List<CutsceneActor>();
            public readonly HashSet<CutsceneActor> UsedActors = new HashSet<CutsceneActor>();
            public ErrorHandler OnError;
            public Coroutine Routine;
            public bool InkFailed;
            public bool Ended;
            public bool HasLine;
            public DialogueLine Line;

            public Playback(string name, Story story, DialogueMode mode)
            {
                Name = name;
                Story = story;
                Mode = mode;
            }
        }

        readonly ActorRegistry _actors = new ActorRegistry();
        readonly CommandRegistry _commands = new CommandRegistry();
        AdvanceInput _advance;
        Playback _cutscene;
        Playback _bark;

        /// <summary>True while a cutscene plays.</summary>
        public bool IsPlaying => _cutscene != null;
        public bool IsBarkPlaying => _bark != null;
        public CommandRegistry Commands => _commands;
        /// <summary>The actors scripts can use, refreshed each time an event starts.</summary>
        public ActorRegistry Actors => _actors;

        /// <summary>Raised when a line is shown.</summary>
        public event Action<DialogueLine> LineShown;
        /// <summary>Raised when a shown line goes away: moved on, or its event stopped.</summary>
        public event Action<DialogueLine> LineHidden;
        /// <summary>Raised when a cutscene ends or is stopped.</summary>
        public event Action Finished;

        void Awake()
        {
            _advance = new AdvanceInput();
            _commands.AddType<CutsceneActor>("actor", ConvertActor);
            DirectorCommands.RegisterAll(_commands, this);
        }

        void OnEnable() => _advance.Enable();

        void Start()
        {
            if (dialogue != null && dialogue.PlayOnStart) Play();
        }

        void OnDisable()
        {
            Stop();
            _advance.Disable();
        }

        void OnDestroy() => _advance.Dispose();

        // ---- Playing ----

        /// <summary>Plays the asset's opening story from the top.</summary>
        public void Play()
        {
            if (!HasAsset()) return;

            InkFile opening = dialogue.OpeningStory;
            if (opening == null)
            {
                Debug.LogError($"{name}: {dialogue.name} has no ink files.", dialogue);
                return;
            }
            Play(opening.name, opening.name, null);
        }

        /// <summary>Plays an event: "file" for a whole ink file, "file.knot" for one knot of it.</summary>
        public void Play(string eventName)
        {
            if (!HasAsset()) return;
            if (string.IsNullOrWhiteSpace(eventName))
            {
                Debug.LogError($"{name}: Play needs an event name, e.g. \"scene1_exterior.guard_idle\".", this);
                return;
            }

            eventName = eventName.Trim();
            int dot = eventName.IndexOf('.');
            string file = dot < 0 ? eventName : eventName.Substring(0, dot);
            string knot = dot < 0 ? null : eventName.Substring(dot + 1);
            Play(eventName, file, knot);
        }

        void Play(string eventName, string file, string knot)
        {
            if (!dialogue.HasStory(file))
            {
                Debug.LogError($"{name}: cannot play '{eventName}': {dialogue.name} has no ink file named '{file}'.", this);
                return;
            }

            Story story = dialogue.CreateStory(file);
            if (story == null) return;   // already reported

            if (knot != null && !HasPath(story, knot))
            {
                Debug.LogError($"{name}: cannot play '{eventName}': {file}.ink has no knot '{knot}'.", this);
                return;
            }

            DialogueMode mode = ReadMode(knot == null ? story.globalTags : story.TagsForContentAtPath(knot), eventName);

            if (mode == DialogueMode.Bark)
            {
                if (_cutscene != null)
                {
                    Debug.Log($"{name}: bark '{eventName}' ignored; cutscene '{_cutscene.Name}' is playing.", this);
                    return;
                }
                StopBarks();
            }
            else
            {
                Stop();
            }

            if (knot != null)
            {
                try
                {
                    story.ChoosePathString(knot);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{name}: cannot play '{eventName}'.", this);
                    Debug.LogException(e, this);
                    return;
                }
            }

            Begin(new Playback(eventName, story, mode));
        }

        bool HasAsset()
        {
            if (dialogue != null) return true;
            Debug.LogError($"{name}: no SceneDialogueAsset assigned.", this);
            return false;
        }

        static bool HasPath(Story story, string path) =>
            story.ContentAtPath(new Ink.Runtime.Path(path)).correctObj is Container;

        DialogueMode ReadMode(List<string> tags, string eventName)
        {
            DialogueMode mode = DialogueTags.ReadMode(tags, out string unknownTag);
            if (unknownTag != null)
                Debug.LogWarning($"{name} [{eventName}]: unknown '# {unknownTag}'; the only mode is '# {DialogueTags.ModeKey}: {DialogueTags.BarkValue}'. Playing it as a cutscene.", this);
            return mode;
        }

        void Begin(Playback p)
        {
            p.OnError = (message, type) => OnInkError(p, message, type);
            p.Story.onError += p.OnError;

            _actors.Refresh();
            if (p.Mode == DialogueMode.Blocking) p.Notified.AddRange(_actors.All);

            if (p.Mode == DialogueMode.Bark) _bark = p;
            else _cutscene = p;

            // Set before starting: Run can end the event before its first yield.
            p.Routine = StartCoroutine(Run(p));
        }

        // ---- Stopping ----

        /// <summary>Stops everything that is playing: the cutscene and the bark.</summary>
        public void Stop()
        {
            StopBarks();
            if (_cutscene != null) End(_cutscene, stopped: true);
        }

        /// <summary>Stops one event by the name it was played with, e.g. "scene1_exterior.guard_idle".</summary>
        public void Stop(string eventName)
        {
            if (_cutscene != null && _cutscene.Name == eventName) End(_cutscene, stopped: true);
            if (_bark != null && _bark.Name == eventName) End(_bark, stopped: true);
        }

        /// <summary>Stops the bark; a cutscene keeps playing.</summary>
        public void StopBarks()
        {
            if (_bark != null) End(_bark, stopped: true);
        }

        void End(Playback p, bool stopped)
        {
            if (p.Ended) return;
            p.Ended = true;

            if (_cutscene == p) _cutscene = null;
            if (_bark == p) _bark = null;
            if (p.Routine != null) StopCoroutine(p.Routine);
            p.Story.onError -= p.OnError;

            Hide(p);

            if (stopped)
            {
                foreach (CutsceneActor actor in p.UsedActors)
                    if (actor != null) Safely(p, actor.ClearActions, $"{actor.name} ClearActions");
            }

            if (p.Mode == DialogueMode.Blocking)
            {
                // The same actors that were told the cutscene started, even if some became inactive since.
                foreach (CutsceneActor actor in p.Notified)
                    if (actor != null) Safely(p, actor.NotifyStoryEnded, $"{actor.name} On Story Ended");

                Safely(p, () => Finished?.Invoke(), "a Finished listener");
            }
        }

        // ---- Running ----

        // StopCoroutine from inside this coroutine (a listener or a command calling Stop or Play) only takes
        // effect at the next yield, so every step that runs outside code checks p.Ended before going on.
        IEnumerator Run(Playback p)
        {
            foreach (CutsceneActor actor in p.Notified)
                if (actor != null) Safely(p, actor.NotifyStoryStarted, $"{actor.name} On Story Started");
            if (p.Ended) yield break;

            var displayTags = new List<string>();
            while (p.Story.canContinue)
            {
                if (!TryContinue(p, out string raw)) break;

                displayTags.Clear();
                float hold = 0f;
                foreach (string tag in p.Story.currentTags)
                {
                    // The mode tag was read before playing; ink also repeats it on the first line.
                    if (DialogueTags.IsModeTag(tag)) continue;
                    if (DialogueTags.TryRead(tag, DialogueTags.HoldKey, out string holdText))
                    {
                        if (!DialogueTags.TryParseHold(holdText, out hold, out string holdError)) Warn(p, tag, raw, holdError);
                        continue;
                    }

                    switch (CommandParser.Parse(tag, out CommandCall call, out string parseError))
                    {
                        case ParseResult.NotCommand:
                            displayTags.Add(tag);
                            break;
                        case ParseResult.Error:
                            Warn(p, tag, raw, parseError);
                            break;
                        case ParseResult.Command:
                            IEnumerator routine = Invoke(p, call, tag, raw);
                            if (p.Ended) yield break;
                            if (routine != null)
                            {
                                yield return RunCommand(p, routine, tag, raw);
                                if (p.Ended) yield break;
                            }
                            break;
                    }
                }

                string text = raw.Trim();
                if (text.Length > 0)
                {
                    if (hold <= 0f && p.Mode == DialogueMode.Bark) hold = barkLineSeconds;

                    Show(p, SplitSpeaker(text, displayTags.ToArray(), p.Mode, hold));
                    if (p.Ended) yield break;

                    if (hold > 0f) yield return new WaitForSeconds(hold);
                    else yield return WaitForAdvance();
                    Hide(p);
                    if (p.Ended) yield break;
                }

                if (p.Story.currentChoices.Count > 0)
                {
                    if (p.Mode == DialogueMode.Bark)
                    {
                        Debug.LogWarning($"{name} [{p.Name}]: a bark cannot offer choices; ending it.", this);
                        break;
                    }
                    // Choices need the dialogue box; until then take the first one.
                    Debug.LogWarning($"{name} [{p.Name}]: choices are not supported yet; taking \"{p.Story.currentChoices[0].text}\".", this);
                    p.Story.ChooseChoiceIndex(0);
                }
            }

            End(p, stopped: false);
        }

        // ---- Commands ----

        bool ConvertActor(object raw, out CutsceneActor actor, out string error)
        {
            actor = null;
            if (raw is not string id)
            {
                error = $"expected an actor id in quotes, got {CommandRegistry.Describe(raw)}";
                return false;
            }
            if (_actors.TryGet(id, out actor))
            {
                error = null;
                return true;
            }
            error = $"no CutsceneActor with id '{id}' in the loaded scenes";
            return false;
        }

        // Binds and runs the handler. Returns the routine to wait for, or null when there is nothing to wait
        // for or the command failed (already reported). The actors it was given are recorded before it runs,
        // so Stop can clear their queues even if the handler throws halfway.
        IEnumerator Invoke(Playback p, CommandCall call, string tag, string line)
        {
            try
            {
                if (!_commands.TryBind(call, out BoundCommand command, out string error))
                {
                    Warn(p, tag, line, error);
                    return null;
                }

                foreach (object arg in command.Args)
                    if (arg is CutsceneActor actor) p.UsedActors.Add(actor);

                return command.Invoke();
            }
            catch (Exception e)
            {
                ReportCommandException(p, e, tag, line);
                return null;
            }
        }

        // Steps a waiting command by hand, so an exception inside it is caught and the event moves on.
        // (C# does not allow yield return inside a try that has a catch.) Nested coroutines the routine
        // yields are run by Unity and are not covered.
        IEnumerator RunCommand(Playback p, IEnumerator routine, string tag, string line)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) yield break;
                    current = routine.Current;
                }
                catch (Exception e)
                {
                    ReportCommandException(p, e, tag, line);
                    yield break;
                }
                yield return current;
            }
        }

        // ---- Error handling: nothing a script or a command does may leave an event stuck ----

        bool TryContinue(Playback p, out string line)
        {
            line = "";
            try
            {
                line = p.Story.Continue();
            }
            catch (Exception e)
            {
                Debug.LogError($"{name} [{p.Name}]: the ink story failed and has been stopped.", this);
                Debug.LogException(e, this);
                return false;
            }
            return !p.InkFailed;
        }

        // An Error means ink cannot go on (the event ends after this line); a Warning can be ignored.
        void OnInkError(Playback p, string message, ErrorType type)
        {
            if (type == ErrorType.Error)
            {
                p.InkFailed = true;
                Debug.LogError($"{name} [{p.Name}]: ink error, stopping: {message}", this);
            }
            else
            {
                Debug.LogWarning($"{name} [{p.Name}]: ink {type.ToString().ToLowerInvariant()}: {message}", this);
            }
        }

        void ReportCommandException(Playback p, Exception e, string tag, string line)
        {
            if (e is CommandError)
            {
                Warn(p, tag, line, e.Message);
                return;
            }
            Warn(p, tag, line, $"the command crashed ({e.GetType().Name}); see the exception below");
            Debug.LogException(e, this);
        }

        void Safely(Playback p, Action action, string what)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogError($"{name} [{p.Name}]: {what} threw; carrying on.", this);
                Debug.LogException(e, this);
            }
        }

        void Warn(Playback p, string tag, string line, string error)
        {
            string at = string.IsNullOrWhiteSpace(line) ? "" : $" (before line \"{line.Trim()}\")";
            Debug.LogWarning($"{name} [{p.Name}]: skipped #{tag}{at}: {error}", this);
        }

        // ---- Showing lines ----

        static readonly char[] SpeakerSeparators = { ':', '：' };

        // "koren: text" or "koren：text", only when koren is a known actor, so a colon inside a sentence is left alone.
        DialogueLine SplitSpeaker(string text, IReadOnlyList<string> tags, DialogueMode mode, float hold)
        {
            int colon = text.IndexOfAny(SpeakerSeparators);
            if (colon > 0)
            {
                string speaker = text.Substring(0, colon).Trim();
                if (_actors.TryGet(speaker, out _))
                    return new DialogueLine(speaker, text.Substring(colon + 1).Trim(), tags, mode, hold);
            }
            return new DialogueLine(null, text, tags, mode, hold);
        }

        void Show(Playback p, DialogueLine line)
        {
            p.Line = line;
            p.HasLine = true;
            if (logLinesToConsole) LogLine(line);
            Safely(p, () => LineShown?.Invoke(line), "a LineShown listener");
        }

        void Hide(Playback p)
        {
            if (!p.HasLine) return;
            p.HasLine = false;
            DialogueLine line = p.Line;
            Safely(p, () => LineHidden?.Invoke(line), "a LineHidden listener");
        }

        void LogLine(DialogueLine line)
        {
            string mode = line.Mode == DialogueMode.Bark ? "[bark] " : "";
            string tags = line.Tags.Count > 0 ? $"  [#{string.Join(" #", line.Tags)}]" : "";
            string speaker = line.Speaker != null ? $"<b>{line.Speaker}</b>: " : "";
            Debug.Log($"{mode}{speaker}{line.Text}{tags}", this);
        }

        IEnumerator WaitForAdvance()
        {
            yield return null;   // the press that showed this line must not also skip it
            while (!_advance.WasPressedThisFrame) yield return null;
        }
    }
}
