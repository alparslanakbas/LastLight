using System.IO;
using System.IO.Compression;
using LastLight.Enemies;
using LastLight.Items;
using LastLight.Skills;
using LastLight.Voxel;
using LastLight.World;
using UnityEngine;

namespace LastLight.Persistence
{
    /// <summary>
    /// Oyunu diske yazar ve geri yukler.
    ///
    /// Kaydetmesi olmayan bir oyun satilamaz - oyun kapaninca her sey silinen
    /// bir sey urun degil prototiptir. Bu yuzden gorsel isten once geliyor.
    ///
    /// Dunya tumuyle kaydediliyor, tohum + degisiklik listesi olarak degil.
    /// Delta yaklasimi cok daha kucuk dosya uretirdi ama sehir ve agac
    /// yerlesimi uretim sirasina bagli; sonradan bir uretim adimini
    /// degistirdigimizde eski kayitlar sessizce bozulurdu. Tam kayit buyuk
    /// ama guvenilir - ve GZip ile blok verisi cok iyi sikisiyor cunku
    /// komsu bloklar cogunlukla ayni.
    ///
    /// Ikili bicim kullaniliyor: 500 bin blogu JSON'a yazmak hem devasa
    /// dosya hem saniyeler suren ayristirma demek.
    /// </summary>
    public static class SaveSystem
    {
        // Surum 2: yogunluk alani eklendi (puruzsuz arazi). Surum 1
        // kayitlarinda yogunluk yok ve arazi kupsel uretilmisti; yuklemeye
        // calismak yerine reddetmek dogru - yarim donusturulmus bir dunya
        // hem cirkin hem hatali olurdu.
        const int FormatVersion = 2;
        const string FileName = "lastlight.sav";

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool SaveExists => File.Exists(SavePath);

        // ---------- Yazma ----------

        public static bool Save()
        {
            var world = Object.FindAnyObjectByType<VoxelWorld>();
            if (world == null)
            {
                Debug.LogWarning("[Save] VoxelWorld yok, kayit atlandi.");
                return false;
            }

            try
            {
                using var file = File.Create(SavePath);
                using var gzip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal);
                using var w = new BinaryWriter(gzip);

                w.Write(FormatVersion);

                WriteWorld(w, world);
                WritePlayer(w);
                WriteInventory(w);
                WriteSkills(w);
                WriteCycle(w);
                WriteLights(w);

                Debug.Log("[Save] Kaydedildi: " + SavePath);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Save] Kayit basarisiz: " + e.Message);
                return false;
            }
        }

        static void WriteWorld(BinaryWriter w, VoxelWorld world)
        {
            var chunks = world.Chunks;
            w.Write(chunks.Count);

            foreach (var kv in chunks)
            {
                w.Write(kv.Key.x); w.Write(kv.Key.y); w.Write(kv.Key.z);

                var blocks = kv.Value.Raw;
                var shapes = kv.Value.RawShapes;

                // Blok ve bicim byte olarak: enum'lar zaten byte tabanli,
                // int yazmak dosyayi dort kat buyutur.
                for (int i = 0; i < Chunk.BlockCount; i++) w.Write((byte)blocks[i]);
                for (int i = 0; i < Chunk.BlockCount; i++) w.Write((byte)shapes[i]);
                w.Write(kv.Value.RawDensity, 0, Chunk.BlockCount);
            }
        }

        static void WritePlayer(BinaryWriter w)
        {
            var player = GameObject.Find("Player");
            if (player == null)
            {
                w.Write(false);
                return;
            }

            w.Write(true);
            var p = player.transform.position;
            w.Write(p.x); w.Write(p.y); w.Write(p.z);
            w.Write(player.transform.eulerAngles.y);

            var hp = PlayerHealth.Instance;
            w.Write(hp != null ? hp.Current : 100f);
        }

        static void WriteInventory(BinaryWriter w)
        {
            var pi = Object.FindAnyObjectByType<PlayerInventory>();
            if (pi == null) { w.Write(0); w.Write(0); return; }

            var inv = pi.Inventory;
            w.Write(inv.SlotCount);
            for (int i = 0; i < inv.SlotCount; i++)
            {
                w.Write((byte)inv[i].Id);
                w.Write(inv[i].Count);
            }
            w.Write(inv.SelectedIndex);
        }

        static void WriteSkills(BinaryWriter w)
        {
            var skills = PlayerSkills.Instance;
            if (skills == null) { w.Write(0); return; }

            var owned = new System.Collections.Generic.List<PerkId>();
            foreach (var perk in PerkDatabase.All)
                if (skills.State.Has(perk.Id)) owned.Add(perk.Id);

            w.Write(owned.Count);
            foreach (var id in owned) w.Write((byte)id);
        }

        static void WriteCycle(BinaryWriter w)
        {
            var c = DayNightCycle.Instance;
            if (c == null) { w.Write(1); w.Write(0); w.Write(false); w.Write(0f); return; }

            w.Write(c.DayNumber);
            w.Write(c.NightsSurvived);
            w.Write(c.IsNight);
            w.Write(c.PhaseProgress);
        }

        static void WriteLights(BinaryWriter w)
        {
            var lights = Object.FindObjectsByType<PlacedLight>(FindObjectsSortMode.None);
            w.Write(lights.Length);

            foreach (var l in lights)
            {
                var p = l.transform.position;
                w.Write(p.x); w.Write(p.y); w.Write(p.z);
                w.Write(l.FuelRatio);
            }
        }

        // ---------- Okuma ----------

        public static bool Load()
        {
            if (!SaveExists) return false;

            var world = Object.FindAnyObjectByType<VoxelWorld>();
            if (world == null) return false;

            try
            {
                using var file = File.OpenRead(SavePath);
                using var gzip = new GZipStream(file, CompressionMode.Decompress);
                using var r = new BinaryReader(gzip);

                int version = r.ReadInt32();
                if (version != FormatVersion)
                {
                    // Surum uyusmazliginda yuklemeyi reddetmek, yarim yuklenip
                    // bozuk bir dunyayla devam etmekten iyi.
                    Debug.LogWarning("[Save] Kayit surumu uyusmuyor (" + version + "), yeni oyun baslatilacak.");
                    return false;
                }

                ReadWorld(r, world);
                ReadPlayer(r);
                ReadInventory(r);
                ReadSkills(r);
                ReadCycle(r);
                ReadLights(r, world);

                Debug.Log("[Save] Yuklendi.");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Save] Yukleme basarisiz: " + e.Message);
                return false;
            }
        }

        static void ReadWorld(BinaryReader r, VoxelWorld world)
        {
            // Dunya kayittan geliyor: prosedurel uretimi atliyoruz, yoksa
            // once uretip sonra uzerine yazmak bos is.
            world.MarkGenerated();

            int count = r.ReadInt32();
            var blocks = new BlockId[Chunk.BlockCount];
            var shapes = new BlockShape[Chunk.BlockCount];
            var density = new byte[Chunk.BlockCount];

            for (int c = 0; c < count; c++)
            {
                var coord = new Vector3Int(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());

                for (int i = 0; i < Chunk.BlockCount; i++) blocks[i] = (BlockId)r.ReadByte();
                for (int i = 0; i < Chunk.BlockCount; i++) shapes[i] = (BlockShape)r.ReadByte();
                r.Read(density, 0, Chunk.BlockCount);

                world.ApplyLoadedChunk(coord, blocks, shapes, density);
            }

            world.RebuildDirtyChunks();
        }

        static void ReadPlayer(BinaryReader r)
        {
            if (!r.ReadBoolean()) return;

            float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle();
            float yaw = r.ReadSingle();
            float health = r.ReadSingle();

            var player = GameObject.Find("Player");
            if (player == null) return;

            // CharacterController acikken transform'a yazmak yok sayiliyor.
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = new Vector3(x, y, z);
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cc != null) cc.enabled = true;

            var hp = PlayerHealth.Instance;
            if (hp != null) hp.SetHealth(health);
        }

        static void ReadInventory(BinaryReader r)
        {
            int slots = r.ReadInt32();
            var pi = Object.FindAnyObjectByType<PlayerInventory>();

            if (pi == null)
            {
                // Veriyi yine de tuketmeliyiz, yoksa akis kayiyor.
                for (int i = 0; i < slots; i++) { r.ReadByte(); r.ReadInt32(); }
                if (slots > 0) r.ReadInt32();
                return;
            }

            var inv = pi.Inventory;
            inv.Resize(slots);
            inv.Clear();

            for (int i = 0; i < slots; i++)
            {
                var id = (ItemId)r.ReadByte();
                int amount = r.ReadInt32();
                if (id != ItemId.None && amount > 0) inv.SetSlot(i, id, amount);
            }

            if (slots > 0) inv.Select(r.ReadInt32());
        }

        static void ReadSkills(BinaryReader r)
        {
            int count = r.ReadInt32();
            var skills = PlayerSkills.Instance;

            for (int i = 0; i < count; i++)
            {
                var id = (PerkId)r.ReadByte();
                skills?.State.ForceUnlock(id);
            }
        }

        static void ReadCycle(BinaryReader r)
        {
            int day = r.ReadInt32();
            int nights = r.ReadInt32();
            bool isNight = r.ReadBoolean();
            float phase = r.ReadSingle();

            DayNightCycle.Instance?.RestoreState(day, nights, isNight, phase);
        }

        static void ReadLights(BinaryReader r, VoxelWorld world)
        {
            foreach (var old in Object.FindObjectsByType<PlacedLight>(FindObjectsSortMode.None))
                Object.Destroy(old.gameObject);

            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle();
                float fuelRatio = r.ReadSingle();

                float drain = PlacedLight.DrainRateAt(
                    Mathf.FloorToInt(x), Mathf.FloorToInt(z), world.WorldSizeX, world.WorldSizeZ);

                var light = PlacedLight.Spawn(new Vector3(x, y, z), drain);
                light?.SetFuelRatio(fuelRatio);
            }
        }

        public static void DeleteSave()
        {
            if (SaveExists) File.Delete(SavePath);
        }
    }
}
