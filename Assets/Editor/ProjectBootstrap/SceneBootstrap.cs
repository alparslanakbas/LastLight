using System.IO;
using LastLight.Items;
using LastLight.Player;
using LastLight.Skills;
using LastLight.UI;
using LastLight.Voxel;
using LastLight.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

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

            Material mat = CreateBlockMaterial();
            SetupLighting();
            GameObject world = CreateWorld(mat);
            CreatePlayer(world.GetComponent<VoxelWorld>());
            CreateUI();
            CreatePostProcessing();
            EnsureAmbientOcclusion();
            CreateFoliage();
            CreateSkybox();
            CreateEnemySystem();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[SceneBootstrap] Sahne kuruldu: " + world.name + " + Player, atlas malzemesi.");
        }

        // ---------- Malzemeler ----------

        static Material CreateBlockMaterial()
        {
            if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

            // Atlasi indirilen gercek dokulardan uretiyoruz. Kaynak klasoru
            // yoksa prosedurel uretece dusuyoruz ki kurulum yine de tamamlansin.
            const string atlasPath = "Assets/Textures/BlockAtlas.png";
            const string normalPath = "Assets/Textures/BlockAtlasNormal.png";

            if (Directory.Exists("Assets/Textures/Source"))
                BlockAtlasBuilder.Build();
            else if (AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath) == null)
                TextureAtlasGenerator.Generate();

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            var atlasNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[SceneBootstrap] URP/Lit shader bulunamadi.");
                return null;
            }

            const string path = MaterialDir + "/BlockAtlas.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.shader = shader;
            mat.SetTexture("_BaseMap", atlas);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.04f);   // voxel yuzeyler mat olmali

            // Agaclar bu malzemeyi DrawMeshInstanced ile kullaniyor; instancing
            // kapaliyken cizim InvalidOperationException atiyor.
            mat.enableInstancing = true;

            // Normal haritasi duz yuzeyde catlak ve kabartma hissi uretiyor;
            // dokusuz halden sonraki en buyuk gorsel fark bundan geliyor.
            if (atlasNormal != null)
            {
                mat.SetTexture("_BumpMap", atlasNormal);
                mat.SetFloat("_BumpScale", 1.1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        // ---------- Dunya ----------

        static GameObject CreateWorld(Material mat)
        {
            var existing = GameObject.Find("VoxelWorld");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("VoxelWorld");
            var world = go.AddComponent<VoxelWorld>();

            // Alanlar private [SerializeField] oldugu icin SerializedObject uzerinden
            // yaziliyor; reflection ile yazmak Undo/dirty takibini bozar.
            var so = new SerializedObject(world);
            so.FindProperty("blockMaterial").objectReferenceValue = mat;
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
            player.AddComponent<LastLight.Enemies.PlayerHealth>();

            var inv = player.AddComponent<PlayerInventory>();

            player.AddComponent<PlayerSkills>();


            // Blok kirma/koyma. Referanslar burada baglaniyor; runtime'da
            // FindAnyObjectByType ile aramak sahne buyudukce pahali.
            var interaction = player.AddComponent<BlockInteraction>();
            var soi = new SerializedObject(interaction);
            soi.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
            soi.FindProperty("world").objectReferenceValue = world;
            soi.FindProperty("inventory").objectReferenceValue = inv;
            soi.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Arayuz ----------

        const string UiDir = "Assets/UI";
        const string PanelSettingsPath = UiDir + "/PanelSettings.asset";
        const string ThemePath = UiDir + "/LastLightTheme.tss";
        const string UxmlPath = UiDir + "/GameMenu/GameMenu.uxml";

        static void CreateUI()
        {
            var existing = GameObject.Find("GameUI");
            if (existing != null) Object.DestroyImmediate(existing);

            // PanelSettings olmadan UI Toolkit hicbir sey cizmiyor.
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }

            // Tema yazi tipini sagliyor; atanmazsa metinler cizilmiyor.
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme != null) panel.themeStyleSheet = theme;

            // Cozunurluk degisince arayuz olcegi bozulmasin diye sabit referans.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            EditorUtility.SetDirty(panel);

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError("[SceneBootstrap] UXML bulunamadi: " + UxmlPath);
                return;
            }

            var go = new GameObject("GameUI");
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = uxml;
            go.AddComponent<GameMenuController>();
        }

        // ---------- Dusmanlar ----------

        static void CreateEnemySystem()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;

            const string matPath = MaterialDir + "/Enemy.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            // Neredeyse siyah ve tamamen mat: dusmanlar bir siluet olarak
            // okunmali. Parlaklik verirsek karanlikta yansiyip yerlerini
            // belli ediyorlar, oysa gorunmemeleri tehdidin bir parcasi.
            mat.shader = shader;
            mat.SetColor("_BaseColor", new Color(0.055f, 0.05f, 0.065f));
            mat.SetFloat("_Smoothness", 0f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            var existing = GameObject.Find("EnemySpawner");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("EnemySpawner");
            var spawner = go.AddComponent<LastLight.Enemies.EnemySpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("enemyMaterial").objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Gokyuzu ----------

        const string SkyHdriPath = "Assets/Textures/Sky/kloppenheim_06.hdr";
        const string SkyMaterialPath = MaterialDir + "/SkyHDRI.mat";

        /// <summary>
        /// HDRI gokyuzu. Unity'nin varsayilan prosedurel skybox'i duz bir
        /// gradyan; HDRI gercek bir gokyuzu fotografı oldugu icin ufuk,
        /// bulut ve renk gecisi tek dosyayla geliyor.
        /// </summary>
        static void CreateSkybox()
        {
            var hdri = AssetDatabase.LoadAssetAtPath<Texture>(SkyHdriPath);
            if (hdri == null)
            {
                Debug.LogWarning("[SceneBootstrap] HDRI bulunamadi: " + SkyHdriPath);
                return;
            }

            // Latlong (panoramik) HDRI: 2D doku olarak, tekrarli sarilmali.
            var imp = AssetImporter.GetAtPath(SkyHdriPath) as TextureImporter;
            if (imp != null)
            {
                imp.textureShape = TextureImporterShape.Texture2D;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }

            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null)
            {
                Debug.LogWarning("[SceneBootstrap] Skybox/Panoramic shader yok.");
                return;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, SkyMaterialPath);
            }

            mat.shader = shader;
            mat.SetTexture("_MainTex", hdri);
            mat.SetFloat("_Exposure", 1.0f);
            mat.SetFloat("_Rotation", 30f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            RenderSettings.skybox = mat;
        }

        // ---------- Bitki ortusu ----------

        static void CreateFoliage()
        {
            const string grassTex = "Assets/Textures/GrassBlade.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(grassTex) == null)
                TextureAtlasGenerator.GenerateGrassBlade();

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(grassTex);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || tex == null) return;

            const string matPath = MaterialDir + "/Foliage.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.shader = shader;
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.05f);

            // Alfa kesme (seffaflik degil): bitki ortusunde siralama sorunu
            // cikarmiyor ve golge dokebiliyor. Seffaf modda binlerce kopya
            // birbirinin uzerine yanlis sirayla ciziliyor.
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.4f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

            // Iki tarafli: cim yaprağı arkadan da gorunmeli.
            mat.SetFloat("_Cull", 0f);

            // DrawMeshInstanced bunu gerektiriyor.
            mat.enableInstancing = true;

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            var existing = GameObject.Find("Foliage");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("Foliage");
            var scatter = go.AddComponent<LastLight.World.FoliageScatter>();
            var so = new SerializedObject(scatter);
            so.FindProperty("foliageMaterial").objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Agaclar blok atlasini kullaniyor: govde ahsap, yapraklar yaprak
            // hucresinden okuyor, ayri malzeme gerekmiyor.
            var blockMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + "/BlockAtlas.mat");
            var trees = go.AddComponent<LastLight.World.TreeScatter>();
            var sot = new SerializedObject(trees);
            sot.FindProperty("treeMaterial").objectReferenceValue = blockMat;
            sot.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Gorsel islem ----------

        const string VolumeProfilePath = "Assets/Settings/LastLightVolume.asset";

        /// <summary>
        /// Post-processing zinciri. Dusuk poli bir oyunun "ucuz" gorunmesinin
        /// asil sebebi geometri degil isik islemenin olmamasi: tonemapping
        /// olmadan renkler yikaniyor, bloom olmadan isik kaynaklari parlamiyor.
        /// </summary>
        static void CreatePostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            // Var olan override'lari temizleyip bastan kuruyoruz; aksi halde
            // her calistirmada ayni efekt tekrar ekleniyor.
            for (int i = profile.components.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(profile.components[i], true);
            profile.components.Clear();

            // ACES: parlak yerleri yakmadan sikistiriyor. Mesale gibi kucuk ama
            // cok parlak isiklarin oldugu bir oyunda sart.
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            // Bloom esigi 1'in uzerinde: sadece gercekten parlak seyler
            // (mesale, gunes) tasiyor, tum sahne bulanmiyor.
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.05f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.7f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.62f;

            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.overrideState = true;
            color.contrast.value = 12f;
            color.saturation.overrideState = true;
            color.saturation.value = 8f;
            color.postExposure.overrideState = true;
            color.postExposure.value = 0.15f;

            // Kenar karartma dikkati merkeze topluyor ve gece hissini guclendiriyor.
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.28f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.45f;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var existing = GameObject.Find("Global Volume");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("Global Volume");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
        }

        /// <summary>
        /// Ekran uzayi ortam okluzyonu. Voxel bir dunyada hacim hissini ureten
        /// tek sey blok koselerinde biriken golge; onsuz her yuzey duz karton
        /// gibi gorunuyor. Renderer varligina bir kez ekleniyor.
        /// </summary>
        static void EnsureAmbientOcclusion()
        {
            string[] rendererPaths =
            {
                "Assets/Settings/PC_Renderer.asset",
                "Assets/Settings/Mobile_Renderer.asset",
            };

            var ssaoType = System.Type.GetType(
                "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");

            if (ssaoType == null)
            {
                Debug.LogWarning("[SceneBootstrap] SSAO tipi bulunamadi, ortam okluzyonu atlandi.");
                return;
            }

            foreach (var path in rendererPaths)
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (data == null) continue;

                bool already = false;
                foreach (var f in data.rendererFeatures)
                    if (f != null && f.GetType() == ssaoType) already = true;
                if (already) continue;

                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
                feature.name = "ScreenSpaceAmbientOcclusion";

                data.rendererFeatures.Add(feature);
                AssetDatabase.AddObjectToAsset(feature, data);
                EditorUtility.SetDirty(data);
                Debug.Log("[SceneBootstrap] SSAO eklendi: " + path);
            }

            AssetDatabase.SaveAssets();
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

        }
    }
}
