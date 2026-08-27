using System.Collections.Generic;
using UnityEngine;

public class FSM
{
    private IState _curState;
    private readonly Dictionary<StateEvent, IState> _globalTransitions = new();
    private readonly Dictionary<IState, Dictionary<StateEvent, IState>> _transitions = new();
    private readonly Queue<StateEvent> _eventQueue = new();
    private bool _isProcessingQueue;

    public IState CurState => _curState;

    public void AddTransition(IState fromState, StateEvent evt, IState toState)//注册普通转换
    {

        if (!_transitions.ContainsKey(fromState))
            _transitions[fromState] = new Dictionary<StateEvent, IState>();

        _transitions[fromState][evt] = toState;
    }

    public void AddGlobalTransition(StateEvent evt, IState toState)
    {
        _globalTransitions[evt] = toState;
    }

    public void SetInitialState(IState state)
    {
        _curState = state;
        _curState?.OnEnter();
    }

    public void PostEvent(StateEvent evt)
    {
        _eventQueue.Enqueue(evt);
        if (!_isProcessingQueue)
        {
            _isProcessingQueue = true;
            while (_eventQueue.Count > 0)
            {
                var e = _eventQueue.Dequeue();
                ProcesingEvent(e);
            }
            _isProcessingQueue = false;
        }
    }

    private void ProcesingEvent(StateEvent evt)
    {
        if (_curState == null) return;

        if (_globalTransitions.TryGetValue(evt, out var globalToState))
        {
            SetState(globalToState);
            return;
        }

        if (_transitions.TryGetValue(_curState, out var eventMap) && eventMap.TryGetValue(evt, out var toState))
        {
            SetState(toState);
        }
        else
        {
            //Debug.Log($"[FSM] 事件 '{evt}' 无法在状态 '{_curState.GetType().Name}'触发");
        }
    }

    private void SetState(IState newState)
    {
        _curState?.OnExit();
        _curState = newState;
        _curState?.OnEnter();
    }

    public void Update()
    {
        _curState.OnUpdate();
    }
}
