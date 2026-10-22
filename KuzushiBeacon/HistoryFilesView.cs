using System.Diagnostics;
using Roxyz0501.Dalamud.CommonUi;

namespace KuzushiBeacon;

internal sealed class HistoryFilesView : IDisposable
{
    private readonly string[] roots;
    private readonly object sync=new();
    private readonly CancellationTokenSource cancel=new();
    private Task task=Task.CompletedTask;
    private (string Path,long Count,long Bytes)[]? rows;
    private int state, revision=-1;
    private bool disposed;
    public HistoryFilesView(string root)=>roots=[Path.Combine(root,"ledger-matches-v1"),Path.Combine(root,"support-review")];
    // Seven-language canonical labels and status text; no file I/O in Draw.
    private static readonly string[][] Strings=[
        ["履歴ファイル","ファイル数","サイズ","保存場所","保存ディレクトリを開く","更新","集計範囲","論理ファイル長の合計です。SSDの物理使用量・累積書き込み量ではありません。","対戦・レビュー・出力・旧形式・移行記録・一時/退避ファイル。設定とキャッシュは除外。","計測中","計測またはフォルダー表示に失敗しました。更新して再試行してください。","未計測","保存場所はまだ作成されていません。","設定を正常なバックアップから復元しました。","保存に失敗しました。未保存データを保持しています。設定画面で再試行してください。","保存を再試行"],
        ["History files","Files","Size","Directory","Open storage directory","Refresh","Scope","Sum of logical file lengths, not SSD physical usage or cumulative writes.","Matches, reviews, exports, legacy, migration markers, temporary/recovery files. Excludes settings/cache.","Measuring","Measurement or folder opening failed. Refresh to retry.","Not measured","Storage directory has not been created yet.","Settings restored from a valid backup.","Save failed; unsaved data retained. Retry from settings.","Retry save"],
        ["Verlaufsdateien","Dateien","Größe","Ordner","Speicherordner öffnen","Aktualisieren","Umfang","Summe logischer Dateigrößen, keine physische SSD-Nutzung oder Schreibmenge.","Spiele, Rückblicke, Exporte, Altformat, Migration und temporäre/Wiederherstellungsdateien. Ohne Einstellungen/Cache.","Messung läuft","Messung oder Öffnen fehlgeschlagen. Zum Wiederholen aktualisieren.","Nicht gemessen","Speicherordner noch nicht erstellt.","Einstellungen aus gültiger Sicherung wiederhergestellt.","Speichern fehlgeschlagen; Änderungen bleiben erhalten. In Einstellungen erneut versuchen.","Speichern wiederholen"],
        ["Fichiers d’historique","Fichiers","Taille","Dossier","Ouvrir le dossier","Actualiser","Périmètre","Somme des tailles logiques, pas l’utilisation physique du SSD ni les écritures cumulées.","Matchs, bilans, exports, ancien format, migration et fichiers temporaires/récupérés. Hors réglages/cache.","Mesure en cours","Échec de mesure ou d’ouverture. Actualisez pour réessayer.","Non mesuré","Le dossier n’a pas encore été créé.","Réglages restaurés depuis une sauvegarde valide.","Échec de sauvegarde ; modifications conservées. Réessayez dans les réglages.","Réessayer la sauvegarde"],
        ["기록 파일","파일 수","크기","저장 위치","저장 폴더 열기","새로 고침","집계 범위","논리 파일 크기의 합계이며 SSD 물리 사용량이나 누적 쓰기량이 아닙니다.","대전, 리뷰, 내보내기, 이전 형식, 이관 기록, 임시/복구 파일. 설정/캐시는 제외합니다.","측정 중","측정 또는 폴더 열기 실패. 새로 고침으로 재시도하세요.","미측정","저장 폴더가 아직 없습니다.","정상 백업에서 설정을 복원했습니다.","저장 실패. 미저장 데이터를 유지합니다. 설정에서 재시도하세요.","저장 재시도"],
        ["历史文件","文件数","大小","保存位置","打开保存目录","刷新","统计范围","逻辑文件长度之和，不代表SSD物理占用或累计写入量。","对战、回顾、导出、旧格式、迁移记录、临时/恢复文件。不含设置和缓存。","正在统计","统计或打开目录失败，请刷新重试。","未统计","保存目录尚未创建。","已从有效备份恢复设置。","保存失败，未保存数据已保留。请在设置中重试。","重试保存"],
        ["歷史檔案","檔案數","大小","儲存位置","開啟儲存目錄","重新整理","統計範圍","邏輯檔案長度總和，不代表SSD實體用量或累計寫入量。","對戰、回顧、匯出、舊格式、移轉記錄、暫存/復原檔案。不含設定和快取。","統計中","統計或開啟目錄失敗，請重新整理重試。","尚未統計","儲存目錄尚未建立。","已從有效備份復原設定。","儲存失敗，未儲存資料已保留。請於設定中重試。","重試儲存"]
    ];
    public static string FontText=>string.Concat(Strings.SelectMany(x=>x));
    public static string Text(string? language,int id)=>Strings[language switch{"ja"=>0,"de"=>2,"fr"=>3,"ko"=>4,"zh-Hans"=>5,"zh-Hant"=>6,_=>1}][id];
    public void Invalidate(){revision=-1;}
    private void Refresh()
    {
        lock(sync)
        {
            if(disposed||!task.IsCompleted)return;
            state=1;
            task=Task.Run(()=>
            {
                try
                {
                    var result=roots.Select(root=>
                    {
                        long n=0,bytes=0;
                        if(File.Exists(root))throw new IOException("Storage root is not a directory");
                        if(Directory.Exists(root))foreach(var f in Directory.EnumerateFiles(root,"*",new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=false,AttributesToSkip=FileAttributes.ReparsePoint}))
                        {cancel.Token.ThrowIfCancellationRequested();n++;bytes+=new FileInfo(f).Length;}
                        return(root,n,bytes);
                    }).ToArray();
                    lock(sync){rows=result;state=0;}
                }
                catch(OperationCanceledException){}catch{lock(sync){rows=null;state=2;}}
            });
        }
    }
    public void Draw(string? lang,int currentRevision)
    {
        if(revision!=currentRevision){lock(sync){if(task.IsCompleted){revision=currentRevision;Refresh();}}}
        StandardHistoryFilesTab.Snapshot snapshot;
        lock(sync)
        {
            snapshot=new(rows?.Sum(r=>r.Count),rows?.Sum(r=>r.Bytes),Text(lang,8),state switch{1=>Text(lang,9),2=>Text(lang,10),3=>Text(lang,12),_=>rows==null?Text(lang,11):""},
                roots.Select(root=>{var r=rows?.FirstOrDefault(r=>r.Path==root);return new StandardHistoryFilesTab.Location(root,root,r?.Count,r?.Bytes);}).ToArray());
        }
        StandardHistoryFilesTab.Draw(new(Text(lang,0),Text(lang,1),Text(lang,2),Text(lang,3),Text(lang,4),Text(lang,5),Text(lang,6),Text(lang,7)),snapshot,Refresh,Open);
    }
    private void Open(string path)
    {
        if(!roots.Contains(path,StringComparer.OrdinalIgnoreCase))return;
        if(!Directory.Exists(path)){state=3;return;}
        try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch{state=2;}
    }
    public void Dispose(){lock(sync){disposed=true;cancel.Cancel();}task.GetAwaiter().GetResult();cancel.Dispose();}
}
