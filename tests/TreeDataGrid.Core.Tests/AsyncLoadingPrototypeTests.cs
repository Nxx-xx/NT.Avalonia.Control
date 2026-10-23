using System.Collections.ObjectModel;
using System.ComponentModel;
using NT.AvaloniaControl.TreeDataGrid;
using Xunit;

namespace TreeDataGrid.Core.Tests;

/// <summary>E1 design probe only: these helpers are not product asynchronous loading APIs.</summary>
public sealed class AsyncLoadingPrototypeTests
{
    [Fact]
    public void Duplicate_expansion_merges_one_request_and_successful_children_are_cached()
    {
        using var scene = new Scene();
        var first = scene.Coordinator.Expand(scene.Id());
        Assert.Equal(first, scene.Coordinator.Expand(scene.Id()));
        Assert.Single(scene.Loader.Requests); Assert.Equal(1, scene.Coordinator.PendingCount);
        scene.Loader.Complete(0, new Node("child")); scene.Dispatcher.Drain();
        Assert.Single(scene.Roots[0].Children); Assert.Equal(ProbeState.Cached, scene.Coordinator.State(scene.Id()));
        scene.Coordinator.Collapse(scene.Id()); scene.Coordinator.Expand(scene.Id());
        Assert.Single(scene.Loader.Requests); Assert.Equal(2, scene.Source.Rows.Count);
    }

    [Fact]
    public void Failure_is_not_cached_and_retry_uses_a_new_version_without_duplicate_children()
    {
        using var scene = new Scene();
        var first = scene.Coordinator.Expand(scene.Id());
        scene.Loader.Fail(0, new InvalidOperationException("remote failure")); scene.Dispatcher.Drain();
        Assert.Equal(ProbeState.Error, scene.Coordinator.State(scene.Id())); Assert.Empty(scene.Roots[0].Children);
        var retry = scene.Coordinator.Expand(scene.Id());
        Assert.True(retry > first); Assert.Equal(2, scene.Loader.Requests.Count);
        scene.Loader.Complete(1, new Node("retry child")); scene.Dispatcher.Drain();
        Assert.Equal("retry child", Assert.Single(scene.Roots[0].Children).Name);
        Assert.Equal(ProbeState.Cached, scene.Coordinator.State(scene.Id()));
    }

    [Fact]
    public void Collapse_cancels_even_when_loader_ignores_token_and_late_response_does_not_insert_children()
    {
        using var scene = new Scene();
        scene.Coordinator.Expand(scene.Id()); scene.Coordinator.Collapse(scene.Id());
        Assert.True(scene.Loader.Requests[0].Context.Cancellation.IsCancellationRequested);
        scene.Loader.Complete(0, new Node("late")); scene.Dispatcher.Drain();
        Assert.Empty(scene.Roots[0].Children); Assert.Equal(0, scene.Coordinator.PendingCount);
        Assert.Single(scene.Source.Rows); Assert.False(scene.Source.Rows[0].IsExpanded);
    }

    [Fact]
    public void Collapse_after_completion_was_queued_still_rejects_the_response()
    {
        using var scene = new Scene();
        scene.Coordinator.Expand(scene.Id()); scene.Loader.Complete(0, new Node("queued"));
        Assert.Empty(scene.Roots[0].Children);
        scene.Coordinator.Collapse(scene.Id()); scene.Dispatcher.Drain();
        Assert.Empty(scene.Roots[0].Children); Assert.False(scene.Source.Rows[0].IsExpanded);
    }

    [Fact]
    public void Deletion_and_reinsertion_of_same_model_does_not_restore_old_request_identity()
    {
        using var scene = new Scene();
        var oldId = scene.Id(); var root = scene.Roots[0]; scene.Coordinator.Expand(oldId);
        scene.Roots.RemoveAt(0); scene.Roots.Add(root); var newId = scene.Id();
        Assert.NotEqual(oldId, newId); Assert.True(scene.Loader.Requests[0].Context.Cancellation.IsCancellationRequested);
        scene.Loader.Complete(0, new Node("old identity")); scene.Dispatcher.Drain();
        Assert.Empty(root.Children);
        scene.Coordinator.Expand(newId); scene.Loader.Complete(1, new Node("new identity")); scene.Dispatcher.Drain();
        Assert.Equal("new identity", Assert.Single(root.Children).Name);
    }

    [Fact]
    public void Replacing_source_cancels_old_generation_and_only_new_source_receives_children()
    {
        using var scene = new Scene();
        scene.Coordinator.Expand(scene.Id());
        var newRoot = new Node("new root", remote: true);
        using var replacement = CreateSource(new ObservableCollection<Node> { newRoot });
        scene.Coordinator.SetSource(replacement);
        Assert.True(scene.Loader.Requests[0].Context.Cancellation.IsCancellationRequested);
        scene.Loader.Complete(0, new Node("old response")); scene.Dispatcher.Drain();
        Assert.Empty(scene.Roots[0].Children); Assert.Empty(newRoot.Children);
        replacement.TryGetNodeId(new IndexPath(0), out var id);
        scene.Coordinator.Expand(id); scene.Loader.Complete(1, new Node("new response")); scene.Dispatcher.Drain();
        Assert.Equal("new response", Assert.Single(newRoot.Children).Name);
        scene.Coordinator.Dispose();
    }

    [Fact]
    public void Dispose_cancels_and_ignores_responses_that_arrive_after_owner_release()
    {
        using var scene = new Scene();
        scene.Coordinator.Expand(scene.Id()); scene.Coordinator.Dispose();
        Assert.True(scene.Loader.Requests[0].Context.Cancellation.IsCancellationRequested);
        scene.Source.Dispose();
        scene.Loader.Complete(0, new Node("after dispose")); scene.Dispatcher.Drain();
        Assert.Empty(scene.Roots[0].Children); Assert.Equal(0, scene.Coordinator.PendingCount);
    }

    [Fact]
    public void Old_response_arriving_after_new_response_cannot_overwrite_the_new_request()
    {
        using var scene = new Scene();
        var first = scene.Coordinator.Expand(scene.Id());
        var latest = scene.Coordinator.Reload(scene.Id()); Assert.True(latest > first);
        scene.Loader.Complete(1, new Node("latest")); scene.Dispatcher.Drain();
        scene.Loader.Complete(0, new Node("old")); scene.Dispatcher.Drain();
        Assert.Equal("latest", Assert.Single(scene.Roots[0].Children).Name);
        Assert.Equal(ProbeState.Cached, scene.Coordinator.State(scene.Id()));
    }

    [Fact]
    public void Independent_node_request_survives_unrelated_source_projection_version_change()
    {
        using var scene = new Scene(2);
        scene.Coordinator.Expand(scene.Id(0)); scene.Coordinator.Expand(scene.Id(1));
        var version = scene.Source.Version;
        scene.Loader.Complete(0, new Node("first child")); scene.Dispatcher.Drain(); Assert.True(scene.Source.Version > version);
        scene.Loader.Complete(1, new Node("second child")); scene.Dispatcher.Drain();
        Assert.Single(scene.Roots[0].Children); Assert.Single(scene.Roots[1].Children);
        Assert.Equal(0, scene.Coordinator.PendingCount);
    }

    [Fact]
    public void Explicit_reload_replaces_cached_children_and_failure_keeps_prior_successful_data()
    {
        using var scene = new Scene();
        scene.Coordinator.Expand(scene.Id()); scene.Loader.Complete(0, new Node("cached")); scene.Dispatcher.Drain();
        scene.Coordinator.Reload(scene.Id()); scene.Loader.Fail(1, new InvalidOperationException("refresh failed")); scene.Dispatcher.Drain();
        Assert.Equal("cached", Assert.Single(scene.Roots[0].Children).Name);
        scene.Coordinator.Reload(scene.Id()); scene.Loader.Complete(2, new Node("refreshed")); scene.Dispatcher.Drain();
        Assert.Equal("refreshed", Assert.Single(scene.Roots[0].Children).Name);
        Assert.Equal(ProbeState.Cached, scene.Coordinator.State(scene.Id()));
    }

    [Fact]
    public void Worker_completion_only_posts_and_creating_thread_performs_the_model_write()
    {
        using var scene = new Scene(); var owner = Environment.CurrentManagedThreadId;
        scene.Coordinator.Expand(scene.Id());
        var worker = new Thread(() => scene.Loader.Complete(0, new Node("worker result")));
        worker.Start(); Assert.True(worker.Join(TimeSpan.FromSeconds(2)));
        Assert.Empty(scene.Roots[0].Children);
        scene.Dispatcher.Drain();
        Assert.Single(scene.Roots[0].Children); Assert.Equal(owner, scene.Coordinator.LastApplyThreadId);
    }

    private static HierarchicalTreeDataGridSource<Node> CreateSource(ObservableCollection<Node> roots) => new(roots, node => node.Children,
        new HierarchicalTreeDataGridSourceOptions<Node> { HasChildrenSelector = node => node.Remote });

    private sealed class Scene : IDisposable
    {
        public Scene(int roots = 1)
        {
            Roots = new(Enumerable.Range(0, roots).Select(index => new Node($"root {index}", remote: true)));
            Source = CreateSource(Roots); Coordinator = new(Source, Loader.Load, Dispatcher);
        }
        public ObservableCollection<Node> Roots { get; }
        public HierarchicalTreeDataGridSource<Node> Source { get; }
        public ManualDispatcher Dispatcher { get; } = new();
        public FakeLoader Loader { get; } = new();
        public ProbeCoordinator Coordinator { get; }
        public NodeId Id(int root = 0) { Assert.True(Source.TryGetNodeId(new IndexPath(root), out var id)); return id; }
        public void Dispose() { Coordinator.Dispose(); Source.Dispose(); }
    }

    private sealed class Node(string name, bool remote = false) : INotifyPropertyChanged
    {
        public string Name { get; } = name;
        public bool Remote { get; } = remote;
        public ObservableCollection<Node> Children { get; private set; } = [];
        public event PropertyChangedEventHandler? PropertyChanged;
        public void ReplaceChildren(IReadOnlyList<Node> children)
        {
            // This is a controlled test model, not an arbitrary business setter/transaction adapter.
            Children = new(children); PropertyChanged?.Invoke(this, new(nameof(Children)));
        }
    }

    private sealed record LoadContext(TreeDataGridSource Source, long SourceGeneration, NodeId Node, object Model,
        long RequestVersion, long ProjectionVersion, CancellationToken Cancellation);
    private sealed record FakeRequest(LoadContext Context, TaskCompletionSource<IReadOnlyList<Node>> Completion);
    private sealed class FakeLoader
    {
        public List<FakeRequest> Requests { get; } = [];
        public Task<IReadOnlyList<Node>> Load(LoadContext request)
        {
            var completion = new TaskCompletionSource<IReadOnlyList<Node>>();
            Requests.Add(new(request, completion)); return completion.Task;
        }
        // Deliberately ignores cancellation: the coordinator must independently reject stale responses.
        public void Complete(int request, params Node[] children) => Requests[request].Completion.SetResult(children);
        public void Fail(int request, Exception error) => Requests[request].Completion.SetException(error);
    }

    private sealed class ManualDispatcher
    {
        private readonly int _owner = Environment.CurrentManagedThreadId;
        private readonly Queue<Action> _actions = new();
        public void Post(Action action) { lock (_actions) _actions.Enqueue(action); }
        public void VerifyAccess()
        {
            if (_owner != Environment.CurrentManagedThreadId) throw new InvalidOperationException("Wrong creating thread.");
        }
        public void Drain()
        {
            VerifyAccess();
            while (true)
            {
                Action action;
                lock (_actions) { if (!_actions.TryDequeue(out action!)) return; }
                action();
            }
        }
    }

    private enum ProbeState { Loading, Cached, Error }
    private sealed class ProbeCoordinator : IDisposable
    {
        private sealed record Pending(LoadContext Context, CancellationTokenSource Cancellation);
        private readonly Func<LoadContext, Task<IReadOnlyList<Node>>> _loader;
        private readonly ManualDispatcher _dispatcher;
        private readonly Dictionary<NodeId, Pending> _pending = [];
        private readonly Dictionary<NodeId, ProbeState> _states = [];
        private TreeDataGridSource? _source;
        private long _sourceGeneration;
        private long _requestVersion;
        private bool _disposed;
        public ProbeCoordinator(TreeDataGridSource source, Func<LoadContext, Task<IReadOnlyList<Node>>> loader, ManualDispatcher dispatcher)
        { _loader = loader; _dispatcher = dispatcher; SetSource(source); }
        public int PendingCount { get { _dispatcher.VerifyAccess(); return _pending.Count; } }
        public int? LastApplyThreadId { get; private set; }
        public ProbeState? State(NodeId node) { _dispatcher.VerifyAccess(); return _states.TryGetValue(node, out var state) ? state : null; }

        public void SetSource(TreeDataGridSource source)
        {
            VerifyMutation();
            if (_source is { } old) old.ProjectionChanged -= ProjectionChanged;
            CancelAll(); _sourceGeneration++; _states.Clear(); _source = source;
            source.ProjectionChanged += ProjectionChanged;
        }
        public long Expand(NodeId node) => Expand(node, forceReload: false);
        private long Expand(NodeId node, bool forceReload)
        {
            VerifyMutation(); var source = _source!;
            if (source.IsRefreshDeferred || !source.TryGetRow(node, out var row)) return 0;
            if (_pending.TryGetValue(node, out var pending)) return pending.Context.RequestVersion;
            source.SetExpanded(node, true);
            if (!ReferenceEquals(_source, source) || !source.TryGetRow(node, out row)) return 0;
            if (!forceReload && (_states.TryGetValue(node, out var state) && state == ProbeState.Cached
                || ((Node)row!.Model).Children.Count > 0 || !((Node)row.Model).Remote))
            { _states[node] = ProbeState.Cached; return 0; }
            var cancellation = new CancellationTokenSource();
            var context = new LoadContext(source, _sourceGeneration, node, row!.Model, ++_requestVersion, source.Version, cancellation.Token);
            pending = new(context, cancellation); _pending.Add(node, pending); _states[node] = ProbeState.Loading;
            try
            {
                var task = _loader(context);
                _ = task.ContinueWith(completed => _dispatcher.Post(() => Complete(pending, completed)), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
            catch { _pending.Remove(node); cancellation.Dispose(); _states[node] = ProbeState.Error; }
            return context.RequestVersion;
        }
        public long Reload(NodeId node) { VerifyMutation(); Cancel(node); _states.Remove(node); return Expand(node, forceReload: true); }
        public void Collapse(NodeId node)
        {
            VerifyMutation(); Cancel(node);
            _source?.SetExpanded(node, false);
        }
        public void Dispose()
        {
            _dispatcher.VerifyAccess(); if (_disposed) return;
            _disposed = true;
            if (_source is { } source) source.ProjectionChanged -= ProjectionChanged;
            _source = null; _sourceGeneration++; CancelAll(); _states.Clear();
        }

        private void Complete(Pending pending, Task<IReadOnlyList<Node>> task)
        {
            _dispatcher.VerifyAccess(); var context = pending.Context; var source = _source;
            if (task.IsFaulted) _ = task.Exception;
            if (_disposed || !ReferenceEquals(source, context.Source) || _sourceGeneration != context.SourceGeneration
                || !_pending.TryGetValue(context.Node, out var current) || !ReferenceEquals(current, pending)
                || context.Cancellation.IsCancellationRequested) return;
            if (source!.IsRefreshDeferred || !source.TryGetRow(context.Node, out var row) || !ReferenceEquals(row!.Model, context.Model))
            { Cancel(context.Node); return; }
            _pending.Remove(context.Node); pending.Cancellation.Dispose();
            if (task.IsCanceled) { _states.Remove(context.Node); return; }
            if (task.IsFaulted) { _ = task.Exception; _states[context.Node] = ProbeState.Error; return; }
            var children = task.GetAwaiter().GetResult();
            _states[context.Node] = ProbeState.Cached;
            LastApplyThreadId = Environment.CurrentManagedThreadId;
            ((Node)row!.Model).ReplaceChildren(children);
        }
        private void ProjectionChanged(object? sender, ProjectionChangedEventArgs args)
        {
            _dispatcher.VerifyAccess();
            if (!ReferenceEquals(sender, _source)) return;
            foreach (var node in _pending.Keys.Where(node => !_source!.TryGetRow(node, out _)).ToArray()) Cancel(node);
            foreach (var node in _states.Keys.Where(node => !_source!.TryGetRow(node, out _)).ToArray()) _states.Remove(node);
        }
        private void Cancel(NodeId node)
        {
            if (!_pending.Remove(node, out var pending)) return;
            _states.Remove(node);
            pending.Cancellation.Cancel(); pending.Cancellation.Dispose();
        }
        private void CancelAll() { foreach (var node in _pending.Keys.ToArray()) Cancel(node); }
        private void VerifyMutation() { _dispatcher.VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }
    }
}
