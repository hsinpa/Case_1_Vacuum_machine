# Director

The Director plays **ink** dialogue as scenes: cutscenes that take control of the game, and barks
(background talk) that don't. Writers direct the scene from inside the ink file with command tags:

```ink
=== guard_intro ===
# func walk_to("koren", 12, 4)
# func camera_follow("koren")
# func wait_for("koren")
koren: You there! Stop!
# hold: 2
guard: ...
```

Namespace `Hsinpa.Director`, assembly `DirectorRef`.

---

## Architecture

```
                      SceneDialogueAsset (DialogueRef)
                                 │ CreateStory(file)
                                 ▼
 ┌─────────────────────────── DialogueRunner ───────────────────────────┐
 │  flow: Play / Stop, one cutscene + one bark, line loop, error safety │
 │                                                                      │
 │   DialogueTags ── reads # mode / # hold                              │
 │   CommandParser ─ "# func name(args)"  ──► CommandCall               │
 │   CommandRegistry ─ TryBind(CommandCall) ──► BoundCommand.Invoke()   │
 │   ActorRegistry ── finds CutsceneActors by id                        │
 │   AdvanceInput ─── UI/Advance input action                           │
 │                                                                      │
 │   events: LineShown(DialogueLine) / LineHidden / Finished            │
 └──────────────────────────────────────────────────────────────────────┘
          │ registered at Awake by DirectorCommands.RegisterAll
          ▼
  CoreCommands ─── walk_to, walk, wait, wait_for
  CameraCommands ─ camera_follow, camera_pan, camera_reset
          │ enqueue
          ▼
  CutsceneActor ── a queue of ActorActions, run one after another
          │
          ▼
  ActorAction (abstract) ◄── WalkAction ──► GridAgent (Character)
```

### How one line plays

1. `DialogueRunner` calls `Story.Continue()` and gets the text plus its tags.
2. For each tag, in order:
   - `# mode: ...`: skipped here. The mode was read before the event started.
   - `# hold: 2.5`: the line moves on after 2.5 s.
   - `# func ...`: `CommandParser` → `CommandRegistry.TryBind` (checks argument types and turns `"koren"`
     into a `CutsceneActor`) → `BoundCommand.Invoke()`.
     - A **plain** command (`Register`) starts something and returns at once.
     - A **waiting** command (`RegisterWaiting`) returns a coroutine, and the story holds until it ends.
   - Anything else is a display tag, passed on in `DialogueLine.Tags` (e.g. `# style: shout`).
3. The text is split into speaker and text (`koren: Hello`; the speaker must be a known actor id)
   and raised with `LineShown`.
4. The line waits for `UI/Advance` (cutscene) or for its hold time (bark, or a line with `# hold`), then `LineHidden`.

### Two kinds of "waiting"

This is the most important idea in the folder:

| | Who waits | Example |
|---|---|---|
| **Actor queue** | the *actor*: its actions run one after another | `walk_to` then `walk`: the second walk starts when the first arrives |
| **Waiting command** | the *story*: no more tags or lines until it ends | `wait(1f)`, `camera_pan(...)`, `wait_for("koren")` |

Different actors run at the same time, and the story runs alongside them. `wait_for("koren")` is the
bridge between the two: the story holds until koren's queue is empty.

### Scripts

| Script | Role |
|---|---|
| `DialogueRunner.cs` | The MonoBehaviour you put in the scene. Owns the flow: play, stop, the line loop, ending cleanly. Nothing a script or a command does may leave an event stuck. |
| `DialogueMode.cs` | `Blocking` (cutscene) or `Bark`. |
| `DialogueLine.cs` | What the visuals receive: speaker, text, display tags, mode, auto-advance time. |
| `DialogueTags.cs` | The rules for `# mode:` and `# hold:` (case-insensitive, full-width colon allowed). |
| `CommandParser.cs` | Turns `func walk_to("koren", 12, 4)` into a `CommandCall` (name + string/int/float/bool args). Accepts Chinese-IME punctuation outside strings. |
| `CommandRegistry.cs` | Command names → typed handlers. `TryBind` checks and converts arguments; `BoundCommand.Invoke` runs the handler. `CommandError` is how a handler reports a script mistake. |
| `DirectorCommands.cs` | The one list of command groups. Add a line here when you add a group. |
| `CoreCommands.cs` | Movement and waiting commands. |
| `CameraCommands.cs` | Commands for the scene's `CameraFollow`. |
| `ActorRegistry.cs` | Every `CutsceneActor` in the loaded scenes by id, refreshed when an event starts. |
| `CutsceneActor.cs` | Something scripts can direct (and the speaker name). Runs its `ActorAction` queue; raises `On Story Started / Ended` UnityEvents so its own components react (turn off player input, pause NPC wandering). |
| `AdvanceInput.cs` | Wraps the `UI/Advance` action of `InputSystem_Actions`. Rebind keys in the input asset. |
| `Actions/ActorAction.cs` | Base class of everything an actor can do. |
| `Actions/WalkAction.cs` | Walks a `GridAgent` to a cell or by an offset. |

### Dependencies

`DirectorRef` → `CharacterRef` (GridAgent, CameraFollow), `GridMap` (GridMap2D), `DialogueRef`
(SceneDialogueAsset), `Ink`, `Ink-Libraries`, `Unity.InputSystem`, `InputRef`.
Nothing outside this folder depends on the Director, so Character and Map stay usable without it.

### Rules of thumb

- **Script mistakes are warnings, not crashes.** Throw `CommandError("…")` from a handler when the scene
  can't do what was asked. The runner logs it with the tag and line and carries on.
- **Commands don't know about each other.** They talk to actors, the camera, the map, never to the runner's internals.
- **The runner knows nothing about gameplay.** Input, AI and so on react through `CutsceneActor`'s UnityEvents.

---

## Using the Director from code

Put one `DialogueRunner` in the scene and assign its `SceneDialogueAsset`. Other scripts reference the runner
(drag it into a serialized field) and use its public API.

The examples need `using Hsinpa.Director;`. A script inside an assembly definition must also list `DirectorRef`
in its asmdef references (scripts outside any asmdef see it automatically). `Door` and `SoundBank` below stand in
for your own game classes.

### API at a glance

| Member | What it does |
|---|---|
| `Play()` | Plays the asset's opening story from the top. Called by itself on Start when the asset's **Play On Start** is on. |
| `Play(string eventName)` | Plays `"file"` (a whole ink file) or `"file.knot"` (one knot). The ink's `# mode:` tag decides cutscene or bark. |
| `Stop()` | Stops everything: the bark and the cutscene. |
| `Stop(string eventName)` | Stops one event by the name it was played with. |
| `StopBarks()` | Stops the bark; a cutscene keeps playing. |
| `IsPlaying` / `IsBarkPlaying` | Whether a cutscene / a bark is playing. |
| `LineShown` / `LineHidden` | `event Action<DialogueLine>`: a line appeared / went away. Hook your dialogue box here. |
| `Finished` | `event Action`: a cutscene ended *or* was stopped. Barks never raise it. |
| `Commands` | The `CommandRegistry`, to add project-specific commands. |
| `Actors` | The `ActorRegistry`. Filled when an event starts, so it's empty before the first one. |

What `Play` does with what is already playing:

| Playing now | `Play(cutscene)` | `Play(bark)` |
|---|---|---|
| nothing | starts | starts |
| a bark | stops the bark, starts | replaces the bark |
| a cutscene | stops it (actors told it ended, `Finished` raised), starts | **ignored** (logged) |

Mistakes such as an unknown event name or a missing knot are logged as errors, and `Play` does nothing.
None of these methods throw.

### Example: talk to an NPC

Call `Talk()` from whatever starts the conversation: the player's interact key, a UnityEvent or a Signal.

```csharp
using Hsinpa.Director;
using UnityEngine;

public class NpcTalk : MonoBehaviour
{
    [SerializeField] DialogueRunner runner;
    [SerializeField] string eventName = "scene1_exterior.guard_intro";
    [SerializeField] bool onlyOnce = true;

    bool _talked;

    public void Talk()
    {
        if (runner.IsPlaying) return;          // don't cut into another cutscene
        if (onlyOnce && _talked) return;
        _talked = true;
        runner.Play(eventName);
    }
}
```

### Example: background barks

A bark uses the same `Play`. The ink knot carries `# mode: bark`, so input stays on and lines move on by
themselves. It's safe to call repeatedly: a new bark replaces the old one, and barks are ignored during a cutscene.

```csharp
public class GuardChatter : MonoBehaviour
{
    [SerializeField] DialogueRunner runner;
    [SerializeField] string barkEvent = "scene1_exterior.guard_idle";
    [SerializeField] float everySeconds = 8f;

    float _next;

    void Update()
    {
        if (Time.time < _next || runner.IsPlaying || runner.IsBarkPlaying) return;
        _next = Time.time + everySeconds;
        runner.Play(barkEvent);
    }

    void OnDisable() => runner.Stop(barkEvent);   // walked away: stop only this bark
}
```

### Example: a skip button

`Stop()` ends the cutscene cleanly: the shown line is hidden, actors drop their queued actions, each
`CutsceneActor` gets `On Story Ended` (player input back on), and `Finished` is raised.

```csharp
using UnityEngine.InputSystem;

public class CutsceneSkip : MonoBehaviour
{
    [SerializeField] DialogueRunner runner;
    [SerializeField] InputActionReference skip;   // e.g. UI/Cancel

    void OnEnable() => skip.action.Enable();

    void Update()
    {
        if (runner.IsPlaying && skip.action.WasPressedThisFrame()) runner.Stop();
    }
}
```

Note that skipping leaves actors wherever they stopped walking. If later gameplay needs them at their final spots,
put them there in a `Finished` handler (e.g. with `GridAgent.Warp`).

### Example: do something after a cutscene

Either subscribe to `Finished`...

```csharp
void OnEnable()  => runner.Finished += OnCutsceneFinished;
void OnDisable() => runner.Finished -= OnCutsceneFinished;   // always unsubscribe

void OnCutsceneFinished() => door.Open();
```

...or wait in a coroutine, which reads better for a sequence:

```csharp
IEnumerator IntroSequence()
{
    runner.Play("scene1_exterior.intro");
    yield return new WaitWhile(() => runner.IsPlaying);

    door.Open();
    runner.Play("scene1_exterior.after_intro");
}
```

`Finished` fires for a stopped cutscene too. If "finished" and "skipped" must behave differently, keep your own
flag set by the skip button.

### Example: a dialogue box

The runner prints lines to the Console only while **Log Lines To Console** is on. A dialogue box listens to
the two line events instead. Advancing stays the runner's job (`UI/Advance`), so the box only shows and hides.

```csharp
using Hsinpa.Director;
using TMPro;
using UnityEngine;

public class DialogueBox : MonoBehaviour
{
    [SerializeField] DialogueRunner runner;
    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text speakerLabel;
    [SerializeField] TMP_Text textLabel;
    [SerializeField] GameObject continueArrow;

    void OnEnable()
    {
        runner.LineShown += Show;
        runner.LineHidden += Hide;
    }

    void OnDisable()
    {
        runner.LineShown -= Show;
        runner.LineHidden -= Hide;
    }

    void Show(DialogueLine line)
    {
        if (line.Mode == DialogueMode.Bark) return;      // barks go to speech bubbles instead

        panel.SetActive(true);
        speakerLabel.text = line.Speaker ?? "";          // null = narration
        textLabel.text = line.Text;
        continueArrow.SetActive(line.WaitsForPlayer);    // false for # hold lines

        bool shout = false;
        foreach (string tag in line.Tags) shout |= tag.Trim() == "style: shout";
        textLabel.fontStyle = shout ? FontStyles.Bold : FontStyles.Normal;
    }

    void Hide(DialogueLine line) => panel.SetActive(false);
}
```

`LineHidden` always follows a `LineShown`, including when the event is stopped, so the box never gets stuck open.
(The example uses TextMeshPro; `DirectorRef` doesn't need to reference it, because the box lives in your own assembly.)

### Example: direct an actor from gameplay code

Actors aren't only for ink. Any script can queue actions on a `CutsceneActor` it references:

```csharp
[SerializeField] CutsceneActor koren;

public void SendKorenToTheGate()
{
    koren.Enqueue(WalkAction.To(new Vector2Int(12, 4)));
    koren.Enqueue(WalkAction.By(new Vector2Int(0, 1)));   // one cell up, after arriving
}

public void Halt() => koren.ClearActions();
```

Use `koren.IsBusy` to check whether it's still working through its queue. To find an actor by id instead, use
`runner.Actors.TryGet("koren", out CutsceneActor actor)`, but only once an event has started (see the table above).

### Example: add a project-specific command

Commands don't have to live in this folder. Any script can register into `runner.Commands`. Do it in `Awake` or
`Start`, before the first event plays. A name that's already taken throws.

```csharp
public class DoorCommands : MonoBehaviour
{
    [SerializeField] DialogueRunner runner;
    [SerializeField] Door gateDoor;

    void Awake()
    {
        // # func open_gate()      (does not wait)
        runner.Commands.Register("open_gate", () => gateDoor.Open());

        // # func sfx("door_creak")
        runner.Commands.Register<string>("sfx", clip =>
        {
            if (!SoundBank.TryPlay(clip)) throw new CommandError($"no sound named '{clip}'");
        });
    }
}
```

This is the way to keep game-specific commands (doors, quests, inventory) out of the Director's assembly.
Built-in, reusable commands still go in a group in `DirectorCommands`.

---

## Tip 1: Adding a new Action

An action is one thing an actor does, run in its queue. Use one whenever something takes time or must
happen *after* the actor's previous action (face after arriving, play an animation after walking).
Something instant that doesn't need ordering can be a plain command with no action.

### Lifecycle

```
Enqueue ─► CanStart? (polled every frame until true)
             ─► Begin ─► Tick every frame ─► Finish() ─► next action
                   │
                   ├─ actor disabled  ─► Interrupt  (Begin runs again on re-enable)
                   └─ ClearActions / Stop / actor destroyed ─► Cancel  (dropped for good)
```

Rules:

- **Always end with `Finish()`**, on success *and* on failure. An action that never finishes stalls the actor's
  queue (and any `wait_for` on it). Log a warning and `Finish()` instead of throwing.
- **Find components yourself** in `CanStart`/`Begin` (`actor.TryGetComponent`). `CutsceneActor` stays
  ignorant of abilities.
- **Unsubscribe events** in `Interrupt` and `Cancel`, and stop whatever you started in `Cancel`.
- **One instance per `Enqueue`.** `Enqueue` throws if an action has already run or is already queued.
- **Instant actions chain in the same frame**: an action that calls `Finish()` inside `Begin` lets the next
  one start immediately.

### Example: `face`

**1. The action**, in `Actions/FaceAction.cs`:

```csharp
using Hsinpa.Character;
using UnityEngine;

namespace Hsinpa.Director
{
    /// <summary>Turns the actor's GridAgent to face a direction; (0, -1) is down. Instant.</summary>
    public sealed class FaceAction : ActorAction
    {
        readonly Vector2Int _direction;

        public FaceAction(Vector2Int direction) => _direction = direction;

        public override bool CanStart(CutsceneActor actor) =>
            !actor.TryGetComponent(out GridAgent agent) || agent.IsReady;

        public override void Begin(CutsceneActor actor)
        {
            if (actor.TryGetComponent(out GridAgent agent)) agent.Face(_direction);
            else Debug.LogWarning($"{actor.name}: cannot face, it has no GridAgent.", actor);
            Finish();
        }
    }
}
```

**2. The command**, in a command group (create `ActorCommands.cs` once actor commands outgrow `CoreCommands`):

```csharp
commands.Register<CutsceneActor, int, int>("face", (actor, x, y) =>
    actor.Enqueue(new FaceAction(new Vector2Int(x, y))));
```

**3. Document it** in the group's `<code>` doc comment, as a writer would type it, and say whether it waits.

**4. A new group?** Add one line to `DirectorCommands.RegisterAll`.

Now writers can type:

```ink
# func walk_to("koren", 12, 4)
# func face("koren", 0, -1)      // faces down after arriving, because it is queued
```

### An action that takes time

Follow `WalkAction`: start the work in `Begin`, listen for "done" (an event, or a check in `Tick`), and
`Finish()` there. Keep any target you resolve in `Begin` (like `WalkAction._target`) in a field, so a
restart after `Interrupt` carries on toward the same goal.

### Command parameter types

Handlers take `string`, `int`, `float` (an int is accepted), `bool`, or `CutsceneActor` (the writer types the id in
quotes). Add more with `CommandRegistry.AddType<T>(name, converter)` in `DialogueRunner.Awake`. A
converter must *only convert*: it can run for a command that then fails on a later argument.
Handlers take at most 3 parameters; add a `Register<T1..T4>` overload in `CommandRegistry` if you need more.

---

## Tip 2: Supporting Timeline

Timeline suits hand-animated moments: camera moves with easing curves, sprite animation, effects,
sound. The Director suits scripted logic: grid walks, lines, branching. They work together in two directions.

> **First:** add `"Unity.Timeline"` back to `DirectorRef.asmdef`'s references (it was removed because nothing used it).

### A. Ink plays a Timeline (recommended)

Make the Timeline an **actor**. It gets an id, a queue, `wait_for`, and clean-up on `Stop` for free.

1. On the GameObject with the `PlayableDirector`, add a `CutsceneActor` with id e.g. `intro_timeline`.
   Set the PlayableDirector's **Wrap Mode to None**: with Hold it never reports that it finished.
2. Add a `PlayTimelineAction`:

```csharp
using UnityEngine;
using UnityEngine.Playables;

namespace Hsinpa.Director
{
    /// <summary>Plays the actor's PlayableDirector from the start and finishes when it stops.</summary>
    public sealed class PlayTimelineAction : ActorAction
    {
        PlayableDirector _director;

        public override void Begin(CutsceneActor actor)
        {
            if (!actor.TryGetComponent(out _director))
            {
                Debug.LogWarning($"{actor.name}: cannot play a timeline, it has no PlayableDirector.", actor);
                Finish();
                return;
            }
            _director.stopped += OnStopped;
            _director.time = 0;
            _director.Play();
        }

        public override void Interrupt(CutsceneActor actor) => Release();

        public override void Cancel(CutsceneActor actor)
        {
            Release();
            if (_director != null) _director.Stop();   // story skipped: stop the timeline too
        }

        void OnStopped(PlayableDirector _)
        {
            Release();
            Finish();
        }

        void Release()
        {
            if (_director != null) _director.stopped -= OnStopped;
        }
    }
}
```

3. Register the commands (e.g. a new `TimelineCommands.cs`, added to `DirectorCommands`):

```csharp
// # func timeline("intro_timeline")        start it, the story goes on   (does not wait)
// # func timeline_wait("intro_timeline")   start it and hold the story   (waits)
commands.Register<CutsceneActor>("timeline", actor => actor.Enqueue(new PlayTimelineAction()));

commands.RegisterWaiting<CutsceneActor>("timeline_wait", actor =>
{
    actor.Enqueue(new PlayTimelineAction());
    return WaitUntilIdle(actor);
});

static IEnumerator WaitUntilIdle(CutsceneActor actor)
{
    while (actor != null && actor.isActiveAndEnabled && actor.IsBusy) yield return null;
}
```

Because the Timeline is in the actor's queue, `Stop()` on the runner calls `ClearActions` → `Cancel`, which stops
the Timeline. (Note: the inactive-actor guard means a Timeline on a disabled GameObject is not waited for.)

### B. A Timeline plays ink

To start dialogue from a Timeline, put a **Signal Emitter** on a Signal Track, and on the receiving object a
**Signal Receiver** that calls `DialogueRunner.Play(string)` with the event name, e.g. `scene1_exterior.guard_intro`.

For a Timeline that should *pause until the dialogue ends*, connect a small component: on the signal,
`director.Pause()` and `runner.Play(eventName)`; on `runner.Finished`, `director.Resume()`. Only cutscenes raise
`Finished`, so use this with a cutscene event, not a bark.

### Things to watch when mixing the two

- **Grid position.** A Timeline that animates a character's Transform moves it *off the grid*. `GridAgent`
  doesn't know about it, so occupancy and later walks start from the wrong cell. At the end of the Timeline,
  snap the agent with `GridAgent.Warp(cell, facing)` (e.g. from a Signal), or let the Director walk characters
  and use the Timeline only for cameras, effects and animation.
- **Camera.** `CameraFollow` fights a Timeline that animates the camera. Disable `CameraFollow` for that part
  (Activation track, Signal, or the actor's `On Story Started`). On re-enable it glides back from where the
  Timeline left it.
- **Who owns an object.** At any moment, either a Timeline track or a Director action should drive a given
  Transform or Animator, never both. Run the action, then `wait_for` it, before the Timeline takes over, or the other way round.
- **Skipping.** `runner.Stop()` stops a Timeline started through approach A. Timelines started any other
  way need their own skip handling.
