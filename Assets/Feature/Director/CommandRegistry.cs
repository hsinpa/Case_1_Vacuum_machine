using System;
using System.Collections;
using System.Collections.Generic;

namespace Hsinpa.Director
{
    /// <summary>
    /// Thrown by a command handler when it cannot do what the script asked, e.g. no camera in the scene.
    /// The runner reports it as a warning naming the tag and line, then carries on with the story.
    /// Throw it instead of logging, so every script mistake is reported the same way.
    /// </summary>
    public class CommandError : Exception
    {
        public CommandError(string message) : base(message) { }
    }

    /// <summary>
    /// Turns a parsed argument (string, int, float or bool) into a parameter type.
    /// Only convert: it may run for a command that then fails on a later argument.
    /// </summary>
    public delegate bool ArgumentConverter<T>(object raw, out T value, out string error);

    /// <summary>A command whose arguments have been checked and converted, ready to run.</summary>
    public readonly struct BoundCommand
    {
        public readonly CommandCall Call;
        readonly object[] _args;
        readonly Func<object[], IEnumerator> _handler;

        internal BoundCommand(CommandCall call, object[] args, Func<object[], IEnumerator> handler)
        {
            Call = call;
            _args = args;
            _handler = handler;
        }

        /// <summary>The converted arguments, e.g. a CutsceneActor where the script wrote "koren".</summary>
        public IReadOnlyList<object> Args => _args;

        /// <summary>
        /// Runs the handler. Returns null for a command that does not wait, otherwise the coroutine to
        /// wait for. The handler may throw; the caller is expected to catch.
        /// </summary>
        public IEnumerator Invoke() => _handler(_args);
    }

    /// <summary>
    /// Script commands by name, with their parameter types. There are two kinds:
    /// <list type="bullet">
    /// <item><see cref="Register(string, Action)"/>: the command starts something (a walk) and the story moves on at once.</item>
    /// <item><see cref="RegisterWaiting(string, Func{IEnumerator})"/>: the handler returns a coroutine and the
    /// story holds until it finishes (wait, camera_pan).</item>
    /// </list>
    /// Parameter types are string, int, float, bool, plus any added with <see cref="AddType{T}"/>
    /// (the runner adds CutsceneActor, looked up by id). An int is accepted where a float is expected.
    /// </summary>
    public class CommandRegistry
    {
        delegate bool Converter(object raw, out object value, out string error);

        readonly struct ParamType
        {
            public readonly string Name;
            public readonly Converter Convert;

            public ParamType(string name, Converter convert)
            {
                Name = name;
                Convert = convert;
            }
        }

        readonly struct Entry
        {
            public readonly Type[] Types;
            public readonly Func<object[], IEnumerator> Handler;

            public Entry(Type[] types, Func<object[], IEnumerator> handler)
            {
                Types = types;
                Handler = handler;
            }
        }

        readonly Dictionary<string, Entry> _commands = new Dictionary<string, Entry>();
        readonly Dictionary<Type, ParamType> _types = new Dictionary<Type, ParamType>();

        public CommandRegistry()
        {
            AddType<string>("string", (object raw, out string v, out string e) => Exact(raw, "string", out v, out e));
            AddType<int>("int", (object raw, out int v, out string e) => Exact(raw, "int", out v, out e));
            AddType<bool>("bool", (object raw, out bool v, out string e) => Exact(raw, "bool", out v, out e));
            AddType<float>("float", (object raw, out float v, out string e) =>
            {
                if (raw is int i)
                {
                    v = i;
                    e = null;
                    return true;
                }
                return Exact(raw, "float", out v, out e);
            });
        }

        /// <summary>Lets commands take a parameter of type T. <paramref name="name"/> is shown in error messages.</summary>
        public void AddType<T>(string name, ArgumentConverter<T> convert)
        {
            _types[typeof(T)] = new ParamType(name, (object raw, out object value, out string error) =>
            {
                bool ok = convert(raw, out T typed, out error);
                value = typed;
                return ok;
            });
        }

        // ---- Commands that start something; the story moves on at once ----

        public void Register(string name, Action handler) =>
            Add(name, Array.Empty<Type>(), _ => { handler(); return null; });

        public void Register<T1>(string name, Action<T1> handler) =>
            Add(name, new[] { typeof(T1) }, a => { handler((T1)a[0]); return null; });

        public void Register<T1, T2>(string name, Action<T1, T2> handler) =>
            Add(name, new[] { typeof(T1), typeof(T2) }, a => { handler((T1)a[0], (T2)a[1]); return null; });

        public void Register<T1, T2, T3>(string name, Action<T1, T2, T3> handler) =>
            Add(name, new[] { typeof(T1), typeof(T2), typeof(T3) }, a => { handler((T1)a[0], (T2)a[1], (T3)a[2]); return null; });

        // ---- Commands the story waits for ----

        public void RegisterWaiting(string name, Func<IEnumerator> handler) =>
            Add(name, Array.Empty<Type>(), _ => handler());

        public void RegisterWaiting<T1>(string name, Func<T1, IEnumerator> handler) =>
            Add(name, new[] { typeof(T1) }, a => handler((T1)a[0]));

        public void RegisterWaiting<T1, T2>(string name, Func<T1, T2, IEnumerator> handler) =>
            Add(name, new[] { typeof(T1), typeof(T2) }, a => handler((T1)a[0], (T2)a[1]));

        public void RegisterWaiting<T1, T2, T3>(string name, Func<T1, T2, T3, IEnumerator> handler) =>
            Add(name, new[] { typeof(T1), typeof(T2), typeof(T3) }, a => handler((T1)a[0], (T2)a[1], (T3)a[2]));

        void Add(string name, Type[] types, Func<object[], IEnumerator> handler)
        {
            if (_commands.ContainsKey(name)) throw new ArgumentException($"Command '{name}' is already registered.");
            foreach (Type type in types)
            {
                if (!_types.ContainsKey(type))
                    throw new ArgumentException($"Command '{name}' takes a {type.Name}, which is not a script type; add it with AddType first.");
            }
            _commands[name] = new Entry(types, handler);
        }

        /// <summary>
        /// Checks the arguments against the command's signature and converts them, without running anything.
        /// Run the result with <see cref="BoundCommand.Invoke"/>. A converter added with AddType may throw;
        /// the caller is expected to catch.
        /// </summary>
        public bool TryBind(CommandCall call, out BoundCommand command, out string error)
        {
            command = default;
            error = null;

            if (!_commands.TryGetValue(call.Name, out Entry entry))
            {
                error = $"unknown command '{call.Name}'";
                return false;
            }

            if (call.Args.Length != entry.Types.Length)
            {
                error = $"{call.Name} takes {entry.Types.Length} argument(s) {Signature(entry.Types)}, got {call.Args.Length}";
                return false;
            }

            var args = new object[call.Args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                if (!_types[entry.Types[i]].Convert(call.Args[i], out args[i], out string argError))
                {
                    error = $"{call.Name} argument {i + 1}: {argError} {Signature(entry.Types)}";
                    return false;
                }
            }

            command = new BoundCommand(call, args, entry.Handler);
            return true;
        }

        static bool Exact<T>(object raw, string typeName, out T value, out string error)
        {
            if (raw is T typed)
            {
                value = typed;
                error = null;
                return true;
            }
            value = default;
            error = $"expected {typeName}, got {Describe(raw)}";
            return false;
        }

        /// <summary>How a parsed argument reads in an error message, e.g. <c>int 3</c> or <c>string "koren"</c>.</summary>
        public static string Describe(object raw) => raw switch
        {
            string s => $"string \"{s}\"",
            int i => $"int {i}",
            float f => $"float {f}",
            bool b => $"bool {(b ? "true" : "false")}",
            _ => raw?.GetType().Name ?? "nothing",
        };

        string Signature(Type[] types)
        {
            var names = new string[types.Length];
            for (int i = 0; i < types.Length; i++) names[i] = _types[types[i]].Name;
            return $"({string.Join(", ", names)})";
        }
    }
}
