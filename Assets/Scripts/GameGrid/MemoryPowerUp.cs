using System;
using UnityEngine;

namespace GameGrid
{
    /// <summary>
    /// 记忆模式道具基类
    /// </summary>
    public abstract class MemoryPowerUp
    {
        public string powerUpName;
        public float cooldownTime;
        public bool isActive;
        protected float _lastUsedTime;

        protected MemoryPowerUp(string name, float cooldown)
        {
            powerUpName = name;
            cooldownTime = cooldown;
            isActive = false;
            _lastUsedTime = -cooldown;
        }

        public bool CanUse()
        {
            return !isActive && (Time.time - _lastUsedTime >= cooldownTime);
        }

        public void Use()
        {
            if (!CanUse()) return;
            _lastUsedTime = Time.time;
            isActive = true;
            OnUse();
        }

        protected abstract void OnUse();

        public virtual void Update()
        {
        }

        public virtual void Deactivate()
        {
            isActive = false;
        }
    }
}
