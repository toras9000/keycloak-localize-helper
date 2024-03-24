#r "nuget: Lestaly, 0.58.0"
#r "nuget: Kokuban, 0.2.0"
#load ".messages-properties.csx"
#nullable enable
using System.Text.RegularExpressions;
using Lestaly;
using Kokuban;

var settings = new
{
    // ベースリソースファイルの名称を固定する場合にその名称を指定。
    FixedBaseName = "messages_en.properties",

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

    WriteLine("基準(英語)リソースパスの入力"); Write(">");
    var baseInput = ReadLine().CancelIfWhite().Unquote();
    var baseResFile = CurrentDir.RelativeFile(baseInput).ThrowIfNotExists(d => new PavedMessageException($"ファイル '{d.FullName}' が存在しません。", PavedMessageKind.Warning));
    if (settings.FixedBaseName.IsNotWhite() && baseResFile.Name != settings.FixedBaseName) throw new PavedMessageException($"基準ファイルの名称が '{settings.FixedBaseName}' ではありません。", PavedMessageKind.Warning);
    WriteLine();

    WriteLine("更新対象リソースパスの入力"); Write(">");
    var rebaseInput = ReadLine().CancelIfWhite().Unquote();
    var rebaseResFile = CurrentDir.RelativeFile(rebaseInput).ThrowIfNotExists(d => new PavedMessageException($"ファイル '{d.FullName}' が存在しません。", PavedMessageKind.Warning));
    if (baseResFile.FullName.Equals(rebaseResFile.FullName)) throw new PavedMessageException($"基準と更新対象が同じファイルです。", PavedMessageKind.Warning);
    WriteLine();

    WriteLine("リソースファイルの読み込み");
    var baseResource = LoadMessageEntries(baseResFile, settings.LineEnding).ToArray();
    var rebaseResource = LoadMessageEntries(rebaseResFile, settings.LineEnding).ToArray();

    WriteLine("更新対象をバックアップ");
    rebaseResFile.CopyTo(rebaseResFile.RelativeFile($"{rebaseResFile.Name}.bak-{DateTime.Now:yyyyMMddHHmmss}").FullName);

    WriteLine("基準ファイルを元に更新内容を作成");
    var rebaseDict = rebaseResource.Where(e => e.IsResource).ToLookup(e => e.Resource!.Key);
    var newRes = new List<MessageEntry>();
    foreach (var baseEntry in baseResource)
    {
        if (baseEntry.IsResource)
        {
            if (rebaseDict.Contains(baseEntry.Resource.Key))
            {
                // 更新対象に元から存在するものはそれを反映する
                foreach (var targetEntry in rebaseDict[baseEntry.Resource.Key])
                {
                    newRes.Add(targetEntry);
                }
            }
            else
            {
                // 基準ファイルにしかないものはそのまま反映する
                newRes.Add(baseEntry);
            }
        }
        else
        {
            // リソース情報以外の場合はそのまま反映する
            newRes.Add(baseEntry);
        }
    }

    WriteLine("更新対象を上書き更新");
    SaveMessageEntries(rebaseResFile, newRes, settings.LineEnding, settings.SaveEncoding);

});
