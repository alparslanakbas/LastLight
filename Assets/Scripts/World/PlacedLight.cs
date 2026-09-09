using LastLight.Skills;
using LastLight.Voxel;
using UnityEngine;

namespace LastLight.World
{
    /// <summary>
    /// Yerlestirilen isik kaynagi (mesale). Yakiti bitince soner ve yok olur.
    ///
    /// Isik blok degil ayri bir nesne: blok olsaydi chunk mesh'ine girer,
    /// her sonusunde chunk'in yeniden uretilmesi gerekirdi. Ayri nesne
    /// olarak yuzlerce mesale bile mesh maliyeti dogurmuyor.
    /// </summary>
    public sealed class PlacedLight : MonoBehaviour
    {
        public const float TorchFuel = 180f;   // saniye - yaklasik bir gecenin dortte biri

        static Mesh _sharedCube;
        static Material _torchMaterial;

        float _fuel;
        float _drainRate;
        Light _light;
        float _baseRange;

        /// <summary>Kalan yakitin oranI (0-1). HUD ve sonme efekti icin.</summary>
        public float FuelRatio => _fuel / (TorchFuel * (PlayerSkills.Instance?.State.FuelDurationMultiplier ?? 1f));

        public static PlacedLight Spawn(Vector3 position, float drainRate)
        {
            var go = new GameObject("Torch");
            go.transform.position = position;

            // Gorsel: kucuk bir kup. Ayri bir model gerektirmiyor.
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = GetCube();
            go.transform.localScale = new Vector3(0.18f, 0.5f, 0.18f);

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var lightGo = new GameObject("Point Light");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.45f);
            l.intensity = 3.2f;
            l.range = 11f;
            // Golge kapali: her mesale golge uretirse bir usde 20 isik
            // kare hizini oldurur, kazanc gorsel olarak kucuk.
            l.shadows = LightShadows.None;

            var pl = go.AddComponent<PlacedLight>();
            float bonus = PlayerSkills.Instance?.State.FuelDurationMultiplier ?? 1f;
            pl._fuel = TorchFuel * bonus;
            pl._drainRate = drainRate;
            pl._light = l;
            pl._baseRange = l.range;
            return pl;
        }

        /// <summary>Kayittan yukleme icin kalan yakiti ayarlar.</summary>
        public void SetFuelRatio(float ratio)
        {
            float bonus = LastLight.Skills.PlayerSkills.Instance?.State.FuelDurationMultiplier ?? 1f;
            _fuel = Mathf.Clamp01(ratio) * TorchFuel * bonus;
        }

        void Update()
        {
            _fuel -= Time.deltaTime * _drainRate;

            if (_fuel <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Son dortte birde kisilarak soner - oyuncuya "yakit bitiyor"
            // uyarisi sayac degil isik kendisi versin.
            float t = Mathf.Clamp01(FuelRatio / 0.25f);
            _light.intensity = Mathf.Lerp(0.6f, 3.2f, t);
            _light.range = Mathf.Lerp(_baseRange * 0.45f, _baseRange, t);
        }

        /// <summary>Konuma gore yakit tuketim hizi - biyomdan geliyor.</summary>
        public static float DrainRateAt(int x, int z, int worldX, int worldZ)
        {
            var biome = BiomeMap.At(x, z, worldX, worldZ);
            return BiomeDatabase.Get(biome).FuelDrain;
        }

        static Mesh GetCube()
        {
            if (_sharedCube != null) return _sharedCube;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _sharedCube = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
            return _sharedCube;
        }

        static Material GetMaterial()
        {
            if (_torchMaterial != null) return _torchMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            _torchMaterial = new Material(shader);
            _torchMaterial.SetColor("_BaseColor", new Color(0.95f, 0.70f, 0.28f));
            // Emisyon: mesale kendi karanliginda da gorunsun.
            _torchMaterial.EnableKeyword("_EMISSION");
            _torchMaterial.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.2f) * 2f);
            return _torchMaterial;
        }
    }
}
