using System.IO;
using LastLight.Items;
using LastLight.Player;
using LastLight.Skills;
using LastLight.Voxel;
using LastLight.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectBootstrap
{
    /// <summary>
    /// Oynanabilir test sahnesini programatik kurar. Elle kurmak yerine script
    /// olmasinin sebebi: sahne bozulursa tek komutla yeniden uretilebiliyor ve
    /// hangi degerin neden secildigi kodda kaliyor.
    /// Cagri: -executeMethod ProjectBootstrap.SceneBootstrap.Build
    /// </summary>
    public static class SceneBootstrap
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string MaterialDir = "Assets/Materials";

        /// <summary>Batchmode girisi - isi bitince Editor'u kapatir.</summary>
        public static void Build()
        {
            BuildScene();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Editor acikken menuden calistirmak icin. Batchmode ikinci bir Unity
        /// ornegi acamadigi icin (ayni proje iki surecte acilamaz) sahne
        /// degisiklikleri Editor acikken buradan yapiliyor.
        /// </summary>
        [MenuItem("LastLight/Sahneyi Yeniden Kur")]
        public static void BuildFromMenu()
        {
            BuildScene();
            Debug.Log("[SceneBootstrap] Sahne menuden yeniden kuruldu.");
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Material[] mats = CreateBlockMaterials();
            SetupLighting();
            GameObject world = CreateWorld(mats);
            CreatePlayer(world.GetComponent<VoxelWorld>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[SceneBootstrap] Sahne kuruldu: {world.name} + Player, {mats.Length} malzeme.");
        }

        // ---------- Malzemeler ----------

        static Material[] CreateBlockMaterials()
        {
            if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

            // Dizi indeksi BlockId ile birebir ortusmeli - mesher submesh'leri
            // blok tipi sirasina gore uretiyor.
            var colors = new (string name, Color color, float smoothness)[]
            {
                ("Air",   Color.magenta,                       0f),    // hic cizilmez; indeksi doldurmak icin
                ("Dirt",  new Color(0.42f, 0.31f, 0.20f),      0.05f),
                ("Stone", new Color(0.48f, 0.49f, 0.52f),      0.15f),
                ("Wood",  new Color(0.56f, 0.38f, 0.21f),      0.10f),
                ("Metal", new Color(0.58f, 0.61f, 0.65f),      0.55f),
                ("Concrete", new Color(0.62f, 0.61f, 0.58f),   0.08f),
                ("Road",  new Color(0.20f, 0.20f, 0.22f),      0.20f),
                ("Grass", new Color(0.33f, 0.48f, 0.24f),      0.05f),
                ("Sand",  new Color(0.78f, 0.70f, 0.46f),      0.03f),
                ("Snow",  new Color(0.90f, 0.93f, 0.96f),      0.25f),
                ("Ash",   new Color(0.28f, 0.26f, 0.25f),      0.02f),
                ("Waste", new Color(0.44f, 0.35f, 0.28f),      0.04f),
                ("Leaves",new Color(0.18f, 0.34f, 0.18f),      0.02f),
            };

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[SceneBootstrap] URP/Lit shader bulunamadi.");
                EditorApplication.Exit(1);
                return null;
            }

            var result = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                string path = $"{MaterialDir}/Block_{colors[i].name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                mat.SetColor("_BaseColor", colors[i].color);
                mat.SetFloat("_Smoothness", colors[i].smoothness);
                EditorUtility.SetDirty(mat);
                result[i] = mat;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        // ---------- Dunya ----------

        static GameObject CreateWorld(Material[] mats)
        {
            var existing = GameObject.Find("VoxelWorld");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("VoxelWorld");
            var world = go.AddComponent<VoxelWorld>();

            // Alanlar private [SerializeField] oldugu icin SerializedObject uzerinden
            // yaziliyor; reflection ile yazmak Undo/dirty takibini bozar.
            var so = new SerializedObject(world);
            var arr = so.FindProperty("blockMaterials");
            arr.arraySize = mats.Length;
            for (int i = 0; i < mats.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = mats[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ---------- Oyuncu ----------

        static void CreatePlayer(VoxelWorld world)
        {
            var existing = GameObject.Find("Player");
            if (existing != null) Object.DestroyImmediate(existing);

            var player = new GameObject("Player");
            // Dunya 4x2x4 chunk = 64x32x64 blok; ortasindan ve yuzeyin (max ~14)
            // uzerinden birakiyoruz ki zemine dusup otursun.
            player.transform.position = new Vector3(32f, 24f, 32f);

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.6f;   // 1 blok yuksekligin biraz altinda: tek bloga takilmadan cikar

            var pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);   // goz hizasi

            // Sahnedeki mevcut kamerayi yeniden kullan; yenisini eklersek
            // iki MainCamera olur ve Unity hangisini kullanacagini secemez.
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.transform.SetParent(pivot.transform, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.05f;   // blok icine girince yakin yuzey kirpilmasin
            cam.farClipPlane = 300f;

            var controller = player.AddComponent<PlayerController>();
            var so = new SerializedObject(controller);
            so.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Envanter blok etkilesiminden ONCE ekleniyor: etkilesim bilesenine
            // referans olarak veriliyor.
            var inv = player.AddComponent<PlayerInventory>();

            player.AddComponent<PlayerSkills>();

            var crafting = player.AddComponent<CraftingUI>();
            var soc = new SerializedObject(crafting);
            soc.FindProperty("inventory").objectReferenceValue = inv;
            soc.ApplyModifiedPropertiesWithoutUndo();

            // Blok kirma/koyma. Referanslar burada baglaniyor; runtime'da
            // FindAnyObjectByType ile aramak sahne buyudukce pahali.
            var interaction = player.AddComponent<BlockInteraction>();
            var soi = new SerializedObject(interaction);
            soi.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            soi.FindProperty("world").objectReferenceValue = world;
            soi.FindProperty("inventory").objectReferenceValue = inv;
            soi.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Isik ----------

        static void SetupLighting()
        {
            var light = Object.FindAnyObjectByType<Light>();
            if (light == null)
            {
                var go = new GameObject("Directional Light");
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            // Alcak acili gunes: voxel yuzeylerinde belirgin golge farki uretir,
            // dusuk poli gorunumde derinlik hissini veren sey bu.
            light.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;

            // Gunduz-gece dongusu gunesi kendisi yonetiyor.
            var cycleGo = GameObject.Find("WorldCycle");
            if (cycleGo != null) Object.DestroyImmediate(cycleGo);

            cycleGo = new GameObject("WorldCycle");
            var cycle = cycleGo.AddComponent<DayNightCycle>();
            var so = new SerializedObject(cycle);
            so.FindProperty("sun").objectReferenceValue = light;
            so.ApplyModifiedPropertiesWithoutUndo();

            cycleGo.AddComponent<WorldHUD>();
        }
    }
}
