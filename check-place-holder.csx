#r "nuget: Lestaly, 0.58.0"
#r "nuget: Kokuban, 0.2.0"
#load ".messages-properties.csx"
#nullable enable
using System.Text.RegularExpressions;
using Lestaly;
using Kokuban;

var settings = new
{
    // 基準(英語)リソースファイルの名称
    BaseResName = "messages_en.properties",

    // 検証リソースファイルの名称
    CheckResName = "messages_ja.properties",

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

    WriteLine("配下にリソースを含むディレクトリパスの入力"); Write(">");
    var resDirInput = ReadLine().CancelIfWhite().Unquote();
    var resDir = CurrentDir.RelativeDirectory(resDirInput).ThrowIfNotExists(d => new PavedMessageException($"ディレクトリ '{d.FullName}' が存在しません。", PavedMessageKind.Warning));
    WriteLine();

    WriteLine("プレースホルダチェック"); Write(">");
    var checker = new Regex(@"\{+.+?\}+");
    foreach (var baseResFile in resDir.EnumerateFiles(settings.BaseResName, SearchOption.AllDirectories))
    {
        // 見つかった基準リソースと対応するチェック対象リソース
        var checkResFile = baseResFile.RelativeFile(settings.CheckResName);
        if (!checkResFile.Exists) continue;

        WriteLine("リソースファイルの読み込み");
        var baseResource = LoadMessageEntries(baseResFile, settings.LineEnding).ToArray();
        var checkResource = LoadMessageEntries(checkResFile, settings.LineEnding).ToArray();

        // ベースリソースを辞書化
        var baseResDict = baseResource
            .Where(r => r.IsResource)
            .GroupBy(r => r.Resource!.Key)
            .ToDictionary(g => g.Key, g => g.First());

        // プレースホルダの差異を検出
        WriteLine($"{baseResFile.RelativePathFrom(resDir, ignoreCase: true)}");
        var hasIllegal = false;
        foreach (var checkRes in checkResource.Where(r => r.IsResource).Select(r => r.Resource!))
        {
            // 対応する基準リソースを取得
            if (baseResDict.TryGetValue(checkRes.Key, out var baseEntry))
            {
                // 基準リソースとのプレースホルダ比較
                var baseHolders = checker.Matches(baseEntry.Resource!.Text).Select(m => m.Groups[0].Value).OrderBy(h => h).ToArray();
                var checkHolders = checker.Matches(checkRes.Text).Select(m => m.Groups[0].Value).OrderBy(h => h).ToArray();
                if (!checkHolders.SequenceEqual(baseHolders))
                {
                    hasIllegal = true;
                    WriteLine($" .. {checkRes.Key}: {Chalk.Yellow["difference place holder"]}");
                }
            }
        }

        // 差異が無ければその旨表示
        if (!hasIllegal)
        {
            WriteLine($" .. {Chalk.Green["all verified"]}");
        }
    }

});
