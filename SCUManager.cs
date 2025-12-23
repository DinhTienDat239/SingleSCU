using System;
using System.Collections.Generic;
using DAT.Core.DesignPatterns;
using UnityEngine;

/// <summary>
/// SCU (Single Central Update) Manager
/// - Register callback từ mọi nơi
/// - Update/LateUpdate/FixedUpdate tập trung
/// - Unregister O(1) bằng token
/// </summary>
public class SCUManager : Singleton<SCUManager>
{
    public enum SCUUpdateType : byte
    {
        Update = 0,
        LateUpdate = 1,
        FixedUpdate = 2
    }

    /// <summary>
    /// Token trả về khi Register.
    /// Dùng để Unregister chắc chắn, không phụ thuộc delegate reference.
    /// </summary>
    public readonly struct SCUSubscription : IDisposable
    {
        internal readonly int Id;

        internal SCUSubscription(int id) => Id = id;

        public bool IsValid => Id != 0;

        public void Dispose()
        {
            if (IsValid && HasInstance)
                Instance.Unregister(this);
        }
    }

    private struct Entry
    {
        public int id;
        public SCUUpdateType type;
        public Action tick;
    }

    // ===== Lists theo từng phase =====
    private readonly List<Entry> _update = new(128);
    private readonly List<Entry> _late = new(128);
    private readonly List<Entry> _fixed = new(128);

    // id -> (type, index) để remove O(1)
    private readonly Dictionary<int, (SCUUpdateType type, int index)> _index = new(256);

    // Deferred operations
    private readonly List<Entry> _pendingAdd = new(64);
    private readonly List<int> _pendingRemove = new(64);

    private bool _isTicking;
    private int _aliveCount;
    private int _nextId = 1;

    protected override void Awake()
    {
        base.Awake();
        enabled = false;
    }

    private void Update() => Tick(SCUUpdateType.Update);
    private void LateUpdate() => Tick(SCUUpdateType.LateUpdate);
    private void FixedUpdate() => Tick(SCUUpdateType.FixedUpdate);

    // =========================================================
    // REGISTER OVERLOADS (0 -> 5 tham số)
    // =========================================================

    public SCUSubscription Register(Action action, SCUUpdateType type)
        => action == null ? default : RegisterInternal(action, type);

    public SCUSubscription Register<T1>(Action<T1> action, T1 a1, SCUUpdateType type)
        => action == null ? default : RegisterInternal(() => action(a1), type);

    public SCUSubscription Register<T1, T2>(Action<T1, T2> action, T1 a1, T2 a2, SCUUpdateType type)
        => action == null ? default : RegisterInternal(() => action(a1, a2), type);

    public SCUSubscription Register<T1, T2, T3>(
        Action<T1, T2, T3> action,
        T1 a1, T2 a2, T3 a3,
        SCUUpdateType type)
        => action == null ? default : RegisterInternal(() => action(a1, a2, a3), type);

    public SCUSubscription Register<T1, T2, T3, T4>(
        Action<T1, T2, T3, T4> action,
        T1 a1, T2 a2, T3 a3, T4 a4,
        SCUUpdateType type)
        => action == null ? default : RegisterInternal(() => action(a1, a2, a3, a4), type);

    public SCUSubscription Register<T1, T2, T3, T4, T5>(
        Action<T1, T2, T3, T4, T5> action,
        T1 a1, T2 a2, T3 a3, T4 a4, T5 a5,
        SCUUpdateType type)
        => action == null ? default : RegisterInternal(() => action(a1, a2, a3, a4, a5), type);

    // =========================================================
    // UNREGISTER
    // =========================================================

    public void Unregister(SCUSubscription sub)
    {
        if (!sub.IsValid) return;
        if (!_index.ContainsKey(sub.Id)) return;

        _aliveCount--;

        if (_isTicking)
            _pendingRemove.Add(sub.Id);
        else
            RemoveNow(sub.Id);

        if (_aliveCount <= 0)
            enabled = false;
    }

    // =========================================================
    // CORE
    // =========================================================

    private SCUSubscription RegisterInternal(Action tick, SCUUpdateType type)
    {
        int id = AllocId();

        var entry = new Entry
        {
            id = id,
            type = type,
            tick = tick
        };

        _aliveCount++;
        if (!enabled) enabled = true;

        if (_isTicking)
            _pendingAdd.Add(entry);
        else
            AddNow(entry);

        return new SCUSubscription(id);
    }

    private void Tick(SCUUpdateType type)
    {
        if (_aliveCount <= 0)
        {
            enabled = false;
            return;
        }

        _isTicking = true;

        var list = GetList(type);
        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (!_index.ContainsKey(e.id)) continue;

            try { e.tick?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

        _isTicking = false;
        FlushPending();

        if (_aliveCount <= 0) enabled = false;
    }

    private void FlushPending()
    {
        for (int i = 0; i < _pendingRemove.Count; i++)
            RemoveNow(_pendingRemove[i]);
        _pendingRemove.Clear();

        for (int i = 0; i < _pendingAdd.Count; i++)
            AddNow(_pendingAdd[i]);
        _pendingAdd.Clear();
    }

    private void AddNow(Entry e)
    {
        var list = GetList(e.type);
        int index = list.Count;
        list.Add(e);
        _index[e.id] = (e.type, index);
    }

    private void RemoveNow(int id)
    {
        if (!_index.TryGetValue(id, out var info))
            return;

        var list = GetList(info.type);
        int removeIndex = info.index;
        int lastIndex = list.Count - 1;

        if (removeIndex != lastIndex)
        {
            var last = list[lastIndex];
            list[removeIndex] = last;
            _index[last.id] = (info.type, removeIndex);
        }

        list.RemoveAt(lastIndex);
        _index.Remove(id);
    }

    private List<Entry> GetList(SCUUpdateType type)
    {
        return type switch
        {
            SCUUpdateType.Update => _update,
            SCUUpdateType.LateUpdate => _late,
            SCUUpdateType.FixedUpdate => _fixed,
            _ => _update
        };
    }

    private int AllocId()
    {
        int id = _nextId++;
        if (_nextId == int.MaxValue) _nextId = 1;
        if (id == 0) id = _nextId++;
        return id;
    }
}
