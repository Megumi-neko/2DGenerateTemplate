using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用 GameObject 对象池
/// 用于管理大量 UI 元素的创建和回收，减少频繁实例化/销毁的性能开销
/// 支持预热（Prewarm）和自动扩容
/// 技术选型：使用对象池管理大量 UI 元素
/// </summary>
public class GameObjectPool : IDisposable
{
    /// <summary>对象池队列，先进先出</summary>
    private readonly Queue<GameObject> _pool = new Queue<GameObject>();

    /// <summary>创建新对象的工厂方法</summary>
    private readonly Func<GameObject> _createFunc;

    /// <summary>从池中取出时的回调（激活、重置状态）</summary>
    private readonly Action<GameObject> _onGet;

    /// <summary>归还到池中时的回调（隐藏、清理状态）</summary>
    private readonly Action<GameObject> _onReturn;

    /// <summary>对象的父级 Transform，用于组织层级</summary>
    private readonly Transform _parent;

    /// <summary>池的最大容量，超过时直接销毁</summary>
    private readonly int _maxSize;

    /// <summary>当前池中可用对象数量</summary>
    public int CountInactive => _pool.Count;

    /// <summary>
    /// 构造对象池
    /// </summary>
    /// <param name="createFunc">创建新对象的工厂方法</param>
    /// <param name="parent">对象父级 Transform，用于组织层级</param>
    /// <param name="onGet">从池中取出时的回调</param>
    /// <param name="onReturn">归还到池中时的回调</param>
    /// <param name="maxSize">池的最大容量，超过时直接销毁（默认 100）</param>
    public GameObjectPool(
        Func<GameObject> createFunc,
        Transform parent,
        Action<GameObject> onGet = null,
        Action<GameObject> onReturn = null,
        int maxSize = 100)
    {
        _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc), "对象池创建方法不能为空");
        _parent = parent;
        _onGet = onGet;
        _onReturn = onReturn;
        _maxSize = maxSize;
    }

    /// <summary>
    /// 从池中获取一个对象
    /// 如果池中有可用对象则取出，否则创建新对象
    /// </summary>
    /// <returns>可用的 GameObject</returns>
    public GameObject Get()
    {
        GameObject obj;

        if (_pool.Count > 0)
        {
            obj = _pool.Dequeue();
        }
        else
        {
            obj = _createFunc();
            if (_parent != null)
            {
                obj.transform.SetParent(_parent, false);
            }
        }

        _onGet?.Invoke(obj);
        return obj;
    }

    /// <summary>
    /// 将对象归还到池中
    /// 如果池已满则直接销毁对象
    /// </summary>
    /// <param name="obj">要归还的 GameObject</param>
    public void Return(GameObject obj)
    {
        if (obj == null) return;

        _onReturn?.Invoke(obj);

        if (_pool.Count >= _maxSize)
        {
            // 池已满，直接销毁
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            return;
        }

        if (_parent != null)
        {
            obj.transform.SetParent(_parent, false);
        }

        _pool.Enqueue(obj);
    }

    /// <summary>
    /// 预热池，预先创建指定数量的对象
    /// 适用于已知需要大量对象的场景（如弹幕、广告）
    /// </summary>
    /// <param name="count">预创建数量</param>
    public void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var obj = _createFunc();
            if (_parent != null)
            {
                obj.transform.SetParent(_parent, false);
            }
            _onReturn?.Invoke(obj);
            _pool.Enqueue(obj);
        }
    }

    /// <summary>
    /// 清空池中所有对象
    /// </summary>
    public void Clear()
    {
        foreach (var obj in _pool)
        {
            if (obj != null && Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
        }
        _pool.Clear();
    }

    /// <summary>
    /// 释放池资源，销毁所有缓存对象
    /// </summary>
    public void Dispose()
    {
        Clear();
    }
}