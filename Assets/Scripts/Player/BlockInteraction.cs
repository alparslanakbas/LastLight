using LastLight.Items;
using LastLight.Skills;
using LastLight.Voxel;
using LastLight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight.Player
{
    /// <summary>
    /// Bakilan blogu bulur, kirar ve yerine blok koyar.
    /// Secim kutusu LineRenderer ile ciziliyor - hazir bir "outline" varligi
    /// gerektirmiyor, varlik butcesi sifir oldugu icin bu onemli.
    /// </summary>
    public sealed class BlockInteraction : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] VoxelWorld world;
        [SerializeField] float reach = 5f;
        [SerializeField] PlayerInventory inventory;

        LineRenderer _outline;
        bool _hasTarget;
        Vector3Int _targetBlock;   // bakilan blok
        Vector3Int _placeAt;       // o blogun bos komsusu

        void Awake()
        {
            if (world == null) world = FindAnyObjectByType<VoxelWorld>();
            if (inventory == null) inventory = GetComponent<PlayerInventory>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
            CreateOutline();
        }

        void Update()
        {
            UpdateTarget();

            if (Mouse.current == null || !_hasTarget) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                BlockId broken = world.GetBlock(_targetBlock.x, _targetBlock.y, _targetBlock.z);
                world.SetBlock(_targetBlock.x, _targetBlock.y, _targetBlock.z, BlockId.Air);

                // Kirilan blok envantere. Envanter doluysa fazlasi kayboluyor -
                // yere dusen esya nesnesi henuz yok.
                var drop = ItemDatabase.DropFor(broken);
                if (drop != ItemId.None && inventory != null)
                {
                    int amount = 1;
                    float extra = PlayerSkills.Instance?.State.ExtraDropChance ?? 0f;
                    if (extra > 0f && Random.value < extra) amount++;
                    inventory.Inventory.Add(drop, amount);
                }

                // Yapisal kontrol SetBlock'un icinden degil buradan cagriliyor:
                // cokme sirasinda SetBlock tekrar cagrildigi icin ic ice
                // degerlendirme ve sonsuz dongu riski olurdu.
                StructuralIntegrity.Evaluate(world, _targetBlock);
            }

            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                // Kendi durdugumuz yere blok koymayi engelle - yoksa oyuncu
                // kendini bloklarin icine hapsediyor.
                if (!OverlapsPlayer(_placeAt) && inventory != null)
                {
                    var stack = inventory.Inventory.Selected;

                    // Mesale blok degil isik nesnesi olarak yerlesiyor:
                    // blok olsaydi chunk mesh'ine girer ve her sonusunde
                    // chunk yeniden uretilirdi.
                    if (stack.Id == ItemId.Torch)
                    {
                        float drain = PlacedLight.DrainRateAt(
                            _placeAt.x, _placeAt.z, world.WorldSizeX, world.WorldSizeZ);
                        PlacedLight.Spawn((Vector3)_placeAt + new Vector3(0.5f, 0.25f, 0.5f), drain);
                        inventory.Inventory.RemoveAt(inventory.Inventory.SelectedIndex);
                    }
                    else if (!stack.IsEmpty && ItemDatabase.IsPlaceable(stack.Id))
                    {
                        world.SetBlock(_placeAt.x, _placeAt.y, _placeAt.z, ItemDatabase.BlockFor(stack.Id));
                        inventory.Inventory.RemoveAt(inventory.Inventory.SelectedIndex);
                        StructuralIntegrity.Evaluate(world, _placeAt);
                    }
                }
            }

            // Fare tekerlegi hotbar slotunu degistirir.
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && inventory != null)
                inventory.Inventory.ScrollSelection(scroll > 0 ? 1 : -1);
        }

        void UpdateTarget()
        {
            _hasTarget = false;
            if (cameraPivot == null || world == null) return;

            var ray = new Ray(cameraPivot.position, cameraPivot.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, reach))
            {
                _outline.enabled = false;
                return;
            }

            // Carpma noktasi tam yuzeyde oldugu icin yuvarlama hangi tarafa
            // dusecegi belirsiz; normalin yariya kadar icine/disina kayarak
            // hedefi ve komsusunu kesin belirliyoruz.
            _targetBlock = FloorToBlock(hit.point - hit.normal * 0.5f);
            _placeAt = FloorToBlock(hit.point + hit.normal * 0.5f);

            if (!BlockDatabase.IsSolid(world.GetBlock(_targetBlock.x, _targetBlock.y, _targetBlock.z)))
            {
                _outline.enabled = false;
                return;
            }

            _hasTarget = true;
            DrawOutline(_targetBlock);
        }

        static Vector3Int FloorToBlock(Vector3 p) => new(
            Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));

        bool OverlapsPlayer(Vector3Int block)
        {
            var cc = GetComponent<CharacterController>();
            if (cc == null) return false;

            Vector3 center = transform.position + cc.center;
            var playerBounds = new Bounds(center, new Vector3(cc.radius * 2f, cc.height, cc.radius * 2f));
            var blockBounds = new Bounds((Vector3)block + Vector3.one * 0.5f, Vector3.one);
            return playerBounds.Intersects(blockBounds);
        }

        // ---------- Secim kutusu ----------

        void CreateOutline()
        {
            var go = new GameObject("BlockOutline");
            go.transform.SetParent(transform, false);

            _outline = go.AddComponent<LineRenderer>();
            _outline.useWorldSpace = true;
            _outline.loop = false;
            _outline.widthMultiplier = 0.02f;
            _outline.material = new Material(Shader.Find("Sprites/Default"));
            _outline.startColor = _outline.endColor = new Color(0f, 0f, 0f, 0.8f);
            _outline.positionCount = 16;
            _outline.enabled = false;
            _outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void DrawOutline(Vector3Int b)
        {
            Vector3 o = b;
            const float e = 0.002f;   // yuzeyle cakismasin diye cok kucuk sisirme
            Vector3 lo = o - Vector3.one * e;
            Vector3 hi = o + Vector3.one * (1f + e);

            // Kupun 12 kenarini tek kirik cizgiyle dolasan sira (bazi kenarlar
            // iki kez geciliyor; 12 ayri LineRenderer'dan ucuz).
            var p = new[]
            {
                new Vector3(lo.x, lo.y, lo.z), new Vector3(hi.x, lo.y, lo.z),
                new Vector3(hi.x, lo.y, hi.z), new Vector3(lo.x, lo.y, hi.z),
                new Vector3(lo.x, lo.y, lo.z), new Vector3(lo.x, hi.y, lo.z),
                new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, lo.y, lo.z),
                new Vector3(hi.x, hi.y, lo.z), new Vector3(hi.x, hi.y, hi.z),
                new Vector3(hi.x, lo.y, hi.z), new Vector3(hi.x, hi.y, hi.z),
                new Vector3(lo.x, hi.y, hi.z), new Vector3(lo.x, lo.y, hi.z),
                new Vector3(lo.x, hi.y, hi.z), new Vector3(lo.x, hi.y, lo.z),
            };

            _outline.positionCount = p.Length;
            _outline.SetPositions(p);
            _outline.enabled = true;
        }

        void OnGUI()
        {
            // Nisangah + secili blok tipi. Gecici cozum: UI sistemi kurulunca tasinacak.
            const float s = 4f;
            var c = new Rect(Screen.width * 0.5f - s * 0.5f, Screen.height * 0.5f - s * 0.5f, s, s);
            GUI.color = Color.white;
            GUI.DrawTexture(c, Texture2D.whiteTexture);
        }
    }
}
