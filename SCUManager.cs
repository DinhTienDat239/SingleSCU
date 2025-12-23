using System;
using System.Collections.Generic;
using DAT.Core.DesignPatterns;
using UnityEngine;

public class SCUManager : Singleton<SCUManager>
{
    public enum SCUUpdateType : byte
    {
        Update = 0,
        LateUpdate = 1,
        FixedUpdate = 2
    }

    public readonly struct SCUSubscription : IDisposable
    {
        internal readonly int Id;

        internal SCUSubscription(int id)
        {
            Id = id;
        }

        public bool IsValid
        {
            get { return Id != 0; }
        }

        public void Dispose()
        {
            if (IsValid && HasInstance)
            {
                Instance.Unregister(this);
            }
        }
    }

    private struct Entry
    {
        public int id;
        public SCUUpdateType type;
        public Action tick;
    }

    // ===== Lists theo từng phase =====
    private readonly List<Entry> _update = new List<Entry>(128);
    private readonly List<Entry> _late = new List<Entry>(128);
    private readonly List<Entry> _fixed = new List<Entry>(128);

    // id -> (type, index)
    private readonly Dictionary<int, Tuple<SCUUpdateType, int>> _index =
        new Dictionary<int, Tuple<SCUUpdateType, int>>(256);

    private readonly List<Entry> _pendingAdd = new List<Entry>(64);
    private readonly List<int> _pendingRemove = new List<int>(64);

    private bool _isTicking;
    private int _aliveCount;
    private int _nextId = 1;

    protected override void Awake()
    {
        base.Awake();
        enabled = false;
    }

    private void Update()
    {
        Tick(SCUUpdateType.Update);
    }

    private void LateUpdate()
    {
        Tick(SCUUpdateType.LateUpdate);
    }

    private void FixedUpdate()
    {
        Tick(SCUUpdateType.FixedUpdate);
    }

    // =========================================================
    // REGISTER OVERLOADS
    // =========================================================

    public SCUSubscription Register(Action action, SCUUpdateType type)
    {
        if (action == null) return default(SCUSubscription);
        return RegisterInternal(action, type);
    }

    public SCUSubscription Register<T1>(Action<T1> action, T1 a1, SCUUpdateType type)
    {
        if (action == null) return default(SCUSubscription);
        return RegisterInternal(() => action(a1), type);
    }

    public SCUSubscription Register<T1, T2>(Action<T1, T2> action, T1 a1, T2 a2, SCUUpdateType type)
    {
        if (action == null) return default(SCUSubscription);
        return RegisterInternal(() => action(a1, a2), type);
    }

    // =========================================================
    // UNREGISTER
    // =========================================================

    public void Unregister(SCUSubscription sub)
    {
        if (!sub.IsValid) return;
        if (!_index.ContainsKey(sub.Id)) return;

        _aliveCount--;

        if (_isTicking)
        {
            _pendingRemove.Add(sub.Id);
        }
        else
        {
            RemoveNow(sub.Id);
        }

        if (_aliveCount <= 0)
        {
            enabled = false;
        }
    }

    // =========================================================
    // CORE
    // =========================================================

    private SCUSubscription RegisterInternal(Action tick, SCUUpdateType type)
    {
        int id = AllocId();

        Entry entry = new Entry();
        entry.id = id;
        entry.type = type;
        entry.tick = tick;

        _aliveCount++;
        if (!enabled) enabled = true;

        if (_isTicking)
        {
            _pendingAdd.Add(entry);
        }
        else
        {
            AddNow(entry);
        }

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

        List<Entry> list = GetList(type);
        for (int i = 0; i < list.Count; i++)
        {
            Entry e = list[i];
            if (!_index.ContainsKey(e.id)) continue;

            try
            {
                if (e.tick != null) e.tick.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        _isTicking = false;
        FlushPending();

        if (_aliveCount <= 0) enabled = false;
    }

    private void FlushPending()
    {
        for (int i = 0; i < _pendingRemove.Count; i++)
        {
            RemoveNow(_pendingRemove[i]);
        }
        _pendingRemove.Clear();

        for (int i = 0; i < _pendingAdd.Count; i++)
        {
            AddNow(_pendingAdd[i]);
        }
        _pendingAdd.Clear();
    }

    private void AddNow(Entry e)
    {
        List<Entry> list = GetList(e.type);
        int index = list.Count;
        list.Add(e);
        _index[e.id] = new Tuple<SCUUpdateType, int>(e.type, index);
    }

    private void RemoveNow(int id)
    {
        Tuple<SCUUpdateType, int> info;
        if (!_index.TryGetValue(id, out info))
            return;

        List<Entry> list = GetList(info.Item1);
        int removeIndex = info.Item2;
        int lastIndex = list.Count - 1;

        if (removeIndex != lastIndex)
        {
            Entry last = list[lastIndex];
            list[removeIndex] = last;
            _index[last.id] = new Tuple<SCUUpdateType, int>(info.Item1, removeIndex);
        }

        list.RemoveAt(lastIndex);
        _index.Remove(id);
    }

    private List<Entry> GetList(SCUUpdateType type)
    {
        if (type == SCUUpdateType.Update) return _update;
        if (type == SCUUpdateType.LateUpdate) return _late;
        return _fixed;
    }

    private int AllocId()
    {
        int id = _nextId++;
        if (_nextId == int.MaxValue) _nextId = 1;
        if (id == 0) id = _nextId++;
        return id;
    }
}
