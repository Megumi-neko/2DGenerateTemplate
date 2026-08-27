using System;
using System.Collections.Generic;

public enum StateEvent
{
    EnterIdle,
    EnterMove,
    HoverEnter,
    HoverExit,
    OffScreen,
}
public class EventBus
{
    public static readonly Lazy<EventBus> _instance = new Lazy<EventBus>(() => new EventBus());
    public static EventBus Instance => _instance.Value;

    private EventBus() { }//私有构造函数，防止外部创建
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
        {
            _handlers[type] = new();
        }
        _handlers[type].Add(handler);
    }

    public void UnSubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var list))
        {
            list.Remove(handler);
            if (list.Count == 0)
            {
                _handlers.Remove(type);
            }
        }
    }

    public void Publish<T>(T eventData)
    {
        var type = typeof(T);
        if (!_handlers.TryGetValue(type, out var list)) return;

        var handlers = list.ToArray();

        foreach (var handler in handlers)
        {
            (handler as Action<T>)?.Invoke(eventData);
        }
    }
}