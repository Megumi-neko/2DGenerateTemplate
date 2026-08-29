using System;
using UnityEngine;

namespace Game.Building
{
    [AddComponentMenu("Game/Building/Coin Inventory")]
    [DisallowMultipleComponent]
    public sealed class CoinInventory : MonoBehaviour
    {
        [SerializeField, Min(0)] private int initialCoins = 20;

        private int coins;
        private bool initialized;

        public int Coins => coins;
        public event Action<int> CoinsChanged;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            coins = Mathf.Max(0, initialCoins);
            initialized = true;
        }

        public bool CanSpend(int amount)
        {
            return amount >= 0 && coins >= amount;
        }

        public bool TrySpend(int amount)
        {
            Initialize();
            if (!CanSpend(amount))
            {
                return false;
            }

            if (amount == 0)
            {
                return true;
            }

            coins -= amount;
            CoinsChanged?.Invoke(coins);
            return true;
        }

        public void Add(int amount)
        {
            Initialize();
            if (amount <= 0)
            {
                return;
            }

            coins += amount;
            CoinsChanged?.Invoke(coins);
        }

        public void SetCoins(int amount)
        {
            Initialize();
            int sanitizedAmount = Mathf.Max(0, amount);
            if (coins == sanitizedAmount)
            {
                return;
            }

            coins = sanitizedAmount;
            CoinsChanged?.Invoke(coins);
        }

        internal void InitializeForTests(int amount)
        {
            coins = Mathf.Max(0, amount);
            initialized = true;
            CoinsChanged = null;
        }
    }
}
