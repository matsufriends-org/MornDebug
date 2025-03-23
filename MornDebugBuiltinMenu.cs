using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MornEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace MornDebug
{
    [CreateAssetMenu(fileName = nameof(MornDebugBuiltinMenu), menuName = "Morn/" + nameof(MornDebugBuiltinMenu))]
    public sealed class MornDebugBuiltinMenu : MornDebugMenuBase
    {
        private class SceneTree : MornEditorTreeBase<EditorBuildSettingsScene>
        {
            public SceneTree(string prefix) : base(prefix)
            {
            }

            protected override string NodeToPath(EditorBuildSettingsScene node)
            {
                return node.path;
            }

            protected override void NodeOnGUI(EditorBuildSettingsScene node)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(node.path);
                }
            }
        }

        [SerializeField] private string _scenePathPrefix;
        private SceneTree _tree;

        public override IEnumerable<(string key, Action action)> GetMenuItems()
        {
            yield return ("セーブマネージャ", () =>
            {
                var cachedEnabled = GUI.enabled;
                GUI.enabled = !Application.isPlaying;
                if (GUILayout.Button("PlayerPrefsをリセット"))
                {
                    PlayerPrefs.DeleteAll();
                }

                GUI.enabled = cachedEnabled;
            });
#if UNITY_EDITOR
            yield return ("リロード", () =>
            {
                var cachedEnabled = GUI.enabled;
                GUI.enabled = !Application.isPlaying;
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Reload Domain"))
                    {
                        EditorUtility.RequestScriptReload();
                    }

                    if (GUILayout.Button("Reload Scene"))
                    {
                        var scene = SceneManager.GetActiveScene();
                        var opts = new LoadSceneParameters();
                        EditorSceneManager.LoadSceneInPlayMode(scene.path, opts);
                    }
                }

                GUI.enabled = cachedEnabled;
            });
#endif
            _tree = new SceneTree(_scenePathPrefix);
            foreach (var scene in EditorBuildSettings.scenes)
            {
                _tree.Add(scene);
            }

            yield return ("シーン一覧", () =>
            {
                var cachedEnabled = GUI.enabled;
                GUI.enabled = !Application.isPlaying;
                _tree.OnGUI();
                GUI.enabled = cachedEnabled;
            });
            yield return ("git/便利系", () =>
            {
                var cachedEnabled = GUI.enabled;
                GUI.enabled = !Application.isPlaying;
                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Submodule更新"))
                    {
                        UpdateSubmoduleAsync().Forget();
                    }

                    if (GUILayout.Button("差分全消し"))
                    {
                        DeleteDiffAsync().Forget();
                    }
                }

                GUI.enabled = cachedEnabled;
            });
        }

        private async UniTask UpdateSubmoduleAsync(CancellationToken ct = default)
        {
            var process = MornProcess.MornProcess.CreateAtAssets("git");
            var stashName = $"{nameof(MornDebug)}による退避 {DateTime.Now:yyyy/MM/dd HH:mm:ss}";
            await process.ExecuteAsync($"stash push -m \"{stashName}\"", ct);
            await process.ExecuteAsync($"submodule foreach --recursive git stash push -m \"{stashName}\"", ct);
            await process.ExecuteAsync("submodule deinit -f --all", ct);
            await process.ExecuteAsync("submodule update --init --recursive", ct);
            process.Dispose();
            MornDebugGlobal.Log("submodule更新完了");
        }

        private async UniTask DeleteDiffAsync(CancellationToken ct = default)
        {
            var process = MornProcess.MornProcess.CreateAtAssets("git");
            var stashName = $"{nameof(MornDebug)}による退避 {DateTime.Now:yyyy/MM/dd HH:mm:ss}";
            await process.ExecuteAsync($"stash push -m \"{stashName}\"", ct);
            await process.ExecuteAsync($"submodule foreach --recursive git stash push -m \"{stashName}\"", ct);
            await process.ExecuteAsync("reset --hard HEAD", ct);
            await process.ExecuteAsync("clean -fd", ct);
            process.Dispose();
            MornDebugGlobal.Log("差分全消し完了");
        }
    }
}