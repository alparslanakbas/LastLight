using System;
using UnityEngine;

namespace LastLight.Items
{
    /// <summary>
    /// Oyuncunun envanteri. Ilk HotbarSize slot hotbar, gerisi canta.
    /// Tek dizi kullaniliyor: hotbar ile canta arasinda tasima boylece
    /// indeks degistirmekten ibaret, iki ayri koleksiyon senkronu gerekmiyor.
    /// </summary>
    public sealed class Inventory
    {
        public const int HotbarSize = 8;
        public const int BaseSlots = 32;

        // Sabit dizi degil: "Yuk Tasiyici" perk'i kapasiteyi buyutuyor.
        ItemStack[] _slots = new ItemStack[BaseSlots];

        public int SlotCount => _slots.Length;

        /// <summary>Kapasiteyi degistirir. Kucultme mevcut esyayi korumak icin engelli.</summary>
        public void Resize(int newSize)
        {
            if (newSize <= _slots.Length) return;
            System.Array.Resize(ref _slots, newSize);
            Changed?.Invoke();
        }

        public int SelectedIndex { get; private set; }

        public event Action Changed;

        public ItemStack this[int i] => _slots[i];

        public ItemStack Selected => _slots[SelectedIndex];

        public void Select(int index)
        {
            SelectedIndex = Mathf.Clamp(index, 0, HotbarSize - 1);
            Changed?.Invoke();
        }

        public void ScrollSelection(int delta)
        {
            int n = HotbarSize;
            SelectedIndex = ((SelectedIndex + delta) % n + n) % n;   // negatifte de dogru sarar
            Changed?.Invoke();
        }

        /// <summary>
        /// Esya ekler. Once ayni turden yarim yigin arar, sonra bos slot -
        /// tersi olsaydi envanter ayni esyanin bircok kucuk yiginiyla dolardi.
        /// </summary>
        /// <returns>Sigmayan miktar (0 ise tamami alindi).</returns>
        public int Add(ItemId id, int count)
        {
            if (id == ItemId.None || count <= 0) return 0;

            for (int i = 0; i < _slots.Length && count > 0; i++)
            {
                if (_slots[i].Id != id) continue;
                int space = ItemStack.MaxStack - _slots[i].Count;
                if (space <= 0) continue;

                int move = Mathf.Min(space, count);
                _slots[i] = _slots[i].With(_slots[i].Count + move);
                count -= move;
            }

            for (int i = 0; i < _slots.Length && count > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;
                int move = Mathf.Min(ItemStack.MaxStack, count);
                _slots[i] = new ItemStack(id, move);
                count -= move;
            }

            Changed?.Invoke();
            return count;
        }

        /// <summary>Belirli bir slottan esya dusurur.</summary>
        public bool RemoveAt(int index, int count = 1)
        {
            if (index < 0 || index >= _slots.Length) return false;
            if (_slots[index].IsEmpty || _slots[index].Count < count) return false;

            int left = _slots[index].Count - count;
            _slots[index] = left > 0 ? _slots[index].With(left) : ItemStack.Empty;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Kayittan yukleme icin: tum slotlari bosaltir.</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = ItemStack.Empty;
            Changed?.Invoke();
        }

        /// <summary>Kayittan yukleme icin: belirli slota dogrudan yazar.</summary>
        public void SetSlot(int index, ItemId id, int count)
        {
            if (index < 0 || index >= _slots.Length) return;
            _slots[index] = new ItemStack(id, count);
            Changed?.Invoke();
        }

        /// <summary>Envanterin herhangi bir yerinden toplam sayar.</summary>
        public int CountOf(ItemId id)
        {
            int total = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Id == id) total += _slots[i].Count;
            return total;
        }

        /// <summary>Uretim icin: nerede olursa olsun belirtilen miktari tuketir.</summary>
        public bool Consume(ItemId id, int count)
        {
            if (CountOf(id) < count) return false;

            for (int i = 0; i < _slots.Length && count > 0; i++)
            {
                if (_slots[i].Id != id) continue;
                int take = Mathf.Min(_slots[i].Count, count);
                int left = _slots[i].Count - take;
                _slots[i] = left > 0 ? _slots[i].With(left) : ItemStack.Empty;
                count -= take;
            }

            Changed?.Invoke();
            return true;
        }
    }
}
