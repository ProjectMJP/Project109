using System;
using System.Collections;
using System.Collections.Generic;

public class EventBus<TEventBase>
{
    private readonly Dictionary<Type, IList> _listeners = new();

    public List<T> GetListeners<T>() where T : TEventBase
    {
        var type = typeof(T);
        if (!_listeners.TryGetValue(type, out var list))
        {
            list = new List<T>();
            _listeners[type] = list;
        }
        return (List<T>)list;
    }

    /// <summary>
    /// 등록된 리스너가 존재할 경우에만 리스트를 반환합니다. (새로운 List 힙 할당 방지)
    /// </summary>
    public bool TryGetListeners<T>(out List<T> listeners) where T : TEventBase
    {
        if (_listeners.TryGetValue(typeof(T), out var list))
        {
            listeners = (List<T>)list;
            return listeners.Count > 0;
        }
        listeners = null;
        return false;
    }

    public void Add<T>(T action) where T : TEventBase
    {
        GetListeners<T>().Add(action);
    }

    public void Remove<T>(T action) where T : TEventBase
    {
        GetListeners<T>().Remove(action);
    }

    public void Invoke<T>(Action<T> invoker) where T : TEventBase
    {
        var listeners = GetListeners<T>();
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            invoker(listeners[i]);
        }
    }

    public void Clear()
    {
        _listeners.Clear();
    }
}
