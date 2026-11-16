using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace PachiMoji.EditorTools
{
    /// <summary>
    /// 確認用：激アツの帯を本物の書き出しの流れ（VideoExporter）で書き出し、何コマかだけ PNG に残す。
    /// 右下の印（透かし）が帯の動画にも入っているかを見る。captures/obimark_*/f*.png
    /// </summary>
    public static class ObiMarkCheck
    {
        static readonly string[] Words = { "定時", "退社" };
        static readonly HashSet<int> Keep = new HashSet<int> { 0, 150, 299 };

        // 決めたコマだけ PNG にする書き手（全部書くと重いため）
        class SomeFramesWriter : PngVideoWriter
        {
            public override WriteResult Append(NativeArray<byte> bgra, int frame) =>
                Keep.Contains(frame) ? base.Append(bgra, frame) : WriteResult.Ok;
        }

        public static void Run()
        {
            int code = 0;
            try
            {
                string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../captures"));
                Export(Path.Combine(outDir, "obimark_tall"), new EffectSettings { EffectId = "obi" }, true);
                Export(Path.Combine(outDir, "obimark_wide"), new EffectSettings { EffectId = "obi", ExportWide = true }, true);
                Export(Path.Combine(outDir, "obimark_blue"), new EffectSettings { EffectId = "obi", ObiBlueBack = true }, true);
                Export(Path.Combine(outDir, "obimark_nomark"), new EffectSettings { EffectId = "obi" }, false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                code = 1;
            }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void Export(string dir, EffectSettings fx, bool mark)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ex = new VideoExporter(Words, fx, mark, new SomeFramesWriter(), dir);
            var e = ex.Start();
            for (int guard = 0; ex.Status == ExportStatus.Running && guard < 5000; guard++)
            {
                ex.Step();
                AsyncGPUReadback.WaitAllRequests();
            }
            Debug.Log($"[ObiMarkCheck] {Path.GetFileName(dir)} start={e} status={ex.Status} {ex.W}x{ex.H} frames={ex.TotalFrames}");
            ex.Dispose();
        }
    }
}
