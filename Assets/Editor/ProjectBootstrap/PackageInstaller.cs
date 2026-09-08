using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectBootstrap
{
    // Paketleri PackageManager Client API uzerinden, headless calisacak sekilde kurar.
    public static class PackageInstaller
    {
        static readonly string[] PackagesToAdd =
        {
            "com.unity.burst",        // chunk mesh uretimini Job System'de derleyip hizlandirir
            "com.unity.collections",  // NativeArray/NativeList - Job icinde GC'siz veri
            "com.unity.mathematics",  // Burst uyumlu vektor/matris matematigi
        };

        static readonly string[] PackagesToRemove = { };

        const double TimeoutSeconds = 600;

        static AddAndRemoveRequest _request;
        static double _deadline;

        public static void Install()
        {
            if (PackagesToAdd.Length == 0 && PackagesToRemove.Length == 0)
            {
                Debug.Log("[PackageInstaller] Yapilacak bir sey yok.");
                EditorApplication.Exit(0);
                return;
            }

            Debug.Log($"[PackageInstaller] Ekleniyor: {string.Join(", ", PackagesToAdd)}");
            _request = Client.AddAndRemove(packagesToAdd: PackagesToAdd, packagesToRemove: PackagesToRemove);
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null) return;

            if (!_request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[PackageInstaller] UPM zaman asimina ugradi.");
                    EditorApplication.Exit(2);
                }
                return;
            }

            EditorApplication.update -= Poll;

            if (_request.Status == StatusCode.Success)
            {
                var names = _request.Result.Select(p => $"{p.name}@{p.version}");
                Debug.Log($"[PackageInstaller] Cozumlendi: {string.Join(", ", names)}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[PackageInstaller] Basarisiz: {_request.Error?.message}");
                EditorApplication.Exit(1);
            }
        }
    }
}
