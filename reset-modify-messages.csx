#r "nuget: Lestaly, 0.58.0"
#r "nuget: Kokuban, 0.2.0"
#load ".messages-properties.csx"
#nullable enable
using System.Text.RegularExpressions;
using Lestaly;
using Kokuban;

var settings = new
{
    // 行末キャラクタ
    LineEnding = "\x0A",

    // 保存エンコーディング
    SaveEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
};

return await Paved.RunAsync(config: c => c.AnyPause(), action: async () =>
{
    // ダミー
    await Task.CompletedTask;

    // コンソール準備
    using var outenc = ConsoleWig.OutputEncodingPeriod(Encoding.UTF8);
    using var signal = ConsoleWig.CreateCancelKeyHandlePeriod();

    WriteLine("比較リソース：基準ファイルパスの入力"); Write(">");
    var baseInput = ReadLine().CancelIfWhite().Unquote();
    var baseResFile = CurrentDir.RelativeFile(baseInput).ThrowIfNotExists(d => new PavedMessageException($"ファイル '{d.FullName}' が存在しません。", PavedMessageKind.Warning));
    var baseResource = LoadMessageEntries(baseResFile, settings.LineEnding).ToArray();
    WriteLine();

    WriteLine("比較リソース：更新検出ファイルパスの入力"); Write(">");
    var detectInput = ReadLine().CancelIfWhite().Unquote();
    var detectResFile = CurrentDir.RelativeFile(detectInput).ThrowIfNotExists(d => new PavedMessageException($"ファイル '{d.FullName}' が存在しません。", PavedMessageKind.Warning));
    if (!detectResFile.Name.Equals(baseResFile.Name)) throw new PavedMessageException($"比較基準と更新検出のファイル名が異なります。", PavedMessageKind.Warning);
    if (detectResFile.FullName.Equals(baseResFile.FullName)) throw new PavedMessageException($"比較基準と更新検出が同じファイルです。", PavedMessageKind.Warning);
    var detectResource = LoadMessageEntries(detectResFile, settings.LineEnding).ToArray();
    WriteLine();

    WriteLine("リソースファイルの更新検出");
    var baseTexts = baseResource.Where(r => r.IsResource).Select(r => r.Resource!).DistinctBy(r => r.Key);
    var detectTexts = detectResource.Where(r => r.IsResource).Select(r => r.Resource!).DistinctBy(r => r.Key);
    var diffTexts = detectTexts.Join(baseTexts, r => r.Key, r => r.Key, (detect, @base) => (detect, @base))
        .Where(p => p.detect.Text != p.@base.Text)
        .ToDictionary(p => p.detect.Key, p => p.detect.Text);
    if (diffTexts.Count <= 0) throw new PavedMessageException("リセット対象キーはありません。", PavedMessageKind.Cancelled);

    WriteLine("リセット対象ファイルパスの入力"); Write(">");
    var targetInput = ReadLine().CancelIfWhite().Unquote();
    var targetResFile = CurrentDir.RelativeFile(targetInput).ThrowIfNotExists(d => new PavedMessageException($"ファイル '{d.FullName}' が存在しません。", PavedMessageKind.Warning));
    if (targetResFile.FullName.Equals(baseResFile.FullName)) throw new PavedMessageException($"比較基準とリセット対象が同じファイルです。", PavedMessageKind.Warning);
    if (targetResFile.FullName.Equals(detectResFile.FullName)) throw new PavedMessageException($"更新検出とリセット対象が同じファイルです。", PavedMessageKind.Warning);
    var targetResource = LoadMessageEntries(targetResFile, settings.LineEnding).ToArray();
    WriteLine();

    WriteLine("更新対象をバックアップ");
    targetResFile.CopyTo(targetResFile.RelativeFile($"{targetResFile.Name}.bak-{DateTime.Now:yyyyMMddHHmmss}").FullName);

    WriteLine("更新対象に対して比較リソースの更新リソースをリセット");
    var fixRes = new List<MessageEntry>();
    var hasFix = false;
    foreach (var targetEntry in targetResource)
    {
        if (targetEntry.IsResource && diffTexts.TryGetValue(targetEntry.Resource.Key, out var text))
        {
            hasFix = true;
            fixRes.Add(MessageEntry.OfResource(new(targetEntry.Resource.Key, text)));
        }
        else
        {
            fixRes.Add(targetEntry);
        }
    }
    if (!hasFix) throw new PavedMessageException("リセット対象キーはありません。", PavedMessageKind.Cancelled);

    WriteLine("更新対象を上書き更新");
    SaveMessageEntries(targetResFile, fixRes, settings.LineEnding, settings.SaveEncoding);

});
