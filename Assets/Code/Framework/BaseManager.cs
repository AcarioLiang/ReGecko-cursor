using System.Collections;
using UnityEngine;
using System;

namespace ReGecko.Framework
{
    /// <summary>
    /// 管理器基类
    /// </summary>

    public abstract class BaseManager : MonoBehaviour
    {
        protected bool _init = false;


        public virtual void Init() { _init = true; }
        public bool IsInit() { return _init; }
    }
}
