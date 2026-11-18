using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 既存のTMP_Text全てが参照している"LiberationSans SDF"には日本語グリフが無く、
/// これまで日本語の台詞が一切表示されていなかった(アップストリーム側の既存の問題)。
///
/// Window > TextMeshPro > Font Asset Creator でNotoSansJP.ttfから静的に生成した
/// フォントアセット(Assets/VoidSurvival_10-Days/Fonts/NotoSansJP SDF.asset)を、
/// LiberationSans SDF / LiberationSans SDF - Fallback のフォールバックに追加することで、
/// 既存のシーン・プレファブの参照を一切変更せずに日本語表示を可能にする。
///
/// (TMP_FontAsset.CreateFontAsset を使ったDynamicフォント自動生成は、このTMProバージョンと
/// 現行Unityの組み合わせで複数の不具合(0幅テクスチャ例外、2枚目アトラスの参照破損等)を
/// 引き起こしたため、Font Asset Creatorでの静的生成に切り替えた)
/// </summary>
public static class BatchJapaneseFontSetup
{
    private const string OutputFontAssetPath = "Assets/VoidSurvival_10-Days/Fonts/NotoSansJP SDF.asset";
    private const string LiberationSansSdfPath =
        "Assets/TutorialInfo/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string LiberationSansSdfFallbackPath =
        "Assets/TutorialInfo/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

    [MenuItem("Tools/Setup Japanese Font")]
    public static void Run()
    {
        TMP_FontAsset jpFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath);
        if (jpFontAsset == null)
        {
            Debug.LogError($"[BatchJapaneseFontSetup] {OutputFontAssetPath} が見つかりません。" +
                "先に Window > TextMeshPro > Font Asset Creator でこのパスにフォントアセットを生成してください。");
            return;
        }

        AddFallback(LiberationSansSdfPath, jpFontAsset);
        AddFallback(LiberationSansSdfFallbackPath, jpFontAsset);

        AssetDatabase.SaveAssets();
        Debug.Log("[BatchJapaneseFontSetup] 日本語フォールバックフォントの設定が完了しました。");
    }

    private static void AddFallback(string targetFontAssetPath, TMP_FontAsset jpFontAsset)
    {
        var target = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(targetFontAssetPath);
        if (target == null)
        {
            Debug.LogError($"[BatchJapaneseFontSetup] Fallback target not found: {targetFontAssetPath}");
            return;
        }
        if (target == jpFontAsset) return;
        if (target.fallbackFontAssetTable == null)
        {
            target.fallbackFontAssetTable = new List<TMP_FontAsset>();
        }
        // 前回までの実行で壊れた/削除済みの参照が残っていれば先に取り除く。
        target.fallbackFontAssetTable.RemoveAll(f => f == null);
        if (!target.fallbackFontAssetTable.Contains(jpFontAsset))
        {
            target.fallbackFontAssetTable.Add(jpFontAsset);
            EditorUtility.SetDirty(target);
        }
    }
}
